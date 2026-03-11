using Microsoft.AspNetCore.Mvc;
using P1F_MATA.Function;
using P1F_MATA.Models;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Dynamic;
using System.Security.Claims;

namespace P1F_MATA.Controllers
{
    public class DashboardController : Controller
    {
        // ===================================================================
        // DEPENDENCY INJECTION
        // ===================================================================
        private readonly ApplicationDbContext _context;
        private readonly DatabaseAccessLayer _db;

        public DashboardController(ApplicationDbContext context, DatabaseAccessLayer db)
        {
            _context = context;
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        /// <summary>
        /// Menampilkan halaman Dashboard.
        /// Redirect ke Login jika user belum login.
        /// Menyiapkan data dropdown Facility, Line, dan Station via ViewBag.
        /// </summary>
        public IActionResult Index()
        {
            if (User.FindFirst(ClaimTypes.NameIdentifier)?.Value == null)
                return RedirectToAction("Index", "Login");

            ViewBag.Facilities = GetFacility();
            ViewBag.Lines      = GetLineDashboard();
            ViewBag.Stations   = GetStationDashboard();

            return View();
        }

        // ===================================================================
        // HELPER: GET DROPDOWN DATA
        // ===================================================================

        /// <summary>
        /// Helper generik untuk mengambil data list dari database.
        /// Mendukung parameter dinamis via Action addParams.
        /// </summary>
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
        private List<CodeNameModel> GetLineDashboard()
            => GetMasterData("SELECT DISTINCT line_no FROM mst_linestation ORDER BY line_no ASC", "line_no");

        /// <summary>Mengambil daftar Station unik dari tabel mst_linestation.</summary>
        private List<CodeNameModel> GetStationDashboard()
            => GetMasterData("SELECT DISTINCT station_no FROM mst_linestation ORDER BY station_no ASC", "station_no");

        /// <summary>
        /// API: Mengambil daftar station berdasarkan line yang dipilih.
        /// Digunakan untuk dropdown dinamis di filter Dashboard.
        /// </summary>
        [HttpGet]
        public JsonResult GetStationsByLine(string line_no)
        {
            try
            {
                var stations = GetMasterData(
                    "SELECT station_no FROM mst_linestation WHERE line_no = @line_no",
                    "station_no", null,
                    cmd => cmd.Parameters.AddWithValue("@line_no", line_no));
                return Json(stations);
            }
            catch
            {
                Response.StatusCode = 500;
                return Json(new { error = "An error occurred while fetching stations." });
            }
        }

        // ===================================================================
        // HELPER: STORED PROCEDURE EXECUTOR
        // ===================================================================

        /// <summary>
        /// Helper generik untuk menjalankan stored procedure dashboard.
        /// Menerima filter (facility, line, station, date range) dan mapper baris data.
        /// Jika partialView diisi → kembalikan PartialView.
        /// Jika tidak → kembalikan JSON.
        /// Parameter value dan type bersifat opsional untuk endpoint detail.
        /// </summary>
        private IActionResult ExecuteDashboardStoredProc(
            string storedProcName,
            string facility, string line, string station,
            string date_from, string date_to, string range,
            Action<List<dynamic>, SqlDataReader> mapRow,
            string partialView = null,
            string value = null,
            string type = null)
        {
            var dataList = new List<dynamic>();

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            using (SqlCommand cmd = new SqlCommand(storedProcName, conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@facility",   string.IsNullOrEmpty(facility)  ? (object)DBNull.Value : facility);
                cmd.Parameters.AddWithValue("@line_no",    string.IsNullOrEmpty(line)       ? (object)DBNull.Value : line);
                cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station)    ? (object)DBNull.Value : station);
                cmd.Parameters.AddWithValue("@range",      string.IsNullOrEmpty(range)      ? (object)DBNull.Value : range);
                cmd.Parameters.AddWithValue("@date_from",  string.IsNullOrEmpty(date_from)  ? DateTime.Now.ToString("yyyy-MM-01") : date_from);
                cmd.Parameters.AddWithValue("@date_to",    string.IsNullOrEmpty(date_to)    ? DateTime.Now.ToString("yyyy-MM-dd") : date_to);

                if (value != null) cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);
                if (type  != null) cmd.Parameters.AddWithValue("@type",  string.IsNullOrEmpty(type)  ? (object)DBNull.Value : type);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                    while (reader.Read())
                        mapRow(dataList, reader);
            }

