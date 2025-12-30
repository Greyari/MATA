namespace P1F_TPM360_HUB.Models
{
    public class ImportResult
    {
        public int SuccessCount { get; set; }
        public int ErrorCount { get; set; }
        public string OrderId { get; set; }

        public string Msg { get; set; } 
        public string DetailMsg { get; set; } 
    }
}
