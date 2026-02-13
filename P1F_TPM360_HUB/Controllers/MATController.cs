using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.OleDb;
using System.Data.SqlClient;
using System.Dynamic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;

namespace P1F_TPM360_HUB.Controllers
{
    public class MATController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public MATController(IWebHostEnvironment environment, IHttpContextAccessor httpContextAccessor, ApplicationDbContext context)
        {
            _context = context; 
            _hostingEnvironment = environment;
            _httpContextAccessor = httpContextAccessor;
        }
        private string DbConnection()
        {
            var dbAccess = new DatabaseAccessLayer();
            string dbString = dbAccess.ConnectionString;
            return dbString;
        }
        public IActionResult Observation() 
        {
            var userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;
            var facility = GetFacility();
            var line = GetLine();
            var station = GetStation();
            var tpm_tag = GetTPMTag();
            var op_sesa = GetSesaOP();
            var abn_type = GetAbnType();
            var abn_happen = GetAbnHappen();
            var abn_rootcause = GetAbnRootCause();

            ViewBag.Facility = facility;
            ViewBag.Line = line;
            ViewBag.Station = station;
            ViewBag.TpmTag = tpm_tag;
            ViewBag.SesaOP = op_sesa;
            ViewBag.AbnType = abn_type;
            ViewBag.AbnHappen = abn_happen;
            ViewBag.AbnRootCause = abn_rootcause;

            if (string.IsNullOrEmpty(userLevel) ||
                (!userLevel.Contains("mat") && !userLevel.Contains("mat_admin") && !userLevel.Contains("superadmin")))
            {
                return RedirectToAction("Index", "Login");
            }

            return View();
        }

        public IActionResult TRD()
        {
            return View();
        }

