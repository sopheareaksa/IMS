using System.ComponentModel.DataAnnotations;
using IMS.Models;

namespace IMS.ViewModels;

public class CustomerProfileViewModel
{
    public int CustomerId { get; set; }
    public int UserId { get; set; }

    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    [Display(Name = "Username")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [StringLength(100)]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be at least 2 characters.")]
    [Display(Name = "Full Legal Name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Phone number is required.")]
    [Phone(ErrorMessage = "Please enter a valid phone number.")]
    [StringLength(20)]
    [Display(Name = "Phone Number")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(255)]
    [Display(Name = "Residential Address")]
    public string? Address { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Date of Birth")]
    public DateTime? DateOfBirth { get; set; }

    [StringLength(50)]
    [Display(Name = "National ID / Passport Number")]
    public string? NationalId { get; set; }

    public DateTime MemberSince { get; set; }
    public DateTime? LastLogin { get; set; }
    public string Status { get; set; } = "Active";
    public string Role { get; set; } = "Customer";

    // Change Password Fields
    [DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string? CurrentPassword { get; set; }

    [DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "New password must be at least 6 characters long.")]
    [Display(Name = "New Password")]
    public string? NewPassword { get; set; }

    [DataType(DataType.Password)]
    [Compare("NewPassword", ErrorMessage = "New password and confirmation do not match.")]
    [Display(Name = "Confirm New Password")]
    public string? ConfirmPassword { get; set; }

    // Summary Statistics
    public int ActivePoliciesCount { get; set; }
    public int TotalQuotationsCount { get; set; }
    public int ClaimsCount { get; set; }
    public decimal TotalAmountPaid { get; set; }

    // Recent Activity Lists
    public List<InsurancePolicy> RecentPolicies { get; set; } = new();
    public List<Quotation> RecentQuotations { get; set; } = new();
    public List<Payment> RecentPayments { get; set; } = new();
}
