// ============================================================
// FILE 1: FileManagementService.cs
// ============================================================

using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace P1F_TPM360_HUB.Service
{
    /// <summary>
    /// Service untuk menangani upload file ke folder wwwroot/Upload.
    /// </summary>
    public class FileManagementService
    {
        // Folder root untuk semua upload
        private readonly string _uploadRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Upload");

        /// <summary>
        /// Upload file dengan nama aslinya ke folder wwwroot/Upload.
        /// Mengembalikan path file jika berhasil, atau "Upload Fail: ..." jika gagal.
        /// </summary>
        public string UploadFile(IFormFile file)
        {
            string fileName = ContentDispositionHeaderValue
                .Parse(file.ContentDisposition)
                .FileName.Trim().ToString();

            EnsureDirectoryExists(_uploadRootPath);

            string filePath = Path.Combine(_uploadRootPath, fileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                    file.CopyTo(stream);

                return filePath;
            }
            catch (Exception ex)
            {
                return "Upload Fail: " + ex.Message;
            }
        }

        /// <summary>
        /// Upload file dengan nama baru ke subfolder tertentu di wwwroot/Upload.
        /// Format return: "OK;namaFile" jika berhasil, atau "ERROR;pesan" jika gagal.
        /// </summary>
        public string UploadFileRename(IFormFile file, string filename, string subfolder)
        {
            string originalFileName = Path.GetFileName(
                ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim().ToString());

            string extension   = Path.GetExtension(originalFileName);
            string newFileName = filename + extension;

            string folderPath = Path.Combine(_uploadRootPath, subfolder);
            EnsureDirectoryExists(folderPath);

            string filePath = Path.Combine(folderPath, newFileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                    file.CopyTo(stream);

                return "OK;" + newFileName;
            }
            catch (Exception ex)
            {
                return "ERROR;Upload Fail: " + ex.Message;
            }
        }

        // ── Helper ───────────────────────────────────────────────────────────
        private void EnsureDirectoryExists(string path)
        {
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }
    }
}