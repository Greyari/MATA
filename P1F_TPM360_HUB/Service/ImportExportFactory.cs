using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Http;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;

namespace P1F_TPM360_HUB.Service
{
    /// <summary>
    /// Factory untuk proses import file Excel ke database.
    /// Menangani: Headcount, Overtime (baru), dan Overtime (clarify).
    /// </summary>
    public class ImportExportFactory
    {
        // ===================================================================
        // DEPENDENCY INJECTION
        // ===================================================================
        private readonly FileManagementService _fileManagement;
        private readonly ExcelServiceProvider _excelService;
        private readonly ILogger<ImportExportFactory> _logger;
        private readonly DatabaseAccessLayer _db;

        public ImportExportFactory(
            FileManagementService fileManagement,
            ExcelServiceProvider excelService,
            ILogger<ImportExportFactory> logger,
            DatabaseAccessLayer db)
        {
            _fileManagement = fileManagement;
            _excelService   = excelService;
            _logger         = logger;
            _db             = db;
        }

        // ===================================================================
        // IMPORT HEADCOUNT
        // ===================================================================

        /// <summary>
        /// Import data headcount dari file Excel.
        /// Proses:
        ///   1. Hapus data lama milik uploader (berdasarkan sesa_id)
        ///   2. Upload file ke server
        ///   3. Baca file menjadi DataTable
        ///   4. Bulk insert ke DB, lalu jalankan SP finalisasi
        /// </summary>
        public void ImportHeadcount(IFormFile file, string idLogin, string sesaId)
        {
            // Hapus data upload sebelumnya milik user ini
            ExecuteNonQuery(
                "DELETE FROM tmp_mst_employee WHERE sesa_upload = @sesa_id",
                cmd => cmd.Parameters.AddWithValue("@sesa_id", sesaId));

            string filePath = _fileManagement.UploadFile(file);
            if (filePath == "Upload Fail") return;

            var dataTable = _excelService.Excel_To_DataTablehc(sesaId, new FileInfo(filePath));
            BulkInsertHeadcount(dataTable, idLogin, sesaId);
        }

