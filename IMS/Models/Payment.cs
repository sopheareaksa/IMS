using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("Payment")]
public class Payment
{
    [Key]
    [Column("payId")]
    public int payId { get; set; }

    [Column("inPoId")]
    public int inPoId { get; set; }

    [ForeignKey(nameof(inPoId))]
    public virtual InsurancePolicy? InsurancePolicy { get; set; }

    [Required, StringLength(100)]
    [Column("transactionRef")]
    public string transactionRef { get; set; } = string.Empty;

    [Column("payDate")]
    public DateTime payDate { get; set; } = DateTime.UtcNow;

    [Column("payAmount", TypeName = "decimal(12,2)")]
    public decimal payAmount { get; set; }

    [Required, StringLength(50)]
    [Column("payMethod")]
    public string payMethod { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Column("payStatus")]
    public string payStatus { get; set; } = "Completed"; // Pending, Completed, Failed, Refunded
}