            return partialView != null ? PartialView(partialView, dataList) : Json(dataList);
        }

        /// <summary>
        /// Helper mapper untuk endpoint detail (klik chart → lihat data).
        /// Membaca kolom standar finding dari SqlDataReader dan menambahkannya ke list.
        /// </summary>
        private void MapDetailRow(List<dynamic> list, SqlDataReader reader)
        {
            dynamic row = new ExpandoObject();
            row.finding_date    = reader["finding_date"].ToString();
            row.facility        = reader["facility"].ToString();
            row.line_no         = reader["line_no"].ToString();
            row.station_id      = reader["station_id"].ToString();
            row.tag_id          = reader["tag_id"].ToString();
            row.tag_dept        = reader["tag_dept"].ToString();
            row.operators       = reader["operator"].ToString();
            row.findings        = reader["remark"].ToString();
            row.picture_finding = reader["picture_finding"].ToString();
            row.picture_after   = reader["image"].ToString();
            row.corrective      = reader["corrective"].ToString();
            row.attachment_file = reader["attachment_file"].ToString();
            row.status_request  = reader["status_request"].ToString();
            row.status_dynamic  = reader["status_desc"].ToString();
            row.name_owner      = reader["name_owner"].ToString();
            row.name_validator  = reader["name_validator"].ToString();
            list.Add(row);
        }

        // ===================================================================
        // CHART DATA ENDPOINTS
        // Semua endpoint ini dipanggil via AJAX dari Dashboard view.
        // Masing-masing memanggil stored procedure yang sesuai dan
        // mengembalikan data JSON untuk dirender sebagai chart.
        // ===================================================================

