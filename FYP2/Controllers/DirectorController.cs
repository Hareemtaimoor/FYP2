using FYP2.Models;
using System;
using System.Collections.Generic;
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
using static System.Net.Mime.MediaTypeNames;

namespace FYP2.Controllers
{
    public class DirectorController : ApiController
    {
        private const string FallbackAesKeyBase64 = "vrHFCSCrUlrMHNWFTYJgS09SfZFC+QY0PuMuOz0pyXY=";
        private readonly Teacher_Evaluation_SystemEntities3 db = new Teacher_Evaluation_SystemEntities3();
        private readonly testingEntities2 dbTest = new testingEntities2();

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

                Dictionary<string, int> questionMap = null;
                if (importedRows.Any(r => !r.QuestionId.HasValue))
                {
                    questionMap = dbTest.conQuestion_Answer
                        .ToList()
                        .GroupBy(q => (q.Question ?? string.Empty).Trim(), StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(g => g.Key, g => g.First().Question_ID, StringComparer.OrdinalIgnoreCase);
                }

                var semester = GetAridSemester();
                int savedCount = 0;

                foreach (var row in importedRows)
                {
                    if (string.IsNullOrWhiteSpace(row.EmpNo) ||
                        string.IsNullOrWhiteSpace(row.RegNo) ||
                        string.IsNullOrWhiteSpace(row.CourseNo) ||
                        string.IsNullOrWhiteSpace(row.Discipline))
                    {
                        continue;
                    }

                    int questionId;
                    if (row.QuestionId.HasValue)
                    {
                        questionId = row.QuestionId.Value;
                    }
                    else if (questionMap == null || !questionMap.TryGetValue((row.Question ?? string.Empty).Trim(), out questionId))
                    {
                        continue;
                    }

                    var eval = new ConfEval
                    {
                        Emp_no = Limit(row.EmpNo, 7),
                        Reg_No = Limit(row.RegNo, 50),
                        Course_no = Limit(row.CourseNo, 9),
                        Discipline = Limit(row.Discipline, 20),
                        Semester_no = Limit(semester, 100),
                        Question = row.Question,
                        Question_Desc = questionId,
                        Answer_Marks = row.Rating,
                        Answer_Desc = Limit(GetRatingText(row.Rating), 50),
                        Comment = Limit(row.Comment, 100)
                    };

                    dbTest.ConfEvals.Add(eval);
                    savedCount++;
                }

                if (savedCount == 0)
                {
                    return BadRequest("No rows were imported. Questions did not match database question text.");
                }

                dbTest.SaveChanges();
                return Ok(new
                {
                    message = "Encrypted confidential file imported successfully.",
                    inserted = savedCount
                });
            }
            catch (Exception ex)
            {
                return BadRequest(GetFullExceptionMessage(ex));
            }
        }

        // --- Confidential evaluation (testing DB / ConfEval) — mirror simple Eval dashboard ---

