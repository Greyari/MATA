using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using P1F_MATA.Function;
using P1F_MATA.Models.ViewModels;

namespace P1F_MATA.Controllers
{
    public class ChangePasswordController : Controller
    {
        private readonly DatabaseAccessLayer _db;

        public ChangePasswordController(DatabaseAccessLayer db)
        {
            _db = db;
        }

        // ===================================================================
        // HALAMAN UTAMA
        // ===================================================================

        /// <summary>Tampilkan halaman Change Password dengan data user dari claims.</summary>
        public IActionResult Index()
        {
            var model = new ChangePasswordModel
            {
                Name   = User.FindFirst("P1F_MATA_name")?.Value,
                Email  = User.FindFirst(ClaimTypes.Email)?.Value,
                SesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                Level  = User.FindFirst("P1F_MATA_level")?.Value
            };
            return View(model);
        }

        // ===================================================================
        // ACTION: CHANGE PASSWORD
        // ===================================================================

        /// <summary>
        /// Proses ganti password:
        /// 1. Validasi field tidak kosong
        /// 2. Validasi NewPassword == ConfirmPassword
        /// 3. Cek old password ke database
        /// 4. Update ke password baru
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordModel model)
        {
            var sesaId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(model.OldPassword) ||
                string.IsNullOrWhiteSpace(model.NewPassword) ||
                string.IsNullOrWhiteSpace(model.ConfirmPassword))
            {
                TempData["Error"] = "All password fields are required.";
                return RedirectToAction("Index");
            }

            if (model.NewPassword != model.ConfirmPassword)
            {
                TempData["Error"] = "New password and confirm password do not match.";
                return RedirectToAction("Index");
            }

            var auth       = new Authentication();
            string hashedOld = auth.MD5Hash(model.OldPassword);
            string hashedNew = auth.MD5Hash(model.NewPassword);

            bool oldPasswordValid = await _db.CheckOldPassword(sesaId, hashedOld);
            if (!oldPasswordValid)
            {
                TempData["Error"] = "Old password is incorrect.";
                return RedirectToAction("Index");
            }

            await _db.UpdatePassword(sesaId, hashedNew);

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction("Index");
        }
    }
}