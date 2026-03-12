using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_MATA.Function;
using P1F_MATA.Models;

namespace P1F_MATA.Controllers
{
    /// <summary>
    /// Controller utama aplikasi P1F-MATA.
    /// Menangani: halaman home, proses login/logout, 
    /// akses dari luar (QR / link email), dan routing setelah login.
    /// </summary>
    public class HomeController : Controller
    {
        // Dependency Injection: DAL digunakan untuk validasi login ke database
        private readonly DatabaseAccessLayer _db;

        /// <summary>
        /// Constructor: menerima DatabaseAccessLayer via Dependency Injection.
        /// </summary>
        public HomeController(DatabaseAccessLayer db)
        {
            _db = db;
        }

        // ===================================================================
        // HALAMAN HOME
        // ===================================================================

        /// <summary>
        /// Menampilkan halaman utama (landing page) aplikasi.
        /// Halaman ini bisa diakses tanpa login (form login ditampilkan di sini).
        /// </summary>
        public IActionResult Index() => View();

        // ===================================================================
        // QR / LINK AKSES DARI LUAR
        // ===================================================================

        /// <summary>
        /// Endpoint yang dipanggil saat user scan QR Code atau klik link dari email.
        /// Cara kerja:
        ///   1. Simpan order_id ke cookie sementara (expired dalam 5 menit)
        ///   2. Redirect ke halaman Home (user diminta login terlebih dahulu)
        /// Setelah login berhasil, Open() akan membaca cookie ini dan 
        /// langsung mengarahkan user ke detail ABN yang dimaksud.
        /// </summary>
        /// <param name="order_id">ID ABN yang ingin dibuka dari luar aplikasi</param>
        [AllowAnonymous] // Bisa diakses tanpa login karena berasal dari QR/email
        public IActionResult TPM(string order_id)
        {
            if (!string.IsNullOrEmpty(order_id))
            {
                // Simpan order_id ke cookie dengan masa berlaku 5 menit
                // Cukup untuk menyelesaikan proses login sebelum cookie kedaluwarsa
                Response.Cookies.Append("Pending_OrderId", order_id,
                    new CookieOptions { Expires = DateTime.Now.AddMinutes(5) });
            }

            // Arahkan ke halaman Home (user akan login dari sini)
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Redirect engine yang dijalankan SETELAH login berhasil.
        /// Menentukan ke mana user diarahkan berdasarkan dua kondisi:
        ///
        /// Kondisi 1 — Ada Pending Order (dari QR/email):
        ///   → Hapus cookie, lalu redirect ke halaman Observation dengan order_id tersebut.
        ///
        /// Kondisi 2 — Login biasa:
        ///   → Cek level user. Jika mat / mat_admin / superadmin → ke Observation.
        ///   → Selain itu → kembali ke halaman Home (tidak punya akses).
        /// </summary>
        public IActionResult Open()
        {
            // ---------------------------------------------------------------
            // CEK KONDISI 1: Ada order_id yang disimpan sebelum login (dari QR/email)
            // ---------------------------------------------------------------
            string pendingOrderId = Request.Cookies["Pending_OrderId"];
            if (!string.IsNullOrEmpty(pendingOrderId))
            {
                // Hapus cookie agar tidak digunakan ulang di sesi berikutnya
                Response.Cookies.Delete("Pending_OrderId");

                // Langsung buka halaman Observation dengan order_id dari QR/email
                return RedirectToAction("Observation", "MAT", new { order_id = pendingOrderId });
            }

            // ---------------------------------------------------------------
            // CEK KONDISI 2: Login biasa — tentukan halaman berdasarkan level user
            // ---------------------------------------------------------------
            string userLevel = User.FindFirst("P1F_MATA_level")?.Value ?? "";

            // Level bisa berisi beberapa nilai dipisahkan titik koma, misal: "mat;mat_admin"
            string[] levels = userLevel.Split(';');

            // Hanya user dengan level MAT atau admin yang boleh masuk ke Observation
            if (levels.Contains("mat_admin") || levels.Contains("mat") || levels.Contains("superadmin"))
                return RedirectToAction("Observation", "MAT");

            // User tidak punya akses → kembali ke halaman Home
            return RedirectToAction("Index", "Home");
        }

        // ===================================================================
        // LOGIN
        // ===================================================================

        /// <summary>
        /// Memproses form login yang dikirim dari halaman Index.
        /// Alur:
        ///   1. Validasi model (field tidak boleh kosong, format valid)
        ///   2. Hash password dengan MD5, lalu cocokkan ke database
        ///   3. Jika valid → buat Claims Identity → Sign In → redirect via JSON
        ///   4. Jika tidak valid → tampilkan pesan error di View
        ///
        /// Return JSON (bukan redirect langsung) karena form dikirim via AJAX.
        /// JavaScript di frontend yang menangani redirect berdasarkan response JSON.
        /// </summary>
        /// <param name="user">Model berisi sesa_id dan password dari form login</param>
        [HttpPost]
        [ValidateAntiForgeryToken] // Mencegah serangan CSRF
        public async Task<IActionResult> Login(LoginModel user)
        {
            // Validasi model binding (misal: field required tidak boleh kosong)
            if (!ModelState.IsValid)
                return View("Index");

            // Hash password sebelum dikirim ke database (password tidak pernah plain text)
            string hashedPassword = new Authentication().MD5Hash(user.password);

            // Cek ke database apakah sesa_id + password cocok
            var userDb = await _db.ValidateLogin(user.sesa_id, hashedPassword);

            // Jika tidak cocok → tampilkan pesan error
            if (userDb == null)
            {
                ViewData["Message"] = "User and Password not Registered!";
                return View("Index");
            }

            // ---------------------------------------------------------------
            // BUAT CLAIMS IDENTITY
            // Claims ini disimpan dalam cookie terenkripsi dan dibaca di seluruh controller
            // menggunakan: User.FindFirst("nama_claim")?.Value
            // ---------------------------------------------------------------
            var claimsIdentity = new ClaimsIdentity(CookieAuthenticationDefaults.AuthenticationScheme);
            claimsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userDb.sesa_id));          // SESA ID (username unik)
            claimsIdentity.AddClaim(new Claim("P1F_MATA_name",  userDb.name));                       // Nama lengkap user
            claimsIdentity.AddClaim(new Claim("P1F_MATA_level", string.IsNullOrEmpty(userDb.level)   // Level akses (mat, admin, dll)
                ? "no_access" : userDb.level.ToLower()));                                             //   fallback ke "no_access" jika kosong
            claimsIdentity.AddClaim(new Claim("P1F_MATA_role",  userDb.role  ?? ""));                // Role tambahan user
            claimsIdentity.AddClaim(new Claim("P1F_MATA_lines", userDb.lines ?? ""));                // Line produksi yang bisa diakses user
            claimsIdentity.AddClaim(new Claim(ClaimTypes.Email, userDb.email ?? ""));                // Email user

