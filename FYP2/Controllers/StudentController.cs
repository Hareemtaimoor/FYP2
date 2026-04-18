
using FYP2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using static System.Collections.Specialized.BitVector32;

namespace FYP2.Controllers
{
    public class StudentController : ApiController
    {

        Teacher_Evaluation_SystemEntities3 db = new Teacher_Evaluation_SystemEntities3();
        [HttpGet]

        public HttpResponseMessage GetStudentProfile(string AridNo)
        {
            try
            {

                var res = db.STMTRs
                    .Where(s => s.Reg_No.Trim() == AridNo.Trim())
                    .Select(s => new
                    {
                        s.Reg_No,
                        s.St_firstname,
                        s.St_middlename,
                        s.St_lastname,
                        s.Section,
                        s.Final_course,
                        s.Semester_no
                    })
                    .FirstOrDefault();

                if (res == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Student not found");
                }


                int calculatedSem = 0;
                string semData = res.Semester_no;

                if (!string.IsNullOrEmpty(semData) && semData.Length >= 4)
                {

                    string yearPart = semData.Substring(0, 4);

                    if (int.TryParse(yearPart, out int eyear))
                    {
                        int currentYear = DateTime.Now.Year;
                        int currentMonth = DateTime.Now.Month;
                        int sem = (currentYear - eyear) * 2;
                        calculatedSem = sem - 2;

                        if (currentMonth >= 9)
                        {
                            calculatedSem += 1;
                        }
                    }
                }
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    AridNo = res.Reg_No.Trim(),
                    FullName = (res.St_firstname + " " + (res.St_middlename ?? "") + " " + res.St_lastname).Replace("  ", " ").Trim(),
                    Section = res.Section?.Trim(),
                    Course = res.Final_course?.Trim(),
                    Semester = calculatedSem
                });
            }
            catch (Exception ex)
            {

                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }
        [HttpGet]
        public HttpResponseMessage GetStudentCourses(string AridNo, int semester, string discipline)
        {
            try
            {
                if (string.IsNullOrEmpty(AridNo))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "AridNo is required");
                }

                var enrolledCourses = (from detail in db.Crsdtls
                                       join course in db.CRSMTRs on detail.Course_no equals course.Course_no
                                       join teacher in db.EMPMTRs on detail.Emp_no equals teacher.Emp_no
                                       where detail.REG_NO.Trim() == AridNo.Trim() &&
                                             detail.CrsSemNo == semester &&
                                             detail.DISCIPLINE.Trim() == discipline.Trim()
                                       select new
                                       {   EmpNo=detail.Emp_no.Trim(),
                                           CourseNo = detail.Course_no.Trim(),
                                           CourseName = course.Course_desc.Trim(),
                                           TeacherName = (teacher.Name.Trim()).Trim(),
                                           Section = detail.SECTION.Trim(),
                                           Semester = detail.CrsSemNo
                                       })
                                       .ToList() // Pehle list lein
                                       .GroupBy(x => x.CourseNo) // Course Number ki base par group karein
                                       .Select(g => g.First())   // Har group ka sirf pehla record uthayein
                                       .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, enrolledCourses);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Error: " + ex.Message);
            }
        }
        [HttpGet]
        public IHttpActionResult GetQuestions()
        {
            try
            {
                // Database se questions get karne ki query
                // Description column aapki question type (T/C) ko store kar raha hai
                var questions = db.Question_Answer.Select(q => new
                {
                    Question_Id = q.Question_ID,
                    Question1 = q.Question,
                    // Yahan hum Description (T/C) ko full name mein convert kar rahe hain styling ke liye
                    Question_type = q.Description == "T" ? "Teacher Evaluation" : "Course Evaluation",
                    RawType = q.Description
                }).ToList();

                if (questions == null || questions.Count == 0)
                {
                    return NotFound();
                }

                return Ok(questions);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
        [HttpPost]
      
        public IHttpActionResult SubmitEvaluation(EvaluationRequest request)
        {
            // 1. Validation: Data missing na ho
            if (request == null || request.Answers == null || request.Answers.Count == 0)
            {
                return BadRequest("Required data is missing.");
            }

            string currentSemester = GetAridSemester();

            try
            {
                foreach (var ans in request.Answers)
                {
                    var evaluation = new Eval
                    {
                        // Trimming aur Null check taake validation fail na ho
                        Emp_no = request.Emp_no?.Trim(),
                        Reg_No = request.Reg_no?.Trim(),
                        Course_no = request.Course_no?.Trim(),
                        Discipline = request.Discipline?.Trim(),
                        Semester_no = currentSemester,
                        Question_Desc = ans.Question_ID,
                        Answer_Desc = GetRatingText(ans.Rating),
                        Answer_Marks = ans.Rating
                    };
                    db.Evals.Add(evaluation);
                }
                db.SaveChanges();
                return Ok(new { message = "Success!" });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
            {
                // Ye code aapko batayega ke kis column mein masla hai
                var errorMessages = dbEx.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => x.PropertyName + ": " + x.ErrorMessage);

                var fullErrorMessage = string.Join("; ", errorMessages);
                return InternalServerError(new Exception("Validation Error: " + fullErrorMessage));
            }
        }

        // --- Helper Functions ---
        private string GetAridSemester()
        {
            int year = DateTime.Now.Year;
            int month = DateTime.Now.Month;
            string suffix = (month >= 7) ? "FM" : "SM"; // July-Dec is Fall (FM)
            return $"{year}{suffix}";
        }

        private string GetRatingText(int rating)
        {
            switch (rating)
            {
                case 5: return "Excellent";
                case 4: return "Good";
                case 3: return "Average";
                case 2: return "Below Average";
                case 1: return "Poor";
                default: return "N/A";
            }
        }
        [HttpGet]
     
        public IHttpActionResult GetSupervisorName(string AridNo)
        {
            try
            {
                // Fetches the supervisor name from the Projects table based on reg_no
                var supervisorName = db.Projects
                    .Where(p => p.reg_no == AridNo)
                    .Select(p => p.supervisor)
                    .FirstOrDefault();

                if (supervisorName != null)
                {
                    return Ok(supervisorName);
                }
                return Ok("Not Assigned"); // Default if no project found
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        //Check if Already Evaluated
        [HttpGet]
        public HttpResponseMessage CheckIfAlreadyEvaluated(string AridNo, string CourseCode)
        {
            try
            {
                var res = db.Evals.Where(e => e.Reg_No == AridNo && e.Course_no == CourseCode).FirstOrDefault();
                if (res != null)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, true);
                }
                return Request.CreateResponse(HttpStatusCode.OK, false);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
    }

}
// --- Data Transfer Objects (DTOs) ---
public class EvaluationRequest
{
    public string Emp_no { get; set; }
    public string Reg_no { get; set; }
    public string Course_no { get; set; }
    public string Discipline { get; set; }
    public List<AnswerDetail> Answers { get; set; }
}

public class AnswerDetail
{
    public int Question_ID { get; set; }
    public int Rating { get; set; }
}