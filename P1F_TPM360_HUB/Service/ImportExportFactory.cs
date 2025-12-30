using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;

namespace P1F_TPM360_HUB.Service
{
    public class ImportExportFactory
    {
        //string dbString = new DatabaseAccessLayer().ConnectionString;

        public readonly string ConnectionString = new DatabaseAccessLayer().ConnectionString;
        //public readonly string ConnectionString = "Data Source=10.155.129.223;Initial Catalog=BLP_Material_Tracking;Persist Security Info=True;User ID=semb;Password=Semb@123;MultipleActiveResultSets=true";
        //private readonly FileManagementService _fileManagement;
        //private readonly ExcelServiceProvider _excelService;

        //public ImportExportFactory(FileManagementService fileManagement,
        //    ExcelServiceProvider excelService)
        //{
        //    _fileManagement = fileManagement;
        //    _excelService = excelService;
        //}

        //public ImportExportFactory() :
        //    this(new FileManagementService(),
        //        new ExcelServiceProvider())
        //{
        //}
         private readonly FileManagementService _fileManagement;
        private readonly ExcelServiceProvider _excelService;
        private readonly ILogger<ImportExportFactory> _logger;

        public ImportExportFactory(FileManagementService fileManagement,
            ExcelServiceProvider excelService,
            ILogger<ImportExportFactory> logger)
        {
            _fileManagement = fileManagement;
            _excelService = excelService;
            _logger = logger;
        }

        public ImportExportFactory() :
            this(new FileManagementService(),
                new ExcelServiceProvider(),
                new LoggerFactory().CreateLogger<ImportExportFactory>())
        {
        }




        public void ImportHeadcount(IFormFile file, string id_login, string sesa_id)
        {
            string query = "DELETE FROM tmp_mst_employee WHERE sesa_upload=@sesa_id";
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
            }

            var uploadedFilePath = _fileManagement.UploadFile(file);

            if (uploadedFilePath == "Upload Fail")
            {
                return;
            }
            var fileInfo = new FileInfo(uploadedFilePath);

            var dataTable = _excelService.Excel_To_DataTablehc(sesa_id, fileInfo);

            BulkInsertHeadcount(dataTable, id_login, sesa_id);
        }


