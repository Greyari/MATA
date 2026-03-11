using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.Data.SqlClient;
using P1F_TPM360_HUB.Models.ViewModels;
using P1F_TPM360_HUB.Function;

namespace P1F_TPM360_HUB.Controllers
{
    public class ChangePasswordController : Controller
    {
        // ===================================================================
        // DEPENDENCY INJECTION
        // ===================================================================
        private readonly DatabaseAccessLayer _db;

        public ChangePasswordController(DatabaseAccessLayer db)
        {
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        /// <summary>
        /// Menampilkan halaman Change Password.
        /// Mengambil data biodata user (Name, Email, SesaId, Level) dari claims
        /// dan mengirimkannya ke view via ChangePasswordModel.
        /// </summary>
        public IActionResult Index()
        {
            var model = new ChangePasswordModel
            {
                Name   = User.FindFirst("P1F_TPM360_HUB_name")?.Value,
                Email  = User.FindFirst(ClaimTypes.Email)?.Value,
                SesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                Level  = User.FindFirst("P1F_TPM360_HUB_level")?.Value
            };

            return View(model);
        }

        // ===================================================================
        // ACTION: CHANGE PASSWORD
        // ===================================================================

        /// <summary>
        /// Memproses permintaan ganti password dari form.
        /// Alur validasi:
        ///   1. Cek semua field tidak boleh kosong.
        ///   2. Cek NewPassword dan ConfirmPassword harus sama.
        ///   3. Hash OldPassword lalu cek ke database apakah cocok dengan data user.
        ///   4. Jika cocok, hash NewPassword lalu update ke database.
        /// Menggunakan TempData["Success"] / TempData["Error"] untuk notifikasi SweetAlert di view.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
        {
            var sesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            // Validasi: semua field wajib diisi
            if (string.IsNullOrWhiteSpace(model.OldPassword) ||
                string.IsNullOrWhiteSpace(model.NewPassword) ||
                string.IsNullOrWhiteSpace(model.ConfirmPassword))
            {
                TempData["Error"] = "All password fields are required.";
                return RedirectToAction("Index");
            }

            // Validasi: new password harus sama dengan confirm password
            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["Error"] = "New password and confirm password do not match.";
                return RedirectToAction("Index");
            }

            var auth = new Authentication();
            string hashedOld = auth.MD5Hash(model.OldPassword);
            string hashedNew = auth.MD5Hash(model.NewPassword);

            using (SqlConnection conn = new SqlConnection(_db.GetConnection()))
            {
                await conn.OpenAsync();

                // Cek apakah old password yang diinput sesuai dengan password di database
                string checkQuery = "SELECT COUNT(1) FROM mst_users WHERE sesa_id = @sesa_id AND password = @password";
                SqlCommand checkCmd = new SqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@sesa_id",  sesaId);
                checkCmd.Parameters.AddWithValue("@password", hashedOld);

                int count = (int)await checkCmd.ExecuteScalarAsync();
                if (count == 0)
                {
                    TempData["Error"] = "Old password is incorrect.";
                    return RedirectToAction("Index");
                }

                // Update password baru ke database (dalam bentuk MD5 hash)
                string updateQuery = "UPDATE mst_users SET password = @newPassword WHERE sesa_id = @sesa_id";
                SqlCommand updateCmd = new SqlCommand(updateQuery, conn);
                updateCmd.Parameters.AddWithValue("@newPassword", hashedNew);
                updateCmd.Parameters.AddWithValue("@sesa_id",     sesaId);

                await updateCmd.ExecuteNonQueryAsync();
            }

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction("Index");
        }
    }
}