            // Simpan Claims ke cookie terenkripsi → user dianggap sudah login
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            // Kembalikan JSON ke frontend, JavaScript yang akan melakukan redirect
            return Json(new { success = true, redirectUrl = Url.Action("Open", "Home") });
        }

        // ===================================================================
        // LOGOUT
        // ===================================================================

        /// <summary>
        /// Memproses permintaan logout user.
        /// Alur pembersihan sesi:
        ///   1. Hapus Claims custom dari identity (nama, level, plant, role)
        ///   2. Re-sign-in dengan identity yang sudah dibersihkan
        ///   3. Hapus semua data Session server-side
        ///   4. Hapus semua Cookie browser
        ///   5. Sign out resmi dari Cookie Authentication
        ///   6. Redirect ke halaman Home
        ///
        /// Langkah 1-2 dilakukan sebelum SignOut agar Claims benar-benar terhapus
        /// dari cookie sebelum sesi diakhiri.
        /// </summary>
        [Authorize] // Hanya user yang sudah login yang bisa logout
        public async Task<IActionResult> Logout()
        {
            // Ambil identity user yang sedang login
            var claimsIdentity = (ClaimsIdentity)User.Identity;

            // Hapus semua Claims custom P1F-MATA dari identity
            // (selain NameIdentifier dan Email yang dikelola oleh framework)
            foreach (var claimType in new[] { "P1F_MATA_name", "P1F_MATA_level", "P1F_MATA_plant", "P1F_MATA_role" })
            {
                var claim = claimsIdentity?.FindFirst(claimType);
                if (claim != null) claimsIdentity.RemoveClaim(claim);
            }

            // Re-sign-in dengan identity yang sudah dibersihkan
            // (memperbarui cookie sebelum benar-benar sign out)
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity));

            // Hapus semua data Session server-side
            HttpContext.Session.Clear();

            // Hapus semua Cookie yang ada di browser
            foreach (var key in Request.Cookies.Keys)
                Response.Cookies.Delete(key);

            // Sign out resmi → menghapus cookie autentikasi dari browser
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Kembali ke halaman Home (tampilan login)
            return RedirectToAction("Index", "Home");
        }
    }
}