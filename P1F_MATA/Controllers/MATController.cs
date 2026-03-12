using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_MATA.Function;
using P1F_MATA.Models;
using System.Security.Claims;

namespace P1F_MATA.Controllers
{
    public class MATController : Controller
    {
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly DatabaseAccessLayer _db;

        public MATController(IWebHostEnvironment environment, DatabaseAccessLayer db)
        {
            _hostingEnvironment = environment;
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        /// <summary>Halaman Observation. Siapkan semua dropdown via ViewBag.</summary>
        [Authorize(Policy = "UserLevel")]
        public IActionResult Observation()
        {
            ViewBag.Facility     = _db.GetFacility();
            ViewBag.Line         = _db.GetLine();
            ViewBag.Station      = _db.GetStation();
            ViewBag.TpmTag       = _db.GetTPMTag();
            ViewBag.SesaOP       = _db.GetSesaOP();
            ViewBag.AbnType      = _db.GetAbnType();
            ViewBag.AbnHappen    = _db.GetAbnHappen();
            ViewBag.AbnRootCause = _db.GetAbnRootCause();
            return View();
        }

        // ===================================================================
        // DROPDOWN DINAMIS
        // ===================================================================

        /// <summary>API: Cascade dropdown — ambil station berdasarkan line.</summary>
        [HttpGet]
        public JsonResult GetStationsByLine(string line_no)
        {
            try { return Json(_db.GetStationsByLine(line_no)); }
            catch { Response.StatusCode = 500; return Json(new { error = "An error occurred while fetching stations." }); }
        }

        // ===================================================================
        // ASSIGNED ACTION & ACTION OWNER
        // ===================================================================

        /// <summary>Ambil daftar SESA yang di-assign untuk checklist dan fasilitas tertentu.</summary>
        [HttpPost]
        public JsonResult GetAssignedAction(string amChecklist, string facilityId)
            => Json(_db.GetAssignedAction(amChecklist, facilityId));

        /// <summary>Ambil Action Owner berdasarkan facility dan TPM tag.</summary>
        [HttpGet]
        public JsonResult GetActionOwner(string FacilityId, string TagId)
        {
            var (name, sesaId) = _db.GetActionOwner(FacilityId, TagId);
            return Json(new { name, SesaId = sesaId });
        }

        // ===================================================================
        // UTILITIES
        // ===================================================================

        /// <summary>Ambil range tanggal default via SP GetDateSO.</summary>
        [HttpGet]
        public JsonResult GetDateSO() => Json(_db.GetDateSO());

        // ===================================================================
        // DATA ABN
        // ===================================================================

        /// <summary>Ambil list ABN sesuai filter → kembalikan Partial View tabel.</summary>
        public IActionResult GetABN(string date_from, string date_to, string facility_id)
        {
            string sesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string level  = User.FindFirst("P1F_MATA_level")?.Value;
            var abnList   = _db.GetABNList(date_from, date_to, facility_id, sesaId, level);
            return PartialView("_ABNTable", abnList);
        }

        /// <summary>Ambil detail 1 record ABN → kembalikan JSON.</summary>
        public IActionResult GetABNDetail(string order_id)
            => Json(_db.GetABNDetail(order_id));

        /// <summary>Ambil histori ABN → kembalikan Partial View.</summary>
        [HttpGet]
        public IActionResult GetDetailHistory(string order_id)
        {
            var historyList = _db.GetABNHistory(order_id);
            return PartialView("_TableABNDetailHistory", historyList);
        }

        // ===================================================================
        // PRIVATE HELPERS (NON-DB)
        // ===================================================================

        /// <summary>
        /// Upload file ke folder upload/img/abn.
        /// Format nama file: MAT-[UNIQUEID]-[SUFFIX][EXT]
        /// </summary>
        private async Task<string> SaveFileAsync(IFormFile file, string suffix, string subFolder = null)
        {
            if (file == null || file.Length == 0) return null;

            string uniqueId  = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            string extension = Path.GetExtension(file.FileName);
            string fileName  = $"MAT-{uniqueId}-{suffix}{extension}";

            string folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "upload", "img", "abn");
            if (subFolder != null) folderPath = Path.Combine(folderPath, subFolder);

            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            using var stream = new FileStream(Path.Combine(folderPath, fileName), FileMode.Create);
            await file.CopyToAsync(stream);
            return fileName;
        }

