using System;
using System.Collections.Generic;
using FYP2.Models;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace FYP2.Controllers
{
    public class DirectorController : ApiController
    {
        Teacher_Evaluation_SystemEntities3 db = new Teacher_Evaluation_SystemEntities3();

        // 1. Get All Sessions (Dropdown)
        // URL: /api/Director/GetAllSessions
        [HttpGet]
        public HttpResponseMessage GetAllSessions()
        {
            try
            {
                // Adding .AsNoTracking() makes it faster for read-only lists
                var sessions = db.ALLOCATEs
                                 .AsNoTracking()
                                 .Select(a => a.SOS)
                                 .Distinct()
                                 .Where(s => s != null) // Filter out nulls early
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
                                .Distinct().ToList();

                return Request.CreateResponse(HttpStatusCode.OK, teachers);
            }
            catch (Exception ex) { return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        [HttpGet]
        public HttpResponseMessage GetQuestionsList()
        {
            try
            {
                var questions = db.Question_Answer.Select(q => new { q.Question_ID, q.Question }).ToList();
                return Request.CreateResponse(HttpStatusCode.OK, questions);
            }
            catch (Exception ex) { return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message); }
        }

        [HttpPost]
        public HttpResponseMessage GetComparisonData([FromBody] GraphRequest req)
        {
            try
            {
                // Trim inputs to avoid whitespace mismatches
                var sessionTrim = req.Session?.Trim();
                var courseTrim = req.CourseId?.Trim();

                var queryData = (from ev in db.Evals
                                     // Joining with STMTR to ensure we only get evaluations from students in the specific session
                                 join st in db.STMTRs on ev.Reg_No equals st.Reg_No
                                 where req.TeacherIds.Contains(ev.Emp_no) &&
                                       ev.Course_no == courseTrim &&
                                       st.SOS == sessionTrim && // Matches '2017FM', '2017SM' etc from your image
                                       req.QuestionIds.Contains((int)ev.Question_Desc)
                                 group ev by new { ev.Emp_no, ev.Question_Desc } into g
                                 select new
                                 {
                                     TeacherID = g.Key.Emp_no, // e.g., "BIIT184"
                                     QuestionNo = g.Key.Question_Desc,
                                     // Average of Answer_Marks (the 1-5 values)
                                     AverageRating = g.Average(x => (double?)x.Answer_Marks) ?? 0
                                 }).ToList();

                return Request.CreateResponse(HttpStatusCode.OK, queryData);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.Message);
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
}