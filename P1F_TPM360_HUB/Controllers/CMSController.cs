using iText.Barcodes;
using iText.Kernel.Colors;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using OfficeOpenXml;
using Org.BouncyCastle.Asn1.Cmp;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;
using P1F_TPM360_HUB.Service;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Linq.Dynamic.Core;
using System.Security.Claims;

namespace P1F_TPM360_HUB.Controllers
{
    public class CMSController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DatabaseAccessLayer _db;
        private readonly ImportExportFactory _importexportFactory;
        private readonly ILogger<AdminController> _logger;


        public CMSController(ApplicationDbContext context, ImportExportFactory importexportFactory, ILogger<AdminController> logger)
        {
            _context = context;
            _db = new DatabaseAccessLayer();
            _importexportFactory = importexportFactory ?? throw new ArgumentNullException(nameof(importexportFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IActionResult Index()
        {
            var userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;
            var userSessionLines = User.FindFirst("P1F_TPM360_HUB_lines")?.Value;

            if (string.IsNullOrEmpty(userLevel))
            {
                return RedirectToAction("Index", "Login");
            }
            else if (userLevel.Contains("cm_admin") || userLevel.Contains("cm_user") || userLevel.Contains("superadmin"))
            {
                ViewBag.GetLines = _db.GetLines(userSessionLines);

                return View();
            }
            else
            {
                return RedirectToAction("Index", "Login");
            }
        }
        public IActionResult Borrow()
        {
            string userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            if (string.IsNullOrEmpty(userLevel))
            {
                return RedirectToAction("Index", "Login");
            }
            else if (userLevel.Contains("cm_admin") || userLevel.Contains("cm_user") || userLevel.Contains("superadmin"))
            {
                return View();
            }
            else
            {
                return RedirectToAction("Index", "Login");
            }
        }
        public IActionResult Return()
        {
            string userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            if (string.IsNullOrEmpty(userLevel))
            {
                return RedirectToAction("Index", "Login");
            }
            else if (userLevel.Contains("cm_admin") || userLevel.Contains("cm_user") || userLevel.Contains("superadmin"))
            {
                return View();
            }
            else
            {
                return RedirectToAction("Index", "Login");
            }
        }
        public IActionResult CableLoc()
        {
            string userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;
            string userSessionLines = User.FindFirst("P1F_TPM360_HUB_lines")?.Value;

            if (string.IsNullOrEmpty(userLevel))
            {
                return RedirectToAction("Index", "Login");
            }
            else if (userLevel.Contains("cm_admin") || userLevel.Contains("cm_user") || userLevel.Contains("superadmin"))
            {
                ViewBag.GetLines = _db.GetLines(userSessionLines);

                return View();
            }
            else
            {
                return RedirectToAction("Index", "Login");
            }
        }

        [HttpGet]
        public IActionResult Locations(string line)
        {
            var data = _db.GetLocations(line);
            return Json(data);
        }

        [HttpGet]
        public IActionResult GetDrawerData(string line)
        {
            string userSessionLines = User.FindFirst("P1F_TPM360_HUB_lines")?.Value;

            if (line == "ALL")
            {
                line = userSessionLines;
            }
            var configs = new List<DrawerMapping>();
            var drawerDataDict = new Dictionary<string, DrawerDetail>(StringComparer.OrdinalIgnoreCase);

            using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
            {
                conn.Open();

                // --- STEP 1: AMBIL KONFIGURASI MAPPING ---
                // Modifikasi: pisahkan string querynya
                string queryConfig = @"SELECT line, col_num, row_num, col_text, row_text 
                               FROM mst_drawer_mapping ";

                // Cek jika parameter line ada isinya
                if (!string.IsNullOrEmpty(line))
                {
                    queryConfig += " WHERE line IN (SELECT TRIM(value) FROM STRING_SPLIT(@line, ';')) ";
                }

                queryConfig += " ORDER BY line ASC";

                using (SqlCommand cmd = new SqlCommand(queryConfig, conn))
                {
                    // Tambahkan parameter untuk mencegah SQL Injection
                    if (!string.IsNullOrEmpty(line))
                    {
                        cmd.Parameters.AddWithValue("@line", line);
                    }

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            configs.Add(new DrawerMapping
                            {
                                line = reader["line"]?.ToString() ?? "",
                                col_num = reader["col_num"] != DBNull.Value ? Convert.ToInt32(reader["col_num"]) : 0,
                                row_num = reader["row_num"] != DBNull.Value ? Convert.ToInt32(reader["row_num"]) : 0,
                                col_text = reader["col_text"]?.ToString() ?? "NUMBER",
                                row_text = reader["row_text"]?.ToString() ?? "ALPHABET"
                            });
                        }
                    }
                }

                // --- STEP 2: AMBIL DATA ACTUAL DARI MST_DRAWER ---
                // Modifikasi: Tambahkan WHERE clause juga di sini agar data yang diambil tidak terlalu besar
                string queryData = @"SELECT line, location, act_qty, max_qty, available_qty 
                             FROM mst_drawer ";

                if (!string.IsNullOrEmpty(line))
                {
                    queryData += " WHERE line IN (SELECT TRIM(value) FROM STRING_SPLIT(@line, ';')) ";
                }

                using (SqlCommand cmd = new SqlCommand(queryData, conn))
                {
                    if (!string.IsNullOrEmpty(line))
                    {
                        cmd.Parameters.AddWithValue("@line", line);
                    }

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            string lineDB = reader["line"]?.ToString() ?? "";
                            string locDB = reader["location"]?.ToString() ?? "";

                            // Unique Key
                            string uniqueKey = $"{lineDB}_{locDB}";

                            if (!string.IsNullOrEmpty(locDB) && !drawerDataDict.ContainsKey(uniqueKey))
                            {
                                drawerDataDict.Add(uniqueKey, new DrawerDetail
                                {
                                    Location = locDB,
                                    ActQty = reader["act_qty"] != DBNull.Value ? Convert.ToInt32(reader["act_qty"]) : 0,
                                    MaxQty = reader["max_qty"] != DBNull.Value ? Convert.ToInt32(reader["max_qty"]) : 0,
                                    AvailableQty = reader["available_qty"] != DBNull.Value ? Convert.ToInt32(reader["available_qty"]) : 0
                                });
                            }
                        }
                    }
                }
            }

