using Microsoft.AspNetCore.Mvc;
using P1F_TPM360_HUB.Models;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace P1F_TPM360_HUB.Function
{
    public class DatabaseAccessLayer
    {
        public string ConnectionString = "Data Source=10.155.129.69;Initial Catalog=P1F_MAINT;Persist Security Info=True;User ID=dtuser;Password=DTCavite@2024;MultipleActiveResultSets=true;TrustServerCertificate=True;";

        public List<DropdownModel> GetLevel()
        {
            var levelList = new List<DropdownModel>();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();

                string query = @"
                    SELECT DISTINCT level FROM mst_level
                ";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            levelList.Add(new DropdownModel
                            {
                                Code = reader["level"].ToString(),
                            });
                        }
                    }
                }
            }
            return levelList;
        }
        public List<DropdownModel> GetRole()
        {
            var roleList = new List<DropdownModel>();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();

                string query = @"
                    SELECT DISTINCT role FROM mst_role
                ";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            roleList.Add(new DropdownModel
                            {
                                Code = reader["role"].ToString(),
                            });
                        }
                    }
                }
            }
            return roleList;
        }

        public List<DropdownModel> GetLines(string sessionLines)
        {
            var lineList = new List<DropdownModel>();

            if (string.IsNullOrEmpty(sessionLines))
            {
                return lineList;
            }

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();

                string query = @"
            SELECT DISTINCT line 
            FROM mst_drawer_mapping
            WHERE line IN (SELECT TRIM(value) FROM STRING_SPLIT(@SessionLines, ';'))
        ";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@SessionLines", sessionLines);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lineList.Add(new DropdownModel
                            {
                                Code = reader["line"].ToString(),
                            });
                        }
                    }
                }
            }
            return lineList;
        }
        
        public List<DropdownModel> GetLocations(string line)
        {
            var lineList = new List<DropdownModel>();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();

                string query = @"
                    SELECT DISTINCT location FROM mst_drawer WHERE line = @line
                ";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@line", line ?? (object)DBNull.Value);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            lineList.Add(new DropdownModel
                            {
                                Code = reader["location"].ToString(),
                            });
                        }
                    }
                }
            }
            return lineList;
        }
        
        public List<DrawerCapacity> GetDrawerCapacities(string line, string location)
        {
            var list = new List<DrawerCapacity>();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();
                // Ambil data kapasitas Max Qty per Location
                string query = "SELECT line, location, max_qty FROM mst_drawer WHERE line = @line";

                if (location != "ALL" && !string.IsNullOrEmpty(location))
                {
                    query += " AND location = @location";
                }

                // Order by location agar urut
                query += " ORDER BY location ASC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@line", line ?? (object)DBNull.Value);
                    if (location != "ALL" && !string.IsNullOrEmpty(location))
                    {
                        cmd.Parameters.AddWithValue("@location", location);
                    }

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new DrawerCapacity
                            {
                                Line = reader["line"].ToString(),
                                Location = reader["location"].ToString(),
                                MaxQty = reader["max_qty"] != DBNull.Value ? Convert.ToInt32(reader["max_qty"]) : 0
                            });
                        }
                    }
                }
            }
            return list;
        }
        
        public List<CableViewModel> GetCables(string line, string location)
        {
            var list = new List<CableViewModel>();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();

                // Query dasar
                string query = @"
                SELECT cable_id, cable_part, cable_description, unit_model, status, location
                FROM mst_cable 
                WHERE line = @line 
            ";

                // Jika location BUKAN 'ALL', tambahkan filter spesifik
                if (location != "ALL" && !string.IsNullOrEmpty(location))
                {
                    query += " AND location = @location";
                }

                // Order by location supaya grouping rapi
                query += " ORDER BY location ASC, cable_id ASC";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@line", line ?? (object)DBNull.Value);

                    if (location != "ALL" && !string.IsNullOrEmpty(location))
                    {
                        cmd.Parameters.AddWithValue("@location", location);
                    }

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            list.Add(new CableViewModel
                            {
                                CableId = reader["cable_id"] != DBNull.Value ? reader["cable_id"].ToString() : "",
                                CablePart = reader["cable_part"] != DBNull.Value ? reader["cable_part"].ToString() : "",
                                CableDescription = reader["cable_description"] != DBNull.Value ? reader["cable_description"].ToString() : "",
                                UnitModel = reader["unit_model"] != DBNull.Value ? reader["unit_model"].ToString() : "",
                                Status = reader["status"] != DBNull.Value ? reader["status"].ToString() : "",
                                LocationGroup = reader["location"] != DBNull.Value ? reader["location"].ToString() : ""
                            });
                        }
                    }
                }
            }
            return list;
        }

        public CableLocationResult GetLocationByQr(string qrCode)
        {
            CableLocationResult result = null;

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();
                string query = @"SELECT TOP 1 line, location 
                         FROM mst_cable 
                         WHERE cable_id = @qrCode OR qr_code = @qrCode";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@qrCode", qrCode);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            result = new CableLocationResult
                            {
                                Line = reader["line"]?.ToString(),
                                Location = reader["location"]?.ToString()
                            };
                        }
                    }
                }
            }
            return result;
        }

        public bool UpdateCableStatusToIn(string cableId, string sesa_id)
        {
            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();
                string query = @"RETURN_FROM_DASHBOARD";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@cable_id", cableId);
                    cmd.Parameters.AddWithValue("@return_sesa", sesa_id);

                    int rowsAffected = cmd.ExecuteNonQuery();
                    return rowsAffected > 0;
                }
            }
        }

        public List<CodeNameModel> GetEmployee()
        {
            var incidentList = new List<CodeNameModel>();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();

                string query = @"
                    SELECT 
                        COALESCE(a.sesa_id, b.sesa_id) AS sesa_id, 
                        COALESCE(a.employee_name, b.name) AS employee_name
                    FROM mst_employee a 
                    FULL OUTER JOIN mst_users b ON a.sesa_id = b.sesa_id
                ";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            incidentList.Add(new CodeNameModel
                            {
                                Code = reader["sesa_id"].ToString(),
                                Name = reader["employee_name"].ToString(),
                            });
                        }
                    }
                }
            }
            return incidentList;
        }
        public List<CodeNameModel> GetEmployeeSE()
        {
            var incidentList = new List<CodeNameModel>();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();

                string query = @"
                    SELECT 
                        COALESCE(a.sesa_id, b.sesa_id) AS sesa_id, 
                        COALESCE(a.employee_name, b.name) AS employee_name
                    FROM mst_employee a 
                    FULL OUTER JOIN mst_users b ON a.sesa_id = b.sesa_id
                    WHERE (a.sesa_id like '%SESA%' AND b.sesa_id like '%SESA%') OR a.plant = 'DUMMY'
                    AND a.employee_status = 1
                ";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            incidentList.Add(new CodeNameModel
                            {
                                Code = reader["sesa_id"].ToString(),
                                Name = reader["employee_name"].ToString(),
                            });
                        }
                    }
                }
            }
            return incidentList;
        }
        public List<CodeNameModel> GetPanelRecommendation()
        {
            var incidentList = new List<CodeNameModel>();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();

                string query = "SELECT id_corrective, corrective FROM mst_panel_recommendation";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            incidentList.Add(new CodeNameModel
                            {
                                Code = reader["id_corrective"].ToString(),
                                Name = reader["corrective"].ToString(),
                            });
                        }
                    }
                }
            }
            return incidentList;
        }
        public List<CodeNameModel> GetReferences(string ref_code)
        {
            var incidentList = new List<CodeNameModel>();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();

                string query = "SELECT file_reference, title FROM mst_reference_tool WHERE ref_code = @ref_code";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    cmd.Parameters.AddWithValue("@ref_code", ref_code);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            incidentList.Add(new CodeNameModel
                            {
                                Code = reader["file_reference"].ToString(),
                                Name = reader["title"].ToString(),
                            });
                        }
                    }
                }
            }
            return incidentList;
        }
        public List<CodeNameModel> GetOrganizations()
        {
            var employeeList = new List<CodeNameModel>();

            using (SqlConnection con = new SqlConnection(ConnectionString))
            {
                con.Open();

                string query = "SELECT organization FROM mst_organization where organization <> 'ALL Employee'";

                using (SqlCommand cmd = new SqlCommand(query, con))
                {
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            employeeList.Add(new CodeNameModel
                            {
                                Name = reader["organization"].ToString()
                            });
                        }
                    }
                }
            }
            return employeeList;
        }

        public bool AddUser(string sesa_id, string name, string password, string level, string role, string email, string plant, string org)
        {
            if (string.IsNullOrEmpty(password))
            {
                password = "123";
            }

            var hashpassword = new Authentication();
            string passwordHash = hashpassword.MD5Hash(password);
            try
            {
                // Query untuk menambahkan pengguna ke mst_users
                string queryUser = $"INSERT INTO mst_users (sesa_id, name, password, level, role, email, plant, organization) VALUES (@sesa_id, @name, @password, @level, @role, @email, @plant, @org)";

                Console.WriteLine("Add User query:\n" + queryUser);

                using (SqlConnection con = new SqlConnection(ConnectionString))
                {
                    con.Open(); // Buka koneksi di sini

                    using (SqlCommand cmd = new SqlCommand(queryUser, con))
                    {
                        cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@password", passwordHash);
                        cmd.Parameters.AddWithValue("@level", level);
                        cmd.Parameters.AddWithValue("@role", role);
                        cmd.Parameters.AddWithValue("@email", email);
                        cmd.Parameters.AddWithValue("@plant", plant);
                        cmd.Parameters.AddWithValue("@org", org);
                        cmd.ExecuteNonQuery();
                    }

                    // Query untuk menambahkan data ke mst_observer
                    //string queryObserver = $"INSERT INTO [P1F_SERE].[dbo].[mst_observer] (dept, sesa_name, sesa_id, plant, record_date) VALUES (@dept, @sesa_name, @sesa_id, @plant, @record_date)";
                    //using (SqlCommand cmdObserver = new SqlCommand(queryObserver, con))
                    //{
                    //    cmdObserver.Parameters.AddWithValue("@dept", dept);
                    //    cmdObserver.Parameters.AddWithValue("@sesa_name", name);
                    //    cmdObserver.Parameters.AddWithValue("@sesa_id", sesa_id);
                    //    cmdObserver.Parameters.AddWithValue("@plant", plant);
                    //    cmdObserver.Parameters.AddWithValue("@record_date", DateTime.Now);

                    //    // Log data yang akan dimasukkan ke mst_observer
                    //    Console.WriteLine($"Inserting into mst_observer: dept={dept}, sesa_name={name}, sesa_id={sesa_id}, plant={plant}, record_date={DateTime.Now}");

                    //    cmdObserver.ExecuteNonQuery();
                    //}

                    con.Close(); // Tutup koneksi setelah semua operasi selesai
                }

                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return false;
            }
        }

        public List<UserDetailModel> GetUserDetail(string sesa_id)
        {
            List<UserDetailModel> dataList = new List<UserDetailModel>();
            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                conn.Open();
                using (SqlCommand cmd = new SqlCommand("GET_USER_DETAIL", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
                    using SqlDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        while (reader.Read())
                        {
                            UserDetailModel row = new UserDetailModel();
                            row.id = reader["id_user"].ToString();
                            row.sesa_id = reader["sesa_id"].ToString();
                            row.name = reader["name"].ToString();
                            row.email = reader["email"].ToString();
                            row.level = reader["level"].ToString();
                            row.role = reader["role"].ToString();
                            row.lines = reader["lines"].ToString();
                            dataList.Add(row);
                        }
                    }
                }

                conn.Close();
            }
            return dataList;
        }

        public bool AddLog(string id_user, string actionMessage)
        {
            try
            {
                string query = "INSERT INTO tb_log ([id_user], [dates], [actions]) VALUES (@id_user, @dates, @actions)";

                using (SqlConnection con = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query))
                    {
                        cmd.Connection = con;
                        cmd.Parameters.AddWithValue("@id_user", id_user);
                        cmd.Parameters.AddWithValue("@dates", DateTime.Now);
                        cmd.Parameters.AddWithValue("@actions", actionMessage);
                        con.Open();
                        cmd.ExecuteNonQuery();
                    }
                    con.Close();
                }

                return true;
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(e);
                return false;
            }
        }

        public async Task<string> GetRunningId(string Code)
        {

            using (SqlConnection conn = new SqlConnection(ConnectionString))
            {
                await conn.OpenAsync();
                using (SqlCommand cmd = new SqlCommand("GET_RUNNING_ID", conn))
                {
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@Code", Code);

                    // Setup parameter OUTPUT
                    SqlParameter Prefix = new SqlParameter("@Prefix", SqlDbType.VarChar, 225)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(Prefix);

                    SqlParameter Year = new SqlParameter("@Year", SqlDbType.VarChar, 225)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(Year);

                    SqlParameter Month = new SqlParameter("@Month", SqlDbType.VarChar, 225)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(Month);

                    SqlParameter RunningId = new SqlParameter("@RunningId", SqlDbType.Int)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(RunningId);

                    await cmd.ExecuteNonQueryAsync();

                    // Ambil nilai dari parameter OUTPUT
                    string prefix = (Prefix.Value == DBNull.Value) ? null : Prefix.Value?.ToString();
                    string year = (Year.Value == DBNull.Value) ? null : Year.Value?.ToString();
                    string month = (Month.Value == DBNull.Value) ? null : Month.Value?.ToString();
                    int value = (RunningId.Value == DBNull.Value) ? 0 : (int)RunningId.Value;

                    if (string.IsNullOrEmpty(prefix))
                    {
                        throw new Exception($"Running ID Code '{Code}' not found in mst_running_id.");
                    }

                    return $"{prefix}{year}{month}{value:D5}";
                }
            }

            // Format string sesuai keinginan Anda: NTE + 2025 + 11 (2 digit) + 00001 (5 digit)
            //return $"{prefix}{year}{month:D2}{Value:D5}";
        }

        public bool UpdateUserManagement(string id_user, string sesa_id, string name, string plant, string level, string role, string org)
        {
            try
            {
                // Query untuk memperbarui pengguna di mst_users
                string queryUser = $"UPDATE [mst_users] SET sesa_id = @sesa_id, name = @name, level = @level, role = @role, plant = @plant, organization = @org " +
                                   $"WHERE id_user = @id_user";

                Console.WriteLine("Update User query:\n" + queryUser);

                using (SqlConnection con = new SqlConnection(ConnectionString))
                {
                    con.Open();

                    // Memperbarui data di mst_users
                    using (SqlCommand cmd = new SqlCommand(queryUser, con))
                    {
                        cmd.Parameters.AddWithValue("@sesa_id", sesa_id);
                        cmd.Parameters.AddWithValue("@name", name);
                        cmd.Parameters.AddWithValue("@level", level);
                        cmd.Parameters.AddWithValue("@role", role);
                        cmd.Parameters.AddWithValue("@plant", plant);
                        cmd.Parameters.AddWithValue("@org", org);
                        cmd.Parameters.AddWithValue("@id_user", id_user);
                        cmd.ExecuteNonQuery();
                    }

                    con.Close();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

            return true;
        }

        public string GetUserNameById(string id_user)
        {
            try
            {
                string query = "SELECT name FROM mst_users WHERE id_user = @id_user";
                string name = null;

                using (SqlConnection con = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@id_user", id_user);
                        con.Open();
                        SqlDataReader reader = cmd.ExecuteReader();
                        if (reader.Read())
                        {
                            name = reader["name"].ToString();
                        }
                        reader.Close();
                    }
                    con.Close();
                }

                return name;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return null;
            }
        }

        public bool DeleteUser(string id_user)
        {
            try
            {
                string query = "DELETE FROM mst_users WHERE id_user = @id_user";

                using (SqlConnection con = new SqlConnection(ConnectionString))
                {
                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.Parameters.AddWithValue("@id_user", id_user);
                        con.Open();
                        cmd.ExecuteNonQuery();
                    }
                    con.Close();
                }

                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }
        }

        public bool UpdateUserr(string id_user, string password)
        {
            try
            {
                Authentication hashpassword = new Authentication();
                string hashedPassword = hashpassword.MD5Hash(password);

                UserModel user = new UserModel
                {

                    id_user = id_user, 
                    password = hashedPassword
                };

                UpdateUserInDatabase(user);

                Console.WriteLine("User Updated Successfully");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                return false;
            }

            return true;
        }

        private void UpdateUserInDatabase(UserModel user)
        {
            try
            {
                using (SqlConnection con = new SqlConnection(ConnectionString))
                {
                    con.Open();
                    string query = $"UPDATE [mst_users] SET password = '{user.password}' " +
                                   $"WHERE id_user = {user.id_user}";

                    using (SqlCommand cmd = new SqlCommand(query, con))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
            }
        }
    }
}