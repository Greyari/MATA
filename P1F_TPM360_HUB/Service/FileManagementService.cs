using System;
using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace P1F_TPM360_HUB.Service
{
    public class FileManagementService
    {
        public string UploadFile(IFormFile file)
        {
            var fileName = ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim().ToString();

            var mainPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Upload");

            if (!Directory.Exists(mainPath))
            {
                Directory.CreateDirectory(mainPath);
            }

            var filePath = Path.Combine(mainPath, fileName);

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                return filePath;
            }
            catch (Exception e)
            {
                return "Upload Fail: " + e.Message;
            }
        }

        public string UploadFileRename(IFormFile file, string filename, string subfolder)
        {
            var originalFileName = Path.GetFileName(ContentDispositionHeaderValue.Parse(file.ContentDisposition).FileName.Trim().ToString());
            var fileExtension = Path.GetExtension(originalFileName); // Get file extension

            // Create a new file name (e.g., using provided filename and file extension)
            var newFileName = filename + fileExtension;

            var mainPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "Upload", subfolder);

            if (!Directory.Exists(mainPath))
            {
                Directory.CreateDirectory(mainPath);
            }

            var filePath = Path.Combine(mainPath, newFileName); // Use new file name

            try
            {
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    file.CopyTo(stream);
                }

                return "OK;" + newFileName; // Return the new file path
            }
            catch (Exception e)
            {
                return "ERROR;Upload Fail: " + e.Message; // Return detailed error message
            }
        }
    }
}