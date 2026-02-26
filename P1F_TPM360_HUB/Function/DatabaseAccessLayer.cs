using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using P1F_TPM360_HUB.Models;

namespace P1F_TPM360_HUB.Function
{
    /// <summary>
    /// Data Access Layer: semua query langsung ke database melalui class ini.
    /// Connection string diambil dari appsettings.json (key: "DefaultConnection").
    /// </summary>
    public class DatabaseAccessLayer
    {
        // ===================================================================
        // KONEKSI DATABASE
        // ===================================================================
        private readonly string _connectionString;

        public DatabaseAccessLayer(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        /// <summary>Mengembalikan connection string untuk digunakan di controller/service lain.</summary>
        public string GetConnection() => _connectionString;

        // ===================================================================
        // HELPER PRIVATE
        // ===================================================================

        /// <summary>
        /// Helper generik untuk mengambil list data dari database.
        /// Mengurangi boilerplate buka/tutup koneksi.
        /// </summary>
        private List<T> QueryList<T>(string query, Func<SqlDataReader, T> map, Action<SqlCommand> addParams = null)
        {
            var list = new List<T>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    addParams?.Invoke(cmd);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            list.Add(map(reader));
                    }
                }
            }
            return list;
        }

        // ===================================================================
        // DROPDOWN / MASTER DATA
        // ===================================================================

        /// <summary>Mengambil semua level yang tersedia dari mst_level.</summary>
        public List<DropdownModel> GetLevel()
            => QueryList("SELECT DISTINCT level FROM mst_level",
                reader => new DropdownModel { Code = reader["level"].ToString() });

        /// <summary>Mengambil semua role yang tersedia dari mst_role.</summary>
        public List<DropdownModel> GetRole()
            => QueryList("SELECT DISTINCT role FROM mst_role",
                reader => new DropdownModel { Code = reader["role"].ToString() });

        /// <summary>
        /// Mengambil daftar line yang sesuai dengan lines milik user (dari session/claim).
        /// Parameter sessionLines berformat: "LINE1;LINE2;LINE3"
        /// </summary>
        public List<DropdownModel> GetLines(string sessionLines)
        {
            if (string.IsNullOrEmpty(sessionLines))
                return new List<DropdownModel>();

            return QueryList(
                @"SELECT DISTINCT line 
                  FROM mst_drawer_mapping
                  WHERE line IN (SELECT TRIM(value) FROM STRING_SPLIT(@SessionLines, ';'))",
                reader => new DropdownModel { Code = reader["line"].ToString() },
                cmd => cmd.Parameters.AddWithValue("@SessionLines", sessionLines));
        }

        /// <summary>Mengambil semua line dari mst_drawer_mapping.</summary>
        public List<DropdownModel> GetLine()
            => QueryList("SELECT DISTINCT line FROM mst_drawer_mapping",
                reader => new DropdownModel { Code = reader["line"].ToString() });

        /// <summary>Mengambil daftar lokasi berdasarkan line yang dipilih.</summary>
        public List<DropdownModel> GetLocations(string line)
            => QueryList(
                "SELECT DISTINCT location FROM mst_drawer WHERE line = @line",
                reader => new DropdownModel { Code = reader["location"].ToString() },
                cmd => cmd.Parameters.AddWithValue("@line", line ?? (object)DBNull.Value));

        /// <summary>
        /// Mengambil kapasitas drawer (max qty) per lokasi pada line tertentu.
        /// Jika location = "ALL" atau kosong, ambil semua lokasi.
        /// </summary>
        // public List<DrawerCapacity> GetDrawerCapacities(string line, string location)
        // {
        //     string query = "SELECT line, location, max_qty FROM mst_drawer WHERE line = @line";
        //     if (location != "ALL" && !string.IsNullOrEmpty(location))
        //         query += " AND location = @location";
        //     query += " ORDER BY location ASC";

        //     return QueryList(query,
        //         reader => new DrawerCapacity
        //         {
        //             Line     = reader["line"].ToString(),
        //             Location = reader["location"].ToString(),
        //             MaxQty   = reader["max_qty"] != DBNull.Value ? Convert.ToInt32(reader["max_qty"]) : 0
        //         },
        //         cmd =>
        //         {
        //             cmd.Parameters.AddWithValue("@line", line ?? (object)DBNull.Value);
        //             if (location != "ALL" && !string.IsNullOrEmpty(location))
        //                 cmd.Parameters.AddWithValue("@location", location);
        //         });
        // }

        /// <summary>
        /// Mengambil daftar kabel berdasarkan line dan lokasi.
        /// Jika location = "ALL", ambil semua kabel di line tersebut.
        /// </summary>
        // public List<CableViewModel> GetCables(string line, string location)
        // {
        //     string query = @"SELECT cable_id, cable_part, cable_description, unit_model, status, location
        //                      FROM mst_cable 
        //                      WHERE line = @line";
        //     if (location != "ALL" && !string.IsNullOrEmpty(location))
        //         query += " AND location = @location";
        //     query += " ORDER BY location ASC, cable_id ASC";

        //     return QueryList(query,
        //         reader => new CableViewModel
        //         {
        //             CableId          = reader["cable_id"]          != DBNull.Value ? reader["cable_id"].ToString()          : "",
        //             CablePart        = reader["cable_part"]        != DBNull.Value ? reader["cable_part"].ToString()        : "",
        //             CableDescription = reader["cable_description"] != DBNull.Value ? reader["cable_description"].ToString() : "",
        //             UnitModel        = reader["unit_model"]        != DBNull.Value ? reader["unit_model"].ToString()        : "",
        //             Status           = reader["status"]            != DBNull.Value ? reader["status"].ToString()            : "",
        //             LocationGroup    = reader["location"]          != DBNull.Value ? reader["location"].ToString()          : ""
        //         },
        //         cmd =>
        //         {
        //             cmd.Parameters.AddWithValue("@line", line ?? (object)DBNull.Value);
        //             if (location != "ALL" && !string.IsNullOrEmpty(location))
        //                 cmd.Parameters.AddWithValue("@location", location);
        //         });
        // }

        /// <summary>
        /// Mencari line dan lokasi kabel berdasarkan QR code, cable_id, atau unit_model.
        /// </summary>
        // public List<CableLocationResult> GetLocationsByQr(string qrCode)
        //     => QueryList(
        //         @"SELECT DISTINCT line, location 
        //           FROM mst_cable 
        //           WHERE cable_id = @qrCode OR qr_code = @qrCode OR unit_model = @qrCode",
        //         reader => new CableLocationResult
        //         {
        //             Line     = reader["line"]?.ToString(),
        //             Location = reader["location"]?.ToString()
        //         },
        //         cmd => cmd.Parameters.AddWithValue("@qrCode", qrCode));

        // ===================================================================
        // KABEL
        // ===================================================================

        /// <summary>
        /// Update status kabel menjadi "kembali" via SP RETURN_FROM_DASHBOARD.
        /// Mengembalikan true jika ada row yang terpengaruh.
        /// </summary>
        public bool UpdateCableStatusToIn(string cableId, string sesaId)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("RETURN_FROM_DASHBOARD", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@cable_id",     cableId);
                    cmd.Parameters.AddWithValue("@return_sesa",  sesaId);
                    return cmd.ExecuteNonQuery() > 0;
                }
            }
        }

        // ===================================================================
        // EMPLOYEE / REFERENSI
        // ===================================================================

        /// <summary>
        /// Mengambil semua employee (gabungan mst_employee dan mst_users via FULL OUTER JOIN).
        /// </summary>
        public List<CodeNameModel> GetEmployee()
            => QueryList(
                @"SELECT COALESCE(a.sesa_id, b.sesa_id) AS sesa_id, 
                         COALESCE(a.employee_name, b.name) AS employee_name
                  FROM mst_employee a 
                  FULL OUTER JOIN mst_users b ON a.sesa_id = b.sesa_id",
                reader => new CodeNameModel
                {
                    Code = reader["sesa_id"].ToString(),
                    Name = reader["employee_name"].ToString()
                });

        /// <summary>
        /// Mengambil employee yang SESA ID-nya mengandung 'SESA' (karyawan SE),
        /// atau yang berstatus DUMMY dan aktif.
        /// </summary>
        public List<CodeNameModel> GetEmployeeSE()
            => QueryList(
                @"SELECT COALESCE(a.sesa_id, b.sesa_id) AS sesa_id, 
                         COALESCE(a.employee_name, b.name) AS employee_name
                  FROM mst_employee a 
                  FULL OUTER JOIN mst_users b ON a.sesa_id = b.sesa_id
                  WHERE (a.sesa_id LIKE '%SESA%' AND b.sesa_id LIKE '%SESA%') 
                     OR a.plant = 'DUMMY'
                  AND a.employee_status = 1",
                reader => new CodeNameModel
                {
                    Code = reader["sesa_id"].ToString(),
                    Name = reader["employee_name"].ToString()
                });

        /// <summary>Mengambil daftar rekomendasi panel dari mst_panel_recommendation.</summary>
        public List<CodeNameModel> GetPanelRecommendation()
            => QueryList("SELECT id_corrective, corrective FROM mst_panel_recommendation",
                reader => new CodeNameModel
                {
                    Code = reader["id_corrective"].ToString(),
                    Name = reader["corrective"].ToString()
                });

        /// <summary>Mengambil daftar referensi dokumen berdasarkan kode referensi.</summary>
        public List<CodeNameModel> GetReferences(string refCode)
            => QueryList(
                "SELECT file_reference, title FROM mst_reference_tool WHERE ref_code = @ref_code",
                reader => new CodeNameModel
                {
                    Code = reader["file_reference"].ToString(),
                    Name = reader["title"].ToString()
                },
                cmd => cmd.Parameters.AddWithValue("@ref_code", refCode));

        /// <summary>Mengambil daftar organisasi (kecuali 'ALL Employee').</summary>
        public List<CodeNameModel> GetOrganizations()
            => QueryList(
                "SELECT organization FROM mst_organization WHERE organization <> 'ALL Employee'",
                reader => new CodeNameModel { Name = reader["organization"].ToString() });

        // ===================================================================
        // USER MANAGEMENT
        // ===================================================================

        /// <summary>
        /// Menambahkan user baru ke mst_users.
        /// Password akan di-hash MD5. Default password "123" jika kosong.
        /// </summary>
        public bool AddUser(string sesaId, string name, string password, string level, string role, string email, string plant, string org)
        {
            if (string.IsNullOrEmpty(password)) password = "123";

            string hashedPassword = new Authentication().MD5Hash(password);
            string query = @"INSERT INTO mst_users (sesa_id, name, password, level, role, email, plant, organization) 
                             VALUES (@sesa_id, @name, @password, @level, @role, @email, @plant, @org)";

            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@sesa_id",  sesaId);
                    cmd.Parameters.AddWithValue("@name",     name);
                    cmd.Parameters.AddWithValue("@password", hashedPassword);
                    cmd.Parameters.AddWithValue("@level",    level);
                    cmd.Parameters.AddWithValue("@role",     role);
                    cmd.Parameters.AddWithValue("@email",    email);
                    cmd.Parameters.AddWithValue("@plant",    plant);
                    cmd.Parameters.AddWithValue("@org",      org);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// Mengambil detail user berdasarkan sesa_id via SP GET_USER_DETAIL.
        /// Mengembalikan list (biasanya hanya 1 item).
        /// </summary>
        public List<UserDetailModel> GetUserDetail(string sesaId)
        {
            var list = new List<UserDetailModel>();
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("GET_USER_DETAIL", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@sesa_id", sesaId);

                    using SqlDataReader reader = cmd.ExecuteReader();
                    while (reader.Read())
                    {
                        list.Add(new UserDetailModel
                        {
                            id      = reader["id_user"].ToString(),
                            sesa_id = reader["sesa_id"].ToString(),
                            name    = reader["name"].ToString(),
                            email   = reader["email"].ToString(),
                            level   = reader["level"].ToString(),
                            role    = reader["role"].ToString(),
                            lines   = reader["lines"].ToString()
                        });
                    }
                }
            }
            return list;
        }

        /// <summary>Mengupdate data user di mst_users berdasarkan id_user.</summary>
        public bool UpdateUserManagement(string idUser, string sesaId, string name, string plant, string level, string role, string org)
        {
            string query = @"UPDATE mst_users 
                             SET sesa_id = @sesa_id, name = @name, level = @level, 
                                 role = @role, plant = @plant, organization = @org 
                             WHERE id_user = @id_user";
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@sesa_id",  sesaId);
                    cmd.Parameters.AddWithValue("@name",     name);
                    cmd.Parameters.AddWithValue("@level",    level);
                    cmd.Parameters.AddWithValue("@role",     role);
                    cmd.Parameters.AddWithValue("@plant",    plant);
                    cmd.Parameters.AddWithValue("@org",      org);
                    cmd.Parameters.AddWithValue("@id_user",  idUser);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        /// <summary>Mengambil nama user berdasarkan id_user.</summary>
        public string GetUserNameById(string idUser)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("SELECT name FROM mst_users WHERE id_user = @id_user", conn))
                {
                    cmd.Parameters.AddWithValue("@id_user", idUser);
                    conn.Open();
                    using (SqlDataReader reader = cmd.ExecuteReader())
                        return reader.Read() ? reader["name"].ToString() : null;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return null;
            }
        }

        /// <summary>Menghapus user dari mst_users berdasarkan id_user.</summary>
        public bool DeleteUser(string idUser)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand("DELETE FROM mst_users WHERE id_user = @id_user", conn))
                {
                    cmd.Parameters.AddWithValue("@id_user", idUser);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// Update password user berdasarkan id_user.
        /// Password akan di-hash MD5 sebelum disimpan.
        /// </summary>
        public bool UpdateUserr(string idUser, string password)
        {
            try
            {
                string hashedPassword = new Authentication().MD5Hash(password);
                UpdateUserInDatabase(new UserModel { id_user = idUser, password = hashedPassword });
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// [Internal] Update password di database.
        /// CATATAN: query ini menggunakan string interpolation — pertimbangkan menggantinya
        /// dengan parameterized query untuk keamanan lebih baik.
        /// </summary>
        private void UpdateUserInDatabase(UserModel user)
        {
            try
            {
                // ⚠ Sebaiknya gunakan parameterized query untuk menghindari SQL injection
                string query = $"UPDATE mst_users SET password = '{user.password}' WHERE id_user = {user.id_user}";
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }
        }

        // ===================================================================
        // LOG & RUNNING ID
        // ===================================================================

        /// <summary>Menambahkan log aksi user ke tabel tb_log.</summary>
        public bool AddLog(string idUser, string actionMessage)
        {
            try
            {
                string query = "INSERT INTO tb_log (id_user, dates, actions) VALUES (@id_user, @dates, @actions)";
                using (SqlConnection conn = new SqlConnection(_connectionString))
                using (SqlCommand cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@id_user",  idUser);
                    cmd.Parameters.AddWithValue("@dates",    DateTime.Now);
                    cmd.Parameters.AddWithValue("@actions",  actionMessage);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                }
                return true;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
                return false;
            }
        }

        /// <summary>
        /// Mengambil running ID (auto-increment) untuk dokumen tertentu via SP GET_RUNNING_ID.
        /// Format hasil: [Prefix][Year][Month][RunningId 5 digit] contoh: "OT202511000001"
        /// </summary>
        public async Task<string> GetRunningId(string code)
        {
            using (SqlConnection conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("GET_RUNNING_ID", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Code", code);

                    // Output parameters
                    var prefixParam    = AddOutputParam(cmd, "@Prefix",    SqlDbType.VarChar, 225);
                    var yearParam      = AddOutputParam(cmd, "@Year",      SqlDbType.VarChar, 225);
                    var monthParam     = AddOutputParam(cmd, "@Month",     SqlDbType.VarChar, 225);
                    var runningIdParam = AddOutputParam(cmd, "@RunningId", SqlDbType.Int);

                    await cmd.ExecuteNonQueryAsync();

                    string prefix    = prefixParam.Value    == DBNull.Value ? null : prefixParam.Value.ToString();
                    string year      = yearParam.Value      == DBNull.Value ? null : yearParam.Value.ToString();
                    string month     = monthParam.Value     == DBNull.Value ? null : monthParam.Value.ToString();
                    int    runningId = runningIdParam.Value == DBNull.Value ? 0    : (int)runningIdParam.Value;

                    if (string.IsNullOrEmpty(prefix))
                        throw new Exception($"Running ID Code '{code}' not found in mst_running_id.");

                    return $"{prefix}{year}{month}{runningId:D5}";
                }
            }
        }

        // ── Helper untuk output parameter ────────────────────────────────────
        private SqlParameter AddOutputParam(SqlCommand cmd, string name, SqlDbType type, int size = -1)
        {
            var param = size > 0
                ? new SqlParameter(name, type, size) { Direction = ParameterDirection.Output }
                : new SqlParameter(name, type)       { Direction = ParameterDirection.Output };
            cmd.Parameters.Add(param);
            return param;
        }
    }
}