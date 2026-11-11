using System.ComponentModel.DataAnnotations;

namespace IMS.ViewModels;

public class IssuePolicyPaymentViewModel
{
    // Quotation context
    public int QuoteId { get; set; }
    public int InProId { get; set; }
    public int? AgentId { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerPhone { get; set; }
    public string? CustomerNationalId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;

    // Policy Issue Fields
    [Required(ErrorMessage = "Policy Number is required")]
    [Display(Name = "Policy Number")]
    public string PolicyNumber { get; set; } = string.Empty;

    [Required(ErrorMessage = "Coverage Limit is required")]
    [Range(1, 100000000, ErrorMessage = "Coverage limit must be greater than 0")]
    [Display(Name = "Total Coverage Limit ($)")]
    public decimal CoverageAmount { get; set; }

    [Required(ErrorMessage = "Start Date is required")]
    [DataType(DataType.Date)]
    [Display(Name = "Coverage Start Date")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "End Date is required")]
    [DataType(DataType.Date)]
    [Display(Name = "Coverage End Date")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddYears(1);

    [Required(ErrorMessage = "Premium Amount is required")]
    [Range(0.01, 10000000, ErrorMessage = "Premium amount must be greater than 0")]
    [Display(Name = "Agreed Premium Amount ($)")]
    public decimal PremiumAmount { get; set; }

    // Beneficiary Fields
    [Display(Name = "Primary Beneficiary Name")]
    public string? BeneficiaryName { get; set; }

    [Display(Name = "Relationship to Insured")]
    public string? BeneficiaryRelation { get; set; }

    [Display(Name = "Beneficiary Contact Phone")]
    public string? BeneficiaryPhone { get; set; }

    // =========================================================================
    // Payment Method Selection: "QR" (Bakong KHQR) or "FieldForm" (Card / Bank)
    // =========================================================================
    [Display(Name = "Payment Method")]
    public string PaymentMethod { get; set; } = "QR"; // "QR" or "FieldForm"

    [Display(Name = "Field Payment Option")]
    public string FieldPaymentType { get; set; } = "Card"; // "Card" or "BankTransfer"

    // --- Card Details (for Field Form: Card) ---
    [Display(Name = "Cardholder Name")]
    public string? CardHolderName { get; set; }

    [Display(Name = "Card Number")]
    public string? CardNumber { get; set; }

    [Display(Name = "Expiration Date (MM/YY)")]
    public string? CardExpiry { get; set; }

    [Display(Name = "CVV / CVC")]
    public string? CardCvv { get; set; }

    [Display(Name = "Card Network")]
    public string? CardType { get; set; } = "Visa"; // Visa, MasterCard, UnionPay, JCB, American Express

    [Display(Name = "Billing Postal / ZIP Code")]
    public string? BillingZip { get; set; }

    [Display(Name = "Billing Address")]
    public string? BillingAddress { get; set; }

    // --- Direct Bank Transfer Details (for Field Form: BankTransfer) ---
    [Display(Name = "Bank Name")]
    public string? BankName { get; set; } = "ABA Bank"; // ABA Bank, ACLEDA Bank, Canadia Bank, Sathapana Bank, Wing Bank, Vattanac Bank

    [Display(Name = "Bank Account Number")]
    public string? BankAccountNumber { get; set; }

    [Display(Name = "Account Holder Name")]
    public string? BankAccountHolder { get; set; }

    [Display(Name = "Bank Transaction / Ref No.")]
    public string? BankTransactionRef { get; set; }

    [Display(Name = "Payment Notes / Remarks")]
    public string? BankNotes { get; set; }

    // =========================================================================
    // Bakong KHQR details (for QR method)
    // =========================================================================
    public string QrCodeString { get; set; } = string.Empty;
    public string Md5 { get; set; } = string.Empty;
    public string Currency { get; set; } = "USD";
    public long ExpirationTimestamp { get; set; }
    public string BakongAccountId { get; set; } = string.Empty;
    public string MerchantName { get; set; } = string.Empty;
    public string MerchantCity { get; set; } = string.Empty;

    // Status / Messages
    public string? ErrorMessage { get; set; }
}
