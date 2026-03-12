using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_MATA.Function;
using P1F_MATA.Models;
using System.Security.Claims;

namespace P1F_MATA.Controllers
{
    /// <summary>
    /// Controller utama untuk fitur MAT (Maintenance Abnormality Tracking).
    /// Menangani seluruh alur kerja ABN (Abnormality) mulai dari:
    ///   - Input temuan baru oleh Operator/MAT
    ///   - Tindakan oleh Action Owner
    ///   - Penugasan ke Assigned Person
    ///   - Validasi oleh Validator
    ///   - Penentuan aksi berdasarkan role user (dari QR/link email)
    /// </summary>
    public class MATController : Controller
    {
        // Untuk menentukan path penyimpanan file upload (wwwroot)
        private readonly IWebHostEnvironment _hostingEnvironment;

        // DAL sebagai satu-satunya jalur akses ke database
        private readonly DatabaseAccessLayer _db;

        /// <summary>
        /// Constructor: menerima IWebHostEnvironment dan DatabaseAccessLayer via Dependency Injection.
        /// IWebHostEnvironment diperlukan untuk mendapatkan path folder wwwroot saat upload file.
        /// </summary>
        public MATController(IWebHostEnvironment environment, DatabaseAccessLayer db)
        {
            _hostingEnvironment = environment;
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        /// <summary>
        /// Menampilkan halaman Observation (halaman utama input ABN).
        /// Semua dropdown diisi via ViewBag agar tersedia di form input.
        /// Hanya user dengan level yang sesuai Policy "UserLevel" yang bisa mengakses.
        /// </summary>
        [Authorize(Policy = "UserLevel")] // Akses dibatasi sesuai policy di Program.cs
        public IActionResult Observation()
        {
            // Isi semua dropdown yang dibutuhkan di form Observation
            ViewBag.Facility     = _db.GetFacility();     // Dropdown fasilitas/pabrik
            ViewBag.Line         = _db.GetLine();          // Dropdown line produksi
            ViewBag.Station      = _db.GetStation();       // Dropdown stasiun kerja
            ViewBag.TpmTag       = _db.GetTPMTag();        // Dropdown TPM tag (departemen)
            ViewBag.SesaOP       = _db.GetSesaOP();        // Dropdown SESA ID operator
            ViewBag.AbnType      = _db.GetAbnType();       // Dropdown tipe abnormalitas
            ViewBag.AbnHappen    = _db.GetAbnHappen();     // Dropdown kejadian abnormal
            ViewBag.AbnRootCause = _db.GetAbnRootCause();  // Dropdown root cause
            return View();
        }

        // ===================================================================
        // DROPDOWN DINAMIS
        // ===================================================================

        /// <summary>
        /// API endpoint untuk cascade dropdown Station.
        /// Dipanggil via AJAX saat user memilih Line di form,
        /// agar dropdown Station hanya menampilkan station yang berelasi dengan Line tersebut.
        /// </summary>
        /// <param name="line_no">Nomor line yang dipilih user</param>
        [HttpGet]
        public JsonResult GetStationsByLine(string line_no)
        {
            try { return Json(_db.GetStationsByLine(line_no)); }
            catch { Response.StatusCode = 500; return Json(new { error = "An error occurred while fetching stations." }); }
        }

        // ===================================================================
        // ASSIGNED ACTION & ACTION OWNER
        // ===================================================================

        /// <summary>
        /// Mengambil daftar SESA ID yang di-assign untuk kombinasi AM Checklist dan Facility tertentu.
        /// Dipanggil via AJAX saat user memilih AM Checklist di form,
        /// untuk mengisi dropdown "Assigned To".
        /// </summary>
        /// <param name="amChecklist">Kode AM Checklist yang dipilih</param>
        /// <param name="facilityId">ID fasilitas yang dipilih</param>
        [HttpPost]
        public JsonResult GetAssignedAction(string amChecklist, string facilityId)
            => Json(_db.GetAssignedAction(amChecklist, facilityId));

        /// <summary>
        /// Mengambil Action Owner berdasarkan kombinasi Facility dan TPM Tag.
        /// Dipanggil via AJAX untuk auto-fill field "Action Owner" di form input ABN.
        /// Return: { name, SesaId } — nama dan SESA ID Action Owner.
        /// </summary>
        /// <param name="FacilityId">ID fasilitas yang dipilih</param>
        /// <param name="TagId">ID TPM tag yang dipilih</param>
        [HttpGet]
        public JsonResult GetActionOwner(string FacilityId, string TagId)
        {
            var (name, sesaId) = _db.GetActionOwner(FacilityId, TagId);
            return Json(new { name, SesaId = sesaId });
        }

        // ===================================================================
        // UTILITIES
        // ===================================================================

        /// <summary>
        /// Mengambil range tanggal default (From Date dan To Date) via SP GetDateSO.
        /// Digunakan sebagai nilai awal filter tanggal di halaman Observation.
        /// </summary>
        [HttpGet]
        public JsonResult GetDateSO() => Json(_db.GetDateSO());

        // ===================================================================
        // DATA ABN
        // ===================================================================

        /// <summary>
        /// Mengambil daftar ABN sesuai filter tanggal dan fasilitas,
        /// lalu merender hasilnya sebagai Partial View tabel.
        /// Data yang ditampilkan dibatasi sesuai level dan SESA ID user yang login
        /// (logika filtering ada di SP GET_ABN di database).
        /// </summary>
        /// <param name="date_from">Tanggal awal filter</param>
        /// <param name="date_to">Tanggal akhir filter</param>
        /// <param name="facility_id">ID fasilitas filter</param>
        public IActionResult GetABN(string date_from, string date_to, string facility_id)
        {
            // Ambil identitas user yang sedang login dari Claims
            string sesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            string level  = User.FindFirst("P1F_MATA_level")?.Value;

            // SP GET_ABN akan memfilter data berdasarkan level user
            // (misal: level "mat" hanya melihat ABN miliknya sendiri)
            var abnList = _db.GetABNList(date_from, date_to, facility_id, sesaId, level);
            return PartialView("_ABNTable", abnList);
        }

        /// <summary>
        /// Mengambil detail lengkap 1 record ABN berdasarkan order_id.
        /// Dipanggil via AJAX saat user klik baris di tabel ABN
        /// untuk menampilkan modal detail.
        /// </summary>
        /// <param name="order_id">ID unik ABN yang ingin dilihat detailnya</param>
        public IActionResult GetABNDetail(string order_id)
            => Json(_db.GetABNDetail(order_id));

        /// <summary>
        /// Mengambil histori perubahan status 1 record ABN.
        /// Menampilkan siapa yang melakukan aksi apa dan kapan
        /// (riwayat dari Requestor → Action Owner → Assigned → Validator).
        /// </summary>
        /// <param name="order_id">ID unik ABN yang ingin dilihat historinya</param>
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
        /// Menyimpan file upload ke folder wwwroot/upload/img/abn.
        /// Nama file di-generate otomatis dengan format: MAT-[UNIQUEID]-[SUFFIX][EXT]
        /// Contoh: MAT-A1B2C3D4-BEFORE.jpg atau MAT-E5F6G7H8-AFTER.pdf
        ///
        /// Return null jika file kosong/tidak ada.
        /// </summary>
        /// <param name="file">File yang diupload dari form</param>
        /// <param name="suffix">Label file: "BEFORE", "AFTER", atau "PDF"</param>
        /// <param name="subFolder">Sub-folder opsional, misal "pdf" untuk file PDF</param>
        private async Task<string> SaveFileAsync(IFormFile file, string suffix, string subFolder = null)
        {
            // Abaikan jika tidak ada file yang diupload
            if (file == null || file.Length == 0) return null;

            // Buat nama file unik untuk menghindari tabrakan nama
            string uniqueId  = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            string extension = Path.GetExtension(file.FileName);
            string fileName  = $"MAT-{uniqueId}-{suffix}{extension}";

            // Tentukan path folder tujuan (buat folder jika belum ada)
            string folderPath = Path.Combine(_hostingEnvironment.WebRootPath, "upload", "img", "abn");
            if (subFolder != null) folderPath = Path.Combine(folderPath, subFolder);
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            // Salin isi file ke disk secara async
            using var stream = new FileStream(Path.Combine(folderPath, fileName), FileMode.Create);
            await file.CopyToAsync(stream);

            // Kembalikan nama file saja (bukan full path) untuk disimpan di database
            return fileName;
        }

        /// <summary>
        /// Helper konversi nilai string kosong atau placeholder dropdown menjadi DBNull.
        /// Digunakan agar parameter SP tidak mengirim string kosong ke database,
        /// melainkan NULL yang sesuai dengan tipe kolom di SQL Server.
        /// Contoh input yang dikonversi: "", " ", "--- Select Facility ---"
        /// </summary>
        private static object DbVal(string value)
            => string.IsNullOrWhiteSpace(value) || value.Trim().StartsWith("--- Select") ? DBNull.Value : value;

        /// <summary>
        /// Helper parse string tanggal menjadi DateTime.
        /// Jika string tidak valid atau kosong → kembalikan DBNull (NULL ke database).
        /// Digunakan untuk parameter tanggal di semua SP.
        /// </summary>
        private static object ParseDate(string dateString)
            => DateTime.TryParse(dateString, out DateTime parsed) ? parsed : (object)DBNull.Value;

        // ===================================================================
        // INPUT ABN
        // ===================================================================

        /// <summary>
        /// [AddInput] Menambah record ABN baru dari form Observation.
        ///
        /// Alur:
        ///   1. Cek level akses user (hanya mat / mat_admin / superadmin)
        ///   2. Upload foto finding (BEFORE) dan opsional foto action (AFTER) jika "Fixed by myself"
        ///   3. Upload PDF lampiran
        ///   4. Tentukan Validator SESA — dari input user, atau fallback ke Action Owner
        ///   5. Simpan semua data ke database via SP AddAbnormality
        ///
        /// Catatan: Jika "Fixed by myself", foto AFTER dan tanggal selesai wajib diisi.
        /// Jika tidak, foto AFTER dan validator tidak disimpan (null).
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

            // Cek hak akses — hanya level MAT yang boleh input ABN baru
            if (!userLevel.Contains("mat") && !userLevel.Contains("mat_admin") && !userLevel.Contains("superadmin"))
                return Json(new { success = false, message = "You don't have access rights." });

            try
            {
                // Tentukan apakah temuan langsung diselesaikan sendiri oleh pelapor
                bool isFixedByMyself = fixed_myself == "Fixed by myself";

                // Upload foto finding sebelum tindakan (selalu wajib)
                string pictureFinding = await SaveFileAsync(file, "BEFORE");

                // Upload foto sesudah tindakan (hanya jika Fixed by myself)
                string pictureAction  = isFixedByMyself ? await SaveFileAsync(file_action, "AFTER") : null;

                // Upload file PDF lampiran (opsional)
                string attachmentFile = await SaveFileAsync(FormFileAdd, "PDF", "pdf");

                // ---------------------------------------------------------------
                // TENTUKAN VALIDATOR SESA
                // Prioritas: input user → jika tidak valid → fallback ke Action Owner
                // ---------------------------------------------------------------
                string finalValidatorSesa = null;
                if (isFixedByMyself)
                {
                    // Coba cari SESA ID berdasarkan nama yang diinput user
                    finalValidatorSesa = _db.GetSesaIdByName(validated_by_sesa);

                    // Jika tidak ditemukan atau terlalu panjang → gunakan Action Owner sebagai validator
                    if (string.IsNullOrWhiteSpace(finalValidatorSesa) || finalValidatorSesa.Length > 20)
                    {
                        var (_, ownerSesaId) = _db.GetActionOwner(facility_id, tpm_tag);
                        finalValidatorSesa = ownerSesaId;
                    }
                }

                // Simpan record ABN baru ke database via SP AddAbnormality
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
                    isFixedByMyself ? ParseDate(date_completed) : (object)DBNull.Value, // Tanggal selesai hanya jika fixed by myself
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
        /// [AddInput2] Update data ABN oleh Action Owner.
        /// Dipanggil saat Action Owner mengisi form tindakan korektif atas ABN yang ditugaskan kepadanya.
        ///
        /// Alur:
        ///   1. Cek level akses user
        ///   2. Upload foto AFTER dan PDF
        ///   3. Simpan via SP AddActionOwner
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

            // Level "mat" sudah cukup karena Action Owner bisa dari level manapun yang punya akses MAT
            if (!userLevel.Contains("mat") && !userLevel.Contains("admin") && !userLevel.Contains("superadmin"))
                return Json(new { success = false, message = "You don't have access rights." });

            try
            {
                // Upload foto sesudah tindakan dan file PDF lampiran
                string pictureAction  = await SaveFileAsync(file,     "AFTER");
                string attachmentFile = await SaveFileAsync(file_pdf, "AFTER", "pdf");

                // Simpan tindakan Action Owner ke database
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
        /// [AddInput3] Proses validasi oleh Validator.
        /// Dipanggil saat Validator menyetujui atau menolak tindakan yang sudah dilakukan Action Owner.
        /// Tidak ada upload file di sini — cukup remark, status, dan tanggal selesai.
        ///
        /// Status yang mungkin: "Approved" (closed) atau "Rejected" (dikembalikan ke Action Owner).
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
                // Simpan hasil validasi ke database via SP AddValidator
                _db.SaveValidator(sesaId, order_id, remark, status, date_completed);
                return Json(new { success = true, message = "Data berhasil ditambahkan!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Terjadi kesalahan: " + ex.Message });
            }
        }

        /// <summary>
        /// [AddAssigned] Input tindakan korektif oleh Assigned Person.
        /// Dipanggil saat orang yang ditugaskan (assigned) menyelesaikan pekerjaan
        /// dan melaporkan hasilnya (foto AFTER + keterangan).
        ///
        /// Alur:
        ///   1. Cek level akses
        ///   2. Upload foto AFTER dan PDF
        ///   3. Simpan via SP AddAssigned
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
                // Upload bukti foto dan PDF dari tindakan yang sudah dilakukan
                string pictureAction  = await SaveFileAsync(file,     "AFTER");
                string attachmentFile = await SaveFileAsync(file_pdf, "AFTER", "pdf");

                // Simpan laporan tindakan Assigned Person ke database
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
        /// Menentukan aksi apa yang tersedia untuk user terhadap suatu ABN.
        /// Dipanggil saat user membuka link ABN dari QR Code atau email notifikasi.
        ///
        /// Logika penentuan aksi berdasarkan SESA ID user vs data ABN:
        ///
        ///   User adalah Action Owner:
        ///     - Status "0" (baru)      → form Action Owner (isi tindakan)
        ///     - Status "4" (klarifikasi) → form Action Owner Clarify (revisi)
        ///     - Status lain             → hanya lihat detail
        ///
        ///   User adalah Assigned Person:
        ///     - Status "3" (ditugaskan) → form Assigned (isi laporan tindakan)
        ///     - Status lain             → hanya lihat detail
        ///
        ///   User adalah Validator:
        ///     - Status "1" (menunggu validasi) → form Validator (approve/reject)
        ///     - Status lain                    → hanya lihat detail
        ///
        ///   User adalah Requestor (pelapor):
        ///     - Semua status → hanya lihat detail
        ///
        ///   User tidak terkait ABN ini → error "Request not found"
        ///
        /// Return JSON: { success: true, actionToRun: "namaAction" }
        /// </summary>
        /// <param name="order_id">ID ABN yang dibuka dari QR/email</param>
        [HttpGet]
        public JsonResult ValidateAndGetAction(string order_id)
        {
            try
            {
                // Ambil SESA ID user yang sedang login
                string userSesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
                if (string.IsNullOrEmpty(userSesaId))
                    return Json(new { success = false, message = "Your session has expired. Please log in again." });

                // Ambil data ABN dari database untuk keperluan validasi role
                var (statusRequest, requestorSesa, ownerSesa, assignedSesa, validatorSesa, found)
                    = _db.GetABNForValidation(order_id);

                if (!found)
                    return Json(new { success = false, message = "Request not found." });

                // ---------------------------------------------------------------
                // PENCOCOKAN ROLE: bandingkan SESA ID user dengan data ABN
                // ---------------------------------------------------------------

                // User adalah Action Owner
                if (userSesaId == ownerSesa)
                {
                    if (statusRequest == "0") return Json(new { success = true, actionToRun = "actionOwner" });         // ABN baru, perlu ditangani
                    if (statusRequest == "4") return Json(new { success = true, actionToRun = "actionOwnerClarify" });  // Validator minta klarifikasi
                    return Json(new { success = true, actionToRun = "actionDetailABN" });                               // Status lain → lihat saja
                }

                // User adalah Assigned Person
                if (userSesaId == assignedSesa)
                {
                    if (statusRequest == "3") return Json(new { success = true, actionToRun = "actionAssigned" }); // Ditugaskan, perlu laporan
                    return Json(new { success = true, actionToRun = "actionDetailABN" });
                }

                // User adalah Validator
                if (userSesaId == validatorSesa)
                {
                    if (statusRequest == "1") return Json(new { success = true, actionToRun = "actionValidator" }); // Menunggu persetujuan validator
                    return Json(new { success = true, actionToRun = "actionDetailABN" });
                }

                // User adalah Requestor (pelapor ABN) → hanya bisa melihat detail
                if (userSesaId == requestorSesa)
                    return Json(new { success = true, actionToRun = "actionDetailABN" });

                // User tidak terkait dengan ABN ini sama sekali
                return Json(new { success = false, message = "Request not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred on the server: " + ex.Message });
            }
        }
    }
}