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
        public HttpResponseMessage GetAvailableCHRDates(string tId)
        {
            try
            {
                // Sirf un dinon ki dates nikalna jin ki reports exist karti hain
                var dates = db.v_ClassHeldReport
                    .Where(cr => cr.Emp_no == tId)
                    .Select(cr => DbFunctions.TruncateTime(cr.ReportDate))
                    .Distinct()
                    .OrderByDescending(d => d)
                    .ToList();

                // Ensure karein ke list null na ho, empty array bhejain
                var formattedDates = dates
                    .Where(d => d.HasValue)
                    .Select(d => d.Value.ToString("yyyy-MM-dd"))
                    .ToList();

                return Request.CreateResponse(HttpStatusCode.OK, formattedDates);
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
                // 1. Teacher ki basic profile info get karein
                var profile = db.EMPMTRs
                    .Where(t => t.Emp_no == tId)
                    .Select(t => new { t.Name, t.Designation })
                    .FirstOrDefault();

                // 2. CHR Reports fetch karein
                var reports = db.v_ClassHeldReport
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
                   .ToList();

                // 3. Agar data nahi milta toh empty list bhejni chahiye 404 ke bajaye (Web standard)
                // Taake frontend crash na ho
                var responseData = new
                {
                    Profile = profile ?? new { Name = "Not Found", Designation = "N/A" },
                    Reports = reports
                };

                return Request.CreateResponse(HttpStatusCode.OK, responseData);
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
                // 1. Database se dates nikalna aur null values ko filter karna
                // .AsEnumerable() use karne se memory mein formatting asan ho jati hai
                var dates = db.v_TeacherAttendance_EMPMTR
                    .Where(t => t.Emp_no == teacherId && t.AttendanceDate != null)
                    .Select(t => t.AttendanceDate)
                    .ToList();

                // 2. Check karein ke list khali to nahi
                if (dates == null || !dates.Any())
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No records found for this Teacher.");
                }

                // 3. Min aur Max nikalne ka safe tarika
                // Agar list DateTime? (nullable) hai to cast karein, warna seedha use karein
                var rawMin = dates.Min();
                var rawMax = dates.Max();

                if (rawMin == null || rawMax == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "Attendance dates are null in database.");
                }

                // DateTime mein convert karna taake .ToString() kaam kare
                DateTime minDate = Convert.ToDateTime(rawMin);
                DateTime maxDate = Convert.ToDateTime(rawMax);

                var range = new
                {
                    Start = minDate.ToString("yyyy-MM-dd"),
                    End = maxDate.ToString("yyyy-MM-dd")
                };

                return Request.CreateResponse(HttpStatusCode.OK, range);
            }
            catch (Exception ex)
            {
                // Error message ko detail mein bhejien debugging ke liye
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Backend Error: " + ex.Message);
            }
        }

        // Get Teacher Attendance (Date Range)
        [HttpGet]
       // Route lazmi add karein
        public HttpResponseMessage GetTeacherAttendanceRange(string teacherId, DateTime start, DateTime end)
        {
            try
            {
                // 1. Data fetch karte waqt null check aur range apply karein
                var res = db.v_TeacherAttendance_EMPMTR
                    .Where(t => t.Emp_no == teacherId
                           && t.AttendanceDate != null
                           && t.AttendanceDate >= start
                           && t.AttendanceDate <= end)
                    .OrderBy(t => t.AttendanceDate)
                    .ToList();

                // 2. Check karein ke result mila ya nahi
                if (res == null || !res.Any())
                {
                    return Request.CreateErrorResponse(HttpStatusCode.NotFound, "No record found in this date range.");
                }

                return Request.CreateResponse(HttpStatusCode.OK, res);
            }
            catch (Exception ex)
            {
                // Debugging ke liye inner exception bhi check karein
                string error = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, error);
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
        // 1. Teachers ki list get karne ke liye
        [HttpGet]
        public HttpResponseMessage GetAllTeachers()
        {
            try
            {
                var teachersList = db.EMPMTRs
                    .Where(e => e.Designation != null && e.Name != null)
                    .Select(e => new {
                        e.Emp_no,
                        e.Name,
                        e.Designation,
                        e.eval // Ye column lazmi shamil karein
                    })
                    .ToList();

                var finalTeachers = teachersList
                    .Where(e => !e.Designation.Trim().Equals("Junior Lecturer", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(e => e.Name.Trim())
                    .Select(e => new {
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

        // 2. Selected teachers ka 'eval' column update karne ke liye
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

    }
    }


