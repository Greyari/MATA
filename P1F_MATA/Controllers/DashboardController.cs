using Microsoft.AspNetCore.Mvc;
using P1F_MATA.Function;
using Microsoft.Data.SqlClient;
using System.Dynamic;
using System.Security.Claims;

namespace P1F_MATA.Controllers
{
    /// <summary>
    /// Controller untuk halaman Dashboard.
    /// Menyediakan data chart dan detail tabel yang ditampilkan di halaman dashboard.
    /// Semua data diambil dari Stored Procedure (SP) via DatabaseAccessLayer (DAL).
    /// </summary>
    public class DashboardController : Controller
    {
        // Dependency Injection: DAL sebagai satu-satunya jalur akses ke database
        private readonly DatabaseAccessLayer _db;

        /// <summary>
        /// Constructor: menerima DatabaseAccessLayer via Dependency Injection.
        /// </summary>
        public DashboardController(DatabaseAccessLayer db)
        {
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        /// <summary>
        /// Menampilkan halaman Dashboard.
        /// Jika user belum login (tidak ada Claims), redirect ke halaman Login.
        /// ViewBag diisi dengan data dropdown Facility, Line, dan Station
        /// untuk digunakan sebagai filter di halaman dashboard.
        /// </summary>
        public IActionResult Index()
        {
            // Cek apakah user sudah login dengan memeriksa Claims NameIdentifier
            if (User.FindFirst(ClaimTypes.NameIdentifier)?.Value == null)
                return RedirectToAction("Index", "Login");

            // Isi dropdown filter dashboard
            ViewBag.Facilities = _db.GetFacility();       // Daftar fasilitas/pabrik
            ViewBag.Lines      = _db.GetLineDashboard();  // Daftar line produksi
            ViewBag.Stations   = _db.GetStationDashboard(); // Daftar stasiun kerja

            return View();
        }

        // ===================================================================
        // DROPDOWN DINAMIS
        // ===================================================================

        /// <summary>
        /// API endpoint untuk cascade dropdown Station berdasarkan Line yang dipilih.
        /// Dipanggil via AJAX saat user mengganti pilihan Line di filter dashboard.
        /// </summary>
        /// <param name="line_no">Nomor line yang dipilih user</param>
        [HttpGet]
        public JsonResult GetStationsByLine(string line_no)
        {
            try
            {
                // Ambil daftar station yang berelasi dengan line_no
                return Json(_db.GetStationsByLine(line_no));
            }
            catch
            {
                // Kembalikan error 500 jika terjadi masalah di database
                Response.StatusCode = 500;
                return Json(new { error = "An error occurred while fetching stations." });
            }
        }

        // ===================================================================
        // DATE SO
        // ===================================================================

        /// <summary>
        /// Ambil range tanggal default untuk filter dashboard.
        /// Biasanya digunakan sebagai nilai awal (from_date dan to_date)
        /// saat halaman dashboard pertama kali dibuka.
        /// </summary>
        [HttpGet]
        public JsonResult GetDateSO() => Json(_db.GetDateSO());

        // ===================================================================
        // PRIVATE HELPER: CHART + DETAIL EXECUTOR
        // ===================================================================

        /// <summary>
        /// Helper generik yang digunakan oleh semua endpoint chart dan detail.
        /// Tugasnya: memanggil DAL untuk eksekusi SP, lalu memutuskan
        /// apakah hasilnya dikembalikan sebagai JSON (untuk chart)
        /// atau PartialView (untuk tabel detail).
        /// </summary>
        /// <param name="spName">Nama Stored Procedure yang akan dieksekusi</param>
        /// <param name="facility">Filter fasilitas (nullable)</param>
        /// <param name="line">Filter line produksi (nullable)</param>
        /// <param name="station">Filter stasiun (nullable)</param>
        /// <param name="date_from">Tanggal awal filter</param>
        /// <param name="date_to">Tanggal akhir filter</param>
        /// <param name="range">Rentang waktu (daily/weekly/monthly)</param>
        /// <param name="mapRow">Fungsi lambda untuk mapping baris hasil query ke object dynamic</param>
        /// <param name="partialView">Nama partial view. Jika null → kembalikan JSON</param>
        /// <param name="value">Parameter tambahan untuk filter detail (opsional)</param>
        /// <param name="type">Parameter tambahan untuk filter detail (opsional)</param>
        private IActionResult RunDashboardSP(
            string spName,
            string facility, string line, string station,
            string date_from, string date_to, string range,
            Action<List<dynamic>, SqlDataReader> mapRow,
            string partialView = null,
            string value = null, string type = null)
        {
            // Eksekusi SP via DAL dan petakan hasilnya ke List<dynamic>
            var dataList = _db.ExecuteDashboardSP(
                spName, facility, line, station,
                date_from, date_to, range,
                mapRow, value, type);

            // Jika partialView disediakan → render sebagai HTML tabel
            // Jika tidak → kembalikan sebagai JSON (untuk chart JavaScript)
            return partialView != null ? PartialView(partialView, dataList) : Json(dataList);
        }

        /// <summary>
        /// Mapper baris data untuk endpoint detail (saat user klik salah satu batang chart).
        /// Mengonversi SqlDataReader menjadi object dynamic yang berisi kolom-kolom
        /// yang dibutuhkan untuk tabel detail ABN.
        /// </summary>
        private void MapDetailRow(List<dynamic> list, SqlDataReader reader)
        {
            dynamic row = new ExpandoObject();
            row.finding_date    = reader["finding_date"].ToString();   // Tanggal temuan ABN
            row.facility        = reader["facility"].ToString();        // Nama fasilitas
            row.line_no         = reader["line_no"].ToString();         // Nomor line
            row.station_id      = reader["station_id"].ToString();      // ID stasiun
            row.tag_id          = reader["tag_id"].ToString();          // ID TPM tag
            row.tag_dept        = reader["tag_dept"].ToString();        // Nama departemen tag
            row.operators       = reader["operator"].ToString();        // SESA ID operator
            row.findings        = reader["remark"].ToString();          // Deskripsi temuan
            row.picture_finding = reader["picture_finding"].ToString(); // Foto sebelum (BEFORE)
            row.picture_after   = reader["image"].ToString();           // Foto sesudah (AFTER)
            row.corrective      = reader["corrective"].ToString();      // Tindakan korektif
            row.attachment_file = reader["attachment_file"].ToString(); // File PDF lampiran
            row.status_request  = reader["status_request"].ToString();  // Kode status (0,1,2,3,4)
            row.status_dynamic  = reader["status_desc"].ToString();     // Deskripsi status
            row.name_owner      = reader["name_owner"].ToString();      // Nama Action Owner
            row.name_validator  = reader["name_validator"].ToString();  // Nama Validator
            list.Add(row);
        }

        // ===================================================================
        // CHART DATA ENDPOINTS
        // ===================================================================
        // Semua endpoint di bawah ini dipanggil via AJAX [HttpPost] dari halaman dashboard.
        // Hasilnya berupa JSON yang langsung dikonsumsi oleh library chart (misal: Chart.js).
        // Parameter yang diterima: facility, line, station = filter lokasi
        //                          date_from, date_to = filter tanggal
        //                          range = granularitas (daily/weekly/monthly)

        /// <summary>Chart: Jumlah temuan (Findings) vs yang sudah ditutup (Closed) per periode.</summary>
        [HttpPost]
        public IActionResult GET_FINDING_CLOSED(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_FINDING_CLOSED", facility, line, station, date_from, date_to, range,
                (list, r) => {
                    dynamic d = new ExpandoObject();
                    d.Period_Group = r["Period_Group"].ToString(); // Label periode (misal: "Jan 2025")
                    d.Findings     = r["Findings"].ToString();     // Total temuan
                    d.Closed       = r["Closed"].ToString();       // Total yang sudah closed
                    list.Add(d);
                });

        /// <summary>Chart: Total OPL (One Point Lesson) akumulatif vs closed per periode.</summary>
        [HttpPost]
        public IActionResult GET_TOTAL_OPLs(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_OPLS", facility, line, station, date_from, date_to, range,
                (list, r) => {
                    dynamic d = new ExpandoObject();
                    d.Period_Group = r["Period_Group"].ToString();
                    d.Accumulative = r["Accumulative"].ToString(); // Total akumulatif OPL
                    d.Closed       = r["Closed"].ToString();
                    list.Add(d);
                });

        /// <summary>Chart: Distribusi temuan berdasarkan kategori Sharp Eye.</summary>
        [HttpPost]
        public IActionResult GET_SHARP_EYE(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_SHARP_EYE", facility, line, station, date_from, date_to, range,
                (list, r) => {
                    dynamic d = new ExpandoObject();
                    d.FindingName = r["FindingName"].ToString(); // Nama kategori temuan
                    d.BarChart    = r["BarChart"].ToString();    // Nilai untuk bar chart
                    list.Add(d);
                });

        /// <summary>Chart: Distribusi temuan berdasarkan TPM Tag (departemen).</summary>
        [HttpPost]
        public IActionResult GET_TPM_TAG(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_TPM_TAG", facility, line, station, date_from, date_to, range,
                (list, r) => {
                    dynamic d = new ExpandoObject();
                    d.TagId    = r["TagId"].ToString();    // ID TPM tag
                    d.BarChart = r["BarChart"].ToString();
                    list.Add(d);
                });

        /// <summary>Chart: Distribusi temuan berdasarkan Root Cause (penyebab utama).</summary>
        [HttpPost]
        public IActionResult GET_ROOTCAUSE(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_ROOTCAUSE", facility, line, station, date_from, date_to, range,
                (list, r) => {
                    dynamic d = new ExpandoObject();
                    d.AbnRootCause = r["AbnRootCause"].ToString(); // Nama root cause
                    d.BarChart     = r["BarChart"].ToString();
                    list.Add(d);
                });

        /// <summary>Chart: Distribusi temuan berdasarkan Kejadian (Happen).</summary>
        [HttpPost]
        public IActionResult GET_HAPPEN(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_HAPPEN", facility, line, station, date_from, date_to, range,
                (list, r) => {
                    dynamic d = new ExpandoObject();
                    d.AbnHappen = r["AbnHappen"].ToString(); // Jenis kejadian abnormal
                    d.BarChart  = r["BarChart"].ToString();
                    list.Add(d);
                });

        /// <summary>Chart: Distribusi temuan berdasarkan Tipe Abnormalitas.</summary>
        [HttpPost]
        public IActionResult GET_ABN_TYPE(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_ABN_TYPE", facility, line, station, date_from, date_to, range,
                (list, r) => {
                    dynamic d = new ExpandoObject();
                    d.AbnType  = r["AbnType"].ToString(); // Nama tipe abnormalitas
                    d.BarChart = r["BarChart"].ToString();
                    list.Add(d);
                });

        /// <summary>Chart: Perbandingan temuan yang "Fixed by Self" vs yang membutuhkan action owner.</summary>
        [HttpPost]
        public IActionResult GET_FIXED_BY_SELF(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_FIXED_BY_SELF", facility, line, station, date_from, date_to, range,
                (list, r) => {
                    dynamic d = new ExpandoObject();
                    d.FindingName = r["FindingName"].ToString(); // "Fixed by Self" atau "Not Fixed"
                    d.BarChart    = r["BarChart"].ToString();
                    list.Add(d);
                });

        /// <summary>Chart: Jumlah OPL per site/fasilitas.</summary>
        [HttpPost]
        public IActionResult GET_OPLS_PER_SITE(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_OPLS_PER_SITE", facility, line, station, date_from, date_to, range,
                (list, r) => {
                    dynamic d = new ExpandoObject();
                    d.Facility = r["Facility"].ToString(); // Nama fasilitas/site
                    d.Closed   = r["Closed"].ToString();
                    list.Add(d);
                });

        // ===================================================================
        // DETAIL DATA ENDPOINTS
        // ===================================================================
        // Endpoint berikut dipanggil saat user mengklik salah satu bagian chart
        // untuk melihat data mentah di balik angka tersebut.
        // Hasilnya berupa PartialView "_TableDetail" yang berisi tabel HTML.
        // Parameter "value" = nilai yang diklik (misal: nama bulan, nama kategori)
        // Parameter "type" = konteks tambahan (misal: "Findings" atau "Closed")

        /// <summary>Detail tabel untuk chart Finding vs Closed. Parameter 'type' membedakan kolom Finding atau Closed.</summary>
        [HttpPost]
        public IActionResult GET_DETAIL_FINDING_CLOSED(string facility, string line, string station, string date_from, string date_to, string range, string value, string type)
            => RunDashboardSP("GET_DETAIL_FINDING_CLOSED", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value, type);

        /// <summary>Detail tabel untuk chart Total OPL.</summary>
        [HttpPost]
        public IActionResult GET_DETAIL_OPLS(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_OPLS", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail tabel untuk chart OPL per Site.</summary>
        [HttpPost]
        public IActionResult GET_DETAIL_OPLS_PER_SITE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_OPLS_PER_SITE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail tabel untuk chart Sharp Eye.</summary>
        [HttpPost]
        public IActionResult GET_DETAIL_SHARP_EYE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_SHARP_EYE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail tabel untuk chart TPM Tag.</summary>
        [HttpPost]
        public IActionResult GET_DETAIL_TPM_TAG(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_TPM_TAG", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail tabel untuk chart Root Cause.</summary>
        [HttpPost]
        public IActionResult GET_DETAIL_ROOTCAUSE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_ROOTCAUSE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail tabel untuk chart Happen (kejadian).</summary>
        [HttpPost]
        public IActionResult GET_DETAIL_HAPPEN(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_HAPPEN", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail tabel untuk chart Abnormality Type.</summary>
        [HttpPost]
        public IActionResult GET_DETAIL_ABN_TYPE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_ABN_TYPE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail tabel untuk chart Fixed by Self.</summary>
        [HttpPost]
        public IActionResult GET_DETAIL_FIXED_BY_SELF(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_FIXED_BY_SELF", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);
    }
}