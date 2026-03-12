using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using P1F_MATA.Function;
using P1F_MATA.Models.ViewModels;

namespace P1F_MATA.Controllers
{
    /// <summary>
    /// Controller untuk fitur Change Password.
    /// Memungkinkan user yang sudah login untuk mengubah password mereka sendiri.
    /// Data user (nama, email, level) diambil dari Claims cookie yang dibuat saat login.
    /// </summary>
    public class ChangePasswordController : Controller
    {
        // DAL sebagai satu-satunya jalur akses ke database
        private readonly DatabaseAccessLayer _db;

        /// <summary>
        /// Constructor: menerima DatabaseAccessLayer via Dependency Injection.
        /// Pastikan DAL sudah didaftarkan di Program.cs / Startup.cs.
        /// </summary>
        public ChangePasswordController(DatabaseAccessLayer db)
        {
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        /// <summary>
        /// Menampilkan halaman Change Password.
        /// Data user diambil dari Claims cookie (disimpan saat proses Login di HomeController)
        /// dan dikirim ke View melalui model — hanya untuk ditampilkan, bukan diedit di sini.
        /// </summary>
        public IActionResult Index()
        {
            // Ambil data user dari Claims yang tersimpan di cookie autentikasi
            var model = new ChangePasswordModel
            {
                Name   = User.FindFirst("P1F_MATA_name")?.Value,          // Nama lengkap user
                Email  = User.FindFirst(ClaimTypes.Email)?.Value,          // Email user
                SesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value, // SESA ID (username unik)
                Level  = User.FindFirst("P1F_MATA_level")?.Value           // Level akses (mat, admin, dll)
            };

            return View(model);
        }

        // ===================================================================
        // ACTION: CHANGE PASSWORD
        // ===================================================================

        /// <summary>
        /// Memproses permintaan ganti password dari form.
        /// Alur validasi berjalan secara berurutan — jika satu gagal, proses berhenti.
        ///
        /// Langkah:
        ///   1. Validasi semua field tidak boleh kosong
        ///   2. Pastikan NewPassword == ConfirmPassword
        ///   3. Hash old password lalu verifikasi ke database
        ///   4. Hash new password lalu simpan ke database
        ///
        /// Menggunakan TempData untuk meneruskan pesan error/sukses ke halaman
        /// setelah redirect (TempData bertahan hanya untuk 1 request berikutnya).
        /// </summary>
        /// <param name="model">Data dari form: OldPassword, NewPassword, ConfirmPassword</param>
        [HttpPost]
        [ValidateAntiForgeryToken] // Mencegah serangan CSRF (Cross-Site Request Forgery)
        public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
        {
            // Ambil SESA ID dari Claims untuk menentukan user mana yang sedang ganti password
            var sesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // ---------------------------------------------------------------
            // VALIDASI 1: Semua field password wajib diisi
            // ---------------------------------------------------------------
            if (string.IsNullOrWhiteSpace(model.OldPassword) ||
                string.IsNullOrWhiteSpace(model.NewPassword) ||
                string.IsNullOrWhiteSpace(model.ConfirmPassword))
            {
                TempData["Error"] = "All password fields are required.";
                return RedirectToAction("Index");
            }

            // ---------------------------------------------------------------
            // VALIDASI 2: NewPassword dan ConfirmPassword harus identik
            // Dicek sebelum menyentuh database untuk menghemat round-trip
            // ---------------------------------------------------------------
            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["Error"] = "New password and confirm password do not match.";
                return RedirectToAction("Index");
            }

            // ---------------------------------------------------------------
            // HASH PASSWORD menggunakan MD5
            // Password tidak pernah disimpan atau dikirim dalam bentuk plain text
            // ---------------------------------------------------------------
            var auth         = new Authentication();
            string hashedOld = auth.MD5Hash(model.OldPassword); // Untuk dicocokkan dengan hash di DB
            string hashedNew = auth.MD5Hash(model.NewPassword);  // Untuk disimpan sebagai password baru

            // ---------------------------------------------------------------
            // VALIDASI 3: Verifikasi old password ke database
            // Memastikan hanya pemilik akun yang bisa mengganti password
            // ---------------------------------------------------------------
            bool oldPasswordValid = await _db.CheckOldPassword(sesaId, hashedOld);
            if (!oldPasswordValid)
            {
                TempData["Error"] = "Old password is incorrect.";
                return RedirectToAction("Index");
            }

            // ---------------------------------------------------------------
            // UPDATE: Simpan password baru (sudah di-hash) ke database
            // ---------------------------------------------------------------
            await _db.UpdatePassword(sesaId, hashedNew);

            // Beri notifikasi sukses — TempData akan dibaca di halaman Index setelah redirect
            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction("Index");
        }
    }
}