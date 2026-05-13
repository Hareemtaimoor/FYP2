
using FYP2.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Web.Http;
using static System.Collections.Specialized.BitVector32;

namespace FYP2.Controllers
{
    public class StudentController : ApiController
    {
        private const string FallbackAesKeyBase64 = "vrHFCSCrUlrMHNWFTYJgS09SfZFC+QY0PuMuOz0pyXY=";
        private const string CipherHeader = "CYPHER:AES-256-CBC";

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

                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, GetFullExceptionMessage(ex));
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
                        Answer_Desc = GetAnswerDescription(ans),
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

        private string GetAnswerDescription(AnswerDetail answer)
        {
            if (!string.IsNullOrWhiteSpace(answer?.Comment))
            {
                return Limit(NormalizeCsvText(answer.Comment), 50);
            }

            return GetRatingText(answer?.Rating ?? 0);
        }
        [HttpGet]
        public HttpResponseMessage getConfidentialStudent(string AridNo)
        {
            try
            {
                // Removed SEM_STATUS condition
                var studentData = (from s in db.STMTRs
                                   join a in db.Accgpas on s.Reg_No.Trim() equals a.REG_NO.Trim()
                                   where s.Reg_No.Trim() == AridNo.Trim()
                                   && a.CGPA >= 3.70m
                                   select new
                                   {
                                       s.Reg_No,
                                       Name = s.St_firstname + " " + s.St_lastname,
                                       a.CGPA,
                                       s.Section
                                   }).FirstOrDefault();

                if (studentData == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Criteria not met.");
                }

                return Request.CreateResponse(HttpStatusCode.OK, studentData);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
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
        

        [HttpPost]
        [Route("api/student/submit-confidential")]
        public IHttpActionResult SubmitConfidential([FromBody] EvaluationRequest request)
        {
            if (request == null || request.Answers == null || request.Answers.Count == 0)
            {
                return BadRequest("Request body with at least one answer is required.");
            }

            try
            {
                string csvContent = ConvertEvaluationRequestToCsv(request, out int rowCount);
                SendConfidentialCsvEmail(csvContent, rowCount);

                return Ok(new
                {
                    message = "Confidential data submitted successfully.",
                    rows = rowCount
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private string ConvertEvaluationRequestToCsv(EvaluationRequest request, out int rowCount)
        {
            var regNo = request.Reg_no?.Trim();
            var empNo = request.Emp_no?.Trim();
            var courseNo = request.Course_no?.Trim();

            var student = db.STMTRs.FirstOrDefault(s => s.Reg_No.Trim() == regNo);
            var teacher = db.EMPMTRs.FirstOrDefault(t => t.Emp_no.Trim() == empNo);
            var course = db.CRSMTRs.FirstOrDefault(c => c.Course_no.Trim() == courseNo);

            var questionIds = request.Answers.Select(a => a.Question_ID).Distinct().ToList();
            var questionMap = db.Question_Answer
                .Where(q => questionIds.Contains(q.Question_ID))
                .ToDictionary(q => q.Question_ID, q => q.Question);
            var requestQuestionMap = (request.QuestionList ?? new List<QuestionDetail>())
                .Where(q => q.ResolvedQuestionId > 0)
                .GroupBy(q => q.ResolvedQuestionId)
                .ToDictionary(g => g.Key, g => g.First().ResolvedQuestionText);

            var studentName = student == null
                ? string.Empty
                : (student.St_firstname + " " + (student.St_middlename ?? "") + " " + student.St_lastname).Replace("  ", " ").Trim();

            var teacherName = teacher?.Name?.Trim() ?? string.Empty;
            var courseTitle = course?.Course_desc?.Trim() ?? string.Empty;

            var csv = new StringBuilder();
            csv.AppendLine("Emp_no,Reg_no,StudentName,TeacherName,Course_no,CourseTitle,Discipline,Question_ID,Question,Rating,Comment");

            foreach (var answer in request.Answers)
            {
                string questionText;
                if (!requestQuestionMap.TryGetValue(answer.Question_ID, out questionText) &&
                    !questionMap.TryGetValue(answer.Question_ID, out questionText))
                {
                    questionText = "Question not found (ID: " + answer.Question_ID + ")";
                }

                csv.AppendLine(string.Join(",",
                    EscapeCsvField(empNo),
                    EscapeCsvField(regNo),
                    EscapeCsvField(studentName),
                    EscapeCsvField(teacherName),
                    EscapeCsvField(courseNo),
                    EscapeCsvField(courseTitle),
                    EscapeCsvField(request.Discipline?.Trim()),
                    EscapeCsvField(answer.Question_ID.ToString()),
                    EscapeCsvField(questionText),
                    EscapeCsvField(answer.Rating.ToString()),
                    EscapeCsvField(Limit(NormalizeCsvText(answer.Comment), 100))
                ));
            }

            rowCount = request.Answers.Count;
            return csv.ToString();
        }

        private static string EscapeCsvField(string value)
        {
            var safeValue = value ?? string.Empty;
            bool mustQuote = safeValue.Contains(",") || safeValue.Contains("\"") || safeValue.Contains("\n") || safeValue.Contains("\r");

            if (safeValue.Contains("\""))
            {
                safeValue = safeValue.Replace("\"", "\"\"");
            }

            return mustQuote ? "\"" + safeValue + "\"" : safeValue;
        }

        private static string NormalizeCsvText(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? string.Empty
                : value.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
        }

        private static string Limit(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
            {
                return value;
            }

            return value.Substring(0, maxLength);
        }

        private static string GetFullExceptionMessage(Exception ex)
        {
            var messages = new List<string>();
            while (ex != null)
            {
                messages.Add(ex.Message);
                ex = ex.InnerException;
            }

            return string.Join(" | ", messages);
        }

        private void SendConfidentialCsvEmail(string csvContent, int rowCount)
        {
            string smtpHost = ConfigurationManager.AppSettings["ConfidentialSmtpHost"];
            string smtpPortRaw = ConfigurationManager.AppSettings["ConfidentialSmtpPort"];
            string enableSslRaw = ConfigurationManager.AppSettings["ConfidentialSmtpEnableSsl"];
            string smtpUser = ConfigurationManager.AppSettings["ConfidentialSmtpUser"];
            string smtpPass = ConfigurationManager.AppSettings["ConfidentialSmtpPassword"];
            string fromEmail = ConfigurationManager.AppSettings["ConfidentialFromEmail"];
            string aesKeyBase64 = ConfigurationManager.AppSettings["ConfidentialAesKeyBase64"];
            if (string.IsNullOrWhiteSpace(aesKeyBase64))
            {
                aesKeyBase64 = FallbackAesKeyBase64;
            }

            if (string.IsNullOrWhiteSpace(smtpHost) ||
                string.IsNullOrWhiteSpace(smtpPortRaw) ||
                string.IsNullOrWhiteSpace(smtpUser) ||
                string.IsNullOrWhiteSpace(smtpPass) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("SMTP settings are missing in Web.config appSettings.");
            }

            int smtpPort;
            if (!int.TryParse(smtpPortRaw, out smtpPort))
            {
                throw new InvalidOperationException("Invalid ConfidentialSmtpPort value in Web.config.");
            }

            bool enableSsl = true;
            bool.TryParse(enableSslRaw, out enableSsl);
            byte[] encryptedAttachment = EncryptCsvWithAes256(csvContent, aesKeyBase64);

            using (var message = new MailMessage())
            {
                message.From = new MailAddress(fromEmail);
                message.To.Add("teacherevaluation12@gmail.com");
                message.Subject = "Confidential Submission CSV";
                message.Body = "Attached is the AES-256 encrypted confidential submission file.\nRows: " + rowCount;

                var stream = new MemoryStream(encryptedAttachment);
                var attachment = new Attachment(stream, "confidential-submission.csv.enc", "application/octet-stream");
                message.Attachments.Add(attachment);

                using (var smtp = new SmtpClient(smtpHost, smtpPort))
                {
                    smtp.EnableSsl = enableSsl;
                    smtp.Credentials = new NetworkCredential(smtpUser, smtpPass);
                    smtp.Send(message);
                }
            }
        }

        private byte[] EncryptCsvWithAes256(string plainText, string keyBase64)
        {
            var cipherHeader = Encoding.UTF8.GetBytes(CipherHeader);
            byte[] key;
            try
            {
                key = Convert.FromBase64String(keyBase64);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("ConfidentialAesKeyBase64 must be valid Base64.");
            }

            if (key.Length != 32)
            {
                throw new InvalidOperationException("ConfidentialAesKeyBase64 must decode to exactly 32 bytes for AES-256.");
            }

            byte[] plainBytes = Encoding.UTF8.GetBytes(plainText ?? string.Empty);
            using (var aes = Aes.Create())
            {
                aes.KeySize = 256;
                aes.BlockSize = 128;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = key;
                aes.GenerateIV();

                using (var encryptor = aes.CreateEncryptor())
                using (var output = new MemoryStream())
                {
                    // Prefix cipher metadata + IV so receiver can decrypt with the shared key.
                    output.Write(cipherHeader, 0, cipherHeader.Length);
                    output.Write(aes.IV, 0, aes.IV.Length);
                    using (var cryptoStream = new CryptoStream(output, encryptor, CryptoStreamMode.Write))
                    {
                        cryptoStream.Write(plainBytes, 0, plainBytes.Length);
                        cryptoStream.FlushFinalBlock();
                    }
                    return output.ToArray();
                }
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
    public List<QuestionDetail> QuestionList { get; set; }
    public List<AnswerDetail> Answers { get; set; }
}

public class AnswerDetail
{
    public int Question_ID { get; set; }
    public int Rating { get; set; }
    public string Comment { get; set; }
}

public class QuestionDetail
{
    public int Question_ID { get; set; }
    public int Question_Id { get; set; }
    public string Question { get; set; }
    public string Question1 { get; set; }
    public string Question_type { get; set; }
    public string RawType { get; set; }

    public int ResolvedQuestionId
    {
        get { return Question_ID > 0 ? Question_ID : Question_Id; }
    }

    public string ResolvedQuestionText
    {
        get { return !string.IsNullOrWhiteSpace(Question) ? Question : Question1; }
    }
}