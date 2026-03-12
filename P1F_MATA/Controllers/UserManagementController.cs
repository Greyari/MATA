using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_MATA.Function;
using P1F_MATA.Models;
using System.Security.Claims;

namespace P1F_MATA.Controllers
{
    /// <summary>
    /// Controller untuk fitur User Management.
    /// Menangani operasi CRUD user (tambah, edit, hapus) serta pencarian user.
    /// Hanya user dengan level admin / superadmin yang seharusnya bisa mengakses halaman ini.
    /// </summary>
    public class UserManagement : Controller
    {
        // DAL sebagai satu-satunya jalur akses ke database
        private readonly DatabaseAccessLayer _db;

        /// <summary>
        /// Constructor: menerima DatabaseAccessLayer via Dependency Injection.
        /// </summary>
        public UserManagement(DatabaseAccessLayer db)
        {
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        /// <summary>
        /// Menampilkan halaman Dash (dashboard ringkasan User Management).
        /// Hanya user dengan level yang sesuai Policy "UserLevel" yang bisa mengakses.
        /// </summary>
        [Authorize(Policy = "UserLevel")]
        public IActionResult Dash() => View();

        /// <summary>
        /// Menampilkan halaman utama User Management.
        /// Jika user belum login → redirect ke halaman Login.
        /// ViewBag.Levels diisi dengan daftar level yang tersedia
        /// untuk digunakan di dropdown form tambah/edit user.
        /// </summary>
        public IActionResult Index()
        {
            // Cek apakah user sudah login dengan memeriksa Claims NameIdentifier
            if (User.FindFirst(ClaimTypes.NameIdentifier)?.Value == null)
                return RedirectToAction("Index", "Login");

            // Isi dropdown Level untuk form tambah/edit user
            ViewBag.Levels = _db.GetLevel();
            return View();
        }

        // ===================================================================
        // CRUD USER
        // ===================================================================

        /// <summary>
        /// Menambah user baru ke database.
        /// Password di-hash MD5 di controller sebelum dikirim ke DAL,
        /// sehingga password plain text tidak pernah menyentuh database.
        ///
        /// Return JSON: { success, message }
        /// </summary>
        /// <param name="sesa_id">Username unik user (SESA ID)</param>
        /// <param name="name">Nama lengkap user</param>
        /// <param name="password">Password plain text dari form (akan di-hash sebelum disimpan)</param>
        /// <param name="email">Alamat email user</param>
        /// <param name="level">Level akses user (mat, mat_admin, superadmin, dll)</param>
        [HttpPost]
        public JsonResult AddUser(string sesa_id, string name, string password, string email, string level)
        {
            try
            {
                // Hash password sebelum dikirim ke database
                string hashedPassword = new Authentication().MD5Hash(password);
                var (success, rows)   = _db.AddUser(sesa_id, name, hashedPassword, email, level);

                // rows > 0 berarti INSERT berhasil masuk ke database
                return rows > 0
                    ? Json(new { success = true,  message = "User successfully added!" })
                    : Json(new { success = false, message = "Failed to add user to database." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Mengupdate data user yang sudah ada berdasarkan id_user.
        /// Password tidak diubah di sini — gunakan fitur Change Password untuk itu.
        ///
        /// Return JSON: { success, message }
        /// </summary>
        /// <param name="id_user">Primary key user yang akan diupdate</param>
        /// <param name="sesa_id">SESA ID baru</param>
        /// <param name="name">Nama lengkap baru</param>
        /// <param name="email">Email baru</param>
        /// <param name="level">Level akses baru</param>
        [HttpPost]
        public JsonResult UpdateUser(int id_user, string sesa_id, string name, string email, string level)
        {
            try
            {
                var (success, rows) = _db.UpdateUser(id_user, sesa_id, name, email, level);

                // rows == 0 bisa berarti: user tidak ditemukan, atau data tidak berubah
                return rows > 0
                    ? Json(new { success = true,  message = "User updated successfully!" })
                    : Json(new { success = false, message = "No changes made or user not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Menghapus satu user dari database berdasarkan id_user.
        /// Operasi ini permanen dan tidak bisa dibatalkan.
        ///
        /// Return JSON: { success, message }
        /// </summary>
        /// <param name="id">Primary key user yang akan dihapus</param>
        [HttpPost]
        public JsonResult DeleteUser(int id)
        {
            try
            {
                var (success, rows) = _db.DeleteUser(id);

                // rows == 0 berarti user sudah tidak ada di database (sudah dihapus sebelumnya)
                return rows > 0
                    ? Json(new { success = true,  message = "User has been deleted." })
                    : Json(new { success = false, message = "User not found or already deleted." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Menghapus beberapa user sekaligus dalam satu operasi.
        /// Lebih efisien daripada memanggil DeleteUser satu per satu.
        /// Dipanggil saat user mencentang beberapa baris di tabel lalu klik "Delete Selected".
        ///
        /// Return JSON: { success, message } dengan jumlah user yang berhasil dihapus.
        /// </summary>
        /// <param name="ids">List primary key user yang akan dihapus</param>
        [HttpPost]
        public JsonResult DeleteMultipleUser(List<int> ids)
        {
            // Validasi: pastikan ada user yang dipilih sebelum eksekusi ke database
            if (ids == null || ids.Count == 0)
                return Json(new { success = false, message = "No users selected." });

            try
            {
                int rows = _db.DeleteMultipleUsers(ids);
                return Json(new { success = true, message = $"{rows} user(s) deleted successfully." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        // ===================================================================
        // GET / SEARCH USER
        // ===================================================================

        /// <summary>
        /// Mencari user berdasarkan keyword dan merender hasilnya sebagai Partial View tabel.
        /// Pencarian dilakukan pada kolom: sesa_id, name, dan email.
        /// Dipanggil via AJAX saat user mengetik di kolom pencarian,
        /// lalu hasilnya menggantikan isi tabel di halaman tanpa reload penuh.
        /// Jika keyword kosong → tampilkan semua user.
        /// </summary>
        /// <param name="search">Keyword pencarian (bisa kosong untuk tampilkan semua)</param>
        public IActionResult GETUSER(string search)
        {
            var userList = _db.GetUsers(search);
            return PartialView("_TableUser", userList);
        }

        // ===================================================================
        // GET LEVEL EDIT
        // ===================================================================

        /// <summary>
        /// Mengambil level dari database sesuai dengan level yang dimiliki user yang akan diedit.
        /// Digunakan untuk mengisi checkbox/dropdown level di form edit user.
        ///
        /// Input bisa berisi beberapa level dipisahkan titik koma,
        /// contoh: "mat;mat_admin" → dipecah menjadi ["mat", "mat_admin"]
        /// lalu dicari ke database mana yang valid.
        ///
        /// Return JSON: array of string level yang ditemukan di database.
        /// </summary>
        /// <param name="level">String level dari user, bisa multi dipisah ";"</param>
        public JsonResult GetLevelEdit(string level)
        {
            // Pecah string level berdasarkan titik koma, lalu trim spasi
            var levels = level.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim())
                              .ToList();

            // Cek ke database level mana yang valid/terdaftar
            var levelList = _db.GetLevelEdit(levels);
            return Json(levelList);
        }
    }
}