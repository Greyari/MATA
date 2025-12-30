using System.Data.SqlClient;
using Microsoft.AspNetCore.Mvc;
using P1F_TPM360_HUB.Function;
using P1F_TPM360_HUB.Models;

namespace P1F_TPM360_HUB.Controllers
{
    public class LoginController : Controller
    {
        public IActionResult Index()
        {
            return RedirectToAction("Login", "Home");
            //return View();
        }

        public IActionResult SignOut()
        {
            if (HttpContext.Session != null)
            {
                HttpContext.Session.Clear();
            }
            return View("Index");
        }

        private string DbConnection()
        {
            var dbAccess = new DatabaseAccessLayer();
            string dbString = dbAccess.ConnectionString;
            return dbString;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(LoginModel user)
        {
            var hashpassword = new Authentication();

            if (ModelState.IsValid)
            {
                List<LoginModel> userInfo = new List<LoginModel>();
                using (SqlConnection conn = new SqlConnection(DbConnection()))
                {
                    string passwordHash = hashpassword.MD5Hash(user.password);
                    string query = "SELECT * FROM mst_users WHERE sesa_id = @sesa_id AND password = @passwordHash";

                    SqlCommand cmd = new SqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@sesa_id", user.sesa_id);
                    cmd.Parameters.AddWithValue("@passwordHash", passwordHash);
                    conn.Open();
                    SqlDataReader reader = cmd.ExecuteReader();
                    if (reader.HasRows)
                    {
                        DateTime now = DateTime.Now;
                        string id_login = now.ToString("yyMMddHHmmssfff");
                        ViewData["Message"] = "HAS DATA";
                        while (reader.Read())
                        {
                            var loginUser = new LoginModel();
                            loginUser.id = Convert.ToInt32(reader["id_user"]);
                            loginUser.name = reader["name"].ToString();
                            loginUser.sesa_id = reader["sesa_id"].ToString();
                            loginUser.level = reader["level"].ToString(); 
                            loginUser.role = reader["role"].ToString();
                            loginUser.plant = reader["plant"].ToString();
                            loginUser.organization = reader["organization"].ToString();
                            loginUser.department = reader["department"].ToString();
                            loginUser.id_login = id_login;
                            userInfo.Add(loginUser);
                            HttpContext.Session.SetString("id", loginUser.id.ToString());
                            HttpContext.Session.SetString("name", loginUser.name);
                            HttpContext.Session.SetString("organization", loginUser.organization);
                            HttpContext.Session.SetString("sesa_id", loginUser.sesa_id);
                            HttpContext.Session.SetString("level", loginUser.level);
                            HttpContext.Session.SetString("role", loginUser.role);
                            HttpContext.Session.SetString("plant", loginUser.plant);
                            HttpContext.Session.SetString("id_login", loginUser.id_login);
                            HttpContext.Session.SetString("department", loginUser.department);
                        }

                        // Ambil status approved
                        string detailQuery = "SELECT * FROM tbl_approved WHERE approved_sesa = @sesa_id";
                        SqlCommand detailCommand = new SqlCommand(detailQuery, conn);
                        detailCommand.Parameters.AddWithValue("@sesa_id", user.sesa_id);
                        SqlDataReader detailReader = detailCommand.ExecuteReader();
                        if (detailReader.HasRows)
                        {
                            while (detailReader.Read())
                            {
                                string statusApprovedCode = detailReader["status_approved_code"].ToString();
                                HttpContext.Session.SetString("status_approved_code", statusApprovedCode);
                            }
                        }

                        // Update login ID
                        string update_loginID_query = @"UPDATE mst_users SET login_id= (REPLACE(convert(varchar, getdate(),112),'/','') + replace(convert(varchar, getdate(),108),':','')) 
                                                WHERE sesa_id = @sesa_id";
                        SqlCommand updateCommand = new SqlCommand(update_loginID_query, conn);
                        updateCommand.Parameters.AddWithValue("@sesa_id", user.sesa_id);
                        updateCommand.ExecuteNonQuery();

                        string userLevel = HttpContext.Session.GetString("level");
                        string[] levels = userLevel.Split(';'); 

                        if (levels.Contains("admin") || levels.Contains("user") || levels.Contains("superadmin"))
                        {
                            return RedirectToAction("headcountData", "Admin");
                        }
                        else if (levels.Contains("approver"))
                        {
                            return RedirectToAction("Assignment", "Approver");
                        } 
                        else if (levels.Contains("requestor"))
                        {
                            return RedirectToAction("Assignment", "Admin");
                        }
                        else if (levels.Contains("acknowledgement"))
                        {
                            return RedirectToAction("Add_Rep_Acknowledgement", "Admin");
                        }
                        else if (levels.Contains("disp_requestor"))
                        {
                            return RedirectToAction("Discipline", "Operational", new { type = "request" });
                        }
                        else if (levels.Contains("disp_approver"))
                        {
                            return RedirectToAction("Discipline", "Operational", new { type = "approve" });
                        }
                        else if (levels.Contains("disp_admin"))
                        {
                            return RedirectToAction("Discipline", "Operational", new { type = "request" });
                        }
                    }
                    else
                    {
                        ViewData["Message"] = "User  and Password not Registered !";
                    }
                    conn.Close();
                }
            }
            return View("Index");
        }

        //public IActionResult Index(LoginModel user)
        //{
        //    var hashpassword = new Authentication();

        //    if (ModelState.IsValid)
        //    {
        //        List<LoginModel> userInfo = new List<LoginModel>();
        //        using (SqlConnection conn = new SqlConnection(DbConnection()))
        //        {
        //            string passwordHash = hashpassword.MD5Hash(user.password);
        //            string query = "SELECT * FROM mst_users WHERE sesa_id = '" + user.sesa_id + "' AND password = '" + passwordHash + "' ";

        //            SqlCommand cmd = new SqlCommand(query, conn);
        //            conn.Open();
        //            SqlDataReader reader = cmd.ExecuteReader();
        //            if (reader.HasRows)
        //            {
        //                DateTime now = DateTime.Now;
        //                string id_login = now.ToString("yyMMddHHmmssfff");
        //                ViewData["Message"] = "HAS DATA";
        //                while (reader.Read())
        //                {
        //                    var loginUser = new LoginModel();
        //                    loginUser.id = Convert.ToInt32(reader["id_user"]);
        //                    loginUser.name = reader["name"].ToString();
        //                    loginUser.sesa_id = reader["sesa_id"].ToString();
        //                    loginUser.level = reader["level"].ToString();
        //                    loginUser.role = reader["role"].ToString();
        //                    loginUser.plant = reader["plant"].ToString();
        //                    loginUser.organization = reader["organization"].ToString();
        //                    loginUser.department = reader["department"].ToString();
        //                    loginUser.id_login = id_login;
        //                    userInfo.Add(loginUser);
        //                    HttpContext.Session.SetString("id", loginUser.id.ToString());
        //                    HttpContext.Session.SetString("name", loginUser.name);
        //                    HttpContext.Session.SetString("organization", loginUser.organization);
        //                    HttpContext.Session.SetString("sesa_id", loginUser.sesa_id);
        //                    HttpContext.Session.SetString("level", loginUser.level);
        //                    HttpContext.Session.SetString("role", loginUser.role);
        //                    HttpContext.Session.SetString("plant", loginUser.plant);
        //                    HttpContext.Session.SetString("id_login", loginUser.id_login);
        //                    HttpContext.Session.SetString("department", loginUser.department);
        //                }

        //                string detailQuery = "SELECT * FROM tbl_approved WHERE approved_sesa = '" + user.sesa_id + "'";
        //                SqlCommand detailCommand = new SqlCommand(detailQuery, conn);
        //                SqlDataReader detailReader = detailCommand.ExecuteReader();
        //                if (detailReader.HasRows)
        //                {
        //                    while (detailReader.Read())
        //                    {
        //                        string statusApprovedCode = detailReader["status_approved_code"].ToString();
        //                        HttpContext.Session.SetString("status_approved_code", statusApprovedCode);
        //                    }
        //                }

        //                string update_loginID_query = @"UPDATE mst_users SET login_id= (REPLACE(convert(varchar, getdate(),112),'/','') + replace(convert(varchar, getdate(),108),':','')) 
        //                                        WHERE sesa_id = '" + user.sesa_id + "' ";
        //                SqlCommand updateCommand = new SqlCommand(update_loginID_query, conn);
        //                updateCommand.ExecuteNonQuery();

        //                if (HttpContext.Session.GetString("level") == "admin")
        //                {
        //                    int rowsAffected = 0;
        //                    SqlCommand adminCommand = new SqlCommand(update_loginID_query, conn);
        //                    rowsAffected = adminCommand.ExecuteNonQuery();
        //                    return RedirectToAction("headcountData", "Admin");
        //                }
        //                else if (HttpContext.Session.GetString("level") == "requestor")
        //                {
        //                    int rowsAffected = 0;
        //                    SqlCommand adminCommand = new SqlCommand(update_loginID_query, conn);
        //                    rowsAffected = adminCommand.ExecuteNonQuery();
        //                    return RedirectToAction("headcountData", "Admin");
        //                }
        //                else if (HttpContext.Session.GetString("level") == "super admin")
        //                {
        //                    int rowsAffected = 0;
        //                    SqlCommand adminCommand = new SqlCommand(update_loginID_query, conn);
        //                    rowsAffected = adminCommand.ExecuteNonQuery();
        //                    return RedirectToAction("headcountData", "Admin");
        //                }
        //                else if (HttpContext.Session.GetString("level") == "approver")
        //                {
        //                    return RedirectToAction("Dashboard", "Approver");
        //                }
        //                else if (HttpContext.Session.GetString("level") == "picker")
        //                {
        //                    return RedirectToAction("Dashboard", "Picker");
        //                }
        //                else if (HttpContext.Session.GetString("level") == "planner")
        //                {
        //                    return RedirectToAction("Dashboard", "Planner");
        //                }
        //            }
        //            else
        //            {
        //                ViewData["Message"] = "User and Password not Registered !";
        //            }
        //            conn.Close();
        //        }
        //    }
        //    return View("Index");
        //}

        [HttpPost]
        public IActionResult RefreshSession()
        {
            HttpContext.Session.SetString("LastActivity", DateTime.Now.ToString());

            return Json(new { success = true });
        }
    }
}