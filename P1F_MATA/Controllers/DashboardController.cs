using Microsoft.AspNetCore.Mvc;
using P1F_MATA.Function;
using Microsoft.Data.SqlClient;
using System.Dynamic;
using System.Security.Claims;

namespace P1F_MATA.Controllers
{
    public class DashboardController : Controller
    {
        private readonly DatabaseAccessLayer _db;

        public DashboardController(DatabaseAccessLayer db)
        {
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        /// <summary>Tampilkan Dashboard. Redirect ke login jika belum login.</summary>
        public IActionResult Index()
        {
            if (User.FindFirst(ClaimTypes.NameIdentifier)?.Value == null)
                return RedirectToAction("Index", "Login");

            ViewBag.Facilities = _db.GetFacility();
            ViewBag.Lines      = _db.GetLineDashboard();
            ViewBag.Stations   = _db.GetStationDashboard();

            return View();
        }

        // ===================================================================
        // DROPDOWN DINAMIS
        // ===================================================================

        /// <summary>API: Ambil daftar station berdasarkan line (cascade dropdown).</summary>
        [HttpGet]
        public JsonResult GetStationsByLine(string line_no)
        {
            try
            {
                return Json(_db.GetStationsByLine(line_no));
            }
            catch
            {
                Response.StatusCode = 500;
                return Json(new { error = "An error occurred while fetching stations." });
            }
        }

        // ===================================================================
        // DATE SO
        // ===================================================================

        /// <summary>Ambil range tanggal default untuk filter dashboard.</summary>
        [HttpGet]
        public JsonResult GetDateSO() => Json(_db.GetDateSO());

        // ===================================================================
        // PRIVATE HELPER: CHART + DETAIL EXECUTOR
        // ===================================================================

        /// <summary>
        /// Helper yang memanggil DAL untuk eksekusi SP dashboard,
        /// lalu menentukan apakah hasilnya dikembalikan sebagai Json atau PartialView.
        /// </summary>
        private IActionResult RunDashboardSP(
            string spName,
            string facility, string line, string station,
            string date_from, string date_to, string range,
            Action<List<dynamic>, SqlDataReader> mapRow,
            string partialView = null,
            string value = null, string type = null)
        {
            // Catatan: mapRow menggunakan Microsoft.Data.SqlClient.SqlDataReader
            // sesuaikan import jika menggunakan namespace berbeda
            var dataList = _db.ExecuteDashboardSP(spName, facility, line, station, date_from, date_to, range, mapRow, value, type);
            return partialView != null ? PartialView(partialView, dataList) : Json(dataList);
        }

        /// <summary>Mapper untuk endpoint detail (klik chart → tabel detail).</summary>
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
        // ===================================================================

        [HttpPost] public IActionResult GET_FINDING_CLOSED(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_FINDING_CLOSED", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.Period_Group = r["Period_Group"].ToString(); d.Findings = r["Findings"].ToString(); d.Closed = r["Closed"].ToString(); list.Add(d); });

        [HttpPost] public IActionResult GET_TOTAL_OPLs(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_OPLS", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.Period_Group = r["Period_Group"].ToString(); d.Accumulative = r["Accumulative"].ToString(); d.Closed = r["Closed"].ToString(); list.Add(d); });

        [HttpPost] public IActionResult GET_SHARP_EYE(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_SHARP_EYE", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.FindingName = r["FindingName"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        [HttpPost] public IActionResult GET_TPM_TAG(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_TPM_TAG", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.TagId = r["TagId"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        [HttpPost] public IActionResult GET_ROOTCAUSE(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_ROOTCAUSE", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.AbnRootCause = r["AbnRootCause"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        [HttpPost] public IActionResult GET_HAPPEN(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_HAPPEN", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.AbnHappen = r["AbnHappen"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        [HttpPost] public IActionResult GET_ABN_TYPE(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_ABN_TYPE", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.AbnType = r["AbnType"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        [HttpPost] public IActionResult GET_FIXED_BY_SELF(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_FIXED_BY_SELF", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.FindingName = r["FindingName"].ToString(); d.BarChart = r["BarChart"].ToString(); list.Add(d); });

        [HttpPost] public IActionResult GET_OPLS_PER_SITE(string facility, string line, string station, string date_from, string date_to, string range)
            => RunDashboardSP("GET_OPLS_PER_SITE", facility, line, station, date_from, date_to, range,
                (list, r) => { dynamic d = new ExpandoObject(); d.Facility = r["Facility"].ToString(); d.Closed = r["Closed"].ToString(); list.Add(d); });

        // ===================================================================
        // DETAIL DATA ENDPOINTS
        // ===================================================================

        [HttpPost] public IActionResult GET_DETAIL_FINDING_CLOSED(string facility, string line, string station, string date_from, string date_to, string range, string value, string type)
            => RunDashboardSP("GET_DETAIL_FINDING_CLOSED", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value, type);

        [HttpPost] public IActionResult GET_DETAIL_OPLS(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_OPLS", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        [HttpPost] public IActionResult GET_DETAIL_OPLS_PER_SITE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_OPLS_PER_SITE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        [HttpPost] public IActionResult GET_DETAIL_SHARP_EYE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_SHARP_EYE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        [HttpPost] public IActionResult GET_DETAIL_TPM_TAG(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_TPM_TAG", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        [HttpPost] public IActionResult GET_DETAIL_ROOTCAUSE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_ROOTCAUSE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        [HttpPost] public IActionResult GET_DETAIL_HAPPEN(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_HAPPEN", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        [HttpPost] public IActionResult GET_DETAIL_ABN_TYPE(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_ABN_TYPE", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);

        [HttpPost] public IActionResult GET_DETAIL_FIXED_BY_SELF(string facility, string line, string station, string date_from, string date_to, string range, string value)
            => RunDashboardSP("GET_DETAIL_FIXED_BY_SELF", facility, line, station, date_from, date_to, range, MapDetailRow, "_TableDetail", value);
    }
}