using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;
using Microsoft.Data.SqlClient;

namespace P1F_TPM360_HUB.Controllers
{
    public class HomeController : Controller
    {
        // ===================================================================
        // DEPENDENCY INJECTION
        // ===================================================================
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private readonly DatabaseAccessLayer _db;

        public HomeController(
            ILogger<HomeController> logger,
            IConfiguration configuration,
            DatabaseAccessLayer db)
        {
            _logger = logger;
            _configuration = configuration;
            _db = db;
        }

        // ===================================================================
        // HALAMAN HOME
        // ===================================================================

        public async Task<IActionResult> Index()
        {
            return View();
        }

        // ===================================================================
        // PROSES LOGIN
        // ===================================================================

        /// <summary>
        /// Dipanggil setelah SSO berhasil.
        /// Memeriksa level akses user, lalu mengarahkan ke halaman yang sesuai.
        /// </summary>
        [Authorize]
        public async Task<IActionResult> Login()
        {
            string name  = User.FindFirst("P1F_TPM360_HUB_name")?.Value;
            string level = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            // Jika claim belum ada, arahkan ke proses autentikasi SSO dulu
            if (name == null || level == null)
                return RedirectToAction("Index", "Auth");

            // Jika user tidak punya akses
            if (level == "no_access")
            {
                // Hapus claim level agar tidak tersimpan sebagai 'no_access'
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var existingLevel  = claimsIdentity?.FindFirst("P1F_TPM360_HUB_level");
                if (existingLevel != null)
                    claimsIdentity.RemoveClaim(existingLevel);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                TempData["MessageSSO"] = "You don't have access, <br> Please contact admin for support.";
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Open", "Home");
        }

        /// <summary>
        /// Endpoint untuk akses dari luar (scan QR, link email, dll).
        /// Menyimpan order_id ke cookie sementara, lalu arahkan ke login.
        /// Cookie berlaku selama 5 menit.
        /// </summary>
        [AllowAnonymous]
        public IActionResult TPM(string order_id)
        {
            // Simpan order_id sementara (berlaku 5 menit)
            var cookieOptions = new CookieOptions { Expires = DateTime.Now.AddMinutes(5) };
            if (!string.IsNullOrEmpty(order_id))
                Response.Cookies.Append("Pending_OrderId", order_id, cookieOptions);

            return RedirectToAction("Login", "Home");
        }

        /// <summary>
        /// Redirect engine setelah login berhasil.
        /// Jika ada titipan order_id di cookie → langsung ke Observation.
        /// Jika tidak → redirect ke dashboard sesuai level user.
        /// </summary>
        public IActionResult Open()
        {
            // Cek apakah ada order_id yang "dititipkan" sebelum login
            string pendingOrderId = Request.Cookies["Pending_OrderId"];
            if (!string.IsNullOrEmpty(pendingOrderId))
            {
                // Hapus cookie agar tidak redirect berulang (looping)
                Response.Cookies.Delete("Pending_OrderId");

                // Arahkan langsung ke detail observasi
                return RedirectToAction("Observation", "MAT", new { order_id = pendingOrderId });
            }

            // Tidak ada titipan → redirect sesuai level akses
            string userLevel = User.FindFirst("P1F_TPM360_HUB_level")?.Value;
            string[] levels  = userLevel.Split(';');

            if (levels.Contains("mat_admin") || levels.Contains("mat") || levels.Contains("superadmin"))
                return RedirectToAction("Dash", "Admin");

            if (levels.Contains("cm_admin") || levels.Contains("cm_user"))
                return RedirectToAction("Dash", "Admin");

            return RedirectToAction("Index", "Home");
        }

        // ===================================================================
        // MANUAL LOGIN (alternatif tanpa SSO)
        // ===================================================================

        /// <summary>
        /// Login manual menggunakan sesa_id dan password.
        /// Password akan di-hash MD5 sebelum dicocokkan dengan database.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginManual(LoginModel user)
        {
            if (!ModelState.IsValid)
                return View("Index");

            var auth = new Authentication();

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                string hashedPassword = auth.MD5Hash(user.password);
                string query = "SELECT * FROM mst_users WHERE sesa_id = @sesa_id AND password = @password";

                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@sesa_id",  user.sesa_id);
                cmd.Parameters.AddWithValue("@password", hashedPassword);

                await conn.OpenAsync();
                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                // Jika user tidak ditemukan
                if (!reader.HasRows)
                {
                    ViewData["Message"] = "User and Password not Registered!";
                    return View("Index");
                }

                reader.Close();

                // Ambil detail user dari database
                var userDb = _db.GetUserDetail(user.sesa_id).FirstOrDefault();
                if (userDb == null)
                {
                    ViewData["Message"] = "User details not found!";
                    return View("Index");
                }

                // Buat session (claims) untuk user yang berhasil login
                var claimsIdentity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
                claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userDb.sesa_id));
                claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_name",  userDb.name));
                claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_level", string.IsNullOrEmpty(userDb.level) ? "no_access" : userDb.level.ToLower()));
                claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_role",  userDb.role  ?? ""));
                claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_lines", userDb.lines ?? ""));

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                return Json(new { success = true, redirectUrl = Url.Action("Open", "Home") });
            }
        }

        // ===================================================================
        // LOGOUT
        // ===================================================================

        /// <summary>
        /// Logout user: menghapus semua claims, session, cookies, lalu sign out.
        /// </summary>
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;

            // Daftar claim yang perlu dihapus sebelum sign out
            string[] claimTypesToRemove = new[]
            {
                "P1F_TPM360_HUB_name",
                "P1F_TPM360_HUB_level",
                "P1F_TPM360_HUB_plant",
                "P1F_TPM360_HUB_role"
            };

            foreach (var claimType in claimTypesToRemove)
            {
                var claim = claimsIdentity?.FindFirst(claimType);
                if (claim != null)
                    claimsIdentity.RemoveClaim(claim);
            }

            // Refresh cookie dengan claims yang sudah dibersihkan
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            // Bersihkan session dan semua cookies
            HttpContext.Session.Clear();
            foreach (var cookieKey in Request.Cookies.Keys)
                Response.Cookies.Delete(cookieKey);

            // Sign out sepenuhnya
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }
    }
}