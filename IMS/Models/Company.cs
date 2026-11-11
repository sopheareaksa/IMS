using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("Company")]
public class Company
{
    [Key]
    [Column("companyId")]
    public int companyId { get; set; }

    [Column("userId")]
    public int userId { get; set; }

    [ForeignKey(nameof(userId))]
    public virtual UserAccount? UserAccount { get; set; }

    [Required, StringLength(100)]
    [Column("name")]
    public string name { get; set; } = string.Empty;

    [Required, StringLength(50)]
    [Column("regNo")]
    public string regNo { get; set; } = string.Empty;

    [Required, StringLength(100)]
    [Column("email")]
    public string email { get; set; } = string.Empty;

    [Required, StringLength(20)]
    [Column("phone")]
    public string phone { get; set; } = string.Empty;

    [StringLength(255)]
    [Column("address")]
    public string? address { get; set; }

    [Column("isHostCompany")]
    public bool isHostCompany { get; set; } = false;

    [Required, StringLength(20)]
    [Column("approvalStatus")]
    public string approvalStatus { get; set; } = "Pending"; // Pending, Approved, Rejected, Suspended

    [Column("commissionRate", TypeName = "decimal(5,2)")]
    public decimal commissionRate { get; set; } = 0.0m;

    [Column("createdAt")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    // Navigations
    public virtual ICollection<Agent> Agents { get; set; } = new List<Agent>();
    public virtual ICollection<Staff> StaffMembers { get; set; } = new List<Staff>();
    public virtual ICollection<InsuranceProduct> Products { get; set; } = new List<InsuranceProduct>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

