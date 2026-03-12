using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_MATA.Function;
using P1F_MATA.Models;
using System.Security.Claims;

namespace P1F_MATA.Controllers
{
    public class UserManagement : Controller
    {
        private readonly DatabaseAccessLayer _db;

        public UserManagement(DatabaseAccessLayer db)
        {
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        [Authorize(Policy = "UserLevel")]
        public IActionResult Dash() => View();

        /// <summary>Menampilkan halaman User Management. Redirect ke login jika belum login.</summary>
        public IActionResult Index()
        {
            if (User.FindFirst(ClaimTypes.NameIdentifier)?.Value == null)
                return RedirectToAction("Index", "Login");

            ViewBag.Levels = _db.GetLevel();
            return View();
        }

        // ===================================================================
        // CRUD USER
        // ===================================================================

        /// <summary>Tambah user baru. Password di-hash MD5 sebelum dikirim ke DAL.</summary>
        [HttpPost]
        public JsonResult AddUser(string sesa_id, string name, string password, string email, string level)
        {
            try
            {
                string hashedPassword = new Authentication().MD5Hash(password);
                var (success, rows)   = _db.AddUser(sesa_id, name, hashedPassword, email, level);

                return rows > 0
                    ? Json(new { success = true,  message = "User successfully added!" })
                    : Json(new { success = false, message = "Failed to add user to database." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>Update data user berdasarkan id_user.</summary>
        [HttpPost]
        public JsonResult UpdateUser(int id_user, string sesa_id, string name, string email, string level)
        {
            try
            {
                var (success, rows) = _db.UpdateUser(id_user, sesa_id, name, email, level);

                return rows > 0
                    ? Json(new { success = true,  message = "User updated successfully!" })
                    : Json(new { success = false, message = "No changes made or user not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>Hapus user berdasarkan id.</summary>
        [HttpPost]
        public JsonResult DeleteUser(int id)
        {
            try
            {
                var (success, rows) = _db.DeleteUser(id);

                return rows > 0
                    ? Json(new { success = true,  message = "User has been deleted." })
                    : Json(new { success = false, message = "User not found or already deleted." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>Hapus beberapa user sekaligus.</summary>
        [HttpPost]
        public JsonResult DeleteMultipleUser(List<int> ids)
        {
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

        /// <summary>Cari user berdasarkan keyword → kembalikan Partial View tabel.</summary>
        public IActionResult GETUSER(string search)
        {
            var userList = _db.GetUsers(search);
            return PartialView("_TableUser", userList);
        }

        // ===================================================================
        // GET LEVEL EDIT
        // ===================================================================

        /// <summary>Ambil level dari DB sesuai input (bisa multi, dipisah titik koma).</summary>
        public JsonResult GetLevelEdit(string level)
        {
            var levels    = level.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                                 .Select(l => l.Trim()).ToList();
            var levelList = _db.GetLevelEdit(levels);
            return Json(levelList);
        }
    }
}