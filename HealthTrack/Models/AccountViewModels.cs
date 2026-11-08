using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    public class RegisterViewModel
    {
        [Required, StringLength(100)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = "";

        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Phone, StringLength(20)]
        public string? Phone { get; set; }

        [Required]
        [Display(Name = "Register as")]
        public string Role { get; set; } = "Client";   // only Client or Trainer is accepted

        [StringLength(100)]
        [Display(Name = "Specialization (trainers)")]
        public string? Specialization { get; set; }

        [Required, StringLength(100, MinimumLength = 8)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare("Password", ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = "";
    }

    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = "";

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = "";

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}
