using System.ComponentModel.DataAnnotations;
using IMS.Models;

namespace IMS.ViewModels;

public class AdminDashboardViewModel
{
    public int TotalCompanies { get; set; }
    public int PendingCompaniesCount { get; set; }
    public int TotalProducts { get; set; }
    public int TotalQuotations { get; set; }
    public int ActivePoliciesCount { get; set; }
    public int PendingClaimsCount { get; set; }
    public int TotalStaffCount { get; set; }
    public int TotalCustomersCount { get; set; }
    public decimal TotalPremiumsCollected { get; set; }

    public List<Company> PendingCompanies { get; set; } = new();
    public List<Quotation> PendingAdminQuotations { get; set; } = new();
    public int PendingAdminQuotationsCount { get; set; }
    public List<Quotation> RecentQuotations { get; set; } = new();
    public List<Claim> RecentClaims { get; set; } = new();
}

public class AddStaffViewModel
{
    [Required, StringLength(50, MinimumLength = 3)]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    [Required]
    [Display(Name = "Company")]
    public int CompanyId { get; set; }

    [Required]
    [Display(Name = "Supervising Agent")]
    public int AgentId { get; set; }

    [Display(Name = "Receive Quote & Incident Alerts")]
    public bool ReceiveAlerts { get; set; } = true;

    public string Status { get; set; } = "Active";
}

public class CreateProductViewModel
{
    [Required]
    [Display(Name = "Company / Provider")]
    public int CompanyId { get; set; }

    [Required]
    [Display(Name = "Insurance Type / Category")]
    public int InTypeId { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Product Plan Name")]
    public string Name { get; set; } = string.Empty;

    [Required, Range(1, 1000000)]
    [Display(Name = "Base Annual Premium ($)")]
    public decimal BasePremium { get; set; }

    [StringLength(255)]
    [Display(Name = "Plan Details & Benefits")]
    public string? Detail { get; set; }

    public string Status { get; set; } = "Active";
}

public class EditProductViewModel
{
    [Required]
    public int InProId { get; set; }

    [Required]
    [Display(Name = "Company / Provider")]
    public int CompanyId { get; set; }

    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    public bool IsHostCompany { get; set; }

    [Required]
    [Display(Name = "Insurance Type / Category")]
    public int InTypeId { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Product Plan Name")]
    public string Name { get; set; } = string.Empty;

    [Required, Range(1, 1000000)]
    [Display(Name = "Base Annual Premium ($)")]
    public decimal BasePremium { get; set; }

    [StringLength(255)]
    [Display(Name = "Plan Details & Benefits")]
    public string? Detail { get; set; }

    [Required]
    public string Status { get; set; } = "Active";
}

public class EditStaffViewModel
{
    [Required]
    public int StaffId { get; set; }

    public int UserId { get; set; }

    [Display(Name = "Company")]
    public int CompanyId { get; set; }

    [Display(Name = "Company Name")]
    public string? CompanyName { get; set; }

    public bool IsHostCompany { get; set; }

    [Display(Name = "Login Username")]
    public string Username { get; set; } = string.Empty;

    [Required, EmailAddress]
    [Display(Name = "Email Address")]
    public string Email { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Phone]
    public string? Phone { get; set; }

    [Required]
    [Display(Name = "Supervising Agent")]
    public int AgentId { get; set; }

    [Display(Name = "Receive Quote & Incident Alerts")]
    public bool ReceiveAlerts { get; set; } = true;

    [Required]
    public string Status { get; set; } = "Active";

    [StringLength(100, MinimumLength = 6)]
    [DataType(DataType.Password)]
    [Display(Name = "New Password (leave blank to keep current)")]
    public string? NewPassword { get; set; }
}


