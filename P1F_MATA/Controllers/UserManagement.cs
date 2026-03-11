using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_MATA.Function;
using P1F_MATA.Models;
using P1F_MATA.Service;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace P1F_MATA.Controllers
{
    public class UserManagement : Controller
    {
        // ===================================================================
        // DEPENDENCY INJECTION
        // ===================================================================
        private readonly ApplicationDbContext _context;
        private readonly DatabaseAccessLayer _db;

        public UserManagement(
            ApplicationDbContext context,
            ImportExportFactory importExportFactory,
            ILogger<UserManagement> logger,
            DatabaseAccessLayer db)
        {
            _context = context;
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        [Authorize(Policy = "UserLevel")]
        public IActionResult Dash()
        {
            return View();
        }

        /// <summary>
        /// Menampilkan halaman User Management.
        /// Hanya bisa diakses jika user sudah login (sesa_id tidak null).
        /// </summary>
        public IActionResult Index()
        {
            string? sesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (sesaId == null)
                return RedirectToAction("Index", "Login");

            ViewBag.Levels = _db.GetLevel();
            return View();
        }

        // ===================================================================
        // CRUD USER
        // ===================================================================

        /// <summary>
        /// Menambahkan user baru ke database.
        /// Password akan di-hash menggunakan MD5 sebelum disimpan.
        /// </summary>
        [HttpPost]
        public JsonResult AddUser(string sesa_id, string name, string password, string email, string level)
        {
            try
            {
                // Hash password sebelum disimpan
                var auth = new Authentication();
                string hashedPassword = auth.MD5Hash(password);

                string query = @"INSERT INTO mst_users (sesa_id, name, password, email, level, record_date) 
                                 VALUES (@sesa_id, @name, @password, @email, @level, GETDATE())";

                using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@sesa_id",   sesa_id  ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@name",      name     ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@password",  hashedPassword);
                    cmd.Parameters.AddWithValue("@email",     email    ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@level",     level    ?? (object)DBNull.Value);

                    conn.Open();
                    int rowsAffected = cmd.ExecuteNonQuery();

                    return rowsAffected > 0
                        ? Json(new { success = true,  message = "User successfully added!" })
                        : Json(new { success = false, message = "Failed to add user to database." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Mengupdate data user yang sudah ada berdasarkan id_user.
        /// </summary>
        [HttpPost]
        public JsonResult UpdateUser(int id_user, string sesa_id, string name, string email, string level)
        {
            try
            {
                string query = @"UPDATE mst_users 
                                 SET sesa_id     = @sesa_id, 
                                     name        = @name, 
                                     email       = @email, 
                                     level       = @level, 
                                     record_date = GETDATE()
                                 WHERE id_user = @id_user";

                using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@id_user",  id_user);
                    cmd.Parameters.AddWithValue("@sesa_id",  sesa_id ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@name",     name    ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@email",    email   ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@level",    level   ?? (object)DBNull.Value);

                    conn.Open();
                    int rowsAffected = cmd.ExecuteNonQuery();

                    return rowsAffected > 0
                        ? Json(new { success = true,  message = "User updated successfully!" })
                        : Json(new { success = false, message = "No changes made or user not found." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        /// <summary>
        /// Menghapus user dari database berdasarkan id.
        /// </summary>
        [HttpPost]
        public JsonResult DeleteUser(int id)
        {
            try
            {
                string query = "DELETE FROM mst_users WHERE id_user = @id";

                using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@id", id);

                    conn.Open();
                    int rowsAffected = cmd.ExecuteNonQuery();

                    return rowsAffected > 0
                        ? Json(new { success = true,  message = "User has been deleted." })
                        : Json(new { success = false, message = "User not found or already deleted." });
                }
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
        /// Mengambil data user dengan satu keyword pencarian.
        /// Bisa cari berdasarkan name, sesa_id, atau email sekaligus.
        /// </summary>
        public IActionResult GETUSER(string search)
        {
            var userList = new List<UserModel>();
            string keyword = string.IsNullOrWhiteSpace(search) ? "" : search.Trim();

            string query = @"SELECT id_user, sesa_id, name, level, email 
                            FROM mst_users
                            WHERE (@keyword = ''
                                    OR sesa_id LIKE '%' + @keyword + '%'
                                    OR name    LIKE '%' + @keyword + '%'
                                    OR email   LIKE '%' + @keyword + '%')
                            ORDER BY name ASC";

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                cmd.Parameters.AddWithValue("@keyword", keyword);
                conn.Open();

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        userList.Add(new UserModel
                        {
                            id_user = reader["id_user"].ToString(),
                            sesa_id = reader["sesa_id"].ToString(),
                            name    = reader["name"].ToString(),
                            level   = reader["level"].ToString(),
                            email   = reader["email"].ToString()
                        });
                    }
                }
            }

            return PartialView("_TableUser", userList);
        }

        // ===================================================================
        // GET LEVEL (untuk Edit Form)
        // ===================================================================

        /// <summary>
        /// Mengambil daftar level yang sesuai dari database.
        /// Input 'level' bisa berisi beberapa nilai dipisah titik koma (;).
        /// </summary>
        public JsonResult GetLevelEdit(string level)
        {
            var levelList = new List<string>();

            var levels = level.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim())
                              .ToList();

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                conn.Open();

                // Bangun parameter dinamis untuk klausa IN
                var parameters = Enumerable.Range(0, levels.Count)
                                           .Select(i => $"@org{i}")
                                           .ToList();

                string query = $"SELECT DISTINCT level FROM mst_level WHERE level IN ({string.Join(",", parameters)})";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    for (int i = 0; i < levels.Count; i++)
                        cmd.Parameters.AddWithValue($"@org{i}", levels[i]);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            levelList.Add(reader["level"].ToString());
                    }
                }
            }

            return Json(levelList);
        }

        /// <summary>
        /// Menghapus beberapa user sekaligus berdasarkan list id.
        /// </summary>
        [HttpPost]
        public JsonResult DeleteMultipleUser(List<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return Json(new { success = false, message = "No users selected." });

            try
            {
                string query = $"DELETE FROM mst_users WHERE id_user IN ({string.Join(",", ids)})";

                using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    int rows = cmd.ExecuteNonQuery();
                    return Json(new { success = true, message = $"{rows} user(s) deleted successfully." });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }
    }
}