using MailKit.Search;
using MailKit.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using MimeKit;
using Newtonsoft.Json;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using Org.BouncyCastle.Ocsp;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;
using P1F_TPM360_HUB.Service;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.Linq.Dynamic.Core;
using System.Security.Claims;
using System.Text;
using System.Web;
using ZXing;
using static iText.Signatures.LtvVerification;
using static Microsoft.EntityFrameworkCore.DbLoggerCategory.Database;
using static QRCoder.PayloadGenerator;
using static System.Runtime.InteropServices.JavaScript.JSType;
using SmtpClient = MailKit.Net.Smtp.SmtpClient;




namespace P1F_TPM360_HUB.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly DatabaseAccessLayer _db;

        public AdminController(ApplicationDbContext context, ImportExportFactory importexportFactory, ILogger<AdminController> logger)
        {
            _context = context;
            _db = new DatabaseAccessLayer();
        }
        [Authorize(Policy = "UserLevel")]
        public IActionResult Dash()
        {
            return View();
        }
        public IActionResult UserManagement()
        {
            string sesa_id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (sesa_id != null)
            {
                {
                    ViewBag.Levels = _db.GetLevel();
                    ViewBag.Roles = _db.GetRole();

                    return View();
                }
            }
            else
            {
                return RedirectToAction("Index", "Login");
            }
        }

        [HttpPost]
        public JsonResult AddUser(string sesa_id, string name, string password, string email, string level, string role)
        {
            try
            {
                // 1. Enkripsi Password (Menggunakan class Authentication yang sudah ada di proyek Anda)
                var auth = new Authentication();
                string encryptedPassword = auth.MD5Hash(password);

                using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
                {
                    // 2. Query Insert
                    // Sesuaikan nama kolom (id_user biasanya identity, jadi tidak perlu di-insert manual)
                    string query = @"INSERT INTO mst_users (sesa_id, name, password, email, level, role, record_date) 
                             VALUES (@sesa_id, @name, @password, @email, @level, @role, GETDATE())";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@sesa_id", sesa_id ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@name", name ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@password", encryptedPassword);
                        cmd.Parameters.AddWithValue("@email", email ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@level", level ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@role", role ?? (object)DBNull.Value);

                        conn.Open();
                        int rowsAffected = cmd.ExecuteNonQuery();
                        conn.Close();

                        if (rowsAffected > 0)
                        {
                            return Json(new { success = true, message = "User successfully added!" });
                        }
                        else
                        {
                            return Json(new { success = false, message = "Failed to add user to database." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult UpdateUser(int id_user, string sesa_id, string name, string email, string level, string role)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
                {
                    // Query untuk memperbarui data user berdasarkan id_user
                    string query = @"UPDATE mst_users 
                             SET sesa_id = @sesa_id, 
                                 name = @name, 
                                 email = @email, 
                                 level = @level, 
                                 role = @role,
                                 record_date = GETDATE()
                             WHERE id_user = @id_user";

                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id_user", id_user);
                        cmd.Parameters.AddWithValue("@sesa_id", sesa_id ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@name", name ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@email", email ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@level", level ?? (object)DBNull.Value);
                        cmd.Parameters.AddWithValue("@role", role ?? (object)DBNull.Value);

                        conn.Open();
                        int rowsAffected = cmd.ExecuteNonQuery();
                        conn.Close();

                        if (rowsAffected > 0)
                        {
                            return Json(new { success = true, message = "User updated successfully!" });
                        }
                        else
                        {
                            return Json(new { success = false, message = "No changes made or user not found." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult DeleteUser(int id)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
                {
                    string query = "DELETE FROM mst_users WHERE id_user = @id";
                    using (SqlCommand cmd = new SqlCommand(query, conn))
                    {
                        cmd.Parameters.AddWithValue("@id", id);
                        conn.Open();
                        int rowsAffected = cmd.ExecuteNonQuery();
                        conn.Close();

                        if (rowsAffected > 0)
                        {
                            return Json(new { success = true, message = "User has been deleted." });
                        }
                        else
                        {
                            return Json(new { success = false, message = "User not found or already deleted." });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error: " + ex.Message });
            }
        }

        public IActionResult GETUSER(string sesa_id, string level, string name)
        {
            using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
            {
                List<UserModel> dataFPA = new List<UserModel>();

                var query = "GET_SESA";
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    if (sesa_id == null) { cmd.Parameters.AddWithValue("@sesa_id", DBNull.Value); }
                    else { cmd.Parameters.AddWithValue("@sesa_id", sesa_id); }

                    if (level == null) { cmd.Parameters.AddWithValue("@level", DBNull.Value); }
                    else { cmd.Parameters.AddWithValue("@level", level); }

                    if (name == null) { cmd.Parameters.AddWithValue("@name", DBNull.Value); }
                    else { cmd.Parameters.AddWithValue("@name", name); }

                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                var data_list = new UserModel();
                                data_list.id_user = reader["id_user"].ToString();
                                data_list.sesa_id = reader["sesa_id"].ToString();
                                data_list.name = reader["name"].ToString();
                                data_list.level = reader["level"].ToString();
                                data_list.role = reader["role"].ToString();
                                data_list.email = reader["email"].ToString();
                                dataFPA.Add(data_list);
                            }
                        }
                    }
                }
                return PartialView("_TableUser", dataFPA);

            }
        }
        [HttpGet]
        public IActionResult GetUserSesa0(string family)
        {
            List<UserModel> data = new List<UserModel>();
            string query = "SELECT DISTINCT sesa_id as sesa_id FROM mst_users WHERE sesa_id LIKE '%" + family + "%' ORDER BY sesa_id ASC";
            using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query))
                {
                    cmd.Connection = conn;
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var data_list = new UserModel();
                            data_list.Text = reader["sesa_id"].ToString();
                            data_list.Id = reader["sesa_id"].ToString();
                            data.Add(data_list);
                        }
                    }
                    conn.Close();
                }
            }
            return Json(new { items = data });
        }
        
        [HttpGet]
        public IActionResult GetUserName0(string family)
        {
            List<UserModel> data = new List<UserModel>();
            string query = "SELECT DISTINCT name as name FROM mst_users WHERE name LIKE '%" + family + "%' ORDER BY name ASC";
            using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
            {
                using (SqlCommand cmd = new SqlCommand(query))
                {
                    cmd.Connection = conn;
                    conn.Open();

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var data_list = new UserModel();
                            data_list.Text = reader["name"].ToString();
                            data_list.Id = reader["name"].ToString();
                            data.Add(data_list);
                        }
                    }
                    conn.Close();
                }
            }
            return Json(new { items = data });
        }

        public JsonResult GetRoleEdit(string role)
        {
            var roleList = new List<string>();

            if (string.IsNullOrWhiteSpace(role))
            {
                return Json(roleList);
            }

            var roles = role.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim())
                            .ToList();

            if (roles.Count == 0)
            {
                return Json(roleList);
            }

            using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
            {
                conn.Open();

                var parameters = new List<string>();
                using (SqlCommand cmd = new SqlCommand())
                {
                    cmd.Connection = conn;

                    for (int i = 0; i < roles.Count; i++)
                    {
                        string paramName = $"@org{i}";
                        parameters.Add(paramName);
                        cmd.Parameters.AddWithValue(paramName, roles[i]);
                    }

                    string query = $"SELECT DISTINCT role FROM mst_role WHERE role IN ({string.Join(",", parameters)})";

                    cmd.CommandText = query;

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            roleList.Add(reader["role"].ToString());
                        }
                    }
                }
            }

            return Json(roleList);
        }

        public JsonResult GetLevelEdit(string level)
        {
            var levelList = new List<string>();
            var levels = level.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim())
                            .ToList();

            using (SqlConnection conn = new SqlConnection(_db.ConnectionString))
            {
                conn.Open();

                // Buat parameter dinamis untuk IN clause
                var parameters = new List<string>();
                for (int i = 0; i < levels.Count; i++)
                {
                    parameters.Add($"@org{i}");
                }

                string query = $"SELECT DISTINCT level FROM mst_level WHERE level IN ({string.Join(",", parameters)})";

                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    for (int i = 0; i < levels.Count; i++)
                    {
                        cmd.Parameters.AddWithValue($"@org{i}", levels[i]);
                    }

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            levelList.Add(reader["level"].ToString());
                        }
                    }
                }
            }

            return Json(levelList);
        }

    }
}