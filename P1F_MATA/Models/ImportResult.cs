namespace P1F_MATA.Models
{
    public class ImportResult
    {
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public string OrderId { get; set; } = string.Empty;
        public string Msg { get; set; } = string.Empty;
        public string DetailMsg { get; set; } = string.Empty;
    }
}
