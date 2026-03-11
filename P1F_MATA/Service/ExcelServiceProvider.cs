using System.Data;
using System.Globalization;
using System.Text;
using System.Threading;
using OfficeOpenXml;

namespace P1F_MATA.Service
{
    /// <summary>
    /// Service untuk membaca file Excel (.xlsx) atau CSV dan mengkonversinya ke DataTable.
    /// </summary>
    public class ExcelServiceProvider
    {
        // ===================================================================
        // LOAD FILE
        // ===================================================================

        /// <summary>
        /// Membuka file CSV dan memuatnya ke dalam ExcelWorksheet.
        /// Delimiter: koma (,) | Format tanggal: dd-MM-yyyy | Encoding: UTF-8
        /// </summary>
        public ExcelWorksheet Load_Excel_File_CSV(FileInfo fileInfo)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

            var format = new ExcelTextFormat
            {
                Delimiter = ',',
                Culture   = new CultureInfo(Thread.CurrentThread.CurrentCulture.ToString())
                {
                    DateTimeFormat = { ShortDatePattern = "dd-MM-yyyy" }
                },
                Encoding = new UTF8Encoding()
            };

            var excelPackage = new ExcelPackage();
            var worksheet    = excelPackage.Workbook.Worksheets.Add("Sheet 1");
            worksheet.Cells["A1"].LoadFromText(fileInfo, format);

            return worksheet;
        }

        /// <summary>
        /// Membuka file XLSX dan mengembalikan worksheet pertamanya.
        /// </summary>
        public ExcelWorksheet Load_Excel_File_XLSX(FileInfo fileInfo)
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var package = new ExcelPackage(fileInfo);
            return package.Workbook.Worksheets.First();
        }

        // ===================================================================
        // HELPER PRIVATE
        // ===================================================================

        /// <summary>
        /// Memilih loader yang tepat (.xlsx atau .csv) berdasarkan ekstensi file.
        /// </summary>
        private ExcelWorksheet LoadWorksheet(FileInfo fileInfo)
        {
            string path = fileInfo.ToString().ToLower();
            if (path.EndsWith(".xlsx")) return Load_Excel_File_XLSX(fileInfo);
            if (path.EndsWith(".csv"))  return Load_Excel_File_CSV(fileInfo);
            throw new NotSupportedException($"Format file tidak didukung: {fileInfo.Extension}");
        }

        /// <summary>
        /// Membuat DataTable dengan kolom dinamis ("Column 1", "Column 2", dst.)
        /// berdasarkan jumlah kolom pada baris pertama worksheet.
        /// </summary>
        private DataTable CreateBaseDataTable(ExcelWorksheet worksheet)
        {
            var tbl = new DataTable();
            foreach (var cell in worksheet.Cells[1, 1, 1, worksheet.Dimension.End.Column])
                tbl.Columns.Add($"Column {cell.Start.Column}");
            return tbl;
        }

        /// <summary>
        /// Menambahkan kolom ke DataTable jika belum ada.
        /// </summary>
        private void EnsureColumn(DataTable tbl, string columnName)
        {
            if (!tbl.Columns.Contains(columnName))
                tbl.Columns.Add(columnName);
        }

        /// <summary>
        /// Membaca baris-baris worksheet (mulai dari baris ke-2 jika hasHeader=true)
        /// dan menambahkannya ke DataTable. Berhenti saat menemukan baris kosong.
        /// </summary>
        private void FillRows(ExcelWorksheet worksheet, DataTable tbl, Action<DataRow> setExtraColumns, bool hasHeader = true)
        {
            int startRow = hasHeader ? 2 : 1;

            for (int rowNum = startRow; rowNum <= worksheet.Dimension.End.Row; rowNum++)
            {
                var worksheetRow = worksheet.Cells[rowNum, 1, rowNum, worksheet.Dimension.End.Column];
                bool isRowEmpty  = true;
                DataRow tblRow   = tbl.NewRow();

                foreach (var cell in worksheetRow)
                {
                    if (!string.IsNullOrWhiteSpace(cell.Text))
                    {
                        isRowEmpty = false;
                        tblRow[cell.Start.Column - 1] = cell.Text.Trim().Replace(",", "");
                    }
                }

                // Hentikan jika baris kosong (asumsi data sudah habis)
                if (isRowEmpty) break;

                setExtraColumns(tblRow);
                tbl.Rows.Add(tblRow);
            }
        }

        // ===================================================================
        // PUBLIC: KONVERSI EXCEL → DATATABLE
        // ===================================================================

        /// <summary>
        /// Konversi file Excel/CSV ke DataTable untuk import data Headcount.
        /// Menambahkan kolom 'sesa_upload' untuk tracking siapa yang mengupload.
        /// </summary>
        public DataTable Excel_To_DataTablehc(string sesaId, FileInfo fileInfo, int row = 1, int col = 1, bool hasHeader = true)
        {
            var worksheet = LoadWorksheet(fileInfo);
            var tbl       = CreateBaseDataTable(worksheet);

            EnsureColumn(tbl, "sesa_upload");

            FillRows(worksheet, tbl, tblRow =>
            {
                tblRow["sesa_upload"] = sesaId;
            }, hasHeader);

            return tbl;
        }

        /// <summary>
        /// Konversi file Excel/CSV ke DataTable untuk import data Overtime (baru).
        /// Menambahkan kolom: sesa_id, sesa_submitter, category_submitter.
        /// </summary>
        public DataTable Excel_To_DataTable(string sesaId, string categorySubmitter, FileInfo fileInfo, int row = 1, int col = 1, bool hasHeader = true)
        {
            var worksheet = LoadWorksheet(fileInfo);
            var tbl       = CreateBaseDataTable(worksheet);

            EnsureColumn(tbl, "sesa_id");
            EnsureColumn(tbl, "sesa_submitter");
            EnsureColumn(tbl, "category_submitter");

            FillRows(worksheet, tbl, tblRow =>
            {
                tblRow["sesa_id"]           = sesaId;
                tblRow["sesa_submitter"]     = sesaId;
                tblRow["category_submitter"] = categorySubmitter;
            }, hasHeader);

            return tbl;
        }

        /// <summary>
        /// Konversi file Excel/CSV ke DataTable untuk import data Overtime (clarify/edit).
        /// Sama seperti Excel_To_DataTable, digunakan khusus untuk proses clarify dengan order_id.
        /// </summary>
        public DataTable Excel_To_DataTable2(string sesaId, string categorySubmitter, FileInfo fileInfo, string orderId, int row = 1, int col = 1, bool hasHeader = true)
        {
            var worksheet = LoadWorksheet(fileInfo);
            var tbl       = CreateBaseDataTable(worksheet);

            EnsureColumn(tbl, "sesa_id");
            EnsureColumn(tbl, "sesa_submitter");
            EnsureColumn(tbl, "category_submitter");

            FillRows(worksheet, tbl, tblRow =>
            {
                tblRow["sesa_id"]           = sesaId;
                tblRow["sesa_submitter"]     = sesaId;
                tblRow["category_submitter"] = categorySubmitter;
            }, hasHeader);

            return tbl;
        }
    }
}