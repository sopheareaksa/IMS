using System.ComponentModel.DataAnnotations;

namespace IMS.ViewModels
{
    public class RegisterCustomerViewModel
    {
        [Required]
        [StringLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        public string Password { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string Phone { get; set; } = string.Empty;

        public string? Address { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string? NationalId { get; set; }
    }
}

