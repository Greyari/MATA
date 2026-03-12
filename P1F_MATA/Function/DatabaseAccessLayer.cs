using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using P1F_MATA.Models;

namespace P1F_MATA.Function
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
    }
}