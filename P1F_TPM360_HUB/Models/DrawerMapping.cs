namespace P1F_TPM360_HUB.Models
{
    public class DrawerMapping
    {
        public int id { get; set; }
        public string line { get; set; } = string.Empty;
        public int col_num { get; set; }
        public int row_num { get; set; }
        public string col_text { get; set; } = string.Empty;
        public string row_text { get; set; } = string.Empty;

    }
    public class LineDataViewModel
    {
        public string LineName { get; set; } = string.Empty;
        public int ColNum { get; set; }
        public List<GridItemViewModel> Items { get; set; } = new List<GridItemViewModel>();
    }

    public class GridItemViewModel
    {
        public string Label { get; set; } = string.Empty;
        public string SubLabel { get; set; } = string.Empty;
        public bool IsAlert { get; set; }
    }

    public class DrawerDetail
    {
        public string Location { get; set; } = string.Empty;
        public int ActQty { get; set; }
        public int MaxQty { get; set; }
        public int AvailableQty { get; set; }
    }
}