            // --- STEP 3: MAPPING DAN LOGIC TAMPILAN (Tidak Berubah) ---
            // Loop ini otomatis hanya akan memproses 1 Line saja karena variable 'configs' di Step 1 sudah difilter.
            var result = new List<LineDataViewModel>();

            foreach (var config in configs)
            {
                var gridItems = new List<GridItemViewModel>();

                for (int r = 0; r < config.row_num; r++)
                {
                    string rowLabel = GetLabel(r, config.row_text);

                    for (int c = 0; c < config.col_num; c++)
                    {
                        string colLabel = GetLabel(c, config.col_text);
                        string finalLabel;

                        if (config.col_text == "ALPHABET")
                            finalLabel = $"{colLabel}{rowLabel}";
                        else
                            finalLabel = $"{rowLabel}{colLabel}";

                        // Key Pencarian
                        string searchKey = $"{config.line}_{finalLabel}";

                        string subLabelText = "-";
                        bool isAlertActive = false;

                        if (drawerDataDict.ContainsKey(searchKey))
                        {
                            var data = drawerDataDict[searchKey];
                            subLabelText = $"{data.ActQty}/{data.MaxQty}";
                            isAlertActive = (data.AvailableQty == 0);
                        }

                        gridItems.Add(new GridItemViewModel
                        {
                            Label = finalLabel,
                            SubLabel = subLabelText,
                            IsAlert = isAlertActive
                        });
                    }
                }

                result.Add(new LineDataViewModel
                {
                    LineName = config.line,
                    ColNum = config.col_num,
                    Items = gridItems
                });
            }

