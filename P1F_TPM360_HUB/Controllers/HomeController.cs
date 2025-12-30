using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;
using System.Data.SqlClient;

namespace P1F_TPM360_HUB.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IConfiguration _configuration;
        private string DbConnection()
        {
            var dbAccess = new DatabaseAccessLayer();
            return dbAccess.ConnectionString;
        }
        public HomeController(ILogger<HomeController> logger, IConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }
        public async Task<IActionResult> Index()
        {
            //string name = User.FindFirst("p1f_headcount_name")?.Value;
            //string level = User.FindFirst("p1f_headcount_level")?.Value;
            //if (name == null || level == null)
            //{
            //    return RedirectToAction("Index", "Auth");
            //}
            //else if (level == "no_access")
            //{
            //    var claimsIdentity = (ClaimsIdentity)User.Identity;

            //    var existLevel = claimsIdentity?.FindFirst("p1f_headcount_level");
            //    if (existLevel != null)
            //    {
            //        claimsIdentity.RemoveClaim(existLevel);
            //    }
            //    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));
            //}
            return View();
        }

        [Authorize]
        public async Task<IActionResult> Login()
        {
            string name = User.FindFirst("P1F_TPM360_HUB_name")?.Value;
            string level = User.FindFirst("P1F_TPM360_HUB_level")?.Value;

            if (name == null || level == null)
            {
                return RedirectToAction("Index", "Auth");
            }
            else if (level == "no_access")
            {
                var claimsIdentity = (ClaimsIdentity)User.Identity;
                var existLevel = claimsIdentity?.FindFirst("P1F_TPM360_HUB_level");
                if (existLevel != null)
                {
                    claimsIdentity.RemoveClaim(existLevel);
                }

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                TempData["MessageSSO"] = "You don't have access, <br> Please contact admin for support.";
                return RedirectToAction("Index", "Home");
            }

            return RedirectToAction("Open", "Home");
        }

        [AllowAnonymous] // Wajib: Agar bisa diakses tanpa login
        public IActionResult TPM(string order_id)
        {
            // 1. Siapkan opsi cookie (bisa kedaluwarsa dalam 5 menit, cukup untuk waktu login)
            var options = new CookieOptions { Expires = DateTime.Now.AddMinutes(5) };

            // 2. Simpan "Titipan" ke dalam Cookie Browser User
            if (!string.IsNullOrEmpty(order_id))
            {
                Response.Cookies.Append("Pending_OrderId", order_id, options);
            }

            // 3. Tendang user ke gerbang Login
            return RedirectToAction("Login", "Home");
        }

        public IActionResult Open()
        {
            //Cek apakah ada titipan di saku user?
            string pendingOrder = Request.Cookies["Pending_OrderId"];

            // Jika ada tipe (request/approve), langsung antar ke tujuan!
            if (!string.IsNullOrEmpty(pendingOrder))
            {
                // PENTING: Hapus cookie supaya tidak redirect terus-menerus (looping)
                Response.Cookies.Delete("Pending_OrderId");

                // Redirect ke Operational/Discipline membawa data titipan tadi
                return RedirectToAction("Observation", "MAT", new { order_id = pendingOrder });
            }

            string user_level = User.FindFirst("P1F_TPM360_HUB_level")?.Value;
            string[] levels = user_level.Split(';');

            if (levels.Contains("mat_admin") || levels.Contains("mat") || levels.Contains("superadmin"))
            {
                return RedirectToAction("Dash", "Admin");
            }
            else if (levels.Contains("cm_admin") || levels.Contains("cm_user"))
            {
                return RedirectToAction("Dash", "Admin");
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        // ================== MANUAL LOGIN ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LoginManual(LoginModel user)
        {
            if (!ModelState.IsValid) return View("Index");

            var hashpassword = new Authentication();
            using (SqlConnection conn = new SqlConnection(DbConnection()))
            {
                string passwordHash = hashpassword.MD5Hash(user.password);
                string query = "SELECT * FROM mst_users WHERE sesa_id = @sesa_id AND password = @password";
                SqlCommand cmd = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@sesa_id", user.sesa_id);
                cmd.Parameters.AddWithValue("@password", passwordHash);

                await conn.OpenAsync();
                SqlDataReader reader = await cmd.ExecuteReaderAsync();

                if (!reader.HasRows)
                {
                    ViewData["Message"] = "User and Password not Registered!";
                    return View("Index");
                }
                reader.Close();

                var db = new DatabaseAccessLayer();
                var user_db = db.GetUserDetail(user.sesa_id).FirstOrDefault();

                if (user_db == null)
                {
                    ViewData["Message"] = "User details not found!";
                    return View("Index");
                }

                var claimsIdentity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);

                // Claims untuk P1F_TPM360_HUB
                claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, user_db.sesa_id));
                claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_name", user_db.name));
                claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_level", string.IsNullOrEmpty(user_db.level) ? "no_access" : user_db.level.ToLower()));
                claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_role", user_db.role ?? ""));
                claimsIdentity.AddClaim(new Claim("P1F_TPM360_HUB_lines", user_db.lines ?? ""));

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity));

                return Json(new { success = true, redirectUrl = Url.Action("Open", "Home") });
            }
        }

        //[HttpPost]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;

            // Hapus claim nama
            var existName = claimsIdentity?.FindFirst("P1F_TPM360_HUB_name");
            if (existName != null)
            {
                claimsIdentity.RemoveClaim(existName);
            }

            // Hapus claim level
            var existLevel = claimsIdentity?.FindFirst("P1F_TPM360_HUB_level");
            if (existLevel != null)
            {
                claimsIdentity.RemoveClaim(existLevel);
            }


            // Hapus claim tambahan kalau ada (apps, plant, login source)

            var existPlant = claimsIdentity?.FindFirst("P1F_TPM360_HUB_plant");
            if (existPlant != null)
            {
                claimsIdentity.RemoveClaim(existPlant);
            }
            var existRole = claimsIdentity?.FindFirst("P1F_TPM360_HUB_role");
            if (existRole != null)
            {
                claimsIdentity.RemoveClaim(existRole);
            }

            // Refresh claims
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity)
            );

            // Clear session & cookies
            HttpContext.Session.Clear();
            foreach (var cookie in Request.Cookies.Keys)
            {
                Response.Cookies.Delete(cookie);
            }

            // SignOut
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return RedirectToAction("Index", "Home");
        }

    }
}