using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_MATA.Function;
using P1F_MATA.Models;

namespace P1F_MATA.Controllers
{
    public class HomeController : Controller
    {
        private readonly DatabaseAccessLayer _db;

        public HomeController(DatabaseAccessLayer db)
        {
            _db = db;
        }

        // ===================================================================
        // HALAMAN HOME
        // ===================================================================

        public IActionResult Index() => View();

        // ===================================================================
        // QR / LINK AKSES DARI LUAR
        // ===================================================================

        /// <summary>
        /// Endpoint untuk akses dari luar (scan QR, link email, dll).
        /// Simpan order_id ke cookie 5 menit, lalu redirect ke login.
        /// </summary>
        [AllowAnonymous]
        public IActionResult TPM(string order_id)
        {
            if (!string.IsNullOrEmpty(order_id))
                Response.Cookies.Append("Pending_OrderId", order_id,
                    new CookieOptions { Expires = DateTime.Now.AddMinutes(5) });

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Redirect engine setelah login berhasil.
        /// Jika ada order_id di cookie → ke Observation. Jika tidak → ke MAT.
        /// </summary>
        public IActionResult Open()
        {
            string pendingOrderId = Request.Cookies["Pending_OrderId"];
            if (!string.IsNullOrEmpty(pendingOrderId))
            {
                Response.Cookies.Delete("Pending_OrderId");
                return RedirectToAction("Observation", "MAT", new { order_id = pendingOrderId });
            }

            string userLevel = User.FindFirst("P1F_MATA_level")?.Value ?? "";
            string[] levels  = userLevel.Split(';');

            if (levels.Contains("mat_admin") || levels.Contains("mat") || levels.Contains("superadmin"))
                return RedirectToAction("Observation", "MAT");

            return RedirectToAction("Index", "Home");
        }

        // ===================================================================
        // LOGIN
        // ===================================================================

        /// <summary>Login manual: validasi sesa_id + password, buat claims, redirect.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginModel user)
        {
            if (!ModelState.IsValid)
                return View("Index");

            string hashedPassword = new Authentication().MD5Hash(user.password);
            var userDb = await _db.ValidateLogin(user.sesa_id, hashedPassword);

            if (userDb == null)
            {
                ViewData["Message"] = "User and Password not Registered!";
                return View("Index");
            }

            var claimsIdentity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userDb.sesa_id));
            claimsIdentity.AddClaim(new Claim("P1F_MATA_name",  userDb.name));
            claimsIdentity.AddClaim(new Claim("P1F_MATA_level", string.IsNullOrEmpty(userDb.level) ? "no_access" : userDb.level.ToLower()));
            claimsIdentity.AddClaim(new Claim("P1F_MATA_role",  userDb.role  ?? ""));
            claimsIdentity.AddClaim(new Claim("P1F_MATA_lines", userDb.lines ?? ""));
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Email, userDb.email ?? ""));

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            return Json(new { success = true, redirectUrl = Url.Action("Open", "Home") });
        }

        // ===================================================================
        // LOGOUT
        // ===================================================================

        /// <summary>Logout: hapus claims, session, cookies, lalu sign out.</summary>
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var claimsIdentity = (ClaimsIdentity)User.Identity;

            foreach (var claimType in new[] { "P1F_MATA_name", "P1F_MATA_level", "P1F_MATA_plant", "P1F_MATA_role" })
            {
                var claim = claimsIdentity?.FindFirst(claimType);
                if (claim != null) claimsIdentity.RemoveClaim(claim);
            }

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            HttpContext.Session.Clear();
            foreach (var key in Request.Cookies.Keys)
                Response.Cookies.Delete(key);

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }
    }
}