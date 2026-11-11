using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("Notification")]
public class Notification
{
    [Key]
    [Column("notificationId")]
    public int notificationId { get; set; }

    [Column("userId")]
    public int? userId { get; set; }

    [ForeignKey(nameof(userId))]
    public virtual UserAccount? UserAccount { get; set; }

    [Column("companyId")]
    public int? companyId { get; set; }

    [ForeignKey(nameof(companyId))]
    public virtual Company? Company { get; set; }

    [StringLength(20)]
    [Column("targetRole")]
    public string? targetRole { get; set; } // Admin, Agent, Staff, Customer, or null

    [Required, StringLength(100)]
    [Column("title")]
    public string title { get; set; } = string.Empty;

    [Required, StringLength(500)]
    [Column("message")]
    public string message { get; set; } = string.Empty;

    [StringLength(255)]
    [Column("linkUrl")]
    public string? linkUrl { get; set; }

    [Column("isRead")]
    public bool isRead { get; set; } = false;

    [Column("createdAt")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    [Required, StringLength(50)]
    [Column("type")]
    public string type { get; set; } = "General"; // CompanyRegistration, QuotationRequest, PolicyCreated, ClaimSubmitted
}

