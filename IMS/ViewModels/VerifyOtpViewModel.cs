using System.ComponentModel.DataAnnotations;

namespace IMS.ViewModels;

public class VerifyOtpViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter the 6-digit OTP.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "OTP must be exactly 6 digits.")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "OTP must contain numbers only.")]
    public string Otp { get; set; } = string.Empty;
}

