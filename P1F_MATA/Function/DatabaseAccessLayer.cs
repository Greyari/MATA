using System.Data;
using System.Dynamic;
using Microsoft.Data.SqlClient;
using P1F_MATA.Models;

namespace P1F_MATA.Function
{
    /// <summary>
    /// Data Access Layer (DAL) — lapisan tunggal yang mengelola semua akses ke database.
    /// 
    /// Aturan arsitektur:
    ///   - Controller TIDAK BOLEH membuka SqlConnection secara langsung.
    ///   - Semua query, stored procedure, dan koneksi database harus melalui class ini.
    ///   - Password yang masuk ke method ini harus sudah dalam bentuk hash (MD5).
    ///
    /// Struktur method dikelompokkan berdasarkan fitur:
    ///   Auth/Login → Change Password → User Management → Master Data →
    ///   Date SO → Dashboard SP → ABN Data → ABN Insert/Update
    /// </summary>
    public class DatabaseAccessLayer
    {
        // ===================================================================
        // KONEKSI
        // ===================================================================

        // Connection string diambil dari appsettings.json key "DefaultConnection"
        private readonly string _connectionString;

        /// <summary>
        /// Constructor: mengambil connection string dari konfigurasi aplikasi via DI.
        /// Pastikan key "DefaultConnection" sudah ada di appsettings.json.
        /// </summary>
        public DatabaseAccessLayer(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection");
        }

        // ===================================================================
        // PRIVATE HELPERS
        // ===================================================================

        /// <summary>
        /// Helper generik untuk mengeksekusi query SELECT dan mengembalikan List of T.
        /// Mengurangi duplikasi kode buka/tutup koneksi di setiap method.
        ///
        /// Cara pakai:
        ///   QueryList("SELECT ...", reader => new MyModel { ... }, cmd => cmd.Parameters.Add(...))
        /// </summary>
        /// <typeparam name="T">Tipe model yang dikembalikan</typeparam>
        /// <param name="query">SQL query yang akan dieksekusi</param>
        /// <param name="map">Fungsi untuk memetakan baris SqlDataReader ke object T</param>
        /// <param name="addParams">Opsional: lambda untuk menambahkan parameter ke SqlCommand</param>
        private List<T> QueryList<T>(string query, Func<SqlDataReader, T> map, Action<SqlCommand> addParams = null)
        {
            var list = new List<T>();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(query, conn);
            addParams?.Invoke(cmd); // Tambahkan parameter jika ada
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(map(reader));
            return list;
        }

        /// <summary>
        /// Helper generik untuk query master data yang hanya butuh dua kolom: Code dan Name.
        /// Digunakan oleh semua method GetFacility, GetLine, GetStation, dst.
        ///
        /// Jika codeColumn atau nameColumn null → field tersebut tidak diisi (tetap null).
        /// </summary>
        /// <param name="query">SQL query yang akan dieksekusi</param>
        /// <param name="codeColumn">Nama kolom untuk field Code (nullable)</param>
        /// <param name="nameColumn">Nama kolom untuk field Name (nullable)</param>
        /// <param name="addParams">Opsional: lambda untuk menambahkan parameter ke SqlCommand</param>
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
        /// Memvalidasi kombinasi sesa_id dan password (sudah di-hash) ke database.
        /// 
        /// Alur:
        ///   1. Cek apakah sesa_id + password cocok di tabel mst_users
        ///   2. Jika cocok → ambil detail user via SP GET_USER_DETAIL
        ///   3. Jika tidak cocok → return null
        ///
        /// Catatan: password yang dikirim harus sudah dalam bentuk MD5 hash.
        /// </summary>
        /// <param name="sesaId">Username unik user (SESA ID)</param>
        /// <param name="hashedPassword">Password yang sudah di-hash MD5</param>
        /// <returns>UserDetailModel jika valid, null jika tidak cocok</returns>
        public async Task<UserDetailModel?> ValidateLogin(string sesaId, string hashedPassword)
        {
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(
                "SELECT COUNT(1) FROM mst_users WHERE sesa_id = @sesa_id AND password = @password", conn);
            cmd.Parameters.AddWithValue("@sesa_id",  sesaId);
            cmd.Parameters.AddWithValue("@password", hashedPassword);
            await conn.OpenAsync();
            int count = (int)await cmd.ExecuteScalarAsync();

            // count == 0 berarti kombinasi sesa_id/password tidak ditemukan
            return count == 0 ? null : GetUserDetail(sesaId).FirstOrDefault();
        }

