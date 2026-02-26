using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;
using P1F_TPM360_HUB.Service;
using System.Data;
using Microsoft.Data.SqlClient;
using System.Security.Claims;

namespace P1F_TPM360_HUB.Controllers
{
    public class AdminController : Controller
    {
        // ===================================================================
        // DEPENDENCY INJECTION
        // ===================================================================
        private readonly ApplicationDbContext _context;
        private readonly DatabaseAccessLayer _db;

        public AdminController(
            ApplicationDbContext context,
            ImportExportFactory importExportFactory,
            ILogger<AdminController> logger,
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
        public IActionResult UserManagement()
        {
            string? sesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (sesaId == null)
                return RedirectToAction("Index", "Login");

            ViewBag.Levels = _db.GetLevel();
            ViewBag.Roles = _db.GetRole();
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
        public JsonResult AddUser(string sesa_id, string name, string password, string email, string level, string role)
        {
            try
            {
                // Hash password sebelum disimpan
                var auth = new Authentication();
                string hashedPassword = auth.MD5Hash(password);

                string query = @"INSERT INTO mst_users (sesa_id, name, password, email, level, role, record_date) 
                                 VALUES (@sesa_id, @name, @password, @email, @level, @role, GETDATE())";

                using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@sesa_id",   sesa_id  ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@name",      name     ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@password",  hashedPassword);
                    cmd.Parameters.AddWithValue("@email",     email    ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@level",     level    ?? (object)DBNull.Value);
                    cmd.Parameters.AddWithValue("@role",      role     ?? (object)DBNull.Value);

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
        public JsonResult UpdateUser(int id_user, string sesa_id, string name, string email, string level, string role)
        {
            try
            {
                string query = @"UPDATE mst_users 
                                 SET sesa_id     = @sesa_id, 
                                     name        = @name, 
                                     email       = @email, 
                                     level       = @level, 
                                     role        = @role,
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
                    cmd.Parameters.AddWithValue("@role",     role    ?? (object)DBNull.Value);

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
        /// Mengambil data user menggunakan stored procedure GET_SESA.
        /// Filter: sesa_id, level, name (null = semua data).
        /// Mengembalikan Partial View tabel user.
        /// </summary>
        public IActionResult GETUSER(string sesa_id, string level, string name)
        {
            var userList = new List<UserModel>();

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            using (SqlCommand cmd = new SqlCommand("GET_SESA", conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@sesa_id", sesa_id != null ? (object)sesa_id : DBNull.Value);
                cmd.Parameters.AddWithValue("@level",   level   != null ? (object)level   : DBNull.Value);
                cmd.Parameters.AddWithValue("@name",    name    != null ? (object)name    : DBNull.Value);

                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        userList.Add(new UserModel
                        {
                            id_user  = reader["id_user"].ToString(),
                            sesa_id  = reader["sesa_id"].ToString(),
                            name     = reader["name"].ToString(),
                            level    = reader["level"].ToString(),
                            role     = reader["role"].ToString(),
                            email    = reader["email"].ToString()
                        });
                    }
                }
            }

            return PartialView("_TableUser", userList);
        }

        /// <summary>
        /// Autocomplete: mencari sesa_id yang mengandung kata kunci 'family'.
        /// </summary>
        [HttpGet]
        public IActionResult GetUserSesa0(string family)
        {
            var data = new List<UserModel>();
            string query = "SELECT DISTINCT sesa_id FROM mst_users WHERE sesa_id LIKE '%" + family + "%' ORDER BY sesa_id ASC";

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        data.Add(new UserModel
                        {
                            Text = reader["sesa_id"].ToString(),
                            Id   = reader["sesa_id"].ToString()
                        });
                    }
                }
            }

            return Json(new { items = data });
        }

        /// <summary>
        /// Autocomplete: mencari name user yang mengandung kata kunci 'family'.
        /// </summary>
        [HttpGet]
        public IActionResult GetUserName0(string family)
        {
            var data = new List<UserModel>();
            string query = "SELECT DISTINCT name FROM mst_users WHERE name LIKE '%" + family + "%' ORDER BY name ASC";

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                conn.Open();
                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        data.Add(new UserModel
                        {
                            Text = reader["name"].ToString(),
                            Id   = reader["name"].ToString()
                        });
                    }
                }
            }

            return Json(new { items = data });
        }

        // ===================================================================
        // GET ROLE & LEVEL (untuk Edit Form)
        // ===================================================================

        /// <summary>
        /// Mengambil daftar role yang sesuai dari database.
        /// Input 'role' bisa berisi beberapa nilai dipisah titik koma (;).
        /// </summary>
        public JsonResult GetRoleEdit(string role)
        {
            var roleList = new List<string>();

            // Jika kosong, langsung kembalikan list kosong
            if (string.IsNullOrWhiteSpace(role))
                return Json(roleList);

            var roles = role.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(r => r.Trim())
                            .ToList();

            if (roles.Count == 0)
                return Json(roleList);

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            using (SqlCommand cmd = new SqlCommand())
            {
                conn.Open();
                cmd.Connection = conn;

                // Bangun parameter dinamis untuk klausa IN
                var parameters = new List<string>();
                for (int i = 0; i < roles.Count; i++)
                {
                    string paramName = $"@org{i}";
                    parameters.Add(paramName);
                    cmd.Parameters.AddWithValue(paramName, roles[i]);
                }

                cmd.CommandText = $"SELECT DISTINCT role FROM mst_role WHERE role IN ({string.Join(",", parameters)})";

                using (SqlDataReader reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                        roleList.Add(reader["role"].ToString());
                }
            }

            return Json(roleList);
        }

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
    }
}