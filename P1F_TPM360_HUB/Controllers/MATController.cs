using Microsoft.AspNetCore.Mvc;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace P1F_TPM360_HUB.Controllers
{
    public class MATController : Controller
    {
        // ===================================================================
        // DEPENDENCY INJECTION
        // ===================================================================
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly DatabaseAccessLayer _db;

        public MATController(
            IWebHostEnvironment environment,
            IHttpContextAccessor httpContextAccessor,
            ApplicationDbContext context,
            DatabaseAccessLayer db)
        {
            _context = context;
            _hostingEnvironment = environment;
            _httpContextAccessor = httpContextAccessor;
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        /// <summary>
        /// Halaman Observation (form input abnormality).
        /// Hanya bisa diakses oleh user dengan level mat, mat_admin, atau superadmin.
        /// Data dropdown (facility, line, station, dll.) disiapkan via ViewBag.
        /// </summary>
        [Authorize(Policy = "UserLevel")]
        public IActionResult Observation()
        {
            // Siapkan data dropdown untuk form
            ViewBag.Facility     = GetFacility();
            ViewBag.Line         = GetLine();
            ViewBag.Station      = GetStation();
            ViewBag.TpmTag       = GetTPMTag();
            ViewBag.SesaOP       = GetSesaOP();
            ViewBag.AbnType      = GetAbnType();
            ViewBag.AbnHappen    = GetAbnHappen();
            ViewBag.AbnRootCause = GetAbnRootCause();

            return View();
        }

        // ===================================================================
        // HELPER: GET DATA MASTER (DROPDOWN)
        // ===================================================================

        /// <summary>Helper generik untuk mengambil data list dari database.</summary>
        private List<CodeNameModel> GetMasterData(string query, string codeColumn = null, string nameColumn = null, Action<SqlCommand> addParams = null)
        {
            var list = new List<CodeNameModel>();
            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    addParams?.Invoke(cmd);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new CodeNameModel
                            {
                                Code = codeColumn != null ? reader[codeColumn].ToString() : null,
                                Name = nameColumn != null ? reader[nameColumn].ToString() : null
                            });
                        }
                    }
                }
            }
            return list;
        }

        /// <summary>Mengambil daftar Facility dari tabel mst_facility.</summary>
        private List<CodeNameModel> GetFacility()
            => GetMasterData("SELECT facility_id, facility FROM mst_facility ORDER BY facility_id ASC", "facility_id", "facility");

        /// <summary>Mengambil daftar Line unik dari tabel mst_linestation.</summary>
        private List<CodeNameModel> GetLine()
            => GetMasterData("SELECT DISTINCT line_no FROM mst_linestation ORDER BY line_no ASC", "line_no");

        /// <summary>Mengambil daftar Station dari tabel mst_station.</summary>
        private List<CodeNameModel> GetStation()
            => GetMasterData("SELECT station_id, station_name FROM mst_station ORDER BY station_id ASC", "station_id", "station_name");

        /// <summary>Mengambil daftar TPM Tag dari tabel mst_tpm_tag.</summary>
        private List<CodeNameModel> GetTPMTag()
            => GetMasterData("SELECT tag_id, tag_dept FROM mst_tpm_tag ORDER BY tag_id ASC", "tag_id", "tag_dept");

        /// <summary>Mengambil daftar Operator (SESA ID + nama) dari view V_OPERATOR.</summary>
        private List<CodeNameModel> GetSesaOP()
            => GetMasterData("SELECT sesa_id, employee_name FROM V_OPERATOR ORDER BY sesa_id ASC", "sesa_id", "employee_name");

        /// <summary>Mengambil daftar tipe abnormality dari tabel mst_abn_type.</summary>
        private List<CodeNameModel> GetAbnType()
            => GetMasterData("SELECT abn_type_id, abn_type FROM mst_abn_type ORDER BY record_date ASC", "abn_type_id", "abn_type");

        /// <summary>Mengambil daftar dampak jika abnormality dibiarkan dari tabel mst_abn_happen.</summary>
        private List<CodeNameModel> GetAbnHappen()
            => GetMasterData("SELECT abn_happen FROM mst_abn_happen ORDER BY record_date ASC", null, "abn_happen");

        /// <summary>Mengambil daftar root cause abnormality dari tabel mst_abn_rootcause.</summary>
        private List<CodeNameModel> GetAbnRootCause()
            => GetMasterData("SELECT abn_rootcause_id, abn_rootcause FROM mst_abn_rootcause ORDER BY record_date ASC", "abn_rootcause_id", "abn_rootcause");

        /// <summary>
        /// Mengambil daftar Station berdasarkan Line yang dipilih.
        /// Digunakan untuk dropdown dinamis (cascade) saat user memilih Line di form.
        /// </summary>
        private List<CodeNameModel> GetStationLine(string lineNo)
            => GetMasterData(
                "SELECT station_no FROM mst_linestation WHERE line_no = @line_no",
                "station_no",
                null,
                cmd => cmd.Parameters.AddWithValue("@line_no", lineNo));

        /// <summary>
        /// API: Mengambil daftar station berdasarkan line yang dipilih (untuk dropdown dinamis).
        /// </summary>
        [HttpGet]
        public JsonResult GetStationsByLine(string line_no)
        {
            try
            {
                var stations = GetStationLine(line_no);
                return Json(stations);
            }
            catch
            {
                Response.StatusCode = 500;
                return Json(new { error = "An error occurred while fetching stations." });
            }
        }

        // ===================================================================
        // ASSIGNED ACTION & ACTION OWNER
        // ===================================================================

        /// <summary>
        /// Mengambil daftar SESA yang di-assign untuk checklist dan fasilitas tertentu.
        /// </summary>
        [HttpPost]
        public JsonResult GetAssignedAction(string amChecklist, string facilityId)
        {
            string query = @"SELECT a.assigned_sesa, b.name 
                             FROM mst_assigned_sesa AS a 
                             LEFT JOIN mst_users AS b ON a.assigned_sesa = b.sesa_id 
                             WHERE a.am_checklist = @AmChecklist AND a.facility_id = @FacilityId 
                             ORDER BY a.record_date ASC";

            var list = GetMasterData(query, "assigned_sesa", "name", cmd =>
            {
                cmd.Parameters.AddWithValue("@AmChecklist", amChecklist);
                cmd.Parameters.AddWithValue("@FacilityId",  facilityId);
            });

            return Json(list);
        }

        /// <summary>
        /// Mengambil Action Owner (pemilik tugas) berdasarkan fasilitas dan TPM tag.
        /// Mengembalikan nama dan SESA ID owner.
        /// </summary>
        [HttpGet]
        public JsonResult GetActionOwner(string FacilityId, string TagId)
        {
            string ownerName   = null;
            string ownerSesaId = null;

            string query = @"SELECT TOP 1 b.name, a.owner_sesa 
                             FROM mst_action_owner AS a 
                             LEFT JOIN mst_users AS b ON a.owner_sesa = b.sesa_id 
                             WHERE a.tag_id = @TagId AND a.facility_id = @FacilityId 
                             ORDER BY a.record_date DESC";

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@FacilityId", FacilityId);
                    cmd.Parameters.AddWithValue("@TagId",      TagId);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            ownerName   = reader["name"].ToString();
                            ownerSesaId = reader["owner_sesa"].ToString();
                        }
                    }
                }
            }

            return Json(new { name = ownerName, SesaId = ownerSesaId });
        }

        // ===================================================================
        // UTILITIES
        // ===================================================================

        /// <summary>
        /// Mengambil range tanggal default (From/To) dari database via SP GetDateSO.
        /// Digunakan untuk mengisi input date_from dan date_to saat halaman pertama kali dibuka.
        /// </summary>
        [HttpGet]
        public JsonResult GetDateSO()
        {
            var date = new DateModel();

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            using (SqlCommand cmd = new SqlCommand("GetDateSO", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    if (reader.Read())
                    {
                        date.FromDate    = reader["From_Date"].ToString();
                        date.CurrentDate = reader["To_Date"].ToString();
                    }
                }
            }

            return Json(date);
        }

        // ===================================================================
        // DATA ABNORMALITY (ABN)
        // ===================================================================

        /// <summary>
        /// Mengambil daftar abnormality berdasarkan filter tanggal dan fasilitas.
        /// Mengembalikan Partial View tabel ABN.
        /// </summary>
        public IActionResult GetABN(string date_from, string date_to, string facility_id)
        {
            string sesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string level  = User.FindFirst("P1F_TPM360_HUB_level")?.Value;
            var abnList   = new List<ABNModel>();

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                SqlCommand cmd = new SqlCommand("GET_ABN", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@date_from",   date_from);
                cmd.Parameters.AddWithValue("@date_to",     date_to);
                cmd.Parameters.AddWithValue("@facility_id", facility_id);
                cmd.Parameters.AddWithValue("@sesa_id",     sesaId);
                cmd.Parameters.AddWithValue("@level",       level);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        abnList.Add(new ABNModel
                        {
                            date_find      = reader["finding_date"] != DBNull.Value
                                             ? ((DateTime)reader["finding_date"]).ToString("MMM dd, yyyy") : "-",
                            facility_id    = reader["facility_id"].ToString(),
                            facility       = reader["facility"].ToString(),
                            order_id       = reader["order_id"].ToString(),
                            line           = reader["line_no"].ToString(),
                            station_id     = reader["station_id"].ToString(),
                            tpm_tag        = reader["tag_dept"].ToString(),
                            tag_id         = reader["tag_id"].ToString(),
                            operator_sesa  = reader["operator"].ToString(),
                            findings       = reader["remark"].ToString(),
                            picture        = reader["picture_finding"].ToString(),
                            name_owner     = reader["name_owner"].ToString(),
                            status_request = reader["status_request"].ToString(),
                            status_dynamic = reader["status_desc"].ToString(),
                            status_action  = reader["status_action"].ToString(),
                            owner_sesa     = reader["owner_sesa"].ToString(),
                            requestor_sesa = reader["sesa_id"].ToString(),
                            assigned_sesa  = reader["assigned_sesa"].ToString(),
                            validator_sesa = reader["validator_sesa"].ToString(),
                            name_validator = reader["name_validator"].ToString(),
                            image          = reader["image"].ToString(),
                            attachment_file= reader["attachment_file"].ToString(),
                            corrective     = reader["corrective"].ToString()
                        });
                    }
                }
            }

            return PartialView("_ABNTable", abnList);
        }

        /// <summary>
        /// Mengambil detail satu record ABN berdasarkan order_id.
        /// Mengembalikan data dalam format JSON.
        /// </summary>
        public IActionResult GetABNDetail(string order_id)
        {
            var data = new ABNModel();
            string query = "SELECT * FROM V_ABNORMALITIES WHERE order_id = @order_id";

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@order_id", order_id);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            data.facility_id        = reader["facility_id"].ToString();
                            data.facility           = reader["facility"].ToString();
                            data.picture            = reader["picture_finding"].ToString();
                            data.order_id           = reader["order_id"].ToString();
                            data.abn_type           = reader["abn_type"].ToString();
                            data.abn_type_id        = reader["abn_type_id"].ToString();
                            data.abn_happen         = reader["abn_happen"].ToString();
                            data.abn_rootcause      = reader["abn_rootcause"].ToString();
                            data.abn_rootcause_id   = reader["abn_rootcause_id"].ToString();
                            data.rootcause_analysis = reader["rootcause_analysis"].ToString();
                            data.machine_part       = reader["machine_part"].ToString();
                            data.am_checklist       = reader["am_checklist"].ToString();
                            data.assigned_name      = reader["assigned_name"].ToString();
                            data.corrective         = reader["corrective"].ToString();
                            data.status_action      = reader["status_action"].ToString();
                            data.image              = reader["image"].ToString();
                            data.target_completion  = reader["target_completion"] != DBNull.Value
                                                      ? ((DateTime)reader["target_completion"]).ToString("yyyy-MM-dd") : "-";
                            data.completed_date     = reader["completed_date"] != DBNull.Value
                                                      ? ((DateTime)reader["completed_date"]).ToString("yyyy-MM-dd") : "-";
                            data.date_find          = reader["finding_date"] != DBNull.Value
                                                      ? ((DateTime)reader["finding_date"]).ToString("yyyy-MM-dd") : "-";
                        }
                    }
                }
            }

            return Json(data);
        }

        /// <summary>
        /// Mengambil histori perubahan status sebuah ABN berdasarkan order_id.
        /// Mengembalikan Partial View tabel history.
        /// </summary>
        [HttpGet]
        public IActionResult GetDetailHistory(string order_id)
        {
            var historyList = new List<ABNModelHistory>();

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                SqlCommand cmd = new SqlCommand("GET_ABN_HISTORY", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@order_id", order_id);
                conn.Open();

                try
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            historyList.Add(new ABNModelHistory
                            {
                                sesa_id     = reader["sesa_id"].ToString(),
                                name        = reader["name"].ToString(),
                                ova         = reader["ova"].ToString(),
                                remark      = reader["remark"] != DBNull.Value ? reader["remark"].ToString() : "-",
                                record_date = reader["record_date"] != DBNull.Value
                                              ? Convert.ToDateTime(reader["record_date"]).ToString("MM/dd/yyyy") : "-"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading ABN history: {ex.Message}");
                }
            }

            return PartialView("_TableABNDetailHistory", historyList);
        }

        // ===================================================================
        // HELPER PRIVATE
        // ===================================================================

        /// <summary>
        /// Mencari sesa_id berdasarkan nama user di database.
        /// Jika nama tidak valid (terlalu pendek/panjang), kembalikan nilai aslinya.
        /// </summary>
        private string GetSesaIdByName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName) || userName.Length < 6 || userName.Length > 50)
                return userName;

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                conn.Open();
                string query = "SELECT TOP 1 sesa_id FROM mst_users WHERE name = @UserName";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@UserName", userName);
                    object result = cmd.ExecuteScalar();
                    return result?.ToString();
                }
            }
        }

        /// <summary>
        /// Menyimpan file ke folder upload/img/abn dan mengembalikan nama file yang dihasilkan.
        /// Format nama file: MAT-[UNIQUEID]-[SUFFIX][EXTENSION]
        /// </summary>
        private async Task<string> SaveFileAsync(IFormFile file, string suffix, string subFolder = null)
        {
            if (file == null || file.Length == 0)
                return null;

            string uniqueId  = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            string extension = Path.GetExtension(file.FileName);
            string fileName  = $"MAT-{uniqueId}-{suffix}{extension}";

            string folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "upload", "img", "abn");
            if (subFolder != null)
                folderPath = Path.Combine(folderPath, subFolder);

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            string filePath = Path.Combine(folderPath, fileName);
            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            return fileName;
        }

        // ===================================================================
        // INPUT ABNORMALITY
        // ===================================================================

        /// <summary>
        /// [AddInput] Menambahkan data abnormality baru (dari form Observation).
        /// Proses:
        ///   1. Validasi level akses user
        ///   2. Upload foto finding, foto action (jika fixed by myself), dan PDF
        ///   3. Tentukan validator SESA
        ///   4. Simpan ke database via stored procedure AddAbnormality
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddInput(
            IFormFile file, IFormFile FormFileAdd,
            string date_find, string facility_id, string line, string station,
            string tpm_tag, string sesa_op, string finding,
            string fixed_myself,
            string abn_type, string abn_happen, string abn_rootcause,
            string input_root, string input_machine, string input_corrective_action,
            IFormFile file_action, string date_target,
            string am_checklist, string assigned_action, string status_for_action,
            string date_completed, string action_owner_sesa, string validated_by_sesa)
        {
            string sesaId    = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            // Validasi akses
            if (!userLevel.Contains("mat") && !userLevel.Contains("mat_admin") && !userLevel.Contains("superadmin"))
                return Json(new { success = false, message = "You don't have access rights." });

            try
            {
                // Fungsi bantu konversi nilai kosong/placeholder menjadi DBNull
                object GetDbValue(string value)
                    => string.IsNullOrWhiteSpace(value) || value.Trim().StartsWith("--- Select") ? DBNull.Value : value;

                // Fungsi bantu parse tanggal
                object ParseDate(string dateString)
                    => DateTime.TryParse(dateString, out DateTime parsed) ? parsed : (object)DBNull.Value;

                // Fungsi bantu: cari sesa_id dari nama (jika input adalah nama, bukan SESA)
                string ResolveSesaId(string input)
                {
                    if (string.IsNullOrWhiteSpace(input) || input.Length < 6 || input.StartsWith("SESA"))
                        return input;

                    using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand("SELECT TOP 1 sesa_id FROM mst_users WHERE name = @UserName", conn))
                        {
                            cmd.Parameters.AddWithValue("@UserName", input);
                            object result = cmd.ExecuteScalar();
                            return result?.ToString();
                        }
                    }
                }

                bool isFixedByMyself = fixed_myself == "Fixed by myself";

                // Upload foto finding (BEFORE)
                string fileNameFinding = await SaveFileAsync(file, "BEFORE");

                // Upload foto action (AFTER) — hanya jika 'Fixed by myself'
                string fileNameAction = isFixedByMyself ? await SaveFileAsync(file_action, "AFTER") : null;

                // Upload lampiran PDF
                string fileNamePdf = await SaveFileAsync(FormFileAdd, "PDF", "pdf");

                // Tentukan SESA validator
                string finalValidatorSesa = null;
                if (isFixedByMyself)
                {
                    finalValidatorSesa = ResolveSesaId(validated_by_sesa);

                    // Jika validator tidak valid, fallback ke action owner dari database
                    if (string.IsNullOrWhiteSpace(finalValidatorSesa) || finalValidatorSesa.Length > 20)
                    {
                        var ownerResult = GetActionOwner(facility_id, tpm_tag);
                        finalValidatorSesa = (ownerResult.Value as dynamic)?.SesaId;
                    }
                }

                string finalActionOwnerSesa = GetDbValue(action_owner_sesa)?.ToString();

                // Simpan ke database
                using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("AddAbnormality", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@date_find",               ParseDate(date_find));
                        cmd.Parameters.AddWithValue("@sesa_id",                 sesaId);
                        cmd.Parameters.AddWithValue("@facility_id",             facility_id);
                        cmd.Parameters.AddWithValue("@line",                    line);
                        cmd.Parameters.AddWithValue("@station",                 station);
                        cmd.Parameters.AddWithValue("@tpm_tag",                 tpm_tag);
                        cmd.Parameters.AddWithValue("@sesa_op",                 GetDbValue(sesa_op));
                        cmd.Parameters.AddWithValue("@finding",                 finding);
                        cmd.Parameters.AddWithValue("@picture_finding",         GetDbValue(fileNameFinding));
                        cmd.Parameters.AddWithValue("@fixed_by_type_value",     fixed_myself);
                        cmd.Parameters.AddWithValue("@abn_type",                GetDbValue(abn_type));
                        cmd.Parameters.AddWithValue("@abn_happen",              GetDbValue(abn_happen));
                        cmd.Parameters.AddWithValue("@abn_rootcause",           GetDbValue(abn_rootcause));
                        cmd.Parameters.AddWithValue("@input_root",              GetDbValue(input_root));
                        cmd.Parameters.AddWithValue("@input_machine",           GetDbValue(input_machine));
                        cmd.Parameters.AddWithValue("@input_corrective_action", GetDbValue(input_corrective_action));
                        cmd.Parameters.AddWithValue("@picture_action",          GetDbValue(fileNameAction));
                        cmd.Parameters.AddWithValue("@date_target",             ParseDate(date_target));
                        cmd.Parameters.AddWithValue("@am_checklist",            GetDbValue(am_checklist));
                        cmd.Parameters.AddWithValue("@assigned_action",         GetDbValue(assigned_action));
                        cmd.Parameters.AddWithValue("@status_action_value",     GetDbValue(status_for_action));
                        cmd.Parameters.AddWithValue("@date_completed",          isFixedByMyself ? ParseDate(date_completed) : (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@action_owner_sesa_sp",    GetDbValue(finalActionOwnerSesa));
                        cmd.Parameters.AddWithValue("@validated_by_sesa_sp",    GetDbValue(finalValidatorSesa));
                        cmd.Parameters.AddWithValue("@attachment_file",         GetDbValue(fileNamePdf));

                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Data berhasil ditambahkan!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.ToString() });
            }
        }

        /// <summary>
        /// [AddInput2] Update data ABN oleh Action Owner.
        /// Mengupload foto AFTER dan PDF, lalu menyimpan via SP AddActionOwner.
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddInput2(
            IFormFile file, IFormFile file_pdf,
            string order_id, string facility_id,
            string abn_type, string abn_happen, string abn_rootcause,
            string input_root, string input_machine,
            string am_checklist, string assigned_action,
            string input_corrective_action, string date_target,
            string status_for_action, string date_completed)
        {
            string sesaId    = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            if (!userLevel.Contains("mat") && !userLevel.Contains("admin") && !userLevel.Contains("superadmin"))
                return Json(new { success = false, message = "You don't have access rights." });

            try
            {
                object GetDbValue(string value)
                    => string.IsNullOrWhiteSpace(value) || value.Trim() == "--- Select ---" ? DBNull.Value : value;

                object ParseDate(string dateString)
                    => DateTime.TryParse(dateString, out DateTime parsed) ? parsed : (object)DBNull.Value;

                string fileNameAfter = await SaveFileAsync(file,     "AFTER");
                string fileNamePdf   = await SaveFileAsync(file_pdf, "AFTER", "pdf"); // Note: suffix 'AFTER' dipertahankan sesuai aslinya

                using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("AddActionOwner", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@action_owner",            sesaId);
                        cmd.Parameters.AddWithValue("@order_id",                order_id);
                        cmd.Parameters.AddWithValue("@facility_id",             facility_id);
                        cmd.Parameters.AddWithValue("@abn_type",                GetDbValue(abn_type));
                        cmd.Parameters.AddWithValue("@abn_happen",              GetDbValue(abn_happen));
                        cmd.Parameters.AddWithValue("@abn_rootcause",           GetDbValue(abn_rootcause));
                        cmd.Parameters.AddWithValue("@input_root",              GetDbValue(input_root));
                        cmd.Parameters.AddWithValue("@input_machine",           GetDbValue(input_machine));
                        cmd.Parameters.AddWithValue("@am_checklist",            GetDbValue(am_checklist));
                        cmd.Parameters.AddWithValue("@assigned_action",         GetDbValue(assigned_action));
                        cmd.Parameters.AddWithValue("@input_corrective_action", GetDbValue(input_corrective_action));
                        cmd.Parameters.AddWithValue("@date_target",             ParseDate(date_target));
                        cmd.Parameters.AddWithValue("@status",                  status_for_action);
                        cmd.Parameters.AddWithValue("@date_completed",          ParseDate(date_completed));
                        cmd.Parameters.AddWithValue("@picture_action",          fileNameAfter != null ? (object)fileNameAfter : DBNull.Value);
                        cmd.Parameters.AddWithValue("@attachment_file",         fileNamePdf   != null ? (object)fileNamePdf   : DBNull.Value);

                        cmd.ExecuteNonQuery();
                    }
                }

                return Json(new { success = true, message = "Data berhasil ditambahkan!" });
            }
            catch
            {
                return Json(new { success = false, message = "Terjadi kesalahan saat menambahkan data." });
            }
        }

        /// <summary>
        /// [AddInput3] Input validasi dari Validator.
        /// Menyimpan remark, status, dan tanggal selesai via SP AddValidator.
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddInput3(string order_id, string remark, string status, string date_completed)
        {
            string sesaId    = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            if (!userLevel.Contains("mat") && !userLevel.Contains("admin") && !userLevel.Contains("superadmin"))
                return Json(new { success = false, message = "You don't have access rights." });

            try
            {
                using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("AddValidator", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@validator_sesa", sesaId);
                        cmd.Parameters.AddWithValue("@order_id",       order_id);
                        cmd.Parameters.AddWithValue("@remark",         remark);
                        cmd.Parameters.AddWithValue("@status",         status);
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

        /// <summary>
        /// [AddAssigned] Input tindakan dari Assigned Person (penanggung jawab yang di-assign).
        /// Upload foto AFTER dan PDF, lalu simpan via SP AddAssigned.
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddAssigned(
            IFormFile file, IFormFile file_pdf,
            string order_id, string input_corrective, string date_target)
        {
            string sesaId    = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            if (!userLevel.Contains("mat") && !userLevel.Contains("admin") && !userLevel.Contains("superadmin"))
                return Json(new { success = false, message = "You don't have access rights." });

            try
            {
                string fileNameAfter = await SaveFileAsync(file,     "AFTER");
                string fileNamePdf   = await SaveFileAsync(file_pdf, "AFTER", "pdf");

                using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                {
                    conn.Open();
                    using (SqlCommand cmd = new SqlCommand("AddAssigned", conn))
                    {
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@assigned_sesa",   sesaId);
                        cmd.Parameters.AddWithValue("@order_id",         order_id);
                        cmd.Parameters.AddWithValue("@input_corrective", input_corrective);
                        cmd.Parameters.AddWithValue("@date_target",      date_target);
                        cmd.Parameters.AddWithValue("@picture_action",   fileNameAfter != null ? (object)fileNameAfter : DBNull.Value);
                        cmd.Parameters.AddWithValue("@attachment_file",  fileNamePdf   != null ? (object)fileNamePdf   : DBNull.Value);

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

        // ===================================================================
        // VALIDASI AKSI BERDASARKAN ROLE USER (QR / Link Email)
        // ===================================================================

        /// <summary>
        /// Digunakan saat user membuka link ABN (dari QR/email).
        /// Menentukan action apa yang tersedia untuk user ini berdasarkan:
        ///   - Status ABN saat ini (status_request)
        ///   - Role user terhadap ABN ini (owner / assigned / validator / requestor)
        /// Mengembalikan JSON: { success, actionToRun }
        /// </summary>
        [HttpGet]
        public JsonResult ValidateAndGetAction(string order_id)
        {
            try
            {
                string userSesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
                string userLevel  = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

                if (string.IsNullOrEmpty(userSesaId))
                    return Json(new { success = false, message = "Your session has expired. Please log in again." });

                // Ambil data ABN dari database
                string dbStatusRequest = null;
                string dbRequestorSesa = null;
                string dbOwnerSesa     = null;
                string dbAssignedSesa  = null;
                string dbValidatorSesa = null;

                using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                {
                    conn.Open();
                    string query = "SELECT * FROM V_ABNORMALITIES WHERE order_id = @order_id";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@order_id", order_id);
                        using (SqlDataReader reader = cmd.ExecuteReader())
                        {
                            if (!reader.Read())
                                return Json(new { success = false, message = "Request not found." });

                            dbStatusRequest = reader["status_request"] is DBNull ? null : reader["status_request"].ToString();
                            dbRequestorSesa = reader["sesa_id"]        is DBNull ? null : reader["sesa_id"].ToString();
                            dbOwnerSesa     = reader["owner_sesa"]     is DBNull ? null : reader["owner_sesa"].ToString();
                            dbAssignedSesa  = reader["assigned_sesa"]  is DBNull ? null : reader["assigned_sesa"].ToString();
                            dbValidatorSesa = reader["validator_sesa"] is DBNull ? null : reader["validator_sesa"].ToString();
                        }
                    }
                }

                // ── TENTUKAN ACTION BERDASARKAN ROLE USER ────────────────────

                // User adalah Action Owner
                if (userSesaId == dbOwnerSesa)
                {
                    if (dbStatusRequest == "0") return Json(new { success = true, actionToRun = "actionOwner" });
                    if (dbStatusRequest == "4") return Json(new { success = true, actionToRun = "actionOwnerClarify" });
                    return Json(new { success = true, actionToRun = "actionDetailABN" });
                }

                // User adalah Assigned Person
                if (userSesaId == dbAssignedSesa)
                {
                    if (dbStatusRequest == "3") return Json(new { success = true, actionToRun = "actionAssigned" });
                    return Json(new { success = true, actionToRun = "actionDetailABN" });
                }

                // User adalah Validator
                if (userSesaId == dbValidatorSesa)
                {
                    if (dbStatusRequest == "1") return Json(new { success = true, actionToRun = "actionValidator" });
                    return Json(new { success = true, actionToRun = "actionDetailABN" });
                }

                // User adalah Requestor (yang membuat laporan)
                if (userSesaId == dbRequestorSesa)
                    return Json(new { success = true, actionToRun = "actionDetailABN" });

                // User tidak terkait dengan ABN ini
                return Json(new { success = false, message = "Request not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred on the server: " + ex.Message });
            }
        }
    }
}