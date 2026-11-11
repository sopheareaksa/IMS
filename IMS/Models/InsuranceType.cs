using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("InsuranceType")]
public class InsuranceType
{
    [Key]
    [Column("inTypeId")]
    public int inTypeId { get; set; }

    [Required, StringLength(50)]
    [Column("name")]
    public string name { get; set; } = string.Empty;

    [StringLength(255)]
    [Column("description")]
    public string? description { get; set; }

    // Navigations
    public virtual ICollection<InsuranceProduct> Products { get; set; } = new List<InsuranceProduct>();
}

