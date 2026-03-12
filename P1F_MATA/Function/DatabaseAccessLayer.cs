using System.Data;
using System.Dynamic;
using Microsoft.Data.SqlClient;
using P1F_MATA.Models;

namespace P1F_MATA.Function
{
    /// <summary>
    /// Data Access Layer: semua query dan koneksi database terpusat di sini.
    /// Controller tidak boleh membuka SqlConnection secara langsung.
    /// </summary>
    public class DatabaseAccessLayer
    {
        // ===================================================================
        // KONEKSI
        // ===================================================================
        private readonly string _connectionString;

        public DatabaseAccessLayer(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // ===================================================================
        // PRIVATE HELPERS
        // ===================================================================

        /// <summary>Helper generik untuk query yang mengembalikan List.</summary>
        private List<T> QueryList<T>(string query, Func<SqlDataReader, T> map, Action<SqlCommand> addParams = null)
        {
            var list = new List<T>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(query, conn);
            addParams?.Invoke(cmd);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(map(reader));
            return list;
        }

        /// <summary>Helper generik untuk query master data (Code + Name).</summary>
        public List<CodeNameModel> GetMasterData(string query, string codeColumn = null, string nameColumn = null, Action<SqlCommand> addParams = null)
            => QueryList(query, reader => new CodeNameModel
            {
                Code = codeColumn != null ? reader[codeColumn].ToString() : null,
                Name = nameColumn != null ? reader[nameColumn].ToString() : null
            }, addParams);

        // ===================================================================
        // AUTH / LOGIN
        // ===================================================================

        /// <summary>
        /// Validasi login: cek password, lalu ambil detail user.
        /// Return null jika sesa_id/password tidak cocok.
        /// </summary>
        public async Task<UserDetailModel?> ValidateLogin(string sesaId, string hashedPassword)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT COUNT(1) FROM mst_users WHERE sesa_id = @sesa_id AND password = @password", conn);
            cmd.Parameters.AddWithValue("@sesa_id",  sesaId);
            cmd.Parameters.AddWithValue("@password", hashedPassword);
            await conn.OpenAsync();
            int count = (int)await cmd.ExecuteScalarAsync();
            return count == 0 ? null : GetUserDetail(sesaId).FirstOrDefault();
        }

        /// <summary>Mengambil detail user via SP GET_USER_DETAIL.</summary>
        public List<UserDetailModel> GetUserDetail(string sesaId)
        {
            var list = new List<UserDetailModel>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("GET_USER_DETAIL", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@sesa_id", sesaId);
            using var reader = cmd.ExecuteReader();
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
            return list;
        }

        // ===================================================================
        // CHANGE PASSWORD
        // ===================================================================

        /// <summary>Cek apakah old password cocok dengan data di database.</summary>
        public async Task<bool> CheckOldPassword(string sesaId, string hashedPassword)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT COUNT(1) FROM mst_users WHERE sesa_id = @sesa_id AND password = @password", conn);
            cmd.Parameters.AddWithValue("@sesa_id",  sesaId);
            cmd.Parameters.AddWithValue("@password", hashedPassword);
            await conn.OpenAsync();
            return (int)await cmd.ExecuteScalarAsync() > 0;
        }

