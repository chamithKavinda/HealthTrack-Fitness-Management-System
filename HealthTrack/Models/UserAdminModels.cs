using System.ComponentModel.DataAnnotations;

namespace HealthTrack.Models
{
    public class AdminUserRow
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsLocked { get; set; }
        public DateTime? Joined { get; set; }
        public bool IsSelf { get; set; }
        /// <summary>Admin accounts are shown but cannot be changed from this page.</summary>
        public bool CanManage => Role == "Client" || Role == "Trainer";
    }

    public class AdminUsersViewModel
    {
        public List<AdminUserRow> Rows { get; set; } = new();
        public string? Q { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
        public int Page { get; set; } = 1;
        public int Pages { get; set; } = 1;
        public int Total { get; set; }
        public int LockedCount { get; set; }
    }

    public class ResetPasswordInput
    {
        [Required] public string UserId { get; set; } = "";

        [Required(ErrorMessage = "Enter a new password.")]
        [DataType(DataType.Password)]
        [Display(Name = "New password")]
        public string NewPassword { get; set; } = "";

        [Required(ErrorMessage = "Confirm the new password.")]
        [DataType(DataType.Password)]
        [Compare(nameof(NewPassword), ErrorMessage = "The two passwords do not match.")]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = "";
    }

    public class ResetPasswordViewModel
    {
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string Role { get; set; } = "";
        public ResetPasswordInput Input { get; set; } = new();
    }
}
