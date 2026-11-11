using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("Claim")]
public class Claim
{
    [Key]
    [Column("claimId")]
    public int claimId { get; set; }

    [Column("inPoId")]
    public int inPoId { get; set; }

    [ForeignKey(nameof(inPoId))]
    public virtual InsurancePolicy? InsurancePolicy { get; set; }

    [Column("claimDate")]
    public DateTime claimDate { get; set; } = DateTime.UtcNow;

    [Column("incidentDate")]
    public DateTime incidentDate { get; set; }

    [Required, StringLength(50)]
    [Column("incidentType")]
    public string incidentType { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Column("incidentLocation")]
    public string incidentLocation { get; set; } = string.Empty;

    [Column("claimAmount", TypeName = "decimal(12,2)")]
    public decimal claimAmount { get; set; }

    [Required, StringLength(20)]
    [Column("status")]
    public string status { get; set; } = "Submitted"; // Submitted, In Review, Approved, Rejected, Paid
}

