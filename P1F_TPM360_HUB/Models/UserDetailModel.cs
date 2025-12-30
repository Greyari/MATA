using System.ComponentModel.DataAnnotations;
namespace P1F_TPM360_HUB.Models
{
    public class UserDetailModel
    {
        [Key]
        public string? sesa_id { get; set; }
        public string? name { get; set; }
        public string? apps { get; set; }
        public string? plant { get; set; }
        public string? id { get; set; }



        public string? email { get; set; }
        public string? level { get; set; }
        public string? role { get; set; }
        public string? department { get; set; }
        public string? organization { get; set; }
        public string? manager_sesa_id { get; set; }
        public string? manager_name { get; set; }
        public string? manager_email { get; set; }
        public string? lines { get; set; }
    }
}