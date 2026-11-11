namespace IMS.ViewModels;

public class PaymentResultViewModel
{
    public bool Success { get; set; }
    public int QuoteId { get; set; }
    public int PolicyId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal PremiumAmount { get; set; }
    public string Currency { get; set; } = "USD";
    public decimal CoverageAmount { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string TransactionRef { get; set; } = string.Empty;
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string? BeneficiaryName { get; set; }
    public string? BeneficiaryRelation { get; set; }
    public string? ErrorMessage { get; set; }
    public string? Md5 { get; set; }

    // Payment Method Details
    public string PaymentMethod { get; set; } = "Bakong KHQR";
    public string? MaskedAccountOrCard { get; set; }
}