        /// <summary>
        /// Bulk insert data headcount ke tabel tmp_mst_employee,
        /// lalu jalankan SP SP_INSERT_MST_EMPLOYEE untuk memindahkan ke tabel utama.
        /// </summary>
        public void BulkInsertHeadcount(DataTable tbl, string idLogin, string sesaId)
        {
            // Tambahkan kolom id_login dan isi nilainya
            tbl.Columns.Add("id_login", typeof(string));
            foreach (DataRow row in tbl.Rows)
            {
                row["id_login"] = idLogin;
                if (tbl.Columns.Contains("sesa_id"))
                    row["sesa_id"] = sesaId;
            }

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                conn.Open();

                // Bulk insert ke tabel temporary
                using (var bulkCopy = new SqlBulkCopy(conn))
                {
                    bulkCopy.DestinationTableName = "dbo.tmp_mst_employee";
                    bulkCopy.ColumnMappings.Add("Column 1",  "plant");
                    bulkCopy.ColumnMappings.Add("Column 2",  "employee_date");
                    bulkCopy.ColumnMappings.Add("Column 3",  "person_id");
                    bulkCopy.ColumnMappings.Add("Column 4",  "sesa_id");
                    bulkCopy.ColumnMappings.Add("Column 5",  "employee_name");
                    bulkCopy.ColumnMappings.Add("Column 6",  "birth_date");
                    bulkCopy.ColumnMappings.Add("Column 7",  "gender");
                    bulkCopy.ColumnMappings.Add("Column 8",  "hire_date");
                    bulkCopy.ColumnMappings.Add("Column 9",  "manager");
                    bulkCopy.ColumnMappings.Add("Column 10", "hrbp_manager");
                    bulkCopy.ColumnMappings.Add("Column 11", "organization");
                    bulkCopy.ColumnMappings.Add("Column 12", "department");
                    bulkCopy.ColumnMappings.Add("Column 13", "location");
                    bulkCopy.ColumnMappings.Add("Column 14", "cost_type");
                    bulkCopy.ColumnMappings.Add("Column 15", "business_title");
                    bulkCopy.ColumnMappings.Add("Column 16", "prod_area");
                    bulkCopy.ColumnMappings.Add("Column 17", "cell");
                    bulkCopy.ColumnMappings.Add("Column 18", "cell_product_line");
                    bulkCopy.ColumnMappings.Add("Column 19", "process");
                    bulkCopy.ColumnMappings.Add("Column 20", "work_sched");
                    bulkCopy.ColumnMappings.Add("Column 21", "employee_status");
                    bulkCopy.ColumnMappings.Add("sesa_upload", "sesa_upload");
                    bulkCopy.WriteToServer(tbl);
                }

                // Pindahkan data dari tabel temporary ke tabel utama
                using (SqlCommand cmd = new SqlCommand("SP_INSERT_MST_EMPLOYEE", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@sesa_id", sesaId);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        // ===================================================================
        // IMPORT OVERTIME
        // ===================================================================

        /// <summary>
        /// Import data overtime baru dari file Excel.
        /// Proses:
        ///   1. Kosongkan tabel temporary tmp_tbl_overtime
        ///   2. Upload dan baca file
        ///   3. Bulk insert + jalankan SP CREATE_NEW_OVERTIME_UPLOAD_TESTING
        /// Mengembalikan ImportResult berisi order_id dan detail_msg.
        /// </summary>
        public ImportResult ImportOT(IFormFile file, string idLogin, string sesaId, string categorySubmitter)
        {
            ClearOvertimeTempTable(sesaId);

            string filePath = _fileManagement.UploadFile(file);
            if (filePath.StartsWith("Upload Fail"))
                return new ImportResult { DetailMsg = "Upload Fail" };

            var dataTable = _excelService.Excel_To_DataTable(sesaId, categorySubmitter, new FileInfo(filePath));
            return BulkInsertOT(dataTable, idLogin, sesaId, categorySubmitter);
        }

        /// <summary>
        /// Import data overtime untuk proses clarify (edit data yang sudah ada).
        /// Proses: sama seperti ImportOT, tapi menggunakan SP CLARIFY_OVERTIME_UPLOAD_TESTING
        /// dan menghapus data lama berdasarkan order_id.
        /// </summary>
        public ImportResult ImportOT2(IFormFile file, string idLogin, string sesaId, string categorySubmitter, string orderId)
        {
            ClearOvertimeTempTable(sesaId);

            string filePath = _fileManagement.UploadFile(file);
            if (filePath.StartsWith("Upload Fail"))
                return new ImportResult { DetailMsg = "Upload Fail" };

            var dataTable = _excelService.Excel_To_DataTable(sesaId, categorySubmitter, new FileInfo(filePath));
            return BulkInsertOT2(dataTable, idLogin, sesaId, categorySubmitter, orderId);
        }

        /// <summary>
        /// Bulk insert overtime baru ke tmp_tbl_overtime,
        /// lalu jalankan SP CREATE_NEW_OVERTIME_UPLOAD_TESTING.
        /// Mengembalikan order_id baru dan detail pesan dari SP.
        /// </summary>
        public ImportResult BulkInsertOT(DataTable tbl, string idLogin, string sesaId, string categorySubmitter)
        {
            PrepareOvertimeRows(tbl, idLogin, sesaId, categorySubmitter);

            string orderId    = null;
            string detailMsg  = "";

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                conn.Open();

                // Bulk insert ke tabel temporary
                BulkInsertOvertimeToTemp(tbl, conn);

                // Jalankan SP untuk memproses data
                using (SqlCommand cmd = new SqlCommand("CREATE_NEW_OVERTIME_UPLOAD_TESTING", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    var orderIdParam = new SqlParameter("@order_id", SqlDbType.VarChar, 255)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(orderIdParam);

                    // Baca pesan dari SP (jika ada result set)
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.HasRows && reader.Read())
                            detailMsg = reader["detail_msg"].ToString();
                    }

                    cmd.ExecuteNonQuery();
                    orderId = orderIdParam.Value.ToString();
                }

                // Update running ID
                IncrementOvertimeRunningId(conn);
            }

            _logger.LogInformation($"[ImportOT] DetailMsg={detailMsg}");
            return new ImportResult { DetailMsg = detailMsg, OrderId = orderId };
        }

        /// <summary>
        /// Bulk insert overtime clarify ke tmp_tbl_overtime,
        /// hapus data lama, tambah histori, lalu jalankan SP CLARIFY_OVERTIME_UPLOAD_TESTING.
        /// </summary>
        public ImportResult BulkInsertOT2(DataTable tbl, string idLogin, string sesaId, string categorySubmitter, string orderId)
        {
            PrepareOvertimeRows(tbl, idLogin, sesaId, categorySubmitter);

            string newOrderId = null;
            string detailMsg  = "";

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                conn.Open();

                // Bulk insert ke tabel temporary
                BulkInsertOvertimeToTemp(tbl, conn);

                // Hapus data overtime lama berdasarkan order_id
                ExecuteNonQueryOnConn(conn, "DELETE tbl_overtime WHERE order_id = @order_id",
                    cmd => cmd.Parameters.AddWithValue("@order_id", orderId));

                ExecuteNonQueryOnConn(conn, "DELETE tbl_approved_ot WHERE order_id = @order_id",
                    cmd => cmd.Parameters.AddWithValue("@order_id", orderId));

                // Tambah rekam histori clarify
                ExecuteNonQueryOnConn(conn, @"
                    INSERT INTO tbl_approved_ot_history 
                        (sesa_approved, Order_ID, appr_code, status_approved_code, approved_date)
                    VALUES 
                        (@sesa_submitter, @order_id, 'OT04', '5', GETDATE())",
                    cmd =>
                    {
                        cmd.Parameters.AddWithValue("@sesa_submitter", sesaId);
                        cmd.Parameters.AddWithValue("@order_id",       orderId);
                    });

                // Jalankan SP clarify
                using (SqlCommand cmd = new SqlCommand("CLARIFY_OVERTIME_UPLOAD_TESTING", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@order_id", orderId);

                    var orderIdParam = new SqlParameter("@orderID", SqlDbType.VarChar, 255)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(orderIdParam);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.HasRows && reader.Read())
                            detailMsg = reader["detail_msg"].ToString();
                    }

                    cmd.ExecuteNonQuery();
                    newOrderId = orderIdParam.Value.ToString();
                }

                // Update running ID
                IncrementOvertimeRunningId(conn);
            }