        public void BulkInsertHeadcount(DataTable tbl, string id_login, string sesa_id)
        {
            tbl.Columns.Add("id_login", typeof(string));

            foreach (DataRow row in tbl.Rows)
            {
                row["id_login"] = id_login;
                // Pastikan kolom sesa_id sudah ada dan diisi dengan benar
                if (tbl.Columns.Contains("sesa_id"))
                {
                    row["sesa_id"] = sesa_id;
                }
            }

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(conn))
                {
                    sqlBulkCopy.DestinationTableName = "dbo.tmp_mst_employee";

                    sqlBulkCopy.ColumnMappings.Add("Column 1", "plant");
                    sqlBulkCopy.ColumnMappings.Add("Column 2", "employee_date");
                    sqlBulkCopy.ColumnMappings.Add("Column 3", "person_id");
                    sqlBulkCopy.ColumnMappings.Add("Column 4", "sesa_id");
                    sqlBulkCopy.ColumnMappings.Add("Column 5", "employee_name");
                    sqlBulkCopy.ColumnMappings.Add("Column 6", "birth_date");
                    sqlBulkCopy.ColumnMappings.Add("Column 7", "gender");
                    sqlBulkCopy.ColumnMappings.Add("Column 8", "hire_date");
                    sqlBulkCopy.ColumnMappings.Add("Column 9", "manager");
                    sqlBulkCopy.ColumnMappings.Add("Column 10", "hrbp_manager");
                    sqlBulkCopy.ColumnMappings.Add("Column 11", "organization");
                    sqlBulkCopy.ColumnMappings.Add("Column 12", "department");
                    sqlBulkCopy.ColumnMappings.Add("Column 13", "location");
                    sqlBulkCopy.ColumnMappings.Add("Column 14", "cost_type");
                    sqlBulkCopy.ColumnMappings.Add("Column 15", "business_title");
                    sqlBulkCopy.ColumnMappings.Add("Column 16", "prod_area");
                    sqlBulkCopy.ColumnMappings.Add("Column 17", "cell");
                    sqlBulkCopy.ColumnMappings.Add("Column 18", "cell_product_line");
                    sqlBulkCopy.ColumnMappings.Add("Column 19", "process");
                    sqlBulkCopy.ColumnMappings.Add("Column 20", "work_sched");
                    sqlBulkCopy.ColumnMappings.Add("Column 21", "employee_status");
                    sqlBulkCopy.ColumnMappings.Add("sesa_upload", "sesa_upload");

                    conn.Open();
                    sqlBulkCopy.WriteToServer(tbl);
                    conn.Close();
                }
            }

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("SP_INSERT_MST_EMPLOYEE", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
                cmd.ExecuteNonQuery();
                conn.Close();
            }
        }


        //public void ImportOT(IFormFile file, string id_login, string sesa_id,string category_submitter)
        //{
        //    string query = "DELETE FROM tmp_tbl_overtime";
        //    using (SqlConnection conn = new SqlConnection(ConnectionString))
        //    {
        //        using (var cmd = new SqlCommand(query, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
        //            conn.Open();
        //            cmd.ExecuteNonQuery();
        //            conn.Close();
        //        }
        //    }

        //    var uploadedFilePath = _fileManagement.UploadFile(file);

        //    if (uploadedFilePath == "Upload Fail")
        //    {
        //        return;
        //    }
        //    var fileInfo = new FileInfo(uploadedFilePath);

        //    var dataTable = _excelService.Excel_To_DataTable(sesa_id, category_submitter, fileInfo);

        //    BulkInsertOT(dataTable, id_login, sesa_id, category_submitter);
        //}

        //public string ImportOT(IFormFile file, string id_login, string sesa_id, string category_submitter)
        //{
        //    string query = "DELETE FROM tmp_tbl_overtime";
        //    using (SqlConnection conn = new SqlConnection(ConnectionString))
        //    {
        //        using (var cmd = new SqlCommand(query, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
        //            conn.Open();
        //            cmd.ExecuteNonQuery();
        //            conn.Close();
        //        }
        //    }

        //    var uploadedFilePath = _fileManagement.UploadFile(file);

        //    if (uploadedFilePath == "Upload Fail")
        //    {
        //        return null; // or handle the failure case as needed
        //    }
        //    var fileInfo = new FileInfo(uploadedFilePath);

        //    var dataTable = _excelService.Excel_To_DataTable(sesa_id, category_submitter, fileInfo);

        //    // Call BulkInsertOT and capture the order_id
        //    return BulkInsertOT(dataTable, id_login, sesa_id, category_submitter);
        //}

        public ImportResult ImportOT(IFormFile file, string id_login, string sesa_id, string category_submitter)
        {
            string query = "DELETE FROM tmp_tbl_overtime";
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
            }

            var uploadedFilePath = _fileManagement.UploadFile(file);

            if (uploadedFilePath == "Upload Fail")
            {
                return new ImportResult { DetailMsg = "Upload Fail" };
            }
            var fileInfo = new FileInfo(uploadedFilePath);
            var dataTable = _excelService.Excel_To_DataTable(sesa_id, category_submitter, fileInfo);
            var result = BulkInsertOT(dataTable, id_login, sesa_id, category_submitter);
            return result;
        }
        
        public ImportResult ImportOT2(IFormFile file, string id_login, string sesa_id, string category_submitter, string order_id)
        {
            string query = "DELETE FROM tmp_tbl_overtime";
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                using (var cmd = new SqlCommand(query, conn))
                {
                    cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
                    conn.Open();
                    cmd.ExecuteNonQuery();
                    conn.Close();
                }
            }

            var uploadedFilePath = _fileManagement.UploadFile(file);

            if (uploadedFilePath == "Upload Fail")
            {
                return new ImportResult { DetailMsg = "Upload Fail" };
            }
            var fileInfo = new FileInfo(uploadedFilePath);
            var dataTable = _excelService.Excel_To_DataTable(sesa_id, category_submitter, fileInfo);
            var result = BulkInsertOT2(dataTable, id_login, sesa_id, category_submitter, order_id);
            return result;
        }

        //public void ImportOT2(IFormFile file, string id_login, string sesa_id, string category_submitter, string order_id)
        //{
        //    string query = "DELETE FROM tmp_tbl_overtime";
        //    using (SqlConnection conn = new SqlConnection(ConnectionString))
        //    {
        //        using (var cmd = new SqlCommand(query, conn))
        //        {
        //            cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
        //            conn.Open();
        //            cmd.ExecuteNonQuery();
        //            conn.Close();
        //        }
        //    }

        //    var uploadedFilePath = _fileManagement.UploadFile(file);

        //    if (uploadedFilePath == "Upload Fail")
        //    {
        //        return;
        //    }
        //    var fileInfo = new FileInfo(uploadedFilePath);

        //    var dataTable = _excelService.Excel_To_DataTable2(sesa_id, category_submitter, fileInfo, order_id);

        //    BulkInsertOT2(dataTable, id_login, sesa_id, category_submitter, order_id);
        //}

        public ImportResult BulkInsertOT(DataTable tbl, string id_login, string sesa_id, string category_submitter)
        {
            tbl.Columns.Add("id_login", typeof(string));

            foreach (DataRow row in tbl.Rows)
            {
                row["id_login"] = id_login;

                // Ensure the column sesa_id is present and filled correctly
                if (tbl.Columns.Contains("sesa_id"))
                {
                    row["sesa_submitter"] = sesa_id;
                    row["category_submitter"] = category_submitter;
                }
            }

            string orderId = null;
            string detailMsg = "";
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open(); // Open the connection here

                using (SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(conn))
                {
                    sqlBulkCopy.DestinationTableName = "dbo.tmp_tbl_overtime";

                    sqlBulkCopy.ColumnMappings.Add("Column 1", "sesa_id");
                    sqlBulkCopy.ColumnMappings.Add("Column 2", "date_ot");
                    sqlBulkCopy.ColumnMappings.Add("Column 3", "shift");
                    sqlBulkCopy.ColumnMappings.Add("Column 4", "total_hour");
                    sqlBulkCopy.ColumnMappings.Add("Column 5", "reason");
                    sqlBulkCopy.ColumnMappings.Add("sesa_id", "sesa_submitter");
                    sqlBulkCopy.ColumnMappings.Add("category_submitter", "category_submitter");

                    sqlBulkCopy.WriteToServer(tbl);
                }

                // Now that the connection is open, execute the stored procedure
                using (SqlCommand cmd = new SqlCommand("CREATE_NEW_OVERTIME_UPLOAD_TESTING", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;

                    SqlParameter orderIdParam = new SqlParameter("@order_id", SqlDbType.VarChar, 255)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(orderIdParam);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                detailMsg = reader["detail_msg"].ToString();
                            }
                        }
                    }

                    cmd.ExecuteNonQuery(); 

                    orderId = orderIdParam.Value.ToString();
                }

                using (SqlCommand cmd = new SqlCommand("UPDATE mst_running_id SET running_id = running_id + 1 WHERE org_group = @OT", conn))
                {
                    cmd.Parameters.AddWithValue("@OT", "Overtime master");
                    cmd.ExecuteNonQuery(); 
                }
            }

            _logger.LogInformation($"DetailMsg={detailMsg}");

            return new ImportResult { DetailMsg = detailMsg, OrderId = orderId};
        }
        public ImportResult BulkInsertOT2(DataTable tbl, string id_login, string sesa_id, string category_submitter, string order_id)
        {
            tbl.Columns.Add("id_login", typeof(string));

            foreach (DataRow row in tbl.Rows)
            {
                row["id_login"] = id_login;
                if (tbl.Columns.Contains("sesa_id"))
                {
                    row["sesa_submitter"] = sesa_id;
                    row["category_submitter"] = category_submitter;
                }
            }

            string orderId = null;
            string detailMsg = "";
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open(); // Open the connection here

                using (SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(conn))
                {
                    sqlBulkCopy.DestinationTableName = "dbo.tmp_tbl_overtime";

                    sqlBulkCopy.ColumnMappings.Add("Column 1", "sesa_id");
                    sqlBulkCopy.ColumnMappings.Add("Column 2", "date_ot");
                    sqlBulkCopy.ColumnMappings.Add("Column 3", "shift");
                    sqlBulkCopy.ColumnMappings.Add("Column 4", "total_hour");
                    sqlBulkCopy.ColumnMappings.Add("Column 5", "reason");
                    sqlBulkCopy.ColumnMappings.Add("sesa_id", "sesa_submitter");
                    sqlBulkCopy.ColumnMappings.Add("category_submitter", "category_submitter");

                    sqlBulkCopy.WriteToServer(tbl);
                }

                using (SqlCommand cmd = new SqlCommand("Delete tbl_overtime WHERE order_id = @order_id", conn))
                {
                    cmd.Parameters.AddWithValue("@order_id", order_id);
                    cmd.ExecuteNonQuery();
                }
                
                using (SqlCommand cmd = new SqlCommand("Delete tbl_approved_ot WHERE order_id = @order_id", conn))
                {
                    cmd.Parameters.AddWithValue("@order_id", order_id);
                    cmd.ExecuteNonQuery();
                }


                using (SqlCommand cmd = new SqlCommand(@"
                INSERT INTO tbl_approved_ot_history 
                    (sesa_approved, Order_ID, appr_code, status_approved_code, approved_date)
                VALUES 
                    (@sesa_submitter, @order_id, 'OT04', '5', GETDATE());", conn))
                {
                    cmd.Parameters.AddWithValue("@sesa_submitter", sesa_id);
                    cmd.Parameters.AddWithValue("@order_id", order_id);
                    cmd.ExecuteNonQuery();
                }

                using (SqlCommand cmd = new SqlCommand("CLARIFY_OVERTIME_UPLOAD_TESTING", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@order_id", order_id);
                    SqlParameter orderIdParam = new SqlParameter("@orderID", SqlDbType.VarChar, 255)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(orderIdParam);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                detailMsg = reader["detail_msg"].ToString();
                            }
                        }
                    }

                    cmd.ExecuteNonQuery(); 

                    orderId = orderIdParam.Value.ToString();
                }

                
                using (SqlCommand cmd = new SqlCommand("UPDATE mst_running_id SET running_id = running_id + 1 WHERE org_group = @OT", conn))
                {
                    cmd.Parameters.AddWithValue("@OT", "Overtime master");
                    cmd.ExecuteNonQuery(); 
                }
            }

            _logger.LogInformation($"DetailMsg={detailMsg}");

            return new ImportResult { DetailMsg = detailMsg, OrderId = orderId};
        }


        //public void BulkInsertOT2(DataTable tbl, string id_login, string sesa_id, string category_submitter, string order_id)
        //{
        //    // Menambahkan kolom id_login ke DataTable
        //    tbl.Columns.Add("id_login", typeof(string));

        //    foreach (DataRow row in tbl.Rows)
        //    {
        //        row["id_login"] = id_login;

        //        // Pastikan kolom sesa_id sudah ada dan diisi dengan benar
        //        if (tbl.Columns.Contains("sesa_id"))
        //        {
        //            row["sesa_submitter"] = sesa_id;
        //            row["category_submitter"] = category_submitter;
        //        }
        //    }

        //    // Menggunakan SqlBulkCopy untuk memasukkan data ke dalam tabel
        //    using (SqlConnection conn = new SqlConnection(ConnectionString))
        //    {
        //        conn.Open();

        //        using (SqlCommand cmd = new SqlCommand("Delete tbl_overtime WHERE order_id = @order_id", conn))
        //        {
        //            cmd.Parameters.AddWithValue("@order_id", order_id);
        //            cmd.ExecuteNonQuery();
        //        }
        //        using (SqlBulkCopy sqlBulkCopy = new SqlBulkCopy(conn))
        //        {
        //            sqlBulkCopy.DestinationTableName = "dbo.tmp_tbl_overtime";

        //            // Menambahkan pemetaan kolom
        //            sqlBulkCopy.ColumnMappings.Add("Column 1", "sesa_id");
        //            sqlBulkCopy.ColumnMappings.Add("Column 2", "date_ot");
        //            sqlBulkCopy.ColumnMappings.Add("Column 3", "shift");
        //            sqlBulkCopy.ColumnMappings.Add("Column 4", "total_hour");
        //            sqlBulkCopy.ColumnMappings.Add("Column 5", "reason");
        //            sqlBulkCopy.ColumnMappings.Add("sesa_id", "sesa_submitter");
        //            sqlBulkCopy.ColumnMappings.Add("category_submitter", "category_submitter");

        //            // Menulis data ke server
        //            sqlBulkCopy.WriteToServer(tbl);
        //        }

        //        // Memanggil stored procedure untuk membuat upload lembur baru
        //        using (SqlCommand cmd = new SqlCommand("CLARIFY_OVERTIME_UPLOAD", conn))
        //        {
        //            cmd.CommandType = CommandType.StoredProcedure;
        //            cmd.Parameters.AddWithValue("@order_id", order_id);
        //            cmd.ExecuteNonQuery();
        //        }
        //    }
        //}


        public string ImportFileRename(IFormFile file_doc, string filename, string subfolder)
        {
            var uploadedFilePath = _fileManagement.UploadFileRename(file_doc, filename, subfolder);
            return uploadedFilePath;
        }

    }
}
