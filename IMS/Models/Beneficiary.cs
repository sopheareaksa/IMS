using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("Beneficiary")]
public class Beneficiary
{
    [Key]
    [Column("benId")]
    public int benId { get; set; }

    [Column("inPoId")]
    public int inPoId { get; set; }

    [ForeignKey(nameof(inPoId))]
    public virtual InsurancePolicy? InsurancePolicy { get; set; }

    [Required, StringLength(100)]
    [Column("name")]
    public string name { get; set; } = string.Empty;

    [Required, StringLength(50)]
    [Column("relation")]
    public string relation { get; set; } = string.Empty;

    [StringLength(20)]
    [Column("phone")]
    public string? phone { get; set; }

    [Column("sharePercentage", TypeName = "decimal(5,2)")]
    public decimal sharePercentage { get; set; } = 100.00m;
}

