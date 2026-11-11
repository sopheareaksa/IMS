using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("Quotation")]
public class Quotation
{
    [Key]
    [Column("quoteId")]
    public int quoteId { get; set; }

    [Column("customerId")]
    public int customerId { get; set; }

    [ForeignKey(nameof(customerId))]
    public virtual Customer? Customer { get; set; }

    [Column("inProId")]
    public int inProId { get; set; }

    [ForeignKey(nameof(inProId))]
    public virtual InsuranceProduct? InsuranceProduct { get; set; }

    [Column("agentId")]
    public int? agentId { get; set; }

    [ForeignKey(nameof(agentId))]
    public virtual Agent? HandlingAgent { get; set; }

    [Column("requestDate")]
    public DateTime requestDate { get; set; } = DateTime.UtcNow;

    [StringLength(255)]
    [Column("message")]
    public string? message { get; set; }

    [Column("estimatedPremium", TypeName = "decimal(12,2)")]
    public decimal estimatedPremium { get; set; }

    [Required, StringLength(20)]
    [Column("status")]
    public string status { get; set; } = "Pending"; // Pending, Accepted, Rejected, Expired

    // Navigations
    public virtual InsurancePolicy? InsurancePolicy { get; set; }
}

