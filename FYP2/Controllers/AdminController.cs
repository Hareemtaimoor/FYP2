using FYP2.Models;
using ExcelDataReader;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

namespace FYP2.Controllers
{
    public class AdminController : ApiController
    {
        Teacher_Evaluation_SystemEntities3 db = new Teacher_Evaluation_SystemEntities3();
        [HttpPost]
        public async Task<HttpResponseMessage> SaveAttendance()
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                return Request.CreateErrorResponse(HttpStatusCode.UnsupportedMediaType, "Invalid form data");
            }

            try
            {
                var provider = new MultipartMemoryStreamProvider();
                await Request.Content.ReadAsMultipartAsync(provider);
                var file = provider.Contents.FirstOrDefault();
                byte[] fileBytes = await file.ReadAsByteArrayAsync();

                using (var stream = new MemoryStream(fileBytes))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    DataTable dt = ReadExcelDataTable(reader);
                    foreach (DataColumn column in dt.Columns)
                    {
                        column.ColumnName = column.ColumnName.Replace(" ", "").Trim();
                    }

                    int recordsSaved = 0;
                    foreach (DataRow row in dt.Rows)
                    {
                        string empNo = row["Emp_no"]?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(empNo) || empNo.ToLower().Contains("total")) continue;

                        DateTime.TryParse(row["AttendanceDate"]?.ToString(), out DateTime attendanceDate);
                        TimeSpan.TryParse(row["TimeIn"]?.ToString(), out TimeSpan timeIn);
                        TimeSpan.TryParse(row["TimeOut"]?.ToString(), out TimeSpan timeOut);
                        decimal.TryParse(row["DailyHours"]?.ToString(), out decimal dailyHours);

                        var attendance = new AttendanceRecord
                        {
                            Emp_no = empNo,
                            AttendanceDate = attendanceDate != DateTime.MinValue ? attendanceDate : DateTime.Now,
                            DayOfWeek = row["DayOfWeek"]?.ToString()?.Trim() ?? "Monday",
                            TimeIn = timeIn,
                            TimeOut = timeOut,
                            Status = row["Status"]?.ToString()?.Trim() ?? "Present",
                            DailyHours = dailyHours
                        };
                        db.AttendanceRecords.Add(attendance);
                        recordsSaved++;
                    }
                    db.SaveChanges();
                    return Request.CreateResponse(HttpStatusCode.OK, new { Message = $"{recordsSaved} Attendance Records Saved Successfully!" });
                }
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Attendance Error: " + (ex.InnerException?.Message ?? ex.Message));
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> SaveCHR()
        {
            if (!Request.Content.IsMimeMultipartContent())
            {
                return Request.CreateErrorResponse(HttpStatusCode.UnsupportedMediaType, "Invalid form data");
            }

            try
            {
                var provider = new MultipartMemoryStreamProvider();
                await Request.Content.ReadAsMultipartAsync(provider);
                var file = provider.Contents.FirstOrDefault();
                byte[] fileBytes = await file.ReadAsByteArrayAsync();

                using (var stream = new MemoryStream(fileBytes))
                using (var reader = ExcelReaderFactory.CreateReader(stream))
                {
                    DataTable dt = ReadExcelDataTable(reader);

                    foreach (DataColumn col in dt.Columns)
                    {
                        string name = col.ColumnName.Replace(" ", "").Trim().ToLower();
                        if (name == "course" || name == "courseno") col.ColumnName = "Course_no";
                        if (name == "empno" || name == "emp_no") col.ColumnName = "Emp_no";
                    }

                    var validCourses = db.CRSMTRs.ToList().Select(c => new {
                        Course_no = c.Course_no.Trim().ToUpper(),
                        Discipline = c.Discipline.Trim().ToUpper(),
                        SOS = c.SOS.Trim().ToUpper()
                    }).ToList();

                    int recordsAdded = 0;
                    int rowCount = 0;
                    List<string> missingCourses = new List<string>();

                    // --- ID LOGIC (IMPORTANT) ---
                    // Agar aapke model mein column ka naam sirf 'ID' hai, to yahan 'ReportID' ko badal kar 'ID' kar dein.
                    int lastId = db.ClassReports.Any() ? db.ClassReports.Max(c => c.SrNo) : 0;

                    foreach (DataRow row in dt.Rows)
                    {
                        rowCount++;
                        string empNo = row["Emp_no"]?.ToString()?.Trim();
                        if (string.IsNullOrWhiteSpace(empNo)) continue;

                        string courseNo = row["Course_no"]?.ToString()?.Trim()?.Replace(" ", "")?.ToUpper();
                        string altCourseNo = courseNo.StartsWith("CSC-") ? courseNo.Replace("CSC-", "CS-") :
                                            (courseNo.StartsWith("CS-") ? courseNo.Replace("CS-", "CSC-") : courseNo);

                        string rawDisc = row["Discipline"]?.ToString()?.Trim()?.ToUpper()?.Replace(" ", "");
                        string discipline = rawDisc.Contains("-") ? rawDisc.Split('-')[0] : rawDisc;

                        string sos = row["SOS"]?.ToString()?.Trim()?.ToUpper();
                        string altSos = sos.Contains("SOS") ? sos.Replace("SOS", "S0S") : sos.Replace("S0S", "SOS");

                        bool courseExists = validCourses.Any(c =>
                            (c.Course_no == courseNo || c.Course_no == altCourseNo) &&
                            c.Discipline == discipline &&
                            (c.SOS == sos || c.SOS == altSos)
                        );

                        if (!courseExists)
                        {
                            missingCourses.Add($"Row {rowCount}: Course={courseNo}, Discipline={discipline}, SOS={sos}");
                            continue;
                        }

                        int? semester = int.TryParse(row["Semester"]?.ToString(), out int sem) ? (int?)sem : null;
                        DateTime reportDate = DateTime.TryParse(row["ReportDate"]?.ToString(), out DateTime pDate) ? pDate : DateTime.Now;

                        lastId++;
                        var chr = new ClassReport
                        {
                            // AGAR ERROR AAYE, TO NICHE WALI LINE KO 'ID = lastId,' KAR DEIN
                            SrNo = lastId,
                            Emp_no = empNo,
                            Course_no = courseNo,
                            Discipline = discipline,
                            SOS = sos,
                            Semester = semester,
                            Section = row["Section"]?.ToString()?.Trim() ?? "A",
                            Name = row["Name"]?.ToString()?.Trim() ?? row["Teacher"]?.ToString()?.Trim(),
                            Venue = row["Venue"]?.ToString()?.Trim() ?? "Room",
                            Status = row["Status"]?.ToString()?.Trim() ?? "Held",
                            ReportDate = reportDate
                        };

                        db.ClassReports.Add(chr);
                        recordsAdded++;
                    }

                    if (missingCourses.Count > 0)
                    {
                        return Request.CreateResponse(HttpStatusCode.BadRequest, new
                        {
                            Message = "Some rows failed due to Foreign Key mismatch.",
                            MissingCourses = missingCourses
                        });
                    }

                    db.SaveChanges();
                    return Request.CreateResponse(HttpStatusCode.OK, new { Message = $"{recordsAdded} CHR Records Saved Successfully!" });
                }
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException?.InnerException?.Message ?? ex.InnerException?.Message ?? ex.Message;
                return Request.CreateErrorResponse(HttpStatusCode.InternalServerError, "Database Sync Error: " + innerMsg);
            }
        }

        private DataTable ReadExcelDataTable(IExcelDataReader reader)
        {
            var table = new DataTable();

            if (!reader.Read())
            {
                return table;
            }

            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetValue(i)?.ToString()?.Trim();
                if (string.IsNullOrWhiteSpace(columnName))
                {
                    columnName = "Column" + (i + 1);
                }

                table.Columns.Add(GetUniqueColumnName(table, columnName));
            }

            while (reader.Read())
            {
                var row = table.NewRow();
                for (int i = 0; i < reader.FieldCount && i < table.Columns.Count; i++)
                {
                    row[i] = reader.GetValue(i) ?? DBNull.Value;
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private string GetUniqueColumnName(DataTable table, string columnName)
        {
            var uniqueName = columnName;
            int counter = 1;

            while (table.Columns.Contains(uniqueName))
            {
                uniqueName = columnName + counter;
                counter++;
            }

            return uniqueName;
        }
    }
}