        /// <summary>Distinct semester keys stored on confidential imports (e.g. 2026FM). Use for dropdowns.</summary>
        [HttpGet]
        [Route("api/Director/GetConfidentialSemesters")]
        public HttpResponseMessage GetConfidentialSemesters()
        {
            try
            {
                var list = dbTest.ConfEvals.AsNoTracking()
                    .Where(x => x.Semester_no != null)
                    .Select(x => x.Semester_no)
                    .Distinct()
                    .ToList()
                    .Select(s => s.Trim())
                    .Where(s => s.Length > 0)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(s => s)
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, list);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, GetFullExceptionMessage(ex));
            }
        }

        /// <summary>Confidential question bank (testing.conQuestion_Answer).</summary>
        [HttpGet]
        [Route("api/Director/GetConfidentialQuestions")]
        public HttpResponseMessage GetConfidentialQuestions()
        {
            try
            {
                var questions = dbTest.conQuestion_Answer.AsNoTracking()
                    .Select(q => new
                    {
                        q.Question_ID,
                        q.Question,
                        Type = q.Description
                    })
                    .OrderBy(q => q.Question_ID)
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, questions);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, GetFullExceptionMessage(ex));
            }
        }

        /// <summary>
        /// Teachers who appear on confidential evaluations for students in the given SOS session
        /// (ConfEval joined to STMTR on Reg_No). Same response shape as GetAllocatedTeachers.
        /// </summary>
        [HttpGet]
        [Route("api/Director/GetConfidentialAllocatedTeachers")]
        public HttpResponseMessage GetConfidentialAllocatedTeachers(string session)
        {
            try
            {
                var sessionTrim = session?.Trim();
                if (string.IsNullOrEmpty(sessionTrim))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "session query parameter is required.");
                }

                var teachers = (from ce in dbTest.ConfEvals.AsNoTracking()
                                join st in db.STMTRs.AsNoTracking() on ce.Reg_No.Trim() equals st.Reg_No.Trim()
                                where st.SOS.Trim() == sessionTrim
                                join t in db.EMPMTRs.AsNoTracking() on ce.Emp_no equals t.Emp_no
                                select new
                                {
                                    TeacherID = t.Emp_no,
                                    TeacherName = t.Name,
                                    Designation = t.Designation
                                })
                    .Distinct()
                    .ToList();

                if (!teachers.Any())
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No confidential evaluations found for this session.");
                }

                return Request.CreateResponse(HttpStatusCode.OK, teachers);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, GetFullExceptionMessage(ex));
            }
        }

        /// <summary>
        /// Courses that appear on confidential evaluations for students in the given SOS session.
        /// Same response shape as GetAllocatedCourses (CourseNo, CourseName).
        /// </summary>
        [HttpGet]
        [Route("api/Director/GetConfidentialAllocatedCourses")]
        public HttpResponseMessage GetConfidentialAllocatedCourses(string session)
        {
            try
            {
                var sessionTrim = session?.Trim();
                if (string.IsNullOrEmpty(sessionTrim))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "session query parameter is required.");
                }

                var courses = (from ce in dbTest.ConfEvals.AsNoTracking()
                               join st in db.STMTRs.AsNoTracking() on ce.Reg_No.Trim() equals st.Reg_No.Trim()
                               where st.SOS.Trim() == sessionTrim
                               join c in db.CRSMTRs.AsNoTracking() on ce.Course_no equals c.Course_no into cj
                               from c in cj.DefaultIfEmpty()
                               select new
                               {
                                   CourseNo = ce.Course_no,
                                   CourseName = c != null ? c.Course_desc : ce.Course_no
                               })
                    .Distinct()
                    .ToList();

                if (!courses.Any())
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound, "No confidential evaluations found for this session.");
                }

                return Request.CreateResponse(HttpStatusCode.OK, courses);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, GetFullExceptionMessage(ex));
            }
        }

        /// <summary>Paged raw rows from ConfEval. semester = Semester_no (same value used at import).</summary>
        [HttpGet]
        [Route("api/Director/GetConfidentialEvaluations")]
        public HttpResponseMessage GetConfidentialEvaluations(
            string semester = null,
            string empNo = null,
            string courseNo = null,
            string regNo = null,
            string discipline = null,
            int skip = 0,
            int take = 500)
        {
            try
            {
                if (take < 1) take = 50;
                if (take > 2000) take = 2000;
                if (skip < 0) skip = 0;

                var q = dbTest.ConfEvals.AsNoTracking().AsQueryable();

                var semTrim = semester?.Trim();
                if (!string.IsNullOrEmpty(semTrim))
                {
                    q = q.Where(x => x.Semester_no == semTrim);
                }

                var empTrim = empNo?.Trim();
                if (!string.IsNullOrEmpty(empTrim))
                {
                    q = q.Where(x => x.Emp_no == empTrim);
                }

                var courseTrim = courseNo?.Trim();
                if (!string.IsNullOrEmpty(courseTrim))
                {
                    q = q.Where(x => x.Course_no == courseTrim);
                }

                var regTrim = regNo?.Trim();
                if (!string.IsNullOrEmpty(regTrim))
                {
                    q = q.Where(x => x.Reg_No == regTrim);
                }

                var discTrim = discipline?.Trim();
                if (!string.IsNullOrEmpty(discTrim))
                {
                    q = q.Where(x => x.Discipline == discTrim);
                }

                var total = q.Count();

                var rows = q.OrderByDescending(x => x.EvalID)
                    .Skip(skip)
                    .Take(take)
                    .Select(x => new
                    {
                        x.EvalID,
                        x.Emp_no,
                        x.Reg_No,
                        x.Course_no,
                        x.Discipline,
                        x.Semester_no,
                        x.Question,
                        x.Question_Desc,
                        x.Answer_Desc,
                        x.Answer_Marks,
                        x.Comment
                    })
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, new { total, skip, take, rows });
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, GetFullExceptionMessage(ex));
            }
        }

        /// <summary>Average rating per teacher for one confidential semester (ConfEval.Semester_no).</summary>
        [HttpGet]
        [Route("api/Director/GetConfidentialTeacherAverageRatings")]
        public HttpResponseMessage GetConfidentialTeacherAverageRatings(string semester)
        {
            try
            {
                var semTrim = semester?.Trim();
                if (string.IsNullOrEmpty(semTrim))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "semester query parameter is required (ConfEval.Semester_no, e.g. 2026FM).");
                }

                var ratings = (from ce in dbTest.ConfEvals.AsNoTracking()
                                 where ce.Semester_no == semTrim && ce.Answer_Marks != null
                                 group ce by ce.Emp_no into g
                                 select new
                                 {
                                     TeacherID = g.Key,
                                     AverageRating = g.Average(x => (double?)x.Answer_Marks) ?? 0
                                 }).ToList();

                var empIds = ratings.Select(r => r.TeacherID).Where(id => id != null).Distinct().ToList();
                var nameLookup = db.EMPMTRs.AsNoTracking()
                    .Where(t => empIds.Contains(t.Emp_no))
                    .Select(t => new { t.Emp_no, t.Name })
                    .ToList()
                    .GroupBy(t => t.Emp_no.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => (g.First().Name ?? "").Trim(), StringComparer.OrdinalIgnoreCase);

                var result = ratings.Select(r =>
                {
                    var key = (r.TeacherID ?? "").Trim();
                    string name;
                    if (!nameLookup.TryGetValue(key, out name))
                    {
                        name = key;
                    }

                    return new
                    {
                        r.TeacherID,
                        TeacherName = name,
                        AverageRating = Math.Round(r.AverageRating, 1)
                    };
                }).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, GetFullExceptionMessage(ex));
            }
        }

        /// <summary>Same idea as GetComparisonData: averages per teacher and question for confidential rows.</summary>
        [HttpPost]
        [Route("api/Director/GetConfidentialComparisonData")]
        public HttpResponseMessage GetConfidentialComparisonData([FromBody] GraphRequest req)
        {
            try
            {
                if (req == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Request body is required.");
                }

                if (req.TeacherIds == null || !req.TeacherIds.Any(id => !string.IsNullOrWhiteSpace(id)))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "TeacherIds must be a non-empty array.");
                }

                if (req.QuestionIds == null || !req.QuestionIds.Any())
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "QuestionIds must be a non-empty array.");
                }

                var semesterTrim = req.Session?.Trim();
                var courseTrim = req.CourseId?.Trim();
                if (string.IsNullOrEmpty(semesterTrim) || string.IsNullOrEmpty(courseTrim))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Session (confidential semester / Semester_no) and CourseId are required.");
                }

                var teacherIds = req.TeacherIds.Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList();
                var questionIds = req.QuestionIds.Distinct().ToList();

                var queryData = (from ce in dbTest.ConfEvals.AsNoTracking()
                                 where teacherIds.Contains(ce.Emp_no) &&
                                       ce.Course_no == courseTrim &&
                                       ce.Semester_no == semesterTrim &&
                                       questionIds.Contains(ce.Question_Desc)
                                 group ce by new { ce.Emp_no, ce.Question_Desc } into g
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
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, GetFullExceptionMessage(ex));
            }
        }

        /// <summary>Mirror GetTeacherStudentEvalDetails: per-question average for one teacher/course/confidential semester.</summary>
        [HttpGet]
        [Route("api/Director/GetConfidentialTeacherQuestionDetails")]
        public HttpResponseMessage GetConfidentialTeacherQuestionDetails(string teacherId, string semester, string courseId)
        {
            try
            {
                var tId = teacherId?.Trim();
                var sem = semester?.Trim();
                var cId = courseId?.Trim();

                if (string.IsNullOrEmpty(tId) || string.IsNullOrEmpty(sem) || string.IsNullOrEmpty(cId))
                {
                    return Request.CreateResponse(HttpStatusCode.BadRequest, "teacherId, semester, and courseId are required.");
                }

                var data = (from ce in dbTest.ConfEvals.AsNoTracking()
                            where ce.Emp_no == tId && ce.Semester_no == sem && ce.Course_no == cId
                            group ce by ce.Question_Desc into g
                            select new
                            {
                                label = "Q" + g.Key,
                                score = Math.Round(g.Average(x => (double?)x.Answer_Marks) ?? 0, 1)
                            })
                    .OrderBy(x => x.label)
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, data);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, GetFullExceptionMessage(ex));
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

                int? questionId = null;
                string question;
                string comment = string.Empty;
                int ratingColumnIndex;

                if (columns.Count >= 11)
                {
                    if (int.TryParse(columns[7], out int parsedQuestionId))
                    {
                        questionId = parsedQuestionId;
                    }

                    question = columns[8]?.Trim();
                    ratingColumnIndex = 9;
                    comment = columns[10]?.Trim();
                }
                else
                {
                    question = columns[7]?.Trim();
                    ratingColumnIndex = 8;
                }

                if (!int.TryParse(columns[ratingColumnIndex], out int rating))
                {
                    continue;
                }

                results.Add(new ConfidentialCsvRow
                {
                    EmpNo = columns[0]?.Trim(),
                    RegNo = columns[1]?.Trim(),
                    CourseNo = columns[4]?.Trim(),
                    Discipline = columns[6]?.Trim(),
                    QuestionId = questionId,
                    Question = question,
                    Rating = rating,
                    Comment = comment
                });
            }

            return results;
        }

        private string Limit(string value, int maxLength)
        {
            var trimmed = value?.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.Length <= maxLength)
            {
                return trimmed;
            }

            return trimmed.Substring(0, maxLength);
        }

        private string GetFullExceptionMessage(Exception ex)
        {
            var validationException = ex as System.Data.Entity.Validation.DbEntityValidationException;
            if (validationException != null)
            {
                var validationMessages = validationException.EntityValidationErrors
                    .SelectMany(e => e.ValidationErrors)
                    .Select(e => e.PropertyName + ": " + e.ErrorMessage);

                return string.Join(" | ", validationMessages);
            }

            var messages = new List<string>();
            while (ex != null)
            {
                messages.Add(ex.Message);
                ex = ex.InnerException;
            }

            return string.Join(" | ", messages);
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
        [HttpGet]
        [Route("api/Director/GetTeacherPeerEvalDetails")]
        public HttpResponseMessage GetTeacherPeerEvalDetails(string teacherId, string session)
        {
            try
            {
                var tId = teacherId.Trim();
                // Note: Agar aapki PeerEvaluation table mein session ka column nahi hai to niche wali line filter se hata dein
                // var sess = session.Trim(); 

                var data = db.PeerEvaluations
                    .Where(p => p.Target_Emp_no == tId)
                    .GroupBy(p => p.Question_Desc)
                    .Select(g => new
                    {
                        label = "Q" + g.Key,
                        score = Math.Round(g.Average(x => (double?)x.Answer_Marks) ?? 0, 1)
                    })
                    .ToList();

                if (!data.Any())
                {
                    // Empty list agar data na mile
                    return Request.CreateResponse(HttpStatusCode.OK, new List<object>());
                }

                return Request.CreateResponse(HttpStatusCode.OK, data);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
        [HttpGet]
        public HttpResponseMessage GetPeerAverageRatings(string session)
        {
            try
            {
                // Extract year (e.g., "2022")
                var year = session?.Substring(0, 4);

                // PeerEvaluation table mein Answer_Marks aur Target_Emp_no hain
                var peerRatings = db.PeerEvaluations
                    .Where(pe => pe.Answer_Marks != null)
                    // Note: Agar PeerEvaluation mein session ka column nahi hai, 
                    // to year filter hatana hoga ya join lagana hoga.
                    .GroupBy(pe => pe.Target_Emp_no) // Grouping by Evaluated Teacher
                    .Select(g => new
                    {
                        TeacherID = g.Key,
                        AverageRating = g.Average(x => (double?)x.Answer_Marks) ?? 0
                    })
                    .ToList();

                var result = peerRatings.Select(r => new
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
        [HttpGet]
        [Route("api/Director/GetCommonCoursesBySession_Teachers")]
        public HttpResponseMessage GetCommonCoursesBySession_Teachers(string session, string teacherIds)
        {
            try
            {
                var sess = session?.Trim();
                var selectedTeacherList = teacherIds.Split(',')
                    .Select(id => id.Trim())
                    .ToList();
                int teacherCount = selectedTeacherList.Count;

                var courses = (from ev in db.Evals
                               join st in db.STMTRs on ev.Reg_No equals st.Reg_No
                               join c in db.CRSMTRs on ev.Course_no equals c.Course_no
                               // We join with ALLOCATE to link courses to the specific teachers
                               join a in db.ALLOCATEs on new { C = ev.Course_no, S = st.SOS }
                                                 equals new { C = a.COURSE_NO, S = a.SOS }
                               where st.SOS == sess && selectedTeacherList.Contains(ev.Emp_no)
                               group ev by new { ev.Course_no, c.Course_desc } into g
                               // Check if the number of distinct teachers in the evaluations 
                               // for this course matches the count of teachers selected
                               where g.Select(x => x.Emp_no).Distinct().Count() == teacherCount
                               select new
                               {
                                   Course_no = g.Key.Course_no,
                                   Course_desc = g.Key.Course_desc
                               })
                .Distinct()
                .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, courses);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
            }
        }
        [HttpPost]
        public HttpResponseMessage GetGradeDistribution([FromBody] GetGradeDistributionBody body)
        {
            try
            {
                if (body == null)
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Body is required");

                var teacherIds = MergeIdLists(body.TeacherIds, body.teacherIds)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var courseIds = MergeIdLists(body.CourseIds, body.courseIds)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var sessionTrim = (body.Session ?? body.session ?? "").Trim();

                if (teacherIds.Count == 0)
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "TeacherIds is required.");
                if (courseIds.Count == 0)
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "CourseIds is required.");
                if (string.IsNullOrEmpty(sessionTrim))
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Session is required.");

                // One row per student (Reg_No) per teacher: average marks over all selected courses & questions
                var perStudentTeacher = (from ev in db.Evals.AsNoTracking()
                                         join st in db.STMTRs.AsNoTracking() on ev.Reg_No equals st.Reg_No
                                         where st.SOS.Trim() == sessionTrim
                                               && teacherIds.Contains(ev.Emp_no)
                                               && courseIds.Contains(ev.Course_no)
                                         group ev by new { ev.Reg_No, ev.Emp_no } into g
                                         select new
                                         {
                                             g.Key.Reg_No,
                                             g.Key.Emp_no,
                                             AvgMark = g.Average(x => (double?)x.Answer_Marks) ?? 0
                                         }).ToList();

                var nameLookup = db.EMPMTRs.AsNoTracking()
                    .Where(t => teacherIds.Contains(t.Emp_no))
                    .Select(t => new { t.Emp_no, t.Name })
                    .ToList()
                    .GroupBy(t => t.Emp_no, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First().Name ?? g.Key, StringComparer.OrdinalIgnoreCase);

                var result = new List<object>();
                foreach (var tid in teacherIds)
                {
                    int ga = 0, gb = 0, gc = 0, gd = 0;
                    foreach (var row in perStudentTeacher.Where(x => string.Equals(x.Emp_no, tid, StringComparison.OrdinalIgnoreCase)))
                    {
                        switch (LetterGradeFromAvg(row.AvgMark))
                        {
                            case "A": ga++; break;
                            case "B": gb++; break;
                            case "C": gc++; break;
                            default: gd++; break;
                        }
                    }

                    string tname;
                    if (!nameLookup.TryGetValue(tid, out tname) || string.IsNullOrWhiteSpace(tname))
                        tname = tid;

                    result.Add(new
                    {
                        TeacherID = tid,
                        TeacherName = tname,
                        GradeA = ga,
                        GradeB = gb,
                        GradeC = gc,
                        GradeD = gd
                    });
                }

                return Request.CreateResponse(HttpStatusCode.OK, result);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    ex.InnerException?.Message ?? ex.Message);
            }
        }

        private static IEnumerable<string> MergeIdLists(IEnumerable<string> a, IEnumerable<string> b)
        {
            if (a != null)
            {
                foreach (var x in a)
                    yield return x;
            }
            if (b != null)
            {
                foreach (var x in b)
                    yield return x;
            }
        }

        /// <summary>Map 1–5 style averages to A–D (tune thresholds to your policy).</summary>
        private static string LetterGradeFromAvg(double avg)
        {
            if (avg >= 4.25) return "A";
            if (avg >= 3.5) return "B";
            if (avg >= 2.5) return "C";
            return "D";
        }


        public class GetGradeDistributionBody
        {
            public List<string> TeacherIds { get; set; }
            public List<string> CourseIds { get; set; }
            public string Session { get; set; }

            /// <summary>Optional camelCase binding from some clients.</summary>
            public List<string> teacherIds { get; set; }
            public List<string> courseIds { get; set; }
            public string session { get; set; }
        }
        [HttpGet]
        public HttpResponseMessage GetTeacherStudentEvalDetails(
   string teacherId,
   string session,
   string courseId)
        {
            try
            {
                var tId = teacherId?.Trim();
                var sess = session?.Trim();
                var cId = courseId?.Trim();

                if (string.IsNullOrEmpty(tId) ||
                    string.IsNullOrEmpty(sess) ||
                    string.IsNullOrEmpty(cId))
                {
                    return Request.CreateResponse(
                        HttpStatusCode.BadRequest,
                        "All parameters are required.");
                }

                var data = (from ev in db.Evals
                            join st in db.STMTRs
                            on ev.Reg_No.Trim() equals st.Reg_No.Trim()
                            where ev.Emp_no.Trim() == tId
                                  && st.SOS.Trim() == sess
                                  && ev.Course_no.Trim() == cId
                            group ev by ev.Question_Desc into g
                            select new
                            {
                                label = "Q" + g.Key,
                                score = Math.Round(
                                    g.Average(x => (double?)x.Answer_Marks) ?? 0,
                                    1)
                            })
                            .OrderBy(x => x.label)
                            .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, data);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    ex.Message);
            }
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
        public int? QuestionId { get; set; }
        public string Question { get; set; }
        public int Rating { get; set; }
        public string Comment { get; set; }
    }
}