        private List<CodeNameModel> GetFacility()
        {
            var facilityList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " SELECT facility_id, facility FROM mst_facility ORDER BY facility_id ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            facilityList.Add(new CodeNameModel
                            {
                                Code = reader["facility_id"].ToString(),
                                Name = reader["facility"].ToString()
                            });
                        }
                    }
                }
            }
            return facilityList;
        }
        private List<CodeNameModel> GetLine()
        {
            var lineList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                //string query = " SELECT line_no, line_name FROM mst_line ORDER BY line_no ASC";
                string query = " SELECT distinct line_no FROM mst_linestation ORDER BY line_no ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lineList.Add(new CodeNameModel
                            {
                                Code = reader["line_no"].ToString()
                                //Name = reader["line_name"].ToString()
                            });
                        }
                    }
                }
            }
            return lineList;
        }
        private List<CodeNameModel> GetStation()
        {
            var stationList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " SELECT station_id, station_name FROM mst_station ORDER BY station_id ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            stationList.Add(new CodeNameModel
                            {
                                Code = reader["station_id"].ToString(),
                                Name = reader["station_name"].ToString()
                            });
                        }
                    }
                }
            }
            return stationList;
        }
        private List<CodeNameModel> GetStationLine(string line_no)
        {
            var stationList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " select station_no from mst_linestation where line_no = @line_no";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@line_no", line_no);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            stationList.Add(new CodeNameModel
                            {
                                Code = reader["station_no"].ToString()
                                //Name = reader["station_name"].ToString()
                            });
                        }
                    }
                }
            }
            return stationList;
        }
        [HttpGet]
        public JsonResult GetStationsByLine(string line_no)
        {
            try
            {
                var stations = GetStationLine(line_no);
                return Json(stations);
            }
            catch (Exception ex)
            {
                Response.StatusCode = 500;
                return Json(new { error = "An error occurred while fetching stations." });
            }
        }
        private List<CodeNameModel> GetTPMTag()
        {
            var tpmList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " SELECT tag_id, tag_dept FROM mst_tpm_tag ORDER BY tag_id ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            tpmList.Add(new CodeNameModel
                            {
                                Code = reader["tag_id"].ToString(),
                                Name = reader["tag_dept"].ToString()
                            });
                        }
                    }
                }
            }
            return tpmList;
        }
        private List<CodeNameModel> GetSesaOP()
        {
            var opList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " SELECT sesa_id, employee_name from V_OPERATOR ORDER BY sesa_id ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            opList.Add(new CodeNameModel
                            {
                                Code = reader["sesa_id"].ToString(),
                                Name = reader["employee_name"].ToString()
                            });
                        }
                    }
                }
            }
            return opList;
        }
        private List<CodeNameModel> GetAbnType()
        {
            var abnTypeList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " SELECT abn_type_id, abn_type from mst_abn_type ORDER BY record_date ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            abnTypeList.Add(new CodeNameModel
                            {
                                Code = reader["abn_type_id"].ToString(),
                                Name = reader["abn_type"].ToString()
                            });
                        }
                    }
                }
            }
            return abnTypeList;
        }
        private List<CodeNameModel> GetAbnHappen()
        {
            var abnHappenList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " SELECT abn_happen from mst_abn_happen ORDER BY record_date ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            abnHappenList.Add(new CodeNameModel
                            {
                                Name = reader["abn_happen"].ToString()
                            });
                        }
                    }
                }
            }
            return abnHappenList;
        }
        private List<CodeNameModel> GetAbnRootCause()
        {
            var abnRootCauseList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " SELECT abn_rootcause_id, abn_rootcause from mst_abn_rootcause ORDER BY record_date ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            abnRootCauseList.Add(new CodeNameModel
                            {
                                Code = reader["abn_rootcause_id"].ToString(),
                                Name = reader["abn_rootcause"].ToString()
                            });
                        }
                    }
                }
            }
            return abnRootCauseList;
        }
        
        [HttpPost]
        public JsonResult GetAssignedAction(string amChecklist, string facilityId)
        {
            var assignedActionList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " SELECT a.assigned_sesa, b.name from mst_assigned_sesa AS a LEFT JOIN mst_users AS b on a.assigned_sesa = b.sesa_id WHERE a.am_checklist = @AmChecklist and a.facility_id = @FacilityId ORDER BY a.record_date ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@AmChecklist", amChecklist);
                    cmd.Parameters.AddWithValue("@FacilityId", facilityId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            assignedActionList.Add(new CodeNameModel
                            {
                                Code = reader["assigned_sesa"].ToString(),
                                Name = reader["name"].ToString()
                            });
                        }
                    }
                }
            }
            return Json(assignedActionList);
        }

        //[HttpGet]
        //public JsonResult GetActionOwner(string FacilityId, string TagId)
        //{
        //    string ownerName = null;

        //    using (SqlConnection conn = new SqlConnection(DbConnection()))
        //    {
        //        conn.Open();

        //        string query = @"
        //    SELECT TOP 1 b.name 
        //    FROM mst_action_owner AS a 
        //    LEFT JOIN mst_users AS b ON a.owner_sesa = b.sesa_id 
        //    WHERE a.tag_id = @TagId AND a.facility_id = @FacilityId 
        //    ORDER BY a.record_date DESC";

        //        using (SqlCommand cmd = new SqlCommand(query, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@FacilityId", FacilityId);
        //            cmd.Parameters.AddWithValue("@TagId", TagId);

        //            object result = cmd.ExecuteScalar();
        //            if (result != null)
        //            {
        //                ownerName = result.ToString();
        //            }
        //        }
        //    }
        //    return Json(new { name = ownerName });
        //}

        [HttpGet]
        public JsonResult GetActionOwner(string FacilityId, string TagId)
        {
            string ownerName = null;
            string ownerSesaId = null; 

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = @"
            SELECT TOP 1 b.name, a.owner_sesa 
            FROM mst_action_owner AS a 
            LEFT JOIN mst_users AS b ON a.owner_sesa = b.sesa_id 
            WHERE a.tag_id = @TagId AND a.facility_id = @FacilityId 
            ORDER BY a.record_date DESC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FacilityId", FacilityId);
                    cmd.Parameters.AddWithValue("@TagId", TagId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ownerName = reader["name"].ToString();
                            ownerSesaId = reader["owner_sesa"].ToString(); 
                        }
                    }
                }
            }

            return Json(new { name = ownerName, SesaId = ownerSesaId });
        }

        [HttpGet]
        public IActionResult GetDetailHistory(string order_id)
        {
            var data = new List<ABNModelHistory>();
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                SqlCommand command = new SqlCommand("GET_ABN_HISTORY", conn);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@order_id", order_id);
                conn.Open();
                try
                {
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var sesa_id = reader["sesa_id"].ToString();
                            var name = reader["name"].ToString();
                            var ova = reader["ova"].ToString();
                            var remark = reader["remark"] != DBNull.Value ? reader["remark"].ToString() : "-";
                            var record_date = reader["record_date"] != DBNull.Value ? Convert.ToDateTime(reader["record_date"]).ToString("MM/dd/yyyy") : "-";
                
                            data.Add(new ABNModelHistory
                            {
                                sesa_id = sesa_id,
                                name = name,
                                ova = ova,
                                remark = remark,
                                record_date = record_date
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading data: {ex.Message}");
                }
            }
            return PartialView("_TableABNDetailHistory", data);
        }

        //[HttpPost]
        //public async Task<JsonResult> AddInput(IFormFile file, string date_find, string facility_id, string line, string station, string tpm_tag, string sesa_op, string finding)
        //{
        //    var sesa_id = HttpContext.Session.GetString("sesa_id");
        //    //object sesaOpParam = (sesa_op == null || string.IsNullOrWhiteSpace(sesa_op)) ? (object)DBNull.Value : sesa_op;

        //    if (HttpContext.Session.GetString("level") == "mat" || HttpContext.Session.GetString("level") == "mat_admin" || HttpContext.Session.GetString("level") == "superadmin")
        //    {
        //        try
        //        {
        //            object GetDbValue(string value)
        //            {
        //                if (string.IsNullOrWhiteSpace(value) || value.Trim() == "--- Select Operator SESA ---")
        //                {
        //                    return DBNull.Value;
        //                }
        //                return value;
        //            }
        //            string fileNameToSave = null;
        //            if (file != null && file.Length > 0)
        //            {
        //                string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
        //                string originalExtension = Path.GetExtension(file.FileName);

        //                fileNameToSave = $"MAT-{uniqueId}-BEFORE{originalExtension}";

        //                var filePath = Path.Combine("wwwroot", "upload", "img", "abn", fileNameToSave);

        //                using (var stream = new FileStream(filePath, FileMode.Create))
        //                {
        //                    await file.CopyToAsync(stream);
        //                }
        //            }

        //            using (SqlConnection con = new SqlConnection(DbConnection()))
        //            {
        //                con.Open();
        //                using (SqlCommand cmd = new SqlCommand("AddAbnormality", con))
        //                {
        //                    cmd.CommandType = CommandType.StoredProcedure;
        //                    cmd.Parameters.AddWithValue("@date_find", date_find);
        //                    cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
        //                    cmd.Parameters.AddWithValue("@facility_id", facility_id);
        //                    cmd.Parameters.AddWithValue("@line", line);
        //                    cmd.Parameters.AddWithValue("@station", station);
        //                    cmd.Parameters.AddWithValue("@tpm_tag", tpm_tag);
        //                    //cmd.Parameters.AddWithValue("@sesa_op", sesaOpParam);
        //                    cmd.Parameters.AddWithValue("@sesa_op", GetDbValue(sesa_op));
        //                    cmd.Parameters.AddWithValue("@finding", finding);
        //                    cmd.Parameters.AddWithValue("@picture_finding", fileNameToSave != null ? fileNameToSave : (object)DBNull.Value);
        //                    cmd.ExecuteNonQuery();
        //                }
        //            }
        //            return Json(new { success = true, message = "Data berhasil ditambahkan!" });
        //        }
        //        catch (Exception ex)
        //        {
        //            return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
        //        }
        //    }
        //    else
        //    {
        //        return Json(new { success = false, message = "You don't have access rights." });
        //    }
        //}

        private string GetSesaIdByName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName) || userName.Length < 6 || userName.Length > 50)
                return userName; 

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();
                string query = "SELECT TOP 1 sesa_id FROM mst_users WHERE name = @UserName";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserName", userName);
                    object result = cmd.ExecuteScalar();
                    return result != null ? result.ToString() : null; 
                }
            }
        }

        [HttpPost]
        public async Task<JsonResult> AddInput(
       IFormFile file,
       IFormFile FormFileAdd,
       string date_find, string facility_id, string line, string station,
       string tpm_tag, string sesa_op, string finding,
       string fixed_myself,
       string abn_type, string abn_happen, string abn_rootcause, string input_root, string input_machine,
       string input_corrective_action, IFormFile file_action, string date_target,

       string am_checklist,
       string assigned_action,
       string status_for_action,
       string date_completed,
       string action_owner_sesa,
       string validated_by_sesa 
   )
        {
            var sesa_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            if (userLevel.Contains("mat") || userLevel.Contains("mat_admin") || userLevel.Contains("superadmin"))
            {
                try
                {
                    
                    object GetDbValue(string value) { /* ... */ return string.IsNullOrWhiteSpace(value) || value.Trim().StartsWith("--- Select") ? DBNull.Value : value; }
                    object ParseDate(string dateString) { return DateTime.TryParse(dateString, out DateTime parsedDate) ? parsedDate : DBNull.Value; }

                    
                    string GetSesaIdByName(string userName)
                    {
                        if (string.IsNullOrWhiteSpace(userName) || userName.Length < 6 || userName.StartsWith("SESA"))
                            return userName;

                        
                        using (SqlConnection conn = new SqlConnection(DbConnection()))
                        {
                            conn.Open();
                            string query = "SELECT TOP 1 sesa_id FROM mst_users WHERE name = @UserName";
                            using (SqlCommand cmd = new SqlCommand(query, conn))
                            {
                                cmd.Parameters.AddWithValue("@UserName", userName);
                                object result = cmd.ExecuteScalar();
                                return result != null ? result.ToString() : null;
                            }
                        }
                    }

                    string fileNameFinding = null;
                    if (file != null && file.Length > 0)
                    {
                        string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                        string originalExtension = Path.GetExtension(file.FileName);
                        fileNameFinding = $"MAT-{uniqueId}-BEFORE{originalExtension}";
                        var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "upload", "img", "abn", fileNameFinding);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                    }

                    string fileNameAction = null;
                    bool isFixedByMyself = fixed_myself == "Fixed by myself";
                    if (isFixedByMyself && file_action != null && file_action.Length > 0)
                    {
                        string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                        string originalExtension = Path.GetExtension(file_action.FileName);
                        fileNameAction = $"MAT-{uniqueId}-AFTER{originalExtension}";
                        var filePath = Path.Combine(_hostingEnvironment.WebRootPath, "upload", "img", "abn", fileNameAction);
                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file_action.CopyToAsync(stream);
                        }
                    }

                    string fileNamePdf = null;
                    if (FormFileAdd != null && FormFileAdd.Length > 0)
                    {
                        string uniqueIdPdf = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                        string originalExtensionPdf = Path.GetExtension(FormFileAdd.FileName); // Menggunakan FormFileAdd.FileName

                        fileNamePdf = $"MAT-{uniqueIdPdf}-PDF{originalExtensionPdf}";

                        var folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "upload", "img", "abn", "pdf");
                        if (!Directory.Exists(folderPath))
                        {
                            Directory.CreateDirectory(folderPath);
                        }

                        var filePathPdf = Path.Combine(folderPath, fileNamePdf);

                        using (var stream = new FileStream(filePathPdf, FileMode.Create))
                        {
                            await FormFileAdd.CopyToAsync(stream); // Menggunakan FormFileAdd.CopyToAsync
                        }
                    }

                    string finalValidatorSesa = null;
                    string finalActionOwnerSesa = GetDbValue(action_owner_sesa)?.ToString();

                    if (isFixedByMyself)
                    {
                        finalValidatorSesa = GetSesaIdByName(validated_by_sesa);

                        if (string.IsNullOrWhiteSpace(finalValidatorSesa) || finalValidatorSesa.Length > 20)
                        {
                            var ownerResult = GetActionOwner(facility_id, tpm_tag);
                            finalValidatorSesa = (ownerResult.Value as dynamic)?.SesaId;
                        }
                    }


                    using (SqlConnection con = new SqlConnection(DbConnection()))
                    {
                        con.Open();
                        using (SqlCommand cmd = new SqlCommand("AddAbnormality", con))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@date_find", ParseDate(date_find));
                            cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
                            cmd.Parameters.AddWithValue("@facility_id", facility_id);
                            cmd.Parameters.AddWithValue("@line", line);
                            cmd.Parameters.AddWithValue("@station", station);
                            cmd.Parameters.AddWithValue("@tpm_tag", tpm_tag);
                            cmd.Parameters.AddWithValue("@sesa_op", GetDbValue(sesa_op));
                            cmd.Parameters.AddWithValue("@finding", finding);
                            cmd.Parameters.AddWithValue("@picture_finding", GetDbValue(fileNameFinding));
                            cmd.Parameters.AddWithValue("@fixed_by_type_value", fixed_myself);
                            cmd.Parameters.AddWithValue("@abn_type", GetDbValue(abn_type));
                            cmd.Parameters.AddWithValue("@abn_happen", GetDbValue(abn_happen));
                            cmd.Parameters.AddWithValue("@abn_rootcause", GetDbValue(abn_rootcause));
                            cmd.Parameters.AddWithValue("@input_root", GetDbValue(input_root));
                            cmd.Parameters.AddWithValue("@input_machine", GetDbValue(input_machine));
                            cmd.Parameters.AddWithValue("@input_corrective_action", GetDbValue(input_corrective_action));
                            cmd.Parameters.AddWithValue("@picture_action", GetDbValue(fileNameAction));
                            cmd.Parameters.AddWithValue("@date_target", ParseDate(date_target));
                            cmd.Parameters.AddWithValue("@am_checklist", GetDbValue(am_checklist));
                            cmd.Parameters.AddWithValue("@assigned_action", GetDbValue(assigned_action));
                            cmd.Parameters.AddWithValue("@status_action_value", GetDbValue(status_for_action));
                            cmd.Parameters.AddWithValue("@date_completed", isFixedByMyself ? ParseDate(date_completed) : DBNull.Value);
                            cmd.Parameters.AddWithValue("@action_owner_sesa_sp", GetDbValue(finalActionOwnerSesa));
                            cmd.Parameters.AddWithValue("@validated_by_sesa_sp", GetDbValue(finalValidatorSesa));
                            cmd.Parameters.AddWithValue("@attachment_file", GetDbValue(fileNamePdf)); 

                            cmd.ExecuteNonQuery();
                        }
                    }
                    return Json(new { success = true, message = "Data berhasil ditambahkan!" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
                }
            }
            else
            {
                return Json(new { success = false, message = "You don't have access rights." });
            }
        }

        [HttpPost]
        public async Task<JsonResult> AddInput2(IFormFile file, IFormFile file_pdf, string order_id, string facility_id, string abn_type, string abn_happen, string abn_rootcause, string input_root, string input_machine, string am_checklist, string assigned_action, string input_corrective_action, string date_target, string status_for_action, string date_completed)
        {
            var sesa_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            if (userLevel == "mat" || userLevel == "mat_admin")
            {
                try
                {
                    object GetDbValue(string value)
                    {
                        if (string.IsNullOrWhiteSpace(value) || value.Trim() == "--- Select ---")
                        {
                            return DBNull.Value;
                        }
                        return value;
                    }
                    object ParseDate(string dateString)
                    {
                        if (DateTime.TryParse(dateString, out DateTime parsedDate))
                        {
                            return parsedDate; 
                        }
                        return DBNull.Value;
                    }

                    string fileNameToSave = null;
                    if (file != null && file.Length > 0)
                    {
                        string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                        string originalExtension = Path.GetExtension(file.FileName);

                        fileNameToSave = $"MAT-{uniqueId}-AFTER{originalExtension}";

                        var filePath = Path.Combine("wwwroot", "upload", "img", "abn", fileNameToSave);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                    }
                    string fileNameToSave2 = null;
                    if (file_pdf != null && file_pdf.Length > 0)
                    {
                        string uniqueId2 = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                        string originalExtension2 = Path.GetExtension(file_pdf.FileName);

                        fileNameToSave2 = $"MAT-{uniqueId2}-AFTER{originalExtension2}";

                        var filePath2 = Path.Combine("wwwroot", "upload", "img", "abn", "pdf", fileNameToSave2);

                        using (var stream = new FileStream(filePath2, FileMode.Create))
                        {
                            await file_pdf.CopyToAsync(stream);
                        }
                    }

                    using (SqlConnection con = new SqlConnection(DbConnection()))
                    {
                        con.Open();
                        using (SqlCommand cmd = new SqlCommand("AddActionOwner", con))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@action_owner", sesa_id);
                            cmd.Parameters.AddWithValue("@order_id", order_id);
                            cmd.Parameters.AddWithValue("@facility_id", facility_id);
                            cmd.Parameters.AddWithValue("@abn_type", GetDbValue(abn_type));
                            cmd.Parameters.AddWithValue("@abn_happen", GetDbValue(abn_happen));
                            cmd.Parameters.AddWithValue("@abn_rootcause", GetDbValue(abn_rootcause));
                            cmd.Parameters.AddWithValue("@input_root", GetDbValue(input_root));
                            cmd.Parameters.AddWithValue("@input_machine", GetDbValue(input_machine));
                            cmd.Parameters.AddWithValue("@am_checklist", GetDbValue(am_checklist));
                            cmd.Parameters.AddWithValue("@assigned_action", GetDbValue(assigned_action));
                            cmd.Parameters.AddWithValue("@input_corrective_action", GetDbValue(input_corrective_action));
                            cmd.Parameters.AddWithValue("@date_target", ParseDate(date_target));
                            cmd.Parameters.AddWithValue("@status", status_for_action);
                            cmd.Parameters.AddWithValue("@date_completed", ParseDate(date_completed));
                            cmd.Parameters.AddWithValue("@picture_action", fileNameToSave != null ? fileNameToSave : (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@attachment_file", fileNameToSave2 != null ? fileNameToSave2 : (object)DBNull.Value);

                            cmd.ExecuteNonQuery();
                        }
                    }
                    return Json(new { success = true, message = "Data berhasil ditambahkan!" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
                }
            }
            else
            {
                return Json(new { success = false, message = "You don't have access rights." });
            }
        }
        
        [HttpPost]
        public async Task<JsonResult> AddInput3(string order_id, string remark, string status, string date_completed)
        {
            var sesa_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            if (userLevel == "mat" || userLevel == "mat_admin")
            {
                try
                {
                    using (SqlConnection con = new SqlConnection(DbConnection()))
                    {
                        con.Open();
                        using (SqlCommand cmd = new SqlCommand("AddValidator", con))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@validator_sesa", sesa_id);
                            cmd.Parameters.AddWithValue("@order_id", order_id);
                            cmd.Parameters.AddWithValue("@remark", remark);
                            cmd.Parameters.AddWithValue("@status", status);
                            cmd.Parameters.AddWithValue("@date_completed", date_completed);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    return Json(new { success = true, message = "Data berhasil ditambahkan!" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
                }
            }
            else
            {
                return Json(new { success = false, message = "You don't have access rights." });
            }
        }
        
        [HttpPost]
        public async Task<JsonResult> AddAssigned(IFormFile file, IFormFile file_pdf, string order_id, string input_corrective, string date_target)
        {
            var sesa_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            if (userLevel == "mat" || userLevel == "mat_admin")
            {
                try
                {
                    string fileNameToSave = null;
                    if (file != null && file.Length > 0)
                    {
                        string uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                        string originalExtension = Path.GetExtension(file.FileName);

                        fileNameToSave = $"MAT-{uniqueId}-AFTER{originalExtension}";

                        var filePath = Path.Combine("wwwroot", "upload", "img", "abn", fileNameToSave);

                        using (var stream = new FileStream(filePath, FileMode.Create))
                        {
                            await file.CopyToAsync(stream);
                        }
                    }
                    string fileNameToSave2 = null;
                    if (file_pdf != null && file_pdf.Length > 0)
                    {
                        string uniqueId2 = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                        string originalExtension2 = Path.GetExtension(file_pdf.FileName);

                        fileNameToSave2 = $"MAT-{uniqueId2}-AFTER{originalExtension2}";

                        var filePath2 = Path.Combine("wwwroot", "upload", "img", "abn", "pdf", fileNameToSave2);

                        using (var stream = new FileStream(filePath2, FileMode.Create))
                        {
                            await file_pdf.CopyToAsync(stream);
                        }
                    }
                    using (SqlConnection con = new SqlConnection(DbConnection()))
                    {
                        con.Open();
                        using (SqlCommand cmd = new SqlCommand("AddAssigned", con))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.Parameters.AddWithValue("@assigned_sesa", sesa_id);
                            cmd.Parameters.AddWithValue("@order_id", order_id);
                            cmd.Parameters.AddWithValue("@input_corrective", input_corrective);
                            cmd.Parameters.AddWithValue("@date_target", date_target);
                            cmd.Parameters.AddWithValue("@picture_action", fileNameToSave != null ? fileNameToSave : (object)DBNull.Value);
                            cmd.Parameters.AddWithValue("@attachment_file", fileNameToSave2 != null ? fileNameToSave2 : (object)DBNull.Value);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    return Json(new { success = true, message = "Data berhasil ditambahkan!" });
                }
                catch (Exception ex)
                {
                    return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
                }
            }
            else
            {
                return Json(new { success = false, message = "You don't have access rights." });
            }
        }

        public IActionResult GetABN(string date_from, string date_to, string facility_id)
        {
            string sesa_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string level = User.FindFirst("P1F_TPM360_HUB_level")?.Value;
            List<ABNModel> employees = new List<ABNModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                SqlCommand command = new SqlCommand("GET_ABN", conn);
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.AddWithValue("@date_from", date_from);
                command.Parameters.AddWithValue("@date_to", date_to);
                command.Parameters.AddWithValue("@facility_id", facility_id);
                command.Parameters.AddWithValue("@sesa_id", sesa_id);
                command.Parameters.AddWithValue("@level", level);
                conn.Open();
                SqlDataReader reader = command.ExecuteReader();

                while (reader.Read())
                {
                    var employee = new ABNModel
                    {
                        date_find = reader["finding_date"] != DBNull.Value
                        ? ((DateTime)reader["finding_date"]).ToString("MMM dd, yyyy")
                        : "-",
                        facility_id = reader["facility_id"].ToString(),
                        facility = reader["facility"].ToString(),
                        order_id = reader["order_id"].ToString(),
                        line = reader["line_no"].ToString(),
                        station_id = reader["station_id"].ToString(),
                        tpm_tag = reader["tag_dept"].ToString(),
                        tag_id = reader["tag_id"].ToString(),
                        operator_sesa = reader["operator"].ToString(),
                        findings = reader["remark"].ToString(),
                        picture = reader["picture_finding"].ToString(),
                        name_owner = reader["name_owner"].ToString(),
                        status_request = reader["status_request"].ToString(),
                        status_dynamic = reader["status_desc"].ToString(),
                        //status_dynamic = reader["status_request"].ToString() switch
                        //{
                        //    "0" => "Waiting Action Owner",
                        //    "1" => "Waiting Validation",
                        //    "2" => "Done",
                        //    "3" => "Waiting Assigned",
                        //    "4" => "Clarify",
                        //    _ => "Unknown Status"
                        //},
                        status_action = reader["status_action"].ToString(),
                        owner_sesa = reader["owner_sesa"].ToString(),
                        requestor_sesa = reader["sesa_id"].ToString(),
                        assigned_sesa = reader["assigned_sesa"].ToString(),
                        validator_sesa = reader["validator_sesa"].ToString(),
                        name_validator = reader["name_validator"].ToString(),
                        image = reader["image"].ToString(),
                        attachment_file = reader["attachment_file"].ToString(),
                        corrective = reader["corrective"].ToString(),
                    };
                    employees.Add(employee);
                }
                reader.Close();
            }
            return PartialView("_ABNTable", employees);
        }
        public IActionResult GetABNDetail(string order_id)
        {
            var data = new ABNModel();
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();
                string query = @"SELECT *
                         FROM V_ABNORMALITIES
                         WHERE order_id = @order_id";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@order_id", order_id);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            data.facility_id = reader["facility_id"].ToString();
                            data.facility = reader["facility"].ToString();
                            data.picture = reader["picture_finding"].ToString();
                            data.order_id = reader["order_id"].ToString();
                            data.abn_type = reader["abn_type"].ToString();
                            data.abn_type_id = reader["abn_type_id"].ToString();
                            data.abn_happen = reader["abn_happen"].ToString();
                            data.abn_rootcause = reader["abn_rootcause"].ToString();
                            data.abn_rootcause_id = reader["abn_rootcause_id"].ToString();
                            data.rootcause_analysis = reader["rootcause_analysis"].ToString();
                            data.machine_part = reader["machine_part"].ToString();
                            data.am_checklist = reader["am_checklist"].ToString();
                            data.assigned_name = reader["assigned_name"].ToString();
                            data.corrective = reader["corrective"].ToString();
                            data.target_completion = reader["target_completion"] != DBNull.Value ? ((DateTime)reader["target_completion"]).ToString("yyyy-MM-dd") : "-";
                            data.status_action = reader["status_action"].ToString();
                            data.completed_date = reader["completed_date"] != DBNull.Value ? ((DateTime)reader["completed_date"]).ToString("yyyy-MM-dd") : "-";
                            data.date_find = reader["finding_date"] != DBNull.Value ? ((DateTime)reader["finding_date"]).ToString("yyyy-MM-dd") : "-";
                            data.picture = reader["picture_finding"].ToString();
                            data.image = reader["image"].ToString();
                        }
                    }
                }
            }
            return Json(data);
        }

        public IActionResult Index()
        {
            return RedirectToAction("Observation");
        }

        [HttpGet]
        public JsonResult GetDateSO()
        {
            DateModel date = new DateModel();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                using (SqlCommand command = new SqlCommand("GetDateSO", conn))
                {
                    command.CommandType = CommandType.StoredProcedure;

                    conn.Open();
                    using (SqlDataReader reader = command.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            date.FromDate = reader["From_Date"].ToString();
                            date.CurrentDate = reader["To_Date"].ToString();
                        }
                    }
                }
            }

            return Json(date);
        }
        public JsonResult ChangePassword(int id, string oldpsw, string newpsw)
        {

            var hashpassword = new Authentication();
            string Oldpassword = hashpassword.MD5Hash(oldpsw);
            string Newpassword = hashpassword.MD5Hash(newpsw);
            int Excute = 0;
            string query = @"SELECT TOP 1 id_user FROM mst_users WHERE id_user = @id AND password = @password";
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);
                    cmd.Parameters.AddWithValue("@password", Oldpassword);
                    conn.Open();
                    using (SqlDataReader checkpw = cmd.ExecuteReader())
                    {
                        if (checkpw.Read())
                        {
                            cmd.Parameters.Clear();
                            cmd.CommandText = "UPDATE mst_users SET password = @password WHERE id_user = @id";
                            cmd.Parameters.AddWithValue("@id", id);
                            cmd.Parameters.AddWithValue("@password", Newpassword);
                        }
                    }
                    Excute = cmd.ExecuteNonQuery();
                    conn.Close();
                }
            }
            return Json(Excute);
        }
        public IActionResult Profile()
        {
            string userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;
            string id_user = User.FindFirst("P1F_TPM360_HUB_id")?.Value;

            if (userLevel == "mqe" || userLevel == "admin"|| userLevel == "mat" || userLevel == "ldr")
            {
                List<UserManagementModel> users = new List<UserManagementModel>();
                string querySelect = "SELECT TOP 1 id_user, sesa_id, name, level FROM mst_users  WHERE id_user = @id";
                using (SqlConnection conn = new SqlConnection(DbConnection()))
                {
                    using (SqlCommand cmd = new SqlCommand(querySelect))
                    {
                        cmd.Parameters.AddWithValue("@id", id_user);
                        cmd.Connection = conn;
                        conn.Open();
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                users.Add(new UserManagementModel
                                {
                                    id_user = Convert.ToInt32(reader["id_user"]),
                                    sesa_id = Convert.ToString(reader["sesa_id"]),
                                    name = Convert.ToString(reader["name"]),
                                    level = Convert.ToString(reader["level"])
                                });
                            }
                        }
                        conn.Close();
                    }
                }

                return View(users);
            }
            else
            {
                return RedirectToAction("SignOut", "Login");
            }
        }
        private List<CodeNameModel> GetLineDashboard()
        {
            var lineList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " SELECT DISTINCT line_no FROM mst_linestation ORDER BY line_no ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lineList.Add(new CodeNameModel
                            {
                                Code = reader["line_no"].ToString(),
                                //Name = reader["station_name"].ToString()
                            });
                        }
                    }
                }
            }
            return lineList;
        }
        private List<CodeNameModel> GetStationDashboard()
        {
            var stationList = new List<CodeNameModel>();

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                conn.Open();

                string query = " SELECT DISTINCT station_no FROM mst_linestation ORDER BY station_no ASC";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            stationList.Add(new CodeNameModel
                            {
                                Code = reader["station_no"].ToString(),
                            });
                        }
                    }
                }
            }
            return stationList;
        }

        public IActionResult Dashboard()
        {
            if (User.FindFirst(ClaimTypes.NameIdentifier)?.Value != null)
            {
                ViewBag.Facilities = GetFacility();
                ViewBag.Lines = GetLineDashboard();
                ViewBag.Stations = GetStationDashboard();
                return View();
            }
            else
            {
                return RedirectToAction("Index", "Login");
            }
        }
        
        [HttpPost]
        public IActionResult GET_FINDING_CLOSED(string facility, string line, string station, string date_from, string date_to, string range)
        {
            //string range = "Monthly";

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();
                var query = "GET_FINDING_CLOSED";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Handle null inputs
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();

                                data_list.Period_Group = reader["Period_Group"].ToString();
                                data_list.Findings = reader["Findings"].ToString();
                                data_list.Closed = reader["Closed"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return Json(dataFPA);
            }
        }
    
        [HttpPost]
        public IActionResult GET_TOTAL_OPLs(string facility, string line, string station, string date_from, string date_to, string range)
        {
            //string range = "Monthly";

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();
                var query = "GET_OPLS";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Handle null inputs
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();

                                data_list.Period_Group = reader["Period_Group"].ToString();
                                data_list.Accumulative = reader["Accumulative"].ToString();
                                data_list.Closed = reader["Closed"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return Json(dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_SHARP_EYE(string facility, string line, string station, string date_from, string date_to, string range)
        {
            //string range = "Monthly";

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();
                var query = "GET_SHARP_EYE";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Handle null inputs
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();

                                data_list.FindingName = reader["FindingName"].ToString();
                                data_list.BarChart = reader["BarChart"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return Json(dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_TPM_TAG(string facility, string line, string station, string date_from, string date_to, string range)
        {
            //string range = "Monthly";

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();
                var query = "GET_TPM_TAG";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Handle null inputs
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();

                                data_list.TagId = reader["TagId"].ToString();
                                data_list.BarChart = reader["BarChart"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return Json(dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_ROOTCAUSE(string facility, string line, string station, string date_from, string date_to, string range)
        {
            //string range = "Monthly";

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();
                var query = "GET_ROOTCAUSE";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Handle null inputs
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();

                                data_list.AbnRootCause = reader["AbnRootCause"].ToString();
                                data_list.BarChart = reader["BarChart"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return Json(dataFPA);
            }
        }
        
        [HttpPost]
        public IActionResult GET_HAPPEN(string facility, string line, string station, string date_from, string date_to, string range)
        {

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();
                var query = "GET_HAPPEN";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Handle null inputs
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();

                                data_list.AbnHappen = reader["AbnHappen"].ToString();
                                data_list.BarChart = reader["BarChart"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return Json(dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_ABN_TYPE(string facility, string line, string station, string date_from, string date_to, string range)
        {

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();
                var query = "GET_ABN_TYPE";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Handle null inputs
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();

                                data_list.AbnType = reader["AbnType"].ToString();
                                data_list.BarChart = reader["BarChart"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return Json(dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_FIXED_BY_SELF(string facility, string line, string station, string date_from, string date_to, string range)
        {

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();
                var query = "GET_FIXED_BY_SELF";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Handle null inputs
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();

                                data_list.FindingName = reader["FindingName"].ToString();
                                data_list.BarChart = reader["BarChart"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return Json(dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_OPLS_PER_SITE(string facility, string line, string station, string date_from, string date_to, string range)
        {

            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();
                var query = "GET_OPLS_PER_SITE";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    // Handle null inputs
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();

                                data_list.Facility = reader["Facility"].ToString();
                                data_list.Closed = reader["Closed"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return Json(dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_DETAIL_FINDING_CLOSED(string facility, string line, string station, string date_from, string date_to, string range, string value, string type)
        {
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();

                var query = "GET_DETAIL_FINDING_CLOSED";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);
                    cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);
                    cmd.Parameters.AddWithValue("@type", string.IsNullOrEmpty(type) ? (object)DBNull.Value : type);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();
                                data_list.finding_date = reader["finding_date"].ToString();
                                data_list.facility = reader["facility"].ToString();
                                data_list.line_no = reader["line_no"].ToString();
                                data_list.station_id = reader["station_id"].ToString();
                                data_list.tag_id = reader["tag_id"].ToString();
                                data_list.tag_dept = reader["tag_dept"].ToString();
                                data_list.operators = reader["operator"].ToString();
                                data_list.findings = reader["remark"].ToString();
                                data_list.picture_finding = reader["picture_finding"].ToString();
                                data_list.picture_after = reader["image"].ToString();
                                data_list.corrective = reader["corrective"].ToString();
                                data_list.attachment_file = reader["attachment_file"].ToString();
                                data_list.status_request = reader["status_request"].ToString();
                                data_list.status_dynamic = reader["status_desc"].ToString();
                                //data_list.status_dynamic = reader["status_request"].ToString() switch
                                //{
                                //    "0" => "Waiting Action Owner",
                                //    "1" => "Waiting Validation",
                                //    "2" => "Done",
                                //    "3" => "Waiting Assigned",
                                //    "4" => "Clarify",
                                //    _ => "Unknown Status"
                                //};
                                data_list.name_owner = reader["name_owner"].ToString();
                                data_list.name_validator = reader["name_validator"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return PartialView("_TableDetail", dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_DETAIL_OPLS(string facility, string line, string station, string date_from, string date_to, string range, string value)
        {
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();

                var query = "GET_DETAIL_OPLS";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);
                    cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();
                                data_list.finding_date = reader["finding_date"].ToString();
                                data_list.facility = reader["facility"].ToString();
                                data_list.line_no = reader["line_no"].ToString();
                                data_list.station_id = reader["station_id"].ToString();
                                data_list.tag_id = reader["tag_id"].ToString();
                                data_list.tag_dept = reader["tag_dept"].ToString();
                                data_list.operators = reader["operator"].ToString();
                                data_list.findings = reader["remark"].ToString();
                                data_list.picture_finding = reader["picture_finding"].ToString();
                                data_list.picture_after = reader["image"].ToString();
                                data_list.corrective = reader["corrective"].ToString();
                                data_list.attachment_file = reader["attachment_file"].ToString();
                                data_list.status_request = reader["status_request"].ToString();
                                data_list.status_dynamic = reader["status_desc"].ToString();
                                data_list.name_owner = reader["name_owner"].ToString();
                                data_list.name_validator = reader["name_validator"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return PartialView("_TableDetail", dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_DETAIL_OPLS_PER_SITE(string facility, string line, string station, string date_from, string date_to, string range, string value)
        {
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();

                var query = "GET_DETAIL_OPLS_PER_SITE";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);
                    cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();
                                data_list.finding_date = reader["finding_date"].ToString();
                                data_list.facility = reader["facility"].ToString();
                                data_list.line_no = reader["line_no"].ToString();
                                data_list.station_id = reader["station_id"].ToString();
                                data_list.tag_id = reader["tag_id"].ToString();
                                data_list.tag_dept = reader["tag_dept"].ToString();
                                data_list.operators = reader["operator"].ToString();
                                data_list.findings = reader["remark"].ToString();
                                data_list.picture_finding = reader["picture_finding"].ToString();
                                data_list.picture_after = reader["image"].ToString();
                                data_list.corrective = reader["corrective"].ToString();
                                data_list.attachment_file = reader["attachment_file"].ToString();
                                data_list.status_request = reader["status_request"].ToString();
                                data_list.status_dynamic = reader["status_desc"].ToString();
                                data_list.name_owner = reader["name_owner"].ToString();
                                data_list.name_validator = reader["name_validator"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return PartialView("_TableDetail", dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_DETAIL_SHARP_EYE(string facility, string line, string station, string date_from, string date_to, string range, string value)
        {
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();

                var query = "GET_DETAIL_SHARP_EYE";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);
                    cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();
                                data_list.finding_date = reader["finding_date"].ToString();
                                data_list.facility = reader["facility"].ToString();
                                data_list.line_no = reader["line_no"].ToString();
                                data_list.station_id = reader["station_id"].ToString();
                                data_list.tag_id = reader["tag_id"].ToString();
                                data_list.tag_dept = reader["tag_dept"].ToString();
                                data_list.operators = reader["operator"].ToString();
                                data_list.findings = reader["remark"].ToString();
                                data_list.picture_finding = reader["picture_finding"].ToString();
                                data_list.picture_after = reader["image"].ToString();
                                data_list.corrective = reader["corrective"].ToString();
                                data_list.attachment_file = reader["attachment_file"].ToString();
                                data_list.status_request = reader["status_request"].ToString();
                                data_list.status_dynamic = reader["status_desc"].ToString();
                                data_list.name_owner = reader["name_owner"].ToString();
                                data_list.name_validator = reader["name_validator"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return PartialView("_TableDetail", dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_DETAIL_TPM_TAG(string facility, string line, string station, string date_from, string date_to, string range, string value)
        {
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();

                var query = "GET_DETAIL_TPM_TAG";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);
                    cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();
                                data_list.finding_date = reader["finding_date"].ToString();
                                data_list.facility = reader["facility"].ToString();
                                data_list.line_no = reader["line_no"].ToString();
                                data_list.station_id = reader["station_id"].ToString();
                                data_list.tag_id = reader["tag_id"].ToString();
                                data_list.tag_dept = reader["tag_dept"].ToString();
                                data_list.operators = reader["operator"].ToString();
                                data_list.findings = reader["remark"].ToString();
                                data_list.picture_finding = reader["picture_finding"].ToString();
                                data_list.picture_after = reader["image"].ToString();
                                data_list.corrective = reader["corrective"].ToString();
                                data_list.attachment_file = reader["attachment_file"].ToString();
                                data_list.status_request = reader["status_request"].ToString();
                                data_list.status_dynamic = reader["status_desc"].ToString();
                                data_list.name_owner = reader["name_owner"].ToString();
                                data_list.name_validator = reader["name_validator"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return PartialView("_TableDetail", dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_DETAIL_ROOTCAUSE(string facility, string line, string station, string date_from, string date_to, string range, string value)
        {
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();

                var query = "GET_DETAIL_ROOTCAUSE";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);
                    cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();
                                data_list.finding_date = reader["finding_date"].ToString();
                                data_list.facility = reader["facility"].ToString();
                                data_list.line_no = reader["line_no"].ToString();
                                data_list.station_id = reader["station_id"].ToString();
                                data_list.tag_id = reader["tag_id"].ToString();
                                data_list.tag_dept = reader["tag_dept"].ToString();
                                data_list.operators = reader["operator"].ToString();
                                data_list.findings = reader["remark"].ToString();
                                data_list.picture_finding = reader["picture_finding"].ToString();
                                data_list.picture_after = reader["image"].ToString();
                                data_list.corrective = reader["corrective"].ToString();
                                data_list.attachment_file = reader["attachment_file"].ToString();
                                data_list.status_request = reader["status_request"].ToString();
                                data_list.status_dynamic = reader["status_desc"].ToString();
                                data_list.name_owner = reader["name_owner"].ToString();
                                data_list.name_validator = reader["name_validator"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return PartialView("_TableDetail", dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_DETAIL_HAPPEN(string facility, string line, string station, string date_from, string date_to, string range, string value)
        {
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();

                var query = "GET_DETAIL_HAPPEN";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);
                    cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();
                                data_list.finding_date = reader["finding_date"].ToString();
                                data_list.facility = reader["facility"].ToString();
                                data_list.line_no = reader["line_no"].ToString();
                                data_list.station_id = reader["station_id"].ToString();
                                data_list.tag_id = reader["tag_id"].ToString();
                                data_list.tag_dept = reader["tag_dept"].ToString();
                                data_list.operators = reader["operator"].ToString();
                                data_list.findings = reader["remark"].ToString();
                                data_list.picture_finding = reader["picture_finding"].ToString();
                                data_list.picture_after = reader["image"].ToString();
                                data_list.corrective = reader["corrective"].ToString();
                                data_list.attachment_file = reader["attachment_file"].ToString();
                                data_list.status_request = reader["status_request"].ToString();
                                data_list.status_dynamic = reader["status_desc"].ToString();
                                data_list.name_owner = reader["name_owner"].ToString();
                                data_list.name_validator = reader["name_validator"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return PartialView("_TableDetail", dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_DETAIL_ABN_TYPE(string facility, string line, string station, string date_from, string date_to, string range, string value)
        {
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();

                var query = "GET_DETAIL_ABN_TYPE";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);
                    cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();
                                data_list.finding_date = reader["finding_date"].ToString();
                                data_list.facility = reader["facility"].ToString();
                                data_list.line_no = reader["line_no"].ToString();
                                data_list.station_id = reader["station_id"].ToString();
                                data_list.tag_id = reader["tag_id"].ToString();
                                data_list.tag_dept = reader["tag_dept"].ToString();
                                data_list.operators = reader["operator"].ToString();
                                data_list.findings = reader["remark"].ToString();
                                data_list.picture_finding = reader["picture_finding"].ToString();
                                data_list.picture_after = reader["image"].ToString();
                                data_list.corrective = reader["corrective"].ToString();
                                data_list.attachment_file = reader["attachment_file"].ToString();
                                data_list.status_request = reader["status_request"].ToString();
                                data_list.status_dynamic = reader["status_desc"].ToString();
                                data_list.name_owner = reader["name_owner"].ToString();
                                data_list.name_validator = reader["name_validator"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return PartialView("_TableDetail", dataFPA);
            }
        }

        [HttpPost]
        public IActionResult GET_DETAIL_FIXED_BY_SELF(string facility, string line, string station, string date_from, string date_to, string range, string value)
        {
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                List<dynamic> dataFPA = new List<dynamic>();

                var query = "GET_DETAIL_FIXED_BY_SELF";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@facility", string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
                    cmd.Parameters.AddWithValue("@line_no", string.IsNullOrEmpty(line) ? (object)DBNull.Value : line);
                    cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station) ? (object)DBNull.Value : station);
                    cmd.Parameters.AddWithValue("@range", string.IsNullOrEmpty(range) ? (object)DBNull.Value : range);
                    cmd.Parameters.AddWithValue("@date_from", string.IsNullOrEmpty(date_from) ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                    cmd.Parameters.AddWithValue("@date_to", string.IsNullOrEmpty(date_to) ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);
                    cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                dynamic data_list = new ExpandoObject();
                                data_list.finding_date = reader["finding_date"].ToString();
                                data_list.facility = reader["facility"].ToString();
                                data_list.line_no = reader["line_no"].ToString();
                                data_list.station_id = reader["station_id"].ToString();
                                data_list.tag_id = reader["tag_id"].ToString();
                                data_list.tag_dept = reader["tag_dept"].ToString();
                                data_list.operators = reader["operator"].ToString();
                                data_list.findings = reader["remark"].ToString();
                                data_list.picture_finding = reader["picture_finding"].ToString();
                                data_list.picture_after = reader["image"].ToString();
                                data_list.corrective = reader["corrective"].ToString();
                                data_list.attachment_file = reader["attachment_file"].ToString();
                                data_list.status_request = reader["status_request"].ToString();
                                data_list.status_dynamic = reader["status_desc"].ToString();
                                data_list.name_owner = reader["name_owner"].ToString();
                                data_list.name_validator = reader["name_validator"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                    conn.Close();
                }
                return PartialView("_TableDetail", dataFPA);
            }
        }

        [HttpGet]
        public JsonResult ValidateAndGetAction(string order_id)
        {
            try
            {
                string userSesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
                string userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;
                string[] levels = userLevel.Split(';');

                if (string.IsNullOrEmpty(userSesaId))
                {
                    return Json(new { success = false, message = "Your session has expired. Please log in again." });
                }

                string dbStatusRequest = null;
                string dbRequestorSesa = null;
                string dbOwnerSesa = null;
                string dbAssignedSesa = null;
                string dbValidatorSesa = null;

                using (SqlConnection conn = new SqlConnection(DbConnection()))
                {
                    conn.Open();
                    string query = @"SELECT *
                                 FROM V_ABNORMALITIES 
                                 WHERE order_id = @order_id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@order_id", order_id);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                dbStatusRequest = reader["status_request"] is DBNull ? null : reader["status_request"].ToString();
                                dbRequestorSesa = reader["sesa_id"] is DBNull ? null : reader["sesa_id"].ToString();
                                dbOwnerSesa = reader["owner_sesa"] is DBNull ? null : reader["owner_sesa"].ToString();
                                dbAssignedSesa = reader["assigned_sesa"] is DBNull ? null : reader["assigned_sesa"].ToString();
                                dbValidatorSesa = reader["validator_sesa"] is DBNull ? null : reader["validator_sesa"].ToString();
                            }
                            else
                            {
                                return Json(new { success = false, message = "Request not found." });
                            }
                        }
                    }
                }

                if (userSesaId == dbOwnerSesa)
                {
                    if (dbStatusRequest == "0")
                    {
                        return Json(new { success = true, actionToRun = "actionOwner" });
                    }
                    else if (dbStatusRequest == "4")
                    {
                        return Json(new { success = true, actionToRun = "actionOwnerClarify" });
                    }
                    else
                    {
                        return Json(new { success = true, actionToRun = "actionDetailABN" });
                    }
                }
                else if (userSesaId == dbAssignedSesa)
                {
                    if (dbStatusRequest == "3")
                    {
                        return Json(new { success = true, actionToRun = "actionAssigned" });
                    }
                    else
                    {
                        return Json(new { success = true, actionToRun = "actionDetailABN" });
                    }
                }
                else if (userSesaId == dbValidatorSesa)
                {
                    if (dbStatusRequest == "1")
                    {
                        return Json(new { success = true, actionToRun = "actionValidator" });
                    }
                    else
                    {
                        return Json(new { success = true, actionToRun = "actionDetailABN" });
                    }
                }
                else if (userSesaId == dbRequestorSesa)
                {
                    return Json(new { success = true, actionToRun = "actionDetailABN" });
                }
                else
                {
                    return Json(new { success = false, message = "Request not found." });
                }

            return Json(new { success = false, message = "Unknown request type." });
            }

            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred on the server: " + ex.Message });
            }
        }


    }
}