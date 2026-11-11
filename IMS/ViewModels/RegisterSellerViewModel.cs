using System.ComponentModel.DataAnnotations;

namespace IMS.ViewModels
{
    public class RegisterSellerViewModel
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
        public string CompanyName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string CompanyEmail { get; set; } = string.Empty;

        [Required]
        [Phone]
        public string CompanyPhone { get; set; } = string.Empty;

        public string? CompanyAddress { get; set; }

        [Required]
        [StringLength(100)]
        public string LeadAgentFullName { get; set; } = string.Empty;

        public string? Branch { get; set; }
    }
}