            return Json(result);
        }

        [HttpGet]
        public IActionResult SearchCableLocation(string qrCode)
        {
            if (string.IsNullOrWhiteSpace(qrCode))
            {
                return Json(new { success = false, message = "Empty input" });
            }

            // Panggil method DAL yang baru dibuat
            var locationData = _db.GetLocationByQr(qrCode);

            if (locationData != null)
            {
                return Json(new
                {
                    success = true,
                    line = locationData.Line,
                    location = locationData.Location
                });
            }
            else
            {
                return Json(new { success = false, message = "Cable not found in database." });
            }
        }

        [HttpGet]
        public IActionResult GetCableData(string line, string location)
        {
            // 1. Ambil Kerangka Laci (Max Qty per Location)
            var drawers = _db.GetDrawerCapacities(line, location);

            // 2. Ambil Data Kabel yang ada (Isi Laci)
            var cables = _db.GetCables(line, location);

            var result = new List<object>();

            // 3. Gabungkan Data (Logic Card Generator)
            foreach (var drawer in drawers)
            {
                // Ambil kabel-kabel yang milik lokasi ini saja
                var cablesInThisLocation = cables
                    .Where(c => c.LocationGroup == drawer.Location)
                    .ToList();

                var gridItems = new List<object>();

                // Loop sebanyak MAX_QTY yang diset di mst_drawer
                for (int i = 0; i < drawer.MaxQty; i++)
                {
                    // Cek apakah ada kabel di slot index ke-i
                    if (i < cablesInThisLocation.Count)
                    {
                        // JIKA ADA KABEL -> Tampilkan Datanya
                        var cable = cablesInThisLocation[i];
                        gridItems.Add(new
                        {
                            cableId = cable.CableId,
                            cablePart = cable.CablePart,
                            cableDescription = cable.CableDescription,
                            UnitModel = cable.UnitModel,
                            status = cable.Status // IN atau OUT
                        });
                    }
                    else
                    {
                        // JIKA TIDAK ADA KABEL -> Tampilkan 'Unavailable' (Sesuai request)
                        gridItems.Add(new
                        {
                            cableId = "Empty",
                            cablePart = "-",
                            cableDescription = "",
                            UnitModel = "",
                            status = "UNAVAILABLE" // Status khusus untuk legend abu-abu
                        });
                    }
                }

                // Masukkan ke hasil akhir
                result.Add(new
                {
                    name = drawer.Location, // Header (misal: A1)
                    items = gridItems
                });
            }

            return Json(result);
        }

        //----- BORROW -----//
        // --- STEP 1: VALIDASI LOKASI ---
        [HttpGet]
        public IActionResult CheckLocation(string qr_code)
        {
            string locationName = "";
            string lineName = "";
            bool exists = false;

            using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
            {
                conn.Open();
                // Cari data di mst_drawer berdasarkan parameter qr_code
                string query = "SELECT location, line FROM mst_drawer WHERE qr_code = @qr_code";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@qr_code", qr_code);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            exists = true;
                            locationName = reader["location"].ToString();
                            lineName = reader["line"].ToString();
                        }
                    }
                }
            }

            if (exists)
            {
                // Kembalikan data sukses beserta detail lokasi untuk ditampilkan di UI
                return Json(new { success = true, location = locationName, line = lineName });
            }
            else
            {
                return Json(new { success = false, message = "Location/Drawer not found!" });
            }
        }

        // --- STEP 2: SCAN CABLE, VALIDASI, & UPDATE ---
        [HttpPost]
        public IActionResult ProcessBorrow(string cable_qr, string drawer_qr)
        {
            string sesa_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            string cableLine = "", cableLoc = "", cableStatus = "";
            string dbCableId = ""; // Variabel untuk menampung Cable ID dari Database
            string drawerLine = "", drawerLoc = "";
            bool cableFound = false;
            bool drawerFound = false;

            using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
            {
                conn.Open();

                // 1. AMBIL DATA DRAWER
                string queryDrawer = "SELECT line, location FROM mst_drawer WHERE qr_code = @d_qr";
                using (SqlCommand cmd = new SqlCommand(queryDrawer, conn))
                {
                    cmd.Parameters.AddWithValue("@d_qr", drawer_qr);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            drawerFound = true;
                            drawerLine = reader["line"]?.ToString();
                            drawerLoc = reader["location"]?.ToString();
                        }
                    }
                }

                if (!drawerFound) return Json(new { success = false, message = "Original Location Invalid." });

                // 2. AMBIL DATA CABLE (Tambahkan cable_id di SELECT)
                string queryCable = "SELECT cable_id, line, location, status FROM mst_cable WHERE qr_code = @c_qr";

                using (SqlCommand cmd = new SqlCommand(queryCable, conn))
                {
                    cmd.Parameters.AddWithValue("@c_qr", cable_qr);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            cableFound = true;
                            // Simpan data ID asli dari database
                            dbCableId = reader["cable_id"]?.ToString();

                            cableLine = reader["line"]?.ToString();
                            cableLoc = reader["location"]?.ToString();
                            cableStatus = reader["status"]?.ToString();
                        }
                    }
                }

                if (!cableFound) return Json(new { success = false, message = "Cable QR not found in database." });

                // 3. VALIDASI MATCHING
                if (cableLine != drawerLine || cableLoc != drawerLoc)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"Mismatch! Cable belongs to {cableLine}-{cableLoc}, but you are at {drawerLine}-{drawerLoc}."
                    });
                }

                // 4. VALIDASI STATUS
                if (cableStatus != "IN")
                {
                    return Json(new { success = false, message = $"Cable cannot be borrowed. Current status is '{cableStatus}'." });
                }

                // 5. UPDATE STATUS
                string updateQuery = "BORROW_CABLE";
                using (SqlCommand cmd = new SqlCommand(updateQuery, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@c_qr", cable_qr);
                    cmd.Parameters.AddWithValue("@borrow_sesa", sesa_id);
                    int rowsAffected = cmd.ExecuteNonQuery();

                    if (rowsAffected > 0)
                    {
                        // KIRIM 'dbCableId' KE JSON RESPONSE
                        return Json(new
                        {
                            success = true,
                            message = "Success borrowing cable.",
                            realCableId = dbCableId // <--- Data ini yang akan di pakai di View
                        });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Failed to update database." });
                    }
                }
            }
        }

        //----- RETURN -----//
        // --- STEP 1: CEK KABEL (Harus Status OUT) ---
        [HttpGet]
        public IActionResult CheckCableForReturn(string qr_code)
        {
            string line = "", location = "", status = "";
            string cableId = ""; // Tambah variabel untuk menyimpan Cable ID
            bool found = false;

            using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
            {
                conn.Open();
                string query = "SELECT cable_id, line, location, status FROM mst_cable WHERE qr_code = @qr";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@qr", qr_code);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            found = true;
                            cableId = reader["cable_id"]?.ToString();
                            line = reader["line"]?.ToString();
                            location = reader["location"]?.ToString();
                            status = reader["status"]?.ToString();
                        }
                    }
                }
            }

            if (!found)
            {
                return Json(new { success = false, message = "Cable QR not found!" });
            }

            // Validasi: Kabel harus berstatus OUT untuk bisa dikembalikan
            if (status != "OUT")
            {
                return Json(new { success = false, message = $"Cable is currently '{status}'. Only 'OUT' cables can be returned." });
            }

            // Kirim data cableId, line & location untuk ditampilkan/disimpan sementara di UI
            return Json(new { success = true, line = line, location = location, cableId = cableId });
        }
        // --- STEP 2: CEK LOKASI & UPDATE STATUS JADI IN ---

        [HttpPost]
        public IActionResult ProcessReturn(string cable_qr, string location_qr)
        {
            string sesa_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            string cableLine = "", cableLoc = "";
            string drawerLine = "", drawerLoc = "";
            string cableIdFromDb = "";

            using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
            {
                conn.Open();

                // 1. AMBIL DATA CABLE
                string qCable = "SELECT cable_id, line, location FROM mst_cable WHERE qr_code = @c_qr";
                using (SqlCommand cmd = new SqlCommand(qCable, conn))
                {
                    cmd.Parameters.AddWithValue("@c_qr", cable_qr);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            cableIdFromDb = r["cable_id"].ToString(); // Ambil ID yang sebenarnya
                            cableLine = r["line"].ToString();
                            cableLoc = r["location"].ToString();
                        }
                        else return Json(new { success = false, message = "Cable data error." });
                    }
                }

                // 2. AMBIL DATA DRAWER (Berdasarkan QR Location yang discan user)
                string qDrawer = "SELECT line, location FROM mst_drawer WHERE qr_code = @d_qr";
                using (SqlCommand cmd = new SqlCommand(qDrawer, conn))
                {
                    cmd.Parameters.AddWithValue("@d_qr", location_qr);
                    using (SqlDataReader r = cmd.ExecuteReader())
                    {
                        if (r.Read())
                        {
                            drawerLine = r["line"].ToString();
                            drawerLoc = r["location"].ToString();
                        }
                        else return Json(new { success = false, message = "Location QR not found!" });
                    }
                }

                // 3. VALIDASI: APAKAH INI RUMAHNYA?
                if (cableLine != drawerLine || cableLoc != drawerLoc)
                {
                    return Json(new
                    {
                        success = false,
                        message = $"WRONG LOCATION! This cable belongs to {cableLine}-{cableLoc}, but you scanned {drawerLine}-{drawerLoc}."
                    });
                }

                // 4. UPDATE STATUS MENJADI 'IN'
                string update = "RETURN_CABLE";
                using (SqlCommand cmd = new SqlCommand(update, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@return_sesa", sesa_id);
                    cmd.Parameters.AddWithValue("@c_qr", cable_qr);
                    cmd.ExecuteNonQuery();
                }

                // Kirim data untuk Step 3
                return Json(new
                {
                    success = true,
                    message = "Return Successful",
                    summaryCableId = cableIdFromDb, // ID Kabel yang sebenarnya
                    summaryLocation = $"{drawerLine} - {drawerLoc}" // Lokasi yang discan
                });
            }
        }

        [HttpPost]
        public IActionResult ReturnCable(string cableId)
        {
            string sesa_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            try
            {
                bool result = _db.UpdateCableStatusToIn(cableId, sesa_id);

                if (result)
                {
                    return Json(new { success = true, message = "Status updated successfully." });
                }
                else
                {
                    return Json(new { success = false, message = "Cable ID not found or update failed." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
        // Helper Function (sama seperti di View sebelumnya)
        private string GetLabel(int index, string type)
        {
            return type?.ToUpper() == "ALPHABET"
                ? ((char)(65 + index)).ToString()
                : (index + 1).ToString();
        }

    }

}