        /// <summary>
        /// Mengambil detail lengkap user dari Stored Procedure GET_USER_DETAIL.
        /// Digunakan setelah login berhasil untuk mengisi Claims cookie.
        /// Return List karena SP bisa mengembalikan lebih dari 1 baris
        /// (meski dalam praktik hanya 1 user per sesa_id).
        /// </summary>
        /// <param name="sesaId">SESA ID user yang ingin diambil detailnya</param>
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
                    level   = reader["level"].ToString(), // Contoh: "mat;mat_admin"
                    role    = reader["role"].ToString(),
                    lines   = reader["lines"].ToString()  // Line produksi yang bisa diakses user
                });
            }
            return list;
        }

        // ===================================================================
        // CHANGE PASSWORD
        // ===================================================================

        /// <summary>
        /// Memverifikasi apakah old password yang diinput user sesuai dengan yang ada di database.
        /// Digunakan sebelum mengizinkan user mengubah password baru.
        /// Password yang dikirim harus sudah di-hash MD5.
        /// </summary>
        /// <param name="sesaId">SESA ID user yang ingin ganti password</param>
        /// <param name="hashedPassword">Old password yang sudah di-hash</param>
        /// <returns>True jika cocok, false jika tidak</returns>
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

        /// <summary>
        /// Menyimpan password baru ke database.
        /// Password yang dikirim harus sudah dalam bentuk MD5 hash —
        /// method ini tidak melakukan hashing, hanya menyimpan.
        /// </summary>
        /// <param name="sesaId">SESA ID user yang passwordnya akan diubah</param>
        /// <param name="hashedNewPassword">Password baru yang sudah di-hash MD5</param>
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

        /// <summary>
        /// Mengambil semua level unik dari tabel mst_level.
        /// Digunakan untuk mengisi dropdown Level di form tambah/edit user.
        /// </summary>
        public List<DropdownModel> GetLevel()
            => QueryList("SELECT DISTINCT level FROM mst_level",
                reader => new DropdownModel { Code = reader["level"].ToString() });

        /// <summary>
        /// Menambah user baru ke tabel mst_users.
        /// Password harus sudah di-hash MD5 sebelum dikirim ke method ini.
        /// record_date otomatis diisi GETDATE() oleh SQL Server.
        ///
        /// Return: (success: bool, rows: int)
        ///   - success = true jika tidak ada exception
        ///   - rows = jumlah baris yang berhasil diinsert (0 atau 1)
        /// </summary>
        public (bool success, int rows) AddUser(string sesaId, string name, string hashedPassword, string email, string level)
        {
            try
            {
                string query = @"INSERT INTO mst_users (sesa_id, name, password, email, level, record_date)
                                 VALUES (@sesa_id, @name, @password, @email, @level, GETDATE())";
                using var conn = new SqlConnection(_connectionString);
                using var cmd  = new SqlCommand(query, conn);

                // Null-coalescing ke DBNull agar kolom nullable di SQL tidak error
                cmd.Parameters.AddWithValue("@sesa_id",  sesaId  ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@name",     name    ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@password", hashedPassword);
                cmd.Parameters.AddWithValue("@email",    email   ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@level",    level   ?? (object)DBNull.Value);
                conn.Open();
                int rows = cmd.ExecuteNonQuery();
                return (true, rows);
            }
            catch { return (false, 0); } // Exception ditangkap di controller
        }

        /// <summary>
        /// Mengupdate data user yang sudah ada berdasarkan id_user.
        /// Password tidak diupdate di sini — gunakan UpdatePassword() untuk itu.
        /// record_date diperbarui ke GETDATE() setiap kali data diubah.
        ///
        /// Return: (success: bool, rows: int)
        /// </summary>
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

        /// <summary>
        /// Menghapus satu user dari database berdasarkan id_user.
        /// Operasi ini permanen — tidak ada soft delete.
        ///
        /// Return: (success: bool, rows: int)
        ///   - rows == 0 berarti user tidak ditemukan (sudah dihapus sebelumnya)
        /// </summary>
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

        /// <summary>
        /// Menghapus beberapa user sekaligus menggunakan IN clause.
        /// Lebih efisien daripada memanggil DELETE satu per satu.
        ///
        /// Keamanan: ids berisi integer (bukan string), sehingga aman dari SQL injection
        /// meskipun nilai-nilainya digabung langsung ke query string.
        ///
        /// Return: jumlah baris yang berhasil dihapus.
        /// </summary>
        /// <param name="ids">List integer id_user yang akan dihapus</param>
        public int DeleteMultipleUsers(List<int> ids)
        {
            // Integer tidak bisa mengandung karakter berbahaya, aman digabung langsung
            string query = $"DELETE FROM mst_users WHERE id_user IN ({string.Join(",", ids)})";
            using var conn = new SqlConnection(_connectionString);
            using var cmd  = new SqlCommand(query, conn);
            conn.Open();
            return cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Mencari user berdasarkan keyword di kolom sesa_id, name, atau email.
        /// Jika keyword kosong → mengembalikan semua user (tanpa filter).
        /// Hasil diurutkan berdasarkan nama (A-Z).
        /// </summary>
        /// <param name="search">Keyword pencarian, bisa kosong untuk tampilkan semua</param>
        public List<UserModel> GetUsers(string search)
        {
            // Normalisasi keyword — kosong berarti tampilkan semua
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

        /// <summary>
        /// Mengambil level dari tabel mst_level yang cocok dengan list input.
        /// Digunakan untuk memvalidasi level user saat form edit dibuka,
        /// agar checkbox/dropdown level terisi dengan nilai yang tersimpan di DB.
        ///
        /// Menggunakan parameterized query dengan nama dinamis (@org0, @org1, dst)
        /// untuk menghindari SQL injection meski jumlah parameter bervariasi.
        /// </summary>
        /// <param name="levels">List string level yang akan dicari di database</param>
        public List<string> GetLevelEdit(List<string> levels)
        {
            var list = new List<string>();

            // Buat placeholder parameter: @org0, @org1, @org2, dst (sesuai jumlah level)
            var parameters = Enumerable.Range(0, levels.Count).Select(i => $"@org{i}").ToList();
            string query   = $"SELECT DISTINCT level FROM mst_level WHERE level IN ({string.Join(",", parameters)})";

            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand(query, conn);

            // Bind nilai ke setiap placeholder parameter
            for (int i = 0; i < levels.Count; i++)
                cmd.Parameters.AddWithValue($"@org{i}", levels[i]);

            using var reader = cmd.ExecuteReader();
            while (reader.Read()) list.Add(reader["level"].ToString());
            return list;
        }

        // ===================================================================
        // MASTER DATA / DROPDOWN
        // ===================================================================
        // Semua method di bawah menggunakan GetMasterData() sebagai helper generik.
        // Return: List<CodeNameModel> dengan field Code dan Name.

        /// <summary>Daftar fasilitas/pabrik dari mst_facility, diurutkan berdasarkan facility_id.</summary>
        public List<CodeNameModel> GetFacility()
            => GetMasterData("SELECT facility_id, facility FROM mst_facility ORDER BY facility_id ASC", "facility_id", "facility");

        /// <summary>Daftar line produksi unik dari mst_linestation, untuk dropdown di Dashboard.</summary>
        public List<CodeNameModel> GetLineDashboard()
            => GetMasterData("SELECT DISTINCT line_no FROM mst_linestation ORDER BY line_no ASC", "line_no");

        /// <summary>Daftar stasiun kerja unik dari mst_linestation, untuk dropdown di Dashboard.</summary>
        public List<CodeNameModel> GetStationDashboard()
            => GetMasterData("SELECT DISTINCT station_no FROM mst_linestation ORDER BY station_no ASC", "station_no");

        /// <summary>
        /// Daftar stasiun yang berelasi dengan line tertentu.
        /// Digunakan untuk cascade dropdown Line → Station di form Observation dan Dashboard.
        /// </summary>
        /// <param name="lineNo">Nomor line yang dipilih user</param>
        public List<CodeNameModel> GetStationsByLine(string lineNo)
            => GetMasterData(
                "SELECT station_no FROM mst_linestation WHERE line_no = @line_no",
                "station_no", null,
                cmd => cmd.Parameters.AddWithValue("@line_no", lineNo));

        /// <summary>Daftar line produksi unik untuk dropdown di form Observation.</summary>
        public List<CodeNameModel> GetLine()
            => GetMasterData("SELECT DISTINCT line_no FROM mst_linestation ORDER BY line_no ASC", "line_no");

        /// <summary>Daftar stasiun kerja lengkap (id + nama) dari mst_station.</summary>
        public List<CodeNameModel> GetStation()
            => GetMasterData("SELECT station_id, station_name FROM mst_station ORDER BY station_id ASC", "station_id", "station_name");

        /// <summary>Daftar TPM tag (departemen) dari mst_tpm_tag untuk dropdown form Observation.</summary>
        public List<CodeNameModel> GetTPMTag()
            => GetMasterData("SELECT tag_id, tag_dept FROM mst_tpm_tag ORDER BY tag_id ASC", "tag_id", "tag_dept");

        /// <summary>Daftar operator (SESA ID + nama) dari view V_OPERATOR.</summary>
        public List<CodeNameModel> GetSesaOP()
            => GetMasterData("SELECT sesa_id, employee_name FROM V_OPERATOR ORDER BY sesa_id ASC", "sesa_id", "employee_name");

        /// <summary>Daftar tipe abnormalitas dari mst_abn_type, diurutkan berdasarkan tanggal input.</summary>
        public List<CodeNameModel> GetAbnType()
            => GetMasterData("SELECT abn_type_id, abn_type FROM mst_abn_type ORDER BY record_date ASC", "abn_type_id", "abn_type");

        /// <summary>Daftar jenis kejadian abnormal dari mst_abn_happen.</summary>
        public List<CodeNameModel> GetAbnHappen()
            => GetMasterData("SELECT abn_happen FROM mst_abn_happen ORDER BY record_date ASC", null, "abn_happen");

        /// <summary>Daftar root cause (penyebab utama) abnormalitas dari mst_abn_rootcause.</summary>
        public List<CodeNameModel> GetAbnRootCause()
            => GetMasterData("SELECT abn_rootcause_id, abn_rootcause FROM mst_abn_rootcause ORDER BY record_date ASC", "abn_rootcause_id", "abn_rootcause");

        /// <summary>
        /// Mengambil daftar SESA yang di-assign untuk kombinasi AM Checklist dan Fasilitas tertentu.
        /// Join ke mst_users untuk mendapatkan nama lengkap masing-masing assigned person.
        /// Digunakan untuk mengisi dropdown "Assigned To" di form Observation.
        /// </summary>
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

        /// <summary>
        /// Mengambil Action Owner terbaru berdasarkan kombinasi facility dan TPM tag.
        /// Menggunakan TOP 1 ORDER BY record_date DESC untuk mendapatkan yang paling baru.
        ///
        /// Return: tuple (name, sesaId) — nama lengkap dan SESA ID Action Owner.
        /// Jika tidak ditemukan → return (null, null).
        /// </summary>
        /// <param name="facilityId">ID fasilitas</param>
        /// <param name="tagId">ID TPM tag</param>
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

        /// <summary>
        /// Mencari SESA ID berdasarkan nama lengkap user.
        /// Digunakan saat user mengetik nama validator di form input ABN.
        ///
        /// Validasi panjang nama (6-50 karakter) untuk mencegah query tidak perlu.
        /// Jika nama tidak valid atau tidak ditemukan → kembalikan input aslinya.
        /// </summary>
        /// <param name="userName">Nama lengkap user yang ingin dicari SESA ID-nya</param>
        public string GetSesaIdByName(string userName)
        {
            // Abaikan jika nama terlalu pendek/panjang atau kosong
            if (string.IsNullOrWhiteSpace(userName) || userName.Length < 6 || userName.Length > 50)
                return userName;

            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT TOP 1 sesa_id FROM mst_users WHERE name = @UserName", conn);
            cmd.Parameters.AddWithValue("@UserName", userName);

            // Kembalikan null jika tidak ada record yang cocok
            return cmd.ExecuteScalar()?.ToString();
        }

        // ===================================================================
        // DATE SO
        // ===================================================================

        /// <summary>
        /// Mengambil range tanggal default (From Date dan To Date) via SP GetDateSO.
        /// Digunakan sebagai nilai awal filter tanggal di halaman Observation dan Dashboard.
        /// Nilai dikembalikan sebagai DateModel dengan property FromDate dan CurrentDate.
        /// </summary>
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
                date.FromDate    = reader["From_Date"].ToString(); // Tanggal awal default
                date.CurrentDate = reader["To_Date"].ToString();   // Tanggal akhir default (biasanya hari ini)
            }
            return date;
        }

        // ===================================================================
        // DASHBOARD STORED PROCEDURES
        // ===================================================================

        /// <summary>
        /// Eksekutor generik untuk semua Stored Procedure dashboard.
        /// Dipanggil oleh DashboardController melalui RunDashboardSP().
        ///
        /// Semua parameter filter yang kosong/null dikonversi ke DBNull
        /// agar SP di SQL Server bisa menangani filter opsional dengan benar.
        ///
        /// Tanggal default:
        ///   - date_from kosong → awal bulan ini (yyyy-MM-01)
        ///   - date_to kosong   → hari ini (yyyy-MM-dd)
        ///
        /// Parameter value dan type hanya dikirim jika tidak null
        /// (digunakan oleh endpoint detail, tidak oleh chart summary).
        /// </summary>
        /// <param name="spName">Nama Stored Procedure yang akan dipanggil</param>
        /// <param name="mapRow">Lambda untuk memetakan setiap baris hasil ke List dynamic</param>
        /// <param name="value">Filter nilai tambahan untuk detail SP (opsional)</param>
        /// <param name="type">Filter tipe tambahan untuk detail SP (opsional)</param>
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

            // Konversi string kosong ke DBNull agar SP bisa handle filter "semua"
            cmd.Parameters.AddWithValue("@facility",   string.IsNullOrEmpty(facility) ? (object)DBNull.Value : facility);
            cmd.Parameters.AddWithValue("@line_no",    string.IsNullOrEmpty(line)     ? (object)DBNull.Value : line);
            cmd.Parameters.AddWithValue("@station_id", string.IsNullOrEmpty(station)  ? (object)DBNull.Value : station);
            cmd.Parameters.AddWithValue("@range",      string.IsNullOrEmpty(range)    ? (object)DBNull.Value : range);

            // Tanggal default: awal bulan dan hari ini jika tidak diisi
            cmd.Parameters.AddWithValue("@date_from",  string.IsNullOrEmpty(dateFrom) ? DateTime.Now.ToString("yyyy-MM-01") : dateFrom);
            cmd.Parameters.AddWithValue("@date_to",    string.IsNullOrEmpty(dateTo)   ? DateTime.Now.ToString("yyyy-MM-dd") : dateTo);

            // Parameter opsional — hanya ditambahkan jika diperlukan oleh SP detail
            if (value != null) cmd.Parameters.AddWithValue("@value", string.IsNullOrEmpty(value) ? (object)DBNull.Value : value);
            if (type  != null) cmd.Parameters.AddWithValue("@type",  string.IsNullOrEmpty(type)  ? (object)DBNull.Value : type);

            conn.Open();
            using var reader = cmd.ExecuteReader();
            while (reader.Read()) mapRow(list, reader); // Petakan setiap baris ke object dynamic
            return list;
        }

        // ===================================================================
        // ABN / MAT DATA
        // ===================================================================

        /// <summary>
        /// Mengambil daftar ABN via SP GET_ABN sesuai filter tanggal, fasilitas, dan level user.
        /// SP akan memfilter data berdasarkan level:
        ///   - Level "mat"       → hanya ABN milik user sendiri (requestor_sesa = sesaId)
        ///   - Level "mat_admin" / "superadmin" → semua ABN di fasilitas tersebut
        /// </summary>
        /// <param name="dateFrom">Tanggal awal filter</param>
        /// <param name="dateTo">Tanggal akhir filter</param>
        /// <param name="facilityId">ID fasilitas filter</param>
        /// <param name="sesaId">SESA ID user yang sedang login</param>
        /// <param name="level">Level user untuk menentukan scope data yang bisa dilihat</param>
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
                    // Format tanggal temuan: "Jan 15, 2025" atau "-" jika null
                    date_find       = reader["finding_date"] != DBNull.Value ? ((DateTime)reader["finding_date"]).ToString("MMM dd, yyyy") : "-",
                    facility_id     = reader["facility_id"].ToString(),
                    facility        = reader["facility"].ToString(),
                    order_id        = reader["order_id"].ToString(),       // ID unik ABN
                    line            = reader["line_no"].ToString(),
                    station_id      = reader["station_id"].ToString(),
                    tpm_tag         = reader["tag_dept"].ToString(),        // Nama departemen TPM
                    tag_id          = reader["tag_id"].ToString(),
                    operator_sesa   = reader["operator"].ToString(),        // SESA ID operator yang melapor
                    findings        = reader["remark"].ToString(),          // Deskripsi temuan
                    picture         = reader["picture_finding"].ToString(), // Nama file foto BEFORE
                    name_owner      = reader["name_owner"].ToString(),      // Nama Action Owner
                    status_request  = reader["status_request"].ToString(),  // Kode status (0,1,2,3,4)
                    status_dynamic  = reader["status_desc"].ToString(),     // Deskripsi status untuk UI
                    status_action   = reader["status_action"].ToString(),
                    owner_sesa      = reader["owner_sesa"].ToString(),
                    requestor_sesa  = reader["sesa_id"].ToString(),         // SESA ID pelapor ABN
                    assigned_sesa   = reader["assigned_sesa"].ToString(),
                    validator_sesa  = reader["validator_sesa"].ToString(),
                    name_validator  = reader["name_validator"].ToString(),
                    image           = reader["image"].ToString(),            // Nama file foto AFTER
                    attachment_file = reader["attachment_file"].ToString(),  // Nama file PDF lampiran
                    corrective      = reader["corrective"].ToString()        // Tindakan korektif
                });
            }
            return list;
        }

        /// <summary>
        /// Mengambil detail lengkap 1 record ABN dari view V_ABNORMALITIES.
        /// Digunakan saat user klik baris di tabel untuk membuka modal detail.
        /// Jika order_id tidak ditemukan → kembalikan ABNModel kosong (bukan null).
        /// </summary>
        /// <param name="orderId">ID unik ABN yang ingin diambil detailnya</param>
        public ABNModel GetABNDetail(string orderId)
        {
            var data = new ABNModel();
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT * FROM V_ABNORMALITIES WHERE order_id = @order_id", conn);
            cmd.Parameters.AddWithValue("@order_id", orderId);
            using var reader = cmd.ExecuteReader();

            // Jika tidak ada data → kembalikan model kosong daripada null
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

            // Format tanggal: "yyyy-MM-dd" untuk input date HTML, atau "-" jika null
            data.target_completion  = reader["target_completion"] != DBNull.Value ? ((DateTime)reader["target_completion"]).ToString("yyyy-MM-dd") : "-";
            data.completed_date     = reader["completed_date"]    != DBNull.Value ? ((DateTime)reader["completed_date"]).ToString("yyyy-MM-dd")    : "-";
            data.date_find          = reader["finding_date"]      != DBNull.Value ? ((DateTime)reader["finding_date"]).ToString("yyyy-MM-dd")      : "-";
            return data;
        }

        /// <summary>
        /// Mengambil histori perubahan status ABN via SP GET_ABN_HISTORY.
        /// Menampilkan siapa melakukan aksi apa dan kapan
        /// (Requestor → Action Owner → Assigned Person → Validator).
        ///
        /// Error ditangkap dengan try-catch dan dicetak ke Console
        /// agar histori yang gagal tidak menghentikan proses utama.
        /// </summary>
        /// <param name="orderId">ID ABN yang ingin dilihat historinya</param>
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
                        ova         = reader["ova"].ToString(),          // Jenis aksi (OVA = Over/Action)
                        remark      = reader["remark"] != DBNull.Value ? reader["remark"].ToString() : "-",
                        record_date = reader["record_date"] != DBNull.Value
                                      ? Convert.ToDateTime(reader["record_date"]).ToString("MM/dd/yyyy") : "-"
                    });
                }
            }
            catch (Exception ex)
            {
                // Log error ke Console — tidak throw agar UI tidak crash jika histori gagal dimuat
                Console.WriteLine($"Error ABN history: {ex.Message}");
            }
            return list;
        }

        /// <summary>
        /// Mengambil data ABN yang diperlukan untuk menentukan aksi user (ValidateAndGetAction).
        /// Hanya mengambil kolom-kolom yang relevan untuk validasi role:
        ///   statusRequest, requestorSesa, ownerSesa, assignedSesa, validatorSesa.
        ///
        /// Return tuple dengan flag 'found' agar caller bisa membedakan
        /// antara "record tidak ada" vs "semua nilai null".
        /// </summary>
        /// <param name="orderId">ID ABN yang akan divalidasi</param>
        /// <returns>Tuple berisi data validasi dan flag found (true/false)</returns>
        public (string statusRequest, string requestorSesa, string ownerSesa, string assignedSesa, string validatorSesa, bool found)
            GetABNForValidation(string orderId)
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = new SqlCommand("SELECT * FROM V_ABNORMALITIES WHERE order_id = @order_id", conn);
            cmd.Parameters.AddWithValue("@order_id", orderId);
            using var reader = cmd.ExecuteReader();

            // Jika tidak ditemukan → kembalikan semua null dengan found = false
            if (!reader.Read()) return (null, null, null, null, null, false);

            // Gunakan 'is DBNull' check agar tidak throw exception pada kolom nullable
            return (
                reader["status_request"] is DBNull ? null : reader["status_request"].ToString(),
                reader["sesa_id"]        is DBNull ? null : reader["sesa_id"].ToString(),        // requestorSesa
                reader["owner_sesa"]     is DBNull ? null : reader["owner_sesa"].ToString(),
                reader["assigned_sesa"]  is DBNull ? null : reader["assigned_sesa"].ToString(),
                reader["validator_sesa"] is DBNull ? null : reader["validator_sesa"].ToString(),
                true // found = true
            );
        }

        // ===================================================================
        // ABN STORED PROCEDURES (INSERT / UPDATE)
        // ===================================================================

        /// <summary>
        /// Menyimpan record ABN baru ke database via SP AddAbnormality.
        /// Dipanggil dari MATController.AddInput() setelah semua file diupload.
        ///
        /// Semua parameter bertipe object (bukan string) untuk mengakomodasi
        /// nilai DBNull yang dikirim dari controller via helper DbVal() dan ParseDate().
        /// </summary>
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
            cmd.Parameters.AddWithValue("@date_find",               dateFind);         // Tanggal temuan
            cmd.Parameters.AddWithValue("@sesa_id",                 sesaId);           // SESA ID pelapor
            cmd.Parameters.AddWithValue("@facility_id",             facilityId);
            cmd.Parameters.AddWithValue("@line",                    line);
            cmd.Parameters.AddWithValue("@station",                 station);
            cmd.Parameters.AddWithValue("@tpm_tag",                 tpmTag);
            cmd.Parameters.AddWithValue("@sesa_op",                 sesaOp);           // SESA ID operator (nullable)
            cmd.Parameters.AddWithValue("@finding",                 finding);          // Deskripsi temuan
            cmd.Parameters.AddWithValue("@picture_finding",         pictureFinding);   // Nama file foto BEFORE
            cmd.Parameters.AddWithValue("@fixed_by_type_value",     fixedByType);      // "Fixed by myself" atau nilai lain
            cmd.Parameters.AddWithValue("@abn_type",                abnType);
            cmd.Parameters.AddWithValue("@abn_happen",              abnHappen);
            cmd.Parameters.AddWithValue("@abn_rootcause",           abnRootcause);
            cmd.Parameters.AddWithValue("@input_root",              inputRoot);        // Analisis root cause
            cmd.Parameters.AddWithValue("@input_machine",           inputMachine);     // Bagian mesin yang terdampak
            cmd.Parameters.AddWithValue("@input_corrective_action", inputCorrectiveAction);
            cmd.Parameters.AddWithValue("@picture_action",          pictureAction);    // Nama file foto AFTER (nullable)
            cmd.Parameters.AddWithValue("@date_target",             dateTarget);       // Target selesai (nullable)
            cmd.Parameters.AddWithValue("@am_checklist",            amChecklist);
            cmd.Parameters.AddWithValue("@assigned_action",         assignedAction);   // SESA ID assigned person
            cmd.Parameters.AddWithValue("@status_action_value",     statusAction);
            cmd.Parameters.AddWithValue("@date_completed",          dateCompleted);    // Tanggal selesai (nullable)
            cmd.Parameters.AddWithValue("@action_owner_sesa_sp",    actionOwnerSesa);
            cmd.Parameters.AddWithValue("@validated_by_sesa_sp",    validatorSesa);    // SESA ID validator (nullable)
            cmd.Parameters.AddWithValue("@attachment_file",         attachmentFile);   // Nama file PDF (nullable)
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Mengupdate data ABN oleh Action Owner via SP AddActionOwner.
        /// Dipanggil dari MATController.AddInput2() setelah Action Owner mengisi form tindakan.
        /// </summary>
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
            cmd.Parameters.AddWithValue("@action_owner",            sesaId);     // SESA ID Action Owner yang login
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
            cmd.Parameters.AddWithValue("@status",                  statusAction); // Status baru setelah action owner mengisi
            cmd.Parameters.AddWithValue("@date_completed",          dateCompleted);
            cmd.Parameters.AddWithValue("@picture_action",          pictureAction);   // Foto AFTER
            cmd.Parameters.AddWithValue("@attachment_file",         attachmentFile);  // PDF lampiran
            cmd.ExecuteNonQuery();
        }

        /// <summary>
        /// Menyimpan hasil validasi oleh Validator via SP AddValidator.
        /// Dipanggil dari MATController.AddInput3().
        /// Status yang dikirim bisa "Approved" (ABN closed) atau "Rejected" (dikembalikan ke Action Owner).
        /// </summary>
        /// <param name="sesaId">SESA ID Validator yang sedang login</param>
        /// <param name="orderId">ID ABN yang divalidasi</param>
        /// <param name="remark">Catatan dari Validator (alasan approve/reject)</param>
        /// <param name="status">Hasil validasi: "Approved" atau "Rejected"</param>
        /// <param name="dateCompleted">Tanggal selesai jika di-approve</param>
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

        /// <summary>
        /// Menyimpan laporan tindakan oleh Assigned Person via SP AddAssigned.
        /// Dipanggil dari MATController.AddAssigned() setelah Assigned Person menyelesaikan pekerjaan.
        /// </summary>
        /// <param name="sesaId">SESA ID Assigned Person yang sedang login</param>
        /// <param name="orderId">ID ABN yang ditangani</param>
        /// <param name="inputCorrective">Deskripsi tindakan korektif yang sudah dilakukan</param>
        /// <param name="dateTarget">Target tanggal penyelesaian</param>
        /// <param name="pictureAction">Nama file foto bukti tindakan AFTER (nullable)</param>
        /// <param name="attachmentFile">Nama file PDF lampiran (nullable)</param>
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