using System;
using System.Collections.Generic;
using FYP2.Models;
using System.Configuration;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Web;
using System.Web.Http;

namespace FYP2.Controllers
{
    public class DirectorController : ApiController
    {
        private const string FallbackAesKeyBase64 = "vrHFCSCrUlrMHNWFTYJgS09SfZFC+QY0PuMuOz0pyXY=";
        private readonly Teacher_Evaluation_SystemEntities3 db = new Teacher_Evaluation_SystemEntities3();
        private readonly testingEntities1 dbTest = new testingEntities1();

        // 1. Get All Sessions (Dropdown)
        // URL: /api/Director/GetAllSessions
        [HttpGet]
        public HttpResponseMessage GetAllSessions()
        {
            try
            {
                var sessions = db.ALLOCATEs
                    .AsNoTracking()
                    .Select(a => a.SOS)
                    .Distinct()
                    .Where(s => s != null)
                    .OrderByDescending(s => s)
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, sessions);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // 2. Get Allocated Teachers
        // URL: /api/Director/GetAllocatedTeachers?session=2022FM
        [HttpGet]
        public HttpResponseMessage GetAllocatedTeachers(string session)
        {
            try
            {
                var sessionTrimmed = session?.Trim();

                var teachers = (from a in db.ALLOCATEs
                                join t in db.EMPMTRs on a.EMP_NO equals t.Emp_no
                                where a.SOS == sessionTrimmed
                                select new
                                {
                                    TeacherID = t.Emp_no,
                                    TeacherName = t.Name,
                                    Designation = t.Designation
                                })
                    .Distinct()
                    .ToList();

                if (!teachers.Any())
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No teachers found.");

                return Request.CreateResponse(HttpStatusCode.OK, teachers);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // 3. Get Allocated Courses
        // URL: /api/Director/GetAllocatedCourses?session=2022FM
        [HttpGet]
        public HttpResponseMessage GetAllocatedCourses(string session)
        {
            try
            {
                var sessionTrimmed = session?.Trim();

                var courses = (from a in db.ALLOCATEs
                               join c in db.CRSMTRs on a.COURSE_NO equals c.Course_no
                               where a.SOS == sessionTrimmed
                               select new
                               {
                                   CourseNo = c.Course_no,
                                   CourseName = c.Course_desc
                               })
                    .Distinct()
                    .ToList();

                if (!courses.Any())
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No courses found.");

                return Request.CreateResponse(HttpStatusCode.OK, courses);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // 4. Get Teachers assigned to a specific course in a specific session
        // URL: /api/Director/GetTeachersByCourse?courseId=CS101&session=2022FM
        [HttpGet]
        public HttpResponseMessage GetTeachersByCourse(string courseId, string session)
        {
            try
            {
                var sTrim = session?.Trim();
                var cTrim = courseId?.Trim();

                var teachers = (from a in db.ALLOCATEs
                                join t in db.EMPMTRs on a.EMP_NO equals t.Emp_no
                                where a.SOS == sTrim && a.COURSE_NO == cTrim
                                select new { TeacherID = t.Emp_no, TeacherName = t.Name })
                    .Distinct()
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, teachers);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetQuestionsList()
        {
            try
            {
                var questions = db.Question_Answer.Select(q => new { q.Question_ID, q.Question }).ToList();
                return Request.CreateResponse(HttpStatusCode.OK, questions);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage GetComparisonData([FromBody] GraphRequest req)
        {
            try
            {
                var sessionTrim = req.Session?.Trim();
                var courseTrim = req.CourseId?.Trim();

                var queryData = (from ev in db.Evals
                                 join st in db.STMTRs on ev.Reg_No equals st.Reg_No
                                 where req.TeacherIds.Contains(ev.Emp_no) &&
                                       ev.Course_no == courseTrim &&
                                       st.SOS == sessionTrim &&
                                       req.QuestionIds.Contains((int)ev.Question_Desc)
                                 group ev by new { ev.Emp_no, ev.Question_Desc } into g
                                 select new
                                 {
                                     TeacherID = g.Key.Emp_no,
                                     QuestionNo = g.Key.Question_Desc,
                                     AverageRating = g.Average(x => (double?)x.Answer_Marks) ?? 0
                                 }).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, queryData);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        // 5. Get Average Ratings for all teachers in a specific session
        // URL: /api/Director/GetTeacherAverageRatings?session=2022FM
        [HttpGet]
        public HttpResponseMessage GetTeacherAverageRatings(string session)
        {
            try
            {
                var year = session?.Substring(0, 4);

                var ratings = db.Evals
                    .Where(ev => ev.Answer_Marks != null)
                    .Join(db.STMTRs,
                        ev => ev.Reg_No,
                        st => st.Reg_No,
                        (ev, st) => new { ev, st })
                    .Where(x => x.st.SOS.Contains(year))
                    .GroupBy(x => x.ev.Emp_no)
                    .Select(g => new
                    {
                        TeacherID = g.Key,
                        AverageRating = g.Average(x => (double?)x.ev.Answer_Marks) ?? 0
                    })
                    .ToList();

                var result = ratings.Select(r => new
                {
                    r.TeacherID,
                    AverageRating = Math.Round(r.AverageRating, 1)
                }).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    ex.InnerException?.Message ?? ex.Message
                );
            }
        }

        [HttpPost]
        [Route("api/director/import-confidential")]
        public IHttpActionResult ImportConfidential()
        {
            try
            {
                var httpRequest = HttpContext.Current?.Request;
                if (httpRequest == null || httpRequest.Files.Count == 0)
                {
                    return BadRequest("Upload encrypted file using multipart/form-data with a file field.");
                }

                var uploadedFile = httpRequest.Files[0];
                if (uploadedFile == null || uploadedFile.ContentLength == 0)
                {
                    return BadRequest("Uploaded file is empty.");
                }

                byte[] encryptedBytes;
                using (var memory = new MemoryStream())
                {
                    uploadedFile.InputStream.CopyTo(memory);
                    encryptedBytes = memory.ToArray();
                }

                var csvContent = DecryptCsv(encryptedBytes);
                var importedRows = ParseConfidentialCsv(csvContent);

                if (!importedRows.Any())
                {
                    return BadRequest("No records found in decrypted CSV.");
                }

                var questionMap = db.Question_Answer
                    .ToList()
                    .GroupBy(q => (q.Question ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Question_ID, StringComparer.OrdinalIgnoreCase);

                var semester = GetAridSemester();
                int savedCount = 0;

                foreach (var row in importedRows)
                {
                    if (!questionMap.TryGetValue((row.Question ?? string.Empty).Trim(), out int questionId))
                    {
                        continue;
                    }

                    var eval = new Eval
                    {
                        Emp_no = row.EmpNo,
                        Reg_No = row.RegNo,
                        Course_no = row.CourseNo,
                        Discipline = row.Discipline,
                        Semester_no = semester,
                        Question_Desc = questionId,
                        Answer_Marks = row.Rating,
                        Answer_Desc = GetRatingText(row.Rating)
                    };

                    db.Evals.Add(eval);
                    savedCount++;
                }

                if (savedCount == 0)
                {
                    return BadRequest("No rows were imported. Questions did not match database question text.");
                }

                db.SaveChanges();
                return Ok(new
                {
                    message = "Encrypted confidential file imported successfully.",
                    inserted = savedCount
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("api/Director/GetActiveQuestions")]
        public HttpResponseMessage GetActiveQuestions()
        {
            try
            {
                var questions = db.Question_Answer
                    .Where(q => q.IsActive == true || q.IsActive == null)
                    .Select(q => new
                    {
                        Question_ID = q.Question_ID,
                        Question = q.Question,
                        Type = q.Description
                    }).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, questions);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }

        [HttpPost]
        [Route("api/Director/AddQuestion")]
        public IHttpActionResult AddQuestion([FromBody] Question_Answer model)
        {
            try
            {
                if (string.IsNullOrEmpty(model.Question))
                    return BadRequest("Question text is required.");

                var newEntry = new Question_Answer
                {
                    Question = model.Question,
                    Description = string.IsNullOrEmpty(model.Description) ? "T" : model.Description.ToUpper(),
                    IsActive = true
                };

                db.Question_Answer.Add(newEntry);
                db.SaveChanges();
                return Ok("Question Added Successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpDelete]
        [Route("api/Director/RemoveQuestion/{id}")]
        public IHttpActionResult RemoveQuestion(int id)
        {
            try
            {
                var question = db.Question_Answer.Find(id);
                if (question == null)
                    return NotFound();

                question.IsActive = false;
                db.SaveChanges();
                return Ok("Question removed successfully.");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("api/Director/ModifyQuestion")]
        public IHttpActionResult ModifyQuestion([FromBody] Question_Answer model)
        {
            try
            {
                var existingQuestion = db.Question_Answer.Find(model.Question_ID);

                if (existingQuestion == null)
                    return NotFound();

                existingQuestion.Updated_Question = model.Question;
                existingQuestion.IsActive = true;

                db.SaveChanges();

                return Ok("Updated ID " + model.Question_ID + " successfully in the same row.");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // URL: /api/Director/GetGenderFeedbackStats?session=2022FM&courseId=CS101&teacherId=BIIT184
        [HttpGet]
        [Route("api/Director/GetGenderFeedbackStats")]
        public HttpResponseMessage GetGenderFeedbackStats(string session, string courseId = null, string teacherId = null)
        {
            try
            {
                var sessionTrim = session?.Trim();
                var courseTrim = courseId?.Trim();
                var teacherTrim = teacherId?.Trim();

                if (string.IsNullOrEmpty(sessionTrim) || sessionTrim == "placeholder")
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "Valid session is required.");
                }

                var query = from ev in db.Evals
                            join st in db.STMTRs on ev.Reg_No.Trim() equals st.Reg_No.Trim()
                            where st.SOS.Trim() == sessionTrim
                            select new
                            {
                                ev.Answer_Marks,
                                Sex = st.Sex.Trim().ToUpper(),
                                ev.Course_no,
                                ev.Emp_no
                            };

                if (!string.IsNullOrEmpty(courseTrim))
                {
                    query = query.Where(x => x.Course_no.Trim() == courseTrim);
                }

                if (!string.IsNullOrEmpty(teacherTrim))
                {
                    query = query.Where(x => x.Emp_no.Trim() == teacherTrim);
                }

                var data = query.ToList();

                if (!data.Any())
                {
                    return Request.CreateResponse(HttpStatusCode.OK, new
                    {
                        female = "0%",
                        male = "0%",
                        overall = "0%"
                    });
                }

                double femaleAvg = data.Where(x => x.Sex == "F")
                    .Select(x => (double)x.Answer_Marks)
                    .DefaultIfEmpty(0)
                    .Average();

                double maleAvg = data.Where(x => x.Sex == "M")
                    .Select(x => (double)x.Answer_Marks)
                    .DefaultIfEmpty(0)
                    .Average();

                double overallAvg = data.Select(x => (double)x.Answer_Marks)
                    .Average();

                var result = new
                {
                    female = Math.Round((femaleAvg / 5) * 100, 1) + "%",
                    male = Math.Round((maleAvg / 5) * 100, 1) + "%",
                    overall = Math.Round((overallAvg / 5) * 100, 1) + "%"
                };

                return Request.CreateResponse(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Database Error: " + ex.Message);
            }
        }

        private string DecryptCsv(byte[] encryptedPayload)
        {
            string aesKeyBase64 = ConfigurationManager.AppSettings["ConfidentialAesKeyBase64"];
            if (string.IsNullOrWhiteSpace(aesKeyBase64))
            {
                aesKeyBase64 = FallbackAesKeyBase64;
            }

            byte[] key;
            try
            {
                key = Convert.FromBase64String(aesKeyBase64);
            }
            catch (FormatException)
            {
                throw new InvalidOperationException("ConfidentialAesKeyBase64 must be valid Base64.");
            }

            if (key.Length != 32)
            {
                throw new InvalidOperationException("ConfidentialAesKeyBase64 must decode to 32 bytes for AES-256.");
            }

            if (encryptedPayload == null || encryptedPayload.Length <= 16)
            {
                throw new InvalidOperationException("Encrypted file is invalid or too short.");
            }

            var candidateOffsets = ResolveCipherOffsets(encryptedPayload);
            Exception lastCryptoException = null;

            foreach (var offset in candidateOffsets)
            {
                int ivOffset = offset;
                int cipherOffset = ivOffset + 16;
                int cipherLength = encryptedPayload.Length - cipherOffset;

                if (cipherLength <= 0 || (cipherLength % 16) != 0)
                {
                    continue;
                }

                var iv = new byte[16];
                Buffer.BlockCopy(encryptedPayload, ivOffset, iv, 0, iv.Length);

                var cipherBytes = new byte[cipherLength];
                Buffer.BlockCopy(encryptedPayload, cipherOffset, cipherBytes, 0, cipherLength);

                try
                {
                    using (var aes = Aes.Create())
                    {
                        aes.KeySize = 256;
                        aes.BlockSize = 128;
                        aes.Mode = CipherMode.CBC;
                        aes.Padding = PaddingMode.PKCS7;
                        aes.Key = key;
                        aes.IV = iv;

                        using (var decryptor = aes.CreateDecryptor())
                        using (var input = new MemoryStream(cipherBytes))
                        using (var crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read))
                        using (var output = new MemoryStream())
                        {
                            crypto.CopyTo(output);
                            return Encoding.UTF8.GetString(output.ToArray());
                        }
                    }
                }
                catch (CryptographicException ex)
                {
                    lastCryptoException = ex;
                }
            }

            throw new InvalidOperationException(
                "Unable to decrypt confidential file. Ensure file format/header and ConfidentialAesKeyBase64 are correct.",
                lastCryptoException);
        }

        private List<int> ResolveCipherOffsets(byte[] encryptedPayload)
        {
            var offsets = new List<int>();
            var headerNoNewline = Encoding.UTF8.GetBytes("CYPHER:AES-256-CBC");
            var headerWithNewline = Encoding.UTF8.GetBytes("CYPHER:AES-256-CBC\n");
            var headerWithCrLf = Encoding.UTF8.GetBytes("CYPHER:AES-256-CBC\r\n");

            if (StartsWith(encryptedPayload, headerWithNewline))
            {
                offsets.Add(headerWithNewline.Length);
            }

            if (StartsWith(encryptedPayload, headerNoNewline))
            {
                offsets.Add(headerNoNewline.Length);
            }

            if (StartsWith(encryptedPayload, headerWithCrLf))
            {
                offsets.Add(headerWithCrLf.Length);
            }

            offsets.Add(0);
            return offsets.Distinct().ToList();
        }

        private bool StartsWith(byte[] source, byte[] prefix)
        {
            if (source == null || prefix == null || source.Length < prefix.Length)
            {
                return false;
            }

            for (int i = 0; i < prefix.Length; i++)
            {
                if (source[i] != prefix[i])
                {
                    return false;
                }
            }

            return true;
        }

        private List<ConfidentialCsvRow> ParseConfidentialCsv(string csv)
        {
            var results = new List<ConfidentialCsvRow>();
            if (string.IsNullOrWhiteSpace(csv))
            {
                return results;
            }

            var lines = csv
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (lines.Count <= 1)
            {
                return results;
            }

            for (int i = 1; i < lines.Count; i++)
            {
                var columns = SplitCsvLine(lines[i]);
                if (columns.Count < 9)
                {
                    continue;
                }

                if (!int.TryParse(columns[8], out int rating))
                {
                    continue;
                }

                results.Add(new ConfidentialCsvRow
                {
                    EmpNo = columns[0]?.Trim(),
                    RegNo = columns[1]?.Trim(),
                    CourseNo = columns[4]?.Trim(),
                    Discipline = columns[6]?.Trim(),
                    Question = columns[7]?.Trim(),
                    Rating = rating
                });
            }

            return results;
        }

        private List<string> SplitCsvLine(string line)
        {
            var fields = new List<string>();
            if (line == null)
            {
                return fields;
            }

            var current = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (ch == ',' && !inQuotes)
                {
                    fields.Add(current.ToString());
                    current.Clear();
                }
                else
                {
                    current.Append(ch);
                }
            }

            fields.Add(current.ToString());
            return fields;
        }

        private string GetAridSemester()
        {
            int year = DateTime.Now.Year;
            int month = DateTime.Now.Month;
            string suffix = (month >= 7) ? "FM" : "SM";
            return year + suffix;
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
    }

    public class GraphRequest
    {
        public List<string> TeacherIds { get; set; }
        public List<int> QuestionIds { get; set; }
        public string CourseId { get; set; }
        public string Session { get; set; }
    }

    public class ConfidentialCsvRow
    {
        public string EmpNo { get; set; }
        public string RegNo { get; set; }
        public string CourseNo { get; set; }
        public string Discipline { get; set; }
        public string Question { get; set; }
        public int Rating { get; set; }
    }
}