        /// <summary>Data chart: perbandingan jumlah Findings vs Closed per periode.</summary>
        [HttpPost] public IActionResult GET_FINDING_CLOSED(string facility, string line, string station, string date_from, string date_to, string range)
            => ExecuteDashboardStoredProc("GET_FINDING_CLOSED", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.Period_Group = r["Period_Group"].ToString(); d.Findings = r["Findings"].ToString(); d.Closed = r["Closed"].ToString(); list.Add(d); });

        /// <summary>Data chart: total OPL kumulatif dan yang sudah closed per periode.</summary>
        [HttpPost] public IActionResult GET_TOTAL_OPLs(string facility, string line, string station, string date_from, string date_to, string range)
            => ExecuteDashboardStoredProc("GET_OPLS", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.Period_Group = r["Period_Group"].ToString(); d.Accumulative = r["Accumulative"].ToString(); d.Closed = r["Closed"].ToString(); list.Add(d); });

        /// <summary>Data chart: top 10 user yang paling banyak menemukan abnormality (Sharp Eye).</summary>
        [HttpPost] public IActionResult GET_SHARP_EYE(string facility, string line, string station, string date_from, string date_to, string range)
            => ExecuteDashboardStoredProc("GET_SHARP_EYE", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.FindingName = r["FindingName"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        /// <summary>Data chart: jumlah temuan berdasarkan kategori TPM Tag.</summary>
        [HttpPost] public IActionResult GET_TPM_TAG(string facility, string line, string station, string date_from, string date_to, string range)
            => ExecuteDashboardStoredProc("GET_TPM_TAG", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.TagId = r["TagId"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        /// <summary>Data chart: jumlah temuan berdasarkan Root Cause (Human Weakness).</summary>
        [HttpPost] public IActionResult GET_ROOTCAUSE(string facility, string line, string station, string date_from, string date_to, string range)
            => ExecuteDashboardStoredProc("GET_ROOTCAUSE", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.AbnRootCause = r["AbnRootCause"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        /// <summary>Data chart: jumlah temuan berdasarkan dampak jika dibiarkan (What will happen).</summary>
        [HttpPost] public IActionResult GET_HAPPEN(string facility, string line, string station, string date_from, string date_to, string range)
            => ExecuteDashboardStoredProc("GET_HAPPEN", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.AbnHappen = r["AbnHappen"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        /// <summary>Data chart: jumlah temuan berdasarkan tipe abnormality (Seven Types).</summary>
        [HttpPost] public IActionResult GET_ABN_TYPE(string facility, string line, string station, string date_from, string date_to, string range)
            => ExecuteDashboardStoredProc("GET_ABN_TYPE", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.AbnType = r["AbnType"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        /// <summary>Data chart: top 10 user yang paling banyak melakukan Fixed by Self.</summary>
        [HttpPost] public IActionResult GET_FIXED_BY_SELF(string facility, string line, string station, string date_from, string date_to, string range)
            => ExecuteDashboardStoredProc("GET_FIXED_BY_SELF", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.FindingName = r["FindingName"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        /// <summary>Data chart: persentase OPL closed per site/facility (Doughnut chart).</summary>
        [HttpPost] public IActionResult GET_OPLS_PER_SITE(string facility, string line, string station, string date_from, string date_to, string range)
            => ExecuteDashboardStoredProc("GET_OPLS_PER_SITE", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.Facility = r["Facility"].ToString(); d.Closed = r["Closed"].ToString(); list.Add(d); });

        // ===================================================================
        // DETAIL DATA ENDPOINTS
        // Dipanggil saat user klik bar/slice pada chart.
        // Mengembalikan Partial View tabel detail berdasarkan nilai yang diklik.
        // ===================================================================

        /// <summary>Detail data untuk chart Findings vs Closed. Filter tambahan: value (periode) dan type (Findings/Closed).</summary>
        [HttpPost] public IActionResult GET_DETAIL_FINDING_CLOSED(string facility, string line, string station, string date_from, string date_to, string range, string value, string type)
            => ExecuteDashboardStoredProc("GET_DETAIL_FINDING_CLOSED", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value, type);

        /// <summary>Detail data untuk chart Total OPLs. Filter tambahan: value (periode).</summary>
        [HttpPost] public IActionResult GET_DETAIL_OPLS(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => ExecuteDashboardStoredProc("GET_DETAIL_OPLS", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail data untuk chart OPLs per Site. Filter tambahan: value (nama facility).</summary>
        [HttpPost] public IActionResult GET_DETAIL_OPLS_PER_SITE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => ExecuteDashboardStoredProc("GET_DETAIL_OPLS_PER_SITE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail data untuk chart Sharp Eye. Filter tambahan: value (nama user).</summary>
        [HttpPost] public IActionResult GET_DETAIL_SHARP_EYE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => ExecuteDashboardStoredProc("GET_DETAIL_SHARP_EYE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail data untuk chart TPM Tag. Filter tambahan: value (tag id).</summary>
        [HttpPost] public IActionResult GET_DETAIL_TPM_TAG(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => ExecuteDashboardStoredProc("GET_DETAIL_TPM_TAG", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail data untuk chart Root Cause. Filter tambahan: value (nama root cause).</summary>
        [HttpPost] public IActionResult GET_DETAIL_ROOTCAUSE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => ExecuteDashboardStoredProc("GET_DETAIL_ROOTCAUSE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail data untuk chart What Will Happen. Filter tambahan: value (nama dampak).</summary>
        [HttpPost] public IActionResult GET_DETAIL_HAPPEN(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => ExecuteDashboardStoredProc("GET_DETAIL_HAPPEN", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail data untuk chart Seven Types of Abnormality. Filter tambahan: value (tipe abnormality).</summary>
        [HttpPost] public IActionResult GET_DETAIL_ABN_TYPE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => ExecuteDashboardStoredProc("GET_DETAIL_ABN_TYPE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        /// <summary>Detail data untuk chart Fixed by Self. Filter tambahan: value (nama user).</summary>
        [HttpPost] public IActionResult GET_DETAIL_FIXED_BY_SELF(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => ExecuteDashboardStoredProc("GET_DETAIL_FIXED_BY_SELF", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

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
    }
}