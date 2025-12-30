using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OfficeOpenXml;

namespace P1F_TPM360_HUB.Service
{
    public class ExcelServiceProvider
    {
        public ExcelWorksheet Load_Excel_File_CSV(FileInfo fileInfo)
        {
            // csv option
            // set the formatting options
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            ExcelTextFormat format = new ExcelTextFormat
            {
                Delimiter = ',',
                Culture = new CultureInfo(Thread.CurrentThread.CurrentCulture.ToString())
                {
                    DateTimeFormat = { ShortDatePattern = "dd-MM-yyyy" }
                },
                Encoding = new UTF8Encoding()
            };
            ExcelPackage excelPackage = new ExcelPackage();
            // create a WorkSheet
            ExcelWorksheet worksheet = excelPackage.Workbook.Worksheets.Add("Sheet 1");

            // load the CSV data into cell A1
            worksheet.Cells["A1"].LoadFromText(fileInfo, format);
            return worksheet;
        }

        public ExcelWorksheet Load_Excel_File_XLSX(FileInfo fileInfo)
        {
            // xlsx option
            ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;
            var excelPackage_xlsx = new ExcelPackage(fileInfo);
            var excelWorkSheet = excelPackage_xlsx.Workbook.Worksheets.First();
            return excelWorkSheet;
        }

        public DataTable Excel_To_DataTablehc(string sesa_id, FileInfo fileInfo, int row = 1, int col = 1, bool hasHeader = true)
        {
            ExcelWorksheet worksheet = null;
            if (fileInfo.ToString().EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                worksheet = Load_Excel_File_XLSX(fileInfo);
            }
            else if (fileInfo.ToString().EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                worksheet = Load_Excel_File_CSV(fileInfo);
            }

            DataTable tbl = new DataTable();

            // Tambahkan kolom berdasarkan baris pertama dari worksheet
            foreach (var firstRowCell in worksheet.Cells[1, 1, 1, worksheet.Dimension.End.Column])
            {
                tbl.Columns.Add(string.Format("Column {0}", firstRowCell.Start.Column));
            }

            // Tambahkan kolom sesa_id secara eksplisit
            tbl.Columns.Add("sesa_upload", typeof(string));

            var startRow = hasHeader ? 2 : 1;
            for (int rowNum = startRow; rowNum <= worksheet.Dimension.End.Row; rowNum++)
            {
                var worksheetRow = worksheet.Cells[rowNum, 1, rowNum, worksheet.Dimension.End.Column];
                bool isRowEmpty = true;
                DataRow tblRow = tbl.NewRow();

                foreach (var cell in worksheetRow)
                {
                    if (!string.IsNullOrWhiteSpace(cell.Text))
                    {
                        isRowEmpty = false;
                        tblRow[cell.Start.Column - 1] = cell.Text.Trim().Replace(",", "");
                    }
                }

                if (isRowEmpty)
                {
                    break;
                }

                tblRow["sesa_upload"] = sesa_id;
                tbl.Rows.Add(tblRow);
            }

            return tbl;
        }



        public DataTable Excel_To_DataTable(string sesa_id, string category_submitter, FileInfo fileInfo, int row = 1, int col = 1, bool hasHeader = true)
        {
            ExcelWorksheet worksheet = null;
            if (fileInfo.ToString().EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                worksheet = Load_Excel_File_XLSX(fileInfo);
            }
            else if (fileInfo.ToString().EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                worksheet = Load_Excel_File_CSV(fileInfo);
            }

            DataTable tbl = new DataTable();

            // Tambahkan kolom berdasarkan baris pertama dari worksheet
            foreach (var firstRowCell in worksheet.Cells[1, 1, 1, worksheet.Dimension.End.Column])
            {
                tbl.Columns.Add(string.Format("Column {0}", firstRowCell.Start.Column));
            }

            if (!tbl.Columns.Contains("sesa_id"))
            {
                tbl.Columns.Add("sesa_id"); 
            }
            if (!tbl.Columns.Contains("sesa_submitter"))
            {
                tbl.Columns.Add("sesa_submitter");
            }
            if (!tbl.Columns.Contains("category_submitter"))
            {
                tbl.Columns.Add("category_submitter");
            }

            var startRow = hasHeader ? 2 : 1;
            for (int rowNum = startRow; rowNum <= worksheet.Dimension.End.Row; rowNum++)
            {
                var worksheetRow = worksheet.Cells[rowNum, 1, rowNum, worksheet.Dimension.End.Column];
                bool isRowEmpty = true;
                DataRow tblRow = tbl.NewRow();

                foreach (var cell in worksheetRow)
                {
                    if (!string.IsNullOrWhiteSpace(cell.Text))
                    {
                        isRowEmpty = false;
                        tblRow[cell.Start.Column - 1] = cell.Text.Trim().Replace(",", "");
                    }
                }

                if (isRowEmpty)
                {
                    break;
                }

                tblRow["sesa_id"] = sesa_id; 
                tblRow["sesa_submitter"] = sesa_id;
                tblRow["category_submitter"] = category_submitter;
                tbl.Rows.Add(tblRow);
            }

            return tbl;
        }

        public DataTable Excel_To_DataTable2(string sesa_id, string category_submitter, FileInfo fileInfo, string order_id, int row = 1, int col = 1, bool hasHeader = true)
        {
            ExcelWorksheet worksheet = null;
            if (fileInfo.ToString().EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                worksheet = Load_Excel_File_XLSX(fileInfo);
            }
            else if (fileInfo.ToString().EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            {
                worksheet = Load_Excel_File_CSV(fileInfo);
            }

            DataTable tbl = new DataTable();

            // Tambahkan kolom berdasarkan baris pertama dari worksheet
            foreach (var firstRowCell in worksheet.Cells[1, 1, 1, worksheet.Dimension.End.Column])
            {
                tbl.Columns.Add(string.Format("Column {0}", firstRowCell.Start.Column));
            }

            if (!tbl.Columns.Contains("sesa_id"))
            {
                tbl.Columns.Add("sesa_id");
            }
            if (!tbl.Columns.Contains("sesa_submitter"))
            {
                tbl.Columns.Add("sesa_submitter");
            }
            if (!tbl.Columns.Contains("category_submitter"))
            {
                tbl.Columns.Add("category_submitter");
            }

            var startRow = hasHeader ? 2 : 1;
            for (int rowNum = startRow; rowNum <= worksheet.Dimension.End.Row; rowNum++)
            {
                var worksheetRow = worksheet.Cells[rowNum, 1, rowNum, worksheet.Dimension.End.Column];
                bool isRowEmpty = true;
                DataRow tblRow = tbl.NewRow();

                foreach (var cell in worksheetRow)
                {
                    if (!string.IsNullOrWhiteSpace(cell.Text))
                    {
                        isRowEmpty = false;
                        tblRow[cell.Start.Column - 1] = cell.Text.Trim().Replace(",", "");
                    }
                }

                if (isRowEmpty)
                {
                    break;
                }

                tblRow["sesa_id"] = sesa_id;
                tblRow["sesa_submitter"] = sesa_id;
                tblRow["category_submitter"] = category_submitter;
                tbl.Rows.Add(tblRow);
            }

            return tbl;
        }
    }
}