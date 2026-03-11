namespace P1F_MATA.Models
{
    public class UserManagementModel
    {
        public int id_user { get; set; }
        public string sesa_id { get; set; } = string.Empty;
        public string name { get; set; } = string.Empty;
        public string level { get; set; } = string.Empty;
        public string telegram_id { get; set; } = string.Empty;
    }
}
