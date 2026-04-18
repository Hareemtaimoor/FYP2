
using FYP2.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace FYP2.Controllers
{
    public class LogInController : ApiController
    {
        Teacher_Evaluation_SystemEntities3 db =new Teacher_Evaluation_SystemEntities3();
        [HttpGet]
        public HttpResponseMessage GetAllUsers()
        {
            try
            {
                var users = db.Log_In.ToList();
                return Request.CreateResponse(HttpStatusCode.OK, users);
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, ex.ToString());
            }
        }

        [HttpGet]
        public HttpResponseMessage LoginUser(string username, string password)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Userid and password are required");
                }

              
                var user = db.Log_In.FirstOrDefault(u => u.User_id.Trim() == username.Trim() && u.User_password.Trim() == password.Trim());

                if (user == null)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.Unauthorized, "Invalid userid or password");
                }

                int sem = 0;
                string designation = "";
                int? evalStatus = 0;

                string userRole = user.User_type.Trim();

            
                if (userRole.Equals("student", StringComparison.OrdinalIgnoreCase))
                {
                    string session = db.STMTRs
                                        .Where(s => s.Reg_No.Trim() == user.User_id.Trim())
                                        .Select(s => s.Semester_no)
                                        .FirstOrDefault();

                    if (!string.IsNullOrEmpty(session) && session.Length >= 4)
                    {
                        int eyear = int.Parse(session.Substring(0, 4));
                        int currentYear = DateTime.Now.Year;
                        int currentMonth = DateTime.Now.Month;

                        int calculatedSem = (currentYear - eyear) * 2;
                        if (currentMonth >= 9)
                        {
                            calculatedSem += 1;
                        }
                        sem = calculatedSem - 2;
                        if (sem < 1) sem = 1;
                    }
                }
           
                else if (userRole.Contains("teacher") || userRole.Equals("teacher", StringComparison.OrdinalIgnoreCase))
                {
                    var teacherData = db.EMPMTRs.FirstOrDefault(t => t.Emp_no.Trim() == user.User_id.Trim());
                    if (teacherData != null)
                    {
                        designation = teacherData.Designation ?? ""; 
                        evalStatus = teacherData.eval; 
                    }
                }

              
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    message = "Login successful",
                    userid = user.User_id.Trim(),
                    userType = userRole,
                    userName = user.User_name?.Trim(), 
                    semester = sem,
                    designation = designation,
                    eval = evalStatus          
                });
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Error: " + ex.Message);
            }
        }

        [HttpPost]
        public HttpResponseMessage LogoutUser(string userid)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(userid))
                {
                    return Request.CreateErrorResponse(
                        HttpStatusCode.BadRequest,
                        "Userid required"
                    );
                }

                var user = db.Log_In.FirstOrDefault(u => u.User_id == userid);

                if (user == null)
                {
                    return Request.CreateErrorResponse(
                        HttpStatusCode.NotFound,
                        "User not found"
                    );
                }

                return Request.CreateResponse(
                    HttpStatusCode.OK,
                    "User logged out successfully"
                );
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(
                    HttpStatusCode.InternalServerError,
                    ex.ToString()
                );
            }
        }
    }
}
