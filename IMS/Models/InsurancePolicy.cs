using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("InsurancePolicy")]
public class InsurancePolicy
{
    [Key]
    [Column("inPoId")]
    public int inPoId { get; set; }

    [Column("quoteId")]
    public int quoteId { get; set; }

    [ForeignKey(nameof(quoteId))]
    public virtual Quotation? Quotation { get; set; }

    [Column("agentId")]
    public int? agentId { get; set; }

    [ForeignKey(nameof(agentId))]
    public virtual Agent? Agent { get; set; }

    [Required, StringLength(50)]
    [Column("policyNumber")]
    public string policyNumber { get; set; } = string.Empty;

    [Column("startDate")]
    public DateTime startDate { get; set; }

    [Column("endDate")]
    public DateTime endDate { get; set; }

    [Column("coverageAmount", TypeName = "decimal(12,2)")]
    public decimal coverageAmount { get; set; }

    [Column("premiumAmount", TypeName = "decimal(12,2)")]
    public decimal premiumAmount { get; set; }

    [Required, StringLength(20)]
    [Column("status")]
    public string status { get; set; } = "Active"; // Active, Lapsed, Terminated, Expired

    // Navigations
    public virtual ICollection<Claim> Claims { get; set; } = new List<Claim>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<Beneficiary> Beneficiaries { get; set; } = new List<Beneficiary>();
}

