using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("Agent")]
public class Agent
{
    [Key]
    [Column("agentId")]
    public int agentId { get; set; }

    [Column("userId")]
    public int userId { get; set; }

    [ForeignKey(nameof(userId))]
    public virtual UserAccount? UserAccount { get; set; }

    [Column("companyId")]
    public int companyId { get; set; }

    [ForeignKey(nameof(companyId))]
    public virtual Company? Company { get; set; }

    [Required, StringLength(100)]
    [Column("fullName")]
    public string fullName { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Column("phone")]
    public string phone { get; set; } = string.Empty;

    [StringLength(50)]
    [Column("branch")]
    public string? branch { get; set; }

    [Required, StringLength(20)]
    [Column("status")]
    public string status { get; set; } = "Active";

    // Navigations
    public virtual ICollection<Staff> StaffMembers { get; set; } = new List<Staff>();
    public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
    public virtual ICollection<InsurancePolicy> Policies { get; set; } = new List<InsurancePolicy>();
}