        /// <summary>Update password user di database (sudah dalam bentuk hash).</summary>
        public async Task UpdatePassword(string sesaId, string hashedNewPassword)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "UPDATE mst_users SET password = @newPassword WHERE sesa_id = @sesa_id", conn);
            cmd.Parameters.AddWithValue("@newPassword", hashedNewPassword);
            cmd.Parameters.AddWithValue("@sesa_id",     sesaId);
            await conn.OpenAsync();
            await cmd.ExecuteNonQueryAsync();
        }

        // ===================================================================
        // USER MANAGEMENT
        // ===================================================================

        /// <summary>Mengambil semua level dari mst_level.</summary>
        public List<DropdownModel> GetLevel()
            => QueryList("SELECT DISTINCT level FROM mst_level",
                reader => new DropdownModel { Code = reader["level"].ToString() });

        /// <summary>Menambah user baru. Password harus sudah di-hash sebelum dikirim.</summary>
        public (bool success, int rows) AddUser(string sesaId, string name, string hashedPassword, string email, string level)
        {
            try
            {
                string query = @"INSERT INTO mst_users (sesa_id, name, password, email, level, record_date)
                                 VALUES (@sesa_id, @name, @password, @email, @level, GETDATE())";
                using var conn = new SqlConnection(_connectionString);
                using var cmd  = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@sesa_id",  sesaId  ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@name",     name    ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@password", hashedPassword);
                cmd.Parameters.AddWithValue("@email",    email   ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@level",    level   ?? (object)DBNull.Value);
                conn.Open();
                int rows = cmd.ExecuteNonQuery();
                return (true, rows);
            }
            catch { return (false, 0); }
        }

        /// <summary>Update data user berdasarkan id_user.</summary>
        public (bool success, int rows) UpdateUser(int idUser, string sesaId, string name, string email, string level)
        {
            try
            {
                string query = @"UPDATE mst_users
                                 SET sesa_id = @sesa_id, name = @name, email = @email,
                                     level = @level, record_date = GETDATE()
                                 WHERE id_user = @id_user";
                using var conn = new SqlConnection(_connectionString);
                using var cmd  = new SqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@id_user", idUser);
                cmd.Parameters.AddWithValue("@sesa_id", sesaId ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@name",    name   ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@email",   email  ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@level",   level  ?? (object)DBNull.Value);
                conn.Open();
                int rows = cmd.ExecuteNonQuery();
                return (true, rows);
            }
            catch { return (false, 0); }
        }

        /// <summary>Hapus user berdasarkan id_user.</summary>
        public (bool success, int rows) DeleteUser(int id)
        {
            try
            {
                using var conn = new SqlConnection(_connectionString);
                using var cmd  = new SqlCommand("DELETE FROM mst_users WHERE id_user = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                conn.Open();
                int rows = cmd.ExecuteNonQuery();
                return (true, rows);
            }
            catch { return (false, 0); }
        }

        /// <summary>Hapus beberapa user sekaligus. ids berisi integer, aman dari SQL injection.</summary>
        public int DeleteMultipleUsers(List<int> ids)
        {
            string query = $"DELETE FROM mst_users WHERE id_user IN ({string.Join(",", ids)})";
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(query, conn);
            conn.Open();
            return cmd.ExecuteNonQuery();
        }

        /// <summary>Cari user berdasarkan keyword (sesa_id / name / email).</summary>
        public List<UserModel> GetUsers(string search)
        {
            string keyword = string.IsNullOrWhiteSpace(search) ? "" : search.Trim();
            string query   = @"SELECT id_user, sesa_id, name, level, email FROM mst_users
                               WHERE (@keyword = ''
                                   OR sesa_id LIKE '%' + @keyword + '%'
                                   OR name    LIKE '%' + @keyword + '%'
                                   OR email   LIKE '%' + @keyword + '%')
                               ORDER BY name ASC";
            return QueryList(query, r => new UserModel
            {
                id_user = r["id_user"].ToString(),
                sesa_id = r["sesa_id"].ToString(),
                name    = r["name"].ToString(),
                level   = r["level"].ToString(),
                email   = r["email"].ToString()
            }, cmd => cmd.Parameters.AddWithValue("@keyword", keyword));
        }

        /// <summary>Ambil level yang ada di database sesuai list input.</summary>
        public List<string> GetLevelEdit(List<string> levels)
        {
            var list       = new List<string>();
            var parameters = Enumerable.Range(0, levels.Count).Select(i => $"@org{i}").ToList();
            string query   = $"SELECT DISTINCT level FROM mst_level WHERE level IN ({string.Join(",", parameters)})";
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(query, conn);
            for (int i = 0; i < levels.Count; i++)
                cmd.Parameters.AddWithValue($"@org{i}", levels[i]);
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader["level"].ToString());
            return list;
        }

        // ===================================================================
        // MASTER DATA / DROPDOWN
        // ===================================================================

        public List<CodeNameModel> GetFacility()
            => GetMasterData("SELECT facility_id, facility FROM mst_facility ORDER BY facility_id ASC", "facility_id", "facility");

        public List<CodeNameModel> GetLineDashboard()
            => GetMasterData("SELECT DISTINCT line_no FROM mst_linestation ORDER BY line_no ASC", "line_no");

        public List<CodeNameModel> GetStationDashboard()
            => GetMasterData("SELECT DISTINCT station_no FROM mst_linestation ORDER BY station_no ASC", "station_no");

        public List<CodeNameModel> GetStationsByLine(string lineNo)
            => GetMasterData(
                "SELECT station_no FROM mst_linestation WHERE line_no = @line_no",
                "station_no", null,
                cmd => cmd.Parameters.AddWithValue("@line_no", lineNo));

        public List<CodeNameModel> GetLine()
            => GetMasterData("SELECT DISTINCT line_no FROM mst_linestation ORDER BY line_no ASC", "line_no");

        public List<CodeNameModel> GetStation()
            => GetMasterData("SELECT station_id, station_name FROM mst_station ORDER BY station_id ASC", "station_id", "station_name");

        public List<CodeNameModel> GetTPMTag()
            => GetMasterData("SELECT tag_id, tag_dept FROM mst_tpm_tag ORDER BY tag_id ASC", "tag_id", "tag_dept");

        public List<CodeNameModel> GetSesaOP()
            => GetMasterData("SELECT sesa_id, employee_name FROM V_OPERATOR ORDER BY sesa_id ASC", "sesa_id", "employee_name");

        public List<CodeNameModel> GetAbnType()
            => GetMasterData("SELECT abn_type_id, abn_type FROM mst_abn_type ORDER BY record_date ASC", "abn_type_id", "abn_type");

        public List<CodeNameModel> GetAbnHappen()
            => GetMasterData("SELECT abn_happen FROM mst_abn_happen ORDER BY record_date ASC", null, "abn_happen");

        public List<CodeNameModel> GetAbnRootCause()
            => GetMasterData("SELECT abn_rootcause_id, abn_rootcause FROM mst_abn_rootcause ORDER BY record_date ASC", "abn_rootcause_id", "abn_rootcause");

        public List<CodeNameModel> GetAssignedAction(string amChecklist, string facilityId)
            => GetMasterData(
                @"SELECT a.assigned_sesa, b.name
                  FROM mst_assigned_sesa AS a
                  LEFT JOIN mst_users AS b ON a.assigned_sesa = b.sesa_id
                  WHERE a.am_checklist = @AmChecklist AND a.facility_id = @FacilityId
                  ORDER BY a.record_date ASC",
                "assigned_sesa", "name", cmd =>
                {
                    cmd.Parameters.AddWithValue("@AmChecklist", amChecklist);
                    cmd.Parameters.AddWithValue("@FacilityId",  facilityId);
                });

        /// <summary>Ambil Action Owner berdasarkan facility dan tag. Return (name, sesaId).</summary>
        public (string name, string sesaId) GetActionOwner(string facilityId, string tagId)
        {
            string query = @"SELECT TOP 1 b.name, a.owner_sesa
                             FROM mst_action_owner AS a
                             LEFT JOIN mst_users AS b ON a.owner_sesa = b.sesa_id
                             WHERE a.tag_id = @TagId AND a.facility_id = @FacilityId
                             ORDER BY a.record_date DESC";
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@FacilityId", facilityId);
            cmd.Parameters.AddWithValue("@TagId",      tagId);
            using var reader = cmd.ExecuteReader();
            return reader.Read()
                ? (reader["name"].ToString(), reader["owner_sesa"].ToString())
                : (null, null);
        }

        /// <summary>Cari sesa_id berdasarkan nama user.</summary>
        public string GetSesaIdByName(string userName)
        {
            if (string.IsNullOrWhiteSpace(userName) || userName.Length < 6 || userName.Length > 50)
                return userName;
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT TOP 1 sesa_id FROM mst_users WHERE name = @UserName", conn);
            cmd.Parameters.AddWithValue("@UserName", userName);
            return cmd.ExecuteScalar()?.ToString();
        }

        // ===================================================================
        // DATE SO
        // ===================================================================

        /// <summary>Ambil range tanggal default via SP GetDateSO.</summary>
        public DateModel GetDateSO()
        {
            var date = new DateModel();
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("GetDateSO", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            conn.Open();
            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                date.FromDate    = reader["From_Date"].ToString();
                date.CurrentDate = reader["To_Date"].ToString();
            }
            return date;
        }

        // ===================================================================
        // DASHBOARD STORED PROCEDURES
        // ===================================================================

        /// <summary>
        /// Eksekutor generik untuk semua SP dashboard.
        /// Mengembalikan List dynamic — controller yang menentukan Json atau PartialView.
        /// </summary>
        public List<dynamic> ExecuteDashboardSP(
            string spName,
            string facility, string line, string station,
            string dateFrom, string dateTo, string range,
            Action<List<dynamic>, SqlDataReader> mapRow,
            string value = null, string type = null)
        {
            var list = new List<dynamic>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(spName, conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@facility",   string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
            cmd.Parameters.AddWithValue("@line_no",    string.IsNullOrEmpty(line)     ? (object)DBNull.Value : line);
            cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station)  ? (object)DBNull.Value : station);
            cmd.Parameters.AddWithValue("@range",      string.IsNullOrEmpty(range)    ? (object)DBNull.Value : range);
            cmd.Parameters.AddWithValue("@date_from",  string.IsNullOrEmpty(dateFrom) ? DateTime.Now.ToString("yyyy-MM-01") : dateFrom);
            cmd.Parameters.AddWithValue("@date_to",    string.IsNullOrEmpty(dateTo)   ? DateTime.Now.ToString("yyyy-MM-dd") : dateTo);
            if (value != null) cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);
            if (type  != null) cmd.Parameters.AddWithValue("@type",  string.IsNullOrEmpty(type)  ? (object)DBNull.Value : type);
            conn.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) mapRow(list, reader);
            return list;
        }

        // ===================================================================
        // ABN / MAT DATA
        // ===================================================================

        /// <summary>Ambil list ABN via SP GET_ABN.</summary>
        public List<ABNModel> GetABNList(string dateFrom, string dateTo, string facilityId, string sesaId, string level)
        {
            var list = new List<ABNModel>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("GET_ABN", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@date_from",   dateFrom);
            cmd.Parameters.AddWithValue("@date_to",     dateTo);
            cmd.Parameters.AddWithValue("@facility_id", facilityId);
            cmd.Parameters.AddWithValue("@sesa_id",     sesaId);
            cmd.Parameters.AddWithValue("@level",       level);
            conn.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new ABNModel
                {
                    date_find       = reader["finding_date"] != DBNull.Value ? ((DateTime)reader["finding_date"]).ToString("MMM dd, yyyy") : "-",
                    facility_id     = reader["facility_id"].ToString(),
                    facility        = reader["facility"].ToString(),
                    order_id        = reader["order_id"].ToString(),
                    line            = reader["line_no"].ToString(),
                    station_id      = reader["station_id"].ToString(),
                    tpm_tag         = reader["tag_dept"].ToString(),
                    tag_id          = reader["tag_id"].ToString(),
                    operator_sesa   = reader["operator"].ToString(),
                    findings        = reader["remark"].ToString(),
                    picture         = reader["picture_finding"].ToString(),
                    name_owner      = reader["name_owner"].ToString(),
                    status_request  = reader["status_request"].ToString(),
                    status_dynamic  = reader["status_desc"].ToString(),
                    status_action   = reader["status_action"].ToString(),
                    owner_sesa      = reader["owner_sesa"].ToString(),
                    requestor_sesa  = reader["sesa_id"].ToString(),
                    assigned_sesa   = reader["assigned_sesa"].ToString(),
                    validator_sesa  = reader["validator_sesa"].ToString(),
                    name_validator  = reader["name_validator"].ToString(),
                    image           = reader["image"].ToString(),
                    attachment_file = reader["attachment_file"].ToString(),
                    corrective      = reader["corrective"].ToString()
                });
            }
            return list;
        }

        /// <summary>Ambil detail 1 record ABN dari view V_ABNORMALITIES.</summary>
        public ABNModel GetABNDetail(string orderId)
        {
            var data = new ABNModel();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT * FROM V_ABNORMALITIES WHERE order_id = @order_id", conn);
            cmd.Parameters.AddWithValue("@order_id", orderId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return data;
            data.facility_id        = reader["facility_id"].ToString();
            data.facility           = reader["facility"].ToString();
            data.picture            = reader["picture_finding"].ToString();
            data.order_id           = reader["order_id"].ToString();
            data.abn_type           = reader["abn_type"].ToString();
            data.abn_type_id        = reader["abn_type_id"].ToString();
            data.abn_happen         = reader["abn_happen"].ToString();
            data.abn_rootcause      = reader["abn_rootcause"].ToString();
            data.abn_rootcause_id   = reader["abn_rootcause_id"].ToString();
            data.rootcause_analysis = reader["rootcause_analysis"].ToString();
            data.machine_part       = reader["machine_part"].ToString();
            data.am_checklist       = reader["am_checklist"].ToString();
            data.assigned_name      = reader["assigned_name"].ToString();
            data.corrective         = reader["corrective"].ToString();
            data.status_action      = reader["status_action"].ToString();
            data.image              = reader["image"].ToString();
            data.target_completion  = reader["target_completion"] != DBNull.Value ? ((DateTime)reader["target_completion"]).ToString("yyyy-MM-dd") : "-";
            data.completed_date     = reader["completed_date"]    != DBNull.Value ? ((DateTime)reader["completed_date"]).ToString("yyyy-MM-dd")    : "-";
            data.date_find          = reader["finding_date"]      != DBNull.Value ? ((DateTime)reader["finding_date"]).ToString("yyyy-MM-dd")      : "-";
            return data;
        }

        /// <summary>Ambil histori ABN via SP GET_ABN_HISTORY.</summary>
        public List<ABNModelHistory> GetABNHistory(string orderId)
        {
            var list = new List<ABNModelHistory>();
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand("GET_ABN_HISTORY", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@order_id", orderId);
            conn.Open();
            try
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new ABNModelHistory
                    {
                        sesa_id     = reader["sesa_id"].ToString(),
                        name        = reader["name"].ToString(),
                        ova         = reader["ova"].ToString(),
                        remark      = reader["remark"] != DBNull.Value ? reader["remark"].ToString() : "-",
                        record_date = reader["record_date"] != DBNull.Value
                                      ? Convert.ToDateTime(reader["record_date"]).ToString("MM/dd/yyyy") : "-"
                    });
                }
            }
            catch (Exception ex) { Console.WriteLine($"Error ABN history: {ex.Message}"); }
            return list;
        }

        /// <summary>
        /// Ambil data ABN untuk keperluan validasi role (ValidateAndGetAction).
        /// Return tuple: (statusRequest, requestorSesa, ownerSesa, assignedSesa, validatorSesa, found).
        /// </summary>
        public (string statusRequest, string requestorSesa, string ownerSesa, string assignedSesa, string validatorSesa, bool found)
            GetABNForValidation(string orderId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT * FROM V_ABNORMALITIES WHERE order_id = @order_id", conn);
            cmd.Parameters.AddWithValue("@order_id", orderId);
            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return (null, null, null, null, null, false);
            return (
                reader["status_request"] is DBNull ? null : reader["status_request"].ToString(),
                reader["sesa_id"]        is DBNull ? null : reader["sesa_id"].ToString(),
                reader["owner_sesa"]     is DBNull ? null : reader["owner_sesa"].ToString(),
                reader["assigned_sesa"]  is DBNull ? null : reader["assigned_sesa"].ToString(),
                reader["validator_sesa"] is DBNull ? null : reader["validator_sesa"].ToString(),
                true
            );
        }

        // ===================================================================
        // ABN STORED PROCEDURES (INSERT / UPDATE)
        // ===================================================================

        /// <summary>Simpan ABN baru via SP AddAbnormality.</summary>
        public void SaveAbnormality(
            string dateFind, string sesaId, string facilityId, string line, string station,
            string tpmTag, object sesaOp, string finding, object pictureFinding,
            string fixedByType, object abnType, object abnHappen, object abnRootcause,
            object inputRoot, object inputMachine, object inputCorrectiveAction,
            object pictureAction, object dateTarget, object amChecklist,
            object assignedAction, object statusAction, object dateCompleted,
            object actionOwnerSesa, object validatorSesa, object attachmentFile)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("AddAbnormality", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@date_find",               dateFind);
            cmd.Parameters.AddWithValue("@sesa_id",                 sesaId);
            cmd.Parameters.AddWithValue("@facility_id",             facilityId);
            cmd.Parameters.AddWithValue("@line",                    line);
            cmd.Parameters.AddWithValue("@station",                 station);
            cmd.Parameters.AddWithValue("@tpm_tag",                 tpmTag);
            cmd.Parameters.AddWithValue("@sesa_op",                 sesaOp);
            cmd.Parameters.AddWithValue("@finding",                 finding);
            cmd.Parameters.AddWithValue("@picture_finding",         pictureFinding);
            cmd.Parameters.AddWithValue("@fixed_by_type_value",     fixedByType);
            cmd.Parameters.AddWithValue("@abn_type",                abnType);
            cmd.Parameters.AddWithValue("@abn_happen",              abnHappen);
            cmd.Parameters.AddWithValue("@abn_rootcause",           abnRootcause);
            cmd.Parameters.AddWithValue("@input_root",              inputRoot);
            cmd.Parameters.AddWithValue("@input_machine",           inputMachine);
            cmd.Parameters.AddWithValue("@input_corrective_action", inputCorrectiveAction);
            cmd.Parameters.AddWithValue("@picture_action",          pictureAction);
            cmd.Parameters.AddWithValue("@date_target",             dateTarget);
            cmd.Parameters.AddWithValue("@am_checklist",            amChecklist);
            cmd.Parameters.AddWithValue("@assigned_action",         assignedAction);
            cmd.Parameters.AddWithValue("@status_action_value",     statusAction);
            cmd.Parameters.AddWithValue("@date_completed",          dateCompleted);
            cmd.Parameters.AddWithValue("@action_owner_sesa_sp",    actionOwnerSesa);
            cmd.Parameters.AddWithValue("@validated_by_sesa_sp",    validatorSesa);
            cmd.Parameters.AddWithValue("@attachment_file",         attachmentFile);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Update ABN oleh Action Owner via SP AddActionOwner.</summary>
        public void SaveActionOwner(
            string sesaId, string orderId, string facilityId,
            object abnType, object abnHappen, object abnRootcause,
            object inputRoot, object inputMachine, object amChecklist,
            object assignedAction, object inputCorrectiveAction,
            object dateTarget, string statusAction, object dateCompleted,
            object pictureAction, object attachmentFile)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("AddActionOwner", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@action_owner",            sesaId);
            cmd.Parameters.AddWithValue("@order_id",                orderId);
            cmd.Parameters.AddWithValue("@facility_id",             facilityId);
            cmd.Parameters.AddWithValue("@abn_type",                abnType);
            cmd.Parameters.AddWithValue("@abn_happen",              abnHappen);
            cmd.Parameters.AddWithValue("@abn_rootcause",           abnRootcause);
            cmd.Parameters.AddWithValue("@input_root",              inputRoot);
            cmd.Parameters.AddWithValue("@input_machine",           inputMachine);
            cmd.Parameters.AddWithValue("@am_checklist",            amChecklist);
            cmd.Parameters.AddWithValue("@assigned_action",         assignedAction);
            cmd.Parameters.AddWithValue("@input_corrective_action", inputCorrectiveAction);
            cmd.Parameters.AddWithValue("@date_target",             dateTarget);
            cmd.Parameters.AddWithValue("@status",                  statusAction);
            cmd.Parameters.AddWithValue("@date_completed",          dateCompleted);
            cmd.Parameters.AddWithValue("@picture_action",          pictureAction);
            cmd.Parameters.AddWithValue("@attachment_file",         attachmentFile);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Simpan validasi oleh Validator via SP AddValidator.</summary>
        public void SaveValidator(string sesaId, string orderId, string remark, string status, string dateCompleted)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("AddValidator", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@validator_sesa", sesaId);
            cmd.Parameters.AddWithValue("@order_id",       orderId);
            cmd.Parameters.AddWithValue("@remark",         remark);
            cmd.Parameters.AddWithValue("@status",         status);
            cmd.Parameters.AddWithValue("@date_completed", dateCompleted);
            cmd.ExecuteNonQuery();
        }

        /// <summary>Simpan tindakan Assigned Person via SP AddAssigned.</summary>
        public void SaveAssigned(
            string sesaId, string orderId, string inputCorrective,
            string dateTarget, object pictureAction, object attachmentFile)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("AddAssigned", conn);
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.Parameters.AddWithValue("@assigned_sesa",   sesaId);
            cmd.Parameters.AddWithValue("@order_id",         orderId);
            cmd.Parameters.AddWithValue("@input_corrective", inputCorrective);
            cmd.Parameters.AddWithValue("@date_target",      dateTarget);
            cmd.Parameters.AddWithValue("@picture_action",   pictureAction);
            cmd.Parameters.AddWithValue("@attachment_file",  attachmentFile);
            cmd.ExecuteNonQuery();
        }
    }
}