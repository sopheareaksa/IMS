using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("InsuranceProduct")]
public class InsuranceProduct
{
    [Key]
    [Column("inProId")]
    public int inProId { get; set; }

    [Column("companyId")]
    public int companyId { get; set; }

    [ForeignKey(nameof(companyId))]
    public virtual Company? Company { get; set; }

    [Column("inTypeId")]
    public int inTypeId { get; set; }

    [ForeignKey(nameof(inTypeId))]
    public virtual InsuranceType? InsuranceType { get; set; }

    [Required, StringLength(100)]
    [Column("name")]
    public string name { get; set; } = string.Empty;

    [Column("basePremium", TypeName = "decimal(12,2)")]
    public decimal basePremium { get; set; }

    [StringLength(255)]
    [Column("detail")]
    public string? detail { get; set; }

    [Required, StringLength(20)]
    [Column("status")]
    public string status { get; set; } = "Active";

    // Navigations
    public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
}

