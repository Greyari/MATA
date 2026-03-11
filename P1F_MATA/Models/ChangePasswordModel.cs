namespace P1F_MATA.Models.ViewModels
{
    public class ChangePasswordModel
    {
        public string Name { get; set; }
        public string Email { get; set; }
        public string SesaId { get; set; }
        public string Level { get; set; }
        public string OldPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmPassword { get; set; }
    }
}