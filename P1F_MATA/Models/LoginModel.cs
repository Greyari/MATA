namespace P1F_MATA.Models
{
    public class LoginModel
    {
        public int id { get; set; }
        public string? sesa_id { get; set; }
        public string? name { get; set; }
        public string? password { get; set; }
        public string? level { get; set; }
        public string? role { get; set; }
        public string? plant { get; set; }
        public string? department { get; set; }
        public string? organization { get; set; }
        public string? id_login { get; set; }
        public string? status_approved_code { get; set; }
        public string? approved_sesa { get; set; }
    }
}