            _logger.LogInformation($"[ImportOT2] DetailMsg={detailMsg}");
            return new ImportResult { DetailMsg = detailMsg, OrderId = newOrderId };
        }

        // ===================================================================
        // IMPORT FILE (RENAME)
        // ===================================================================

        /// <summary>
        /// Upload file dengan nama baru ke subfolder tertentu.
        /// Mengembalikan path hasil upload dari FileManagementService.
        /// </summary>
        public string ImportFileRename(IFormFile fileDoc, string filename, string subfolder)
        {
            return _fileManagement.UploadFileRename(fileDoc, filename, subfolder);
        }

        // ===================================================================
        // HELPER PRIVATE
        // ===================================================================

        /// <summary>Mengosongkan tabel temporary overtime sebelum import baru.</summary>
        private void ClearOvertimeTempTable(string sesaId)
        {
            ExecuteNonQuery("DELETE FROM tmp_tbl_overtime",
                cmd => cmd.Parameters.AddWithValue("@sesa_id", sesaId));
        }

        /// <summary>
        /// Menambahkan kolom id_login dan mengisi nilai sesa_submitter & category_submitter
        /// pada setiap baris DataTable sebelum di-bulk insert.
        /// </summary>
        private void PrepareOvertimeRows(DataTable tbl, string idLogin, string sesaId, string categorySubmitter)
        {
            tbl.Columns.Add("id_login", typeof(string));
            foreach (DataRow row in tbl.Rows)
            {
                row["id_login"] = idLogin;
                if (tbl.Columns.Contains("sesa_id"))
                {
                    row["sesa_submitter"]     = sesaId;
                    row["category_submitter"] = categorySubmitter;
                }
            }
        }

        /// <summary>Bulk insert DataTable overtime ke tabel tmp_tbl_overtime.</summary>
        private void BulkInsertOvertimeToTemp(DataTable tbl, SqlConnection conn)
        {
            using (var bulkCopy = new SqlBulkCopy(conn))
            {
                bulkCopy.DestinationTableName = "dbo.tmp_tbl_overtime";
                bulkCopy.ColumnMappings.Add("Column 1",          "sesa_id");
                bulkCopy.ColumnMappings.Add("Column 2",          "date_ot");
                bulkCopy.ColumnMappings.Add("Column 3",          "shift");
                bulkCopy.ColumnMappings.Add("Column 4",          "total_hour");
                bulkCopy.ColumnMappings.Add("Column 5",          "reason");
                bulkCopy.ColumnMappings.Add("sesa_id",           "sesa_submitter");
                bulkCopy.ColumnMappings.Add("category_submitter","category_submitter");
                bulkCopy.WriteToServer(tbl);
            }
        }

        /// <summary>Menambah 1 pada running ID overtime di mst_running_id.</summary>
        private void IncrementOvertimeRunningId(SqlConnection conn)
        {
            ExecuteNonQueryOnConn(conn,
                "UPDATE mst_running_id SET running_id = running_id + 1 WHERE org_group = @OT",
                cmd => cmd.Parameters.AddWithValue("@OT", "Overtime master"));
        }

        /// <summary>Eksekusi query non-SELECT dengan koneksi baru (buka/tutup otomatis).</summary>
        private void ExecuteNonQuery(string query, Action<SqlCommand> addParams)
        {
            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                addParams(cmd);
                conn.Open();
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>Eksekusi query non-SELECT menggunakan koneksi yang sudah terbuka.</summary>
        private void ExecuteNonQueryOnConn(SqlConnection conn, string query, Action<SqlCommand> addParams)
        {
            using (SqlCommand cmd = new SqlCommand(query, conn))
            {
                addParams(cmd);
                cmd.ExecuteNonQuery();
            }
        }
    }
}