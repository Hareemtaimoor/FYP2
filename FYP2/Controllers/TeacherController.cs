using FYP2.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace FYP2.Controllers
{
    public class TeacherController : ApiController
    {

        Teacher_Evaluation_SystemEntities3 db = new Teacher_Evaluation_SystemEntities3();

        // 1. Teachers ki list get karne ke liye
        [HttpGet]
        public HttpResponseMessage GetAllTeachers()
        {
            try
            {
                var teachersList = db.EMPMTRs
                    .Where(e => e.Designation != null && e.Name != null)
                    .Select(e => new
                    {
                        e.Emp_no,
                        e.Name,
                        e.Designation,
                        e.eval // Ye column lazmi shamil karein
                    })
                    .ToList();

                var finalTeachers = teachersList
                    .Where(e => !e.Designation.Trim().Equals("Junior Lecturer", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.Name.Trim())
                    .Select(e => new
                    {
                        Emp_no = e.Emp_no,
                        Name = e.Name.Trim(),
                        Designation = e.Designation.Trim(),
                        EvalStatus = e.eval // Isse frontend check karega
                    })
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, finalTeachers);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAllFaculty()
        {
            try
            {
                // 1. Database se fresh list uthayein
                var teachersList = db.EMPMTRs
                    .Where(e => e.Designation != null && e.Name != null)
                    .ToList();

                // 2. Filter check karein (Yahan Junior Lecturer wala "!" filter nahi hona chahiye)
                var finalTeachers = teachersList
                    .OrderBy(e => e.Name.Trim())
                    .Select(e => new
                    {
                        Emp_no = e.Emp_no,
                        Name = e.Name.Trim(),
                        Designation = e.Designation.Trim(),
                        // Junior teachers ka Eval NULL ho sakta hai, isliye 0 handle karein
                        EvalStatus = e.eval ?? 0
                    })
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, finalTeachers);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
        [HttpGet]
        public HttpResponseMessage GetTeacherProfile(string TeacherID)
        {
            if (string.IsNullOrWhiteSpace(TeacherID))
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "TeacherID is required");
            }

            try
            {
                string cleanID = TeacherID.Trim();

                var teacher = db.EMPMTRs
                    .Where(e => e.Emp_no == cleanID)
                    .Select(e => new
                    {
                        e.Emp_no,
                        e.Emp_email,
                        e.Name,
                        e.Designation
                    })
                    .FirstOrDefault();

                if (teacher == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, $"Teacher with ID {cleanID} not found");
                }

                return Request.CreateResponse(HttpStatusCode.OK, teacher);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        [HttpGet]
        public HttpResponseMessage GetTeacherCHR(string tId, DateTime date)
        {
            try
            {
                // 1. Swap .FirstOrDefault() for .ToList() to capture all matching records
                var res = db.v_ClassHeldReport
                   .Where(cr => cr.Emp_no == tId && DbFunctions.TruncateTime(cr.ReportDate) == DbFunctions.TruncateTime(date))
                   .Select(e => new
                   {
                       e.SrNo,
                       e.Course,
                       e.Teacher,
                       e.Discipline_Section,
                       e.Venue,
                       e.Status,
                       e.Late_In,
                       e.Left_Early,
                       e.Remarks
                   })
                   .ToList(); // Returns a List instead of a single object

                // 2. Adjust the check for an empty list
                if (res == null || res.Count == 0)
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, new { message = "No reports jido  found for this date." });
                }

                // 3. This will now return a JSON Array [{}, {}]
                return Request.CreateResponse(HttpStatusCode.OK, res);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetTeacherDateRange(string teacherId)
        {
            try
            {
                var dates = db.v_TeacherAttendance_EMPMTR
                    .Where(t => t.Emp_no == teacherId)
                    .Select(t => t.AttendanceDate)
                    .ToList();

                if (dates == null || dates.Count == 0)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No records found");
                }

                var range = new
                {
                    Start = dates.Min().ToString("yyyy-MM-dd"),
                    End = dates.Max().ToString("yyyy-MM-dd")
                };

                return Request.CreateResponse(HttpStatusCode.OK, range);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // Get Teacher Attendance (Date Range)
        [HttpGet]
        public HttpResponseMessage GetTeacherAttendanceRange(string teacherId, DateTime start, DateTime end)
        {
            try
            {
                var res = db.v_TeacherAttendance_EMPMTR.Where(t => t.Emp_no == teacherId && t.AttendanceDate >= start && t.AttendanceDate <= end).OrderBy(t => t.AttendanceDate).ToList();
                if (res == null || res.Count == 0)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No record in this date range");
                }
                return Request.CreateResponse(HttpStatusCode.OK, res);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        // Add Attendance Comments
        [HttpPost]
        public HttpResponseMessage AddAttendanceComments(int attendanceId, string teacherId, string comments)
        {
            try
            {
                var res = db.AttendanceRecords.Where(a => a.RecordID == attendanceId && a.Emp_no == teacherId).FirstOrDefault();
                if (res == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No Attendance found");
                }
                res.Comments = comments;
                db.SaveChanges();
                return Request.CreateResponse(HttpStatusCode.OK, "Comments added successfully");
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }




        //TEacher Evaluation ke liye helper function (Rating ko text me convert karne ke liye)
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
        // 2. Selected teachers ka 'eval' column update karne ke liye
        [HttpPost]
        [Route("api/Evaluation/SubmitPeer")]
        public IHttpActionResult SubmitPeerEvaluation(PeerEvaluationRequest request)
        {
            // 1. Basic Validation
            if (request == null || request.Answers == null || !request.Answers.Any())
            {
                return BadRequest("Evaluation data or answers are missing.");
            }

            try
            {
                // 2. Map answers to the PeerEvaluation table
                foreach (var ans in request.Answers)
                {
                    var peerEval = new PeerEvaluation
                    {
                        Evaluator_Emp_no = request.Evaluator_Emp_no?.Trim(),
                        Target_Emp_no = request.Target_Emp_no?.Trim(),
                        Question_Desc = ans.Question_ID, // Mapping ID to the column
                        Answer_Marks = ans.Rating,
                        Answer_Desc = GetRatingText(ans.Rating), // Assuming your helper function exists
                        Remarks = request.Suggestion?.Trim()
                    };

                    db.PeerEvaluations.Add(peerEval);
                }

                // 3. Save to Database
                db.SaveChanges();
                return Ok(new { message = "Peer evaluation submitted successfully!" });
            }
            catch (System.Data.Entity.Validation.DbEntityValidationException dbEx)
            {
                // Extracts validation errors (e.g., string too long, null constraint violated)
                var errorMessages = dbEx.EntityValidationErrors
                    .SelectMany(x => x.ValidationErrors)
                    .Select(x => $"{x.PropertyName}: {x.ErrorMessage}");

                return BadRequest("Validation Failed: " + string.Join("; ", errorMessages));
            }
            catch (Exception ex)
            {
                // Returns the full error for debugging (use more generic messages in production)
                return InternalServerError(ex);
            }
        }
        // Check if Peer Already Evaluated
        [HttpGet]
        [Route("api/Evaluation/CheckPeerStatus")]
        public HttpResponseMessage CheckIfAlreadyEvaluated(string evaluatorId, string targetId)
        {
            try
            {
                // We check if a record exists where this specific peer has already 
                // submitted an evaluation for the target colleague.
                var alreadyExists = db.PeerEvaluations.Any(e =>
                    e.Evaluator_Emp_no == evaluatorId.Trim() &&
                    e.Target_Emp_no == targetId.Trim()
                );

                if (alreadyExists)
                {
                    return Request.CreateResponse(HttpStatusCode.OK, true);
                }

                return Request.CreateResponse(HttpStatusCode.OK, false);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Error checking evaluation status: " + ex.Message);
            }
        }
        [HttpPost]
        public HttpResponseMessage SavePeerAssignment(List<string> selectedTeacherIds)
        {
            try
            {
                // 1. Pehle safe check karein ke data null na ho
                if (selectedTeacherIds == null)
                {
                    selectedTeacherIds = new List<string>();
                }

                // 2. Sab teachers ka eval reset karein (Junior Lecturer ke ilawa)
                // Trim aur ToLower lazmi use karein taake exact match ho
                var allEligible = db.EMPMTRs.ToList().Where(e =>
                    e.Designation != null &&
                    e.Designation.Trim().ToLower() != "junior lecturer"
                ).ToList();

                foreach (var t in allEligible)
                {
                    t.eval = 0;
                }

                // 3. Current selection ko 1 karein
                if (selectedTeacherIds.Any())
                {
                    var toUpdate = db.EMPMTRs.Where(e => selectedTeacherIds.Contains(e.Emp_no)).ToList();
                    foreach (var t in toUpdate)
                    {
                        t.eval = 1;
                    }
                }

                db.SaveChanges();
                return Request.CreateResponse(HttpStatusCode.OK, "Success");
            }
            catch (Exception ex)
            {
                // Isse aapko error ki asli wajah pata chal jayegi agar crash hua to
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }


        public class PeerEvaluationRequest
        {
            public string Evaluator_Emp_no { get; set; } // The person giving the evaluation
            public string Target_Emp_no { get; set; }    // The person being evaluated
            public string Suggestion { get; set; }
            public List<PeerAnswer> Answers { get; set; }
        }

        public class PeerAnswer
        {
            public int Question_ID { get; set; }
            public int Rating { get; set; }
        }
    }
}