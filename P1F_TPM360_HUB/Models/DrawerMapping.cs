namespace P1F_TPM360_HUB.Models
{
    public class DrawerMapping
    {
        public int id { get; set; }
        public string line { get; set; }
        public int col_num { get; set; }
        public int row_num { get; set; }
        public string col_text { get; set; }
        public string row_text { get; set; }

    }
    public class LineDataViewModel
    {
        public string LineName { get; set; }
        public int ColNum { get; set; }
        public List<GridItemViewModel> Items { get; set; }
    }

    public class GridItemViewModel
    {
        public string Label { get; set; }
        public string SubLabel { get; set; }
        public bool IsAlert { get; set; }
    }

    public class DrawerDetail
    {
        public string Location { get; set; }
        public int ActQty { get; set; }
        public int MaxQty { get; set; }
        public int AvailableQty { get; set; }
    }
}