        /// <summary>Konversi string kosong/placeholder menjadi DBNull untuk parameter SP.</summary>
        private static object DbVal(string value)
            => string.IsNullOrWhiteSpace(value) || value.Trim().StartsWith("--- Select") ? DBNull.Value : value;

        /// <summary>Parse tanggal string menjadi DateTime atau DBNull.</summary>
        private static object ParseDate(string dateString)
            => DateTime.TryParse(dateString, out DateTime parsed) ? parsed : (object)DBNull.Value;

        // ===================================================================
        // INPUT ABN
        // ===================================================================

        /// <summary>
        /// [AddInput] Tambah ABN baru dari form Observation.
        /// Upload foto finding, foto action (jika fixed by myself), dan PDF.
        /// Lalu simpan via SP AddAbnormality.
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddInput(
            IFormFile file, IFormFile FormFileAdd,
            string date_find, string facility_id, string line, string station,
            string tpm_tag, string sesa_op, string finding, string fixed_myself,
            string abn_type, string abn_happen, string abn_rootcause,
            string input_root, string input_machine, string input_corrective_action,
            IFormFile file_action, string date_target,
            string am_checklist, string assigned_action, string status_for_action,
            string date_completed, string action_owner_sesa, string validated_by_sesa)
        {
            string sesaId    = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userLevel = User.FindFirst("P1F_MATA_level")?.Value;

            if (!userLevel.Contains("mat") && !userLevel.Contains("mat_admin") && !userLevel.Contains("superadmin"))
                return Json(new { success = false, message = "You don't have access rights." });

            try
            {
                bool isFixedByMyself = fixed_myself == "Fixed by myself";

                // Upload file
                string pictureFinding = await SaveFileAsync(file, "BEFORE");
                string pictureAction  = isFixedByMyself ? await SaveFileAsync(file_action, "AFTER") : null;
                string attachmentFile = await SaveFileAsync(FormFileAdd, "PDF", "pdf");

                // Tentukan validator SESA
                string finalValidatorSesa = null;
                if (isFixedByMyself)
                {
                    finalValidatorSesa = _db.GetSesaIdByName(validated_by_sesa);
                    if (string.IsNullOrWhiteSpace(finalValidatorSesa) || finalValidatorSesa.Length > 20)
                    {
                        var (_, ownerSesaId) = _db.GetActionOwner(facility_id, tpm_tag);
                        finalValidatorSesa = ownerSesaId;
                    }
                }

                // Simpan ke DB
                _db.SaveAbnormality(
                    date_find, sesaId, facility_id, line, station,
                    tpm_tag,
                    DbVal(sesa_op),
                    finding,
                    DbVal(pictureFinding),
                    fixed_myself,
                    DbVal(abn_type), DbVal(abn_happen), DbVal(abn_rootcause),
                    DbVal(input_root), DbVal(input_machine), DbVal(input_corrective_action),
                    pictureAction  != null ? pictureAction  : (object)DBNull.Value,
                    ParseDate(date_target),
                    DbVal(am_checklist), DbVal(assigned_action), DbVal(status_for_action),
                    isFixedByMyself ? ParseDate(date_completed) : (object)DBNull.Value,
                    DbVal(action_owner_sesa),
                    finalValidatorSesa != null ? finalValidatorSesa : (object)DBNull.Value,
                    attachmentFile != null ? attachmentFile : (object)DBNull.Value
                );

                return Json(new { success = true, message = "Data berhasil ditambahkan!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.ToString() });
            }
        }

        /// <summary>
        /// [AddInput2] Update ABN oleh Action Owner.
        /// Upload foto AFTER dan PDF, simpan via SP AddActionOwner.
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
            string userLevel = User.FindFirst("P1F_MATA_level")?.Value;

            if (!userLevel.Contains("mat") && !userLevel.Contains("admin") && !userLevel.Contains("superadmin"))
                return Json(new { success = false, message = "You don't have access rights." });

            try
            {
                string pictureAction  = await SaveFileAsync(file,     "AFTER");
                string attachmentFile = await SaveFileAsync(file_pdf, "AFTER", "pdf");

                _db.SaveActionOwner(
                    sesaId, order_id, facility_id,
                    DbVal(abn_type), DbVal(abn_happen), DbVal(abn_rootcause),
                    DbVal(input_root), DbVal(input_machine),
                    DbVal(am_checklist), DbVal(assigned_action), DbVal(input_corrective_action),
                    ParseDate(date_target),
                    status_for_action,
                    ParseDate(date_completed),
                    pictureAction  != null ? pictureAction  : (object)DBNull.Value,
                    attachmentFile != null ? attachmentFile : (object)DBNull.Value
                );

                return Json(new { success = true, message = "Data berhasil ditambahkan!" });
            }
            catch
            {
                return Json(new { success = false, message = "Terjadi kesalahan saat menambahkan data." });
            }
        }

        /// <summary>
        /// [AddInput3] Validasi oleh Validator.
        /// Simpan remark, status, dan tanggal selesai via SP AddValidator.
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddInput3(string order_id, string remark, string status, string date_completed)
        {
            string sesaId    = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userLevel = User.FindFirst("P1F_MATA_level")?.Value;

            if (!userLevel.Contains("mat") && !userLevel.Contains("admin") && !userLevel.Contains("superadmin"))
                return Json(new { success = false, message = "You don't have access rights." });

            try
            {
                _db.SaveValidator(sesaId, order_id, remark, status, date_completed);
                return Json(new { success = true, message = "Data berhasil ditambahkan!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
            }
        }

        /// <summary>
        /// [AddAssigned] Input tindakan dari Assigned Person.
        /// Upload foto AFTER dan PDF, simpan via SP AddAssigned.
        /// </summary>
        [HttpPost]
        public async Task<JsonResult> AddAssigned(
            IFormFile file, IFormFile file_pdf,
            string order_id, string input_corrective, string date_target)
        {
            string sesaId    = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string userLevel = User.FindFirst("P1F_MATA_level")?.Value;

            if (!userLevel.Contains("mat") && !userLevel.Contains("admin") && !userLevel.Contains("superadmin"))
                return Json(new { success = false, message = "You don't have access rights." });

            try
            {
                string pictureAction  = await SaveFileAsync(file,     "AFTER");
                string attachmentFile = await SaveFileAsync(file_pdf, "AFTER", "pdf");

                _db.SaveAssigned(
                    sesaId, order_id, input_corrective, date_target,
                    pictureAction  != null ? pictureAction  : (object)DBNull.Value,
                    attachmentFile != null ? attachmentFile : (object)DBNull.Value
                );

                return Json(new { success = true, message = "Data berhasil ditambahkan!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
            }
        }

        // ===================================================================
        // VALIDASI AKSI BERDASARKAN ROLE USER
        // ===================================================================

        /// <summary>
        /// Digunakan saat user buka link ABN dari QR/email.
        /// Menentukan action apa yang tersedia berdasarkan role user terhadap ABN ini.
        /// Return JSON: { success, actionToRun }
        /// </summary>
        [HttpGet]
        public JsonResult ValidateAndGetAction(string order_id)
        {
            try
            {
                string userSesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
                if (string.IsNullOrEmpty(userSesaId))
                    return Json(new { success = false, message = "Your session has expired. Please log in again." });

                var (statusRequest, requestorSesa, ownerSesa, assignedSesa, validatorSesa, found)
                    = _db.GetABNForValidation(order_id);

                if (!found)
                    return Json(new { success = false, message = "Request not found." });

                // Tentukan action berdasarkan role user
                if (userSesaId == ownerSesa)
                {
                    if (statusRequest == "0") return Json(new { success = true, actionToRun = "actionOwner" });
                    if (statusRequest == "4") return Json(new { success = true, actionToRun = "actionOwnerClarify" });
                    return Json(new { success = true, actionToRun = "actionDetailABN" });
                }

                if (userSesaId == assignedSesa)
                {
                    if (statusRequest == "3") return Json(new { success = true, actionToRun = "actionAssigned" });
                    return Json(new { success = true, actionToRun = "actionDetailABN" });
                }

                if (userSesaId == validatorSesa)
                {
                    if (statusRequest == "1") return Json(new { success = true, actionToRun = "actionValidator" });
                    return Json(new { success = true, actionToRun = "actionDetailABN" });
                }

                if (userSesaId == requestorSesa)
                    return Json(new { success = true, actionToRun = "actionDetailABN" });

                return Json(new { success = false, message = "Request not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred on the server: " + ex.Message });
            }
        }
    }
}