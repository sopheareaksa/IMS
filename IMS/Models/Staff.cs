using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("Staff")]
public class Staff
{
    [Key]
    [Column("staffId")]
    public int staffId { get; set; }

    [Column("userId")]
    public int userId { get; set; }

    [ForeignKey(nameof(userId))]
    public virtual UserAccount? UserAccount { get; set; }

    [Column("agentId")]
    public int agentId { get; set; }

    [ForeignKey(nameof(agentId))]
    public virtual Agent? SupervisingAgent { get; set; }

    [Column("companyId")]
    public int companyId { get; set; }

    [ForeignKey(nameof(companyId))]
    public virtual Company? Company { get; set; }

    [Required, StringLength(100)]
    [Column("fullName")]
    public string fullName { get; set; } = string.Empty;

    [StringLength(20)]
    [Column("phone")]
    public string? phone { get; set; }

    [Column("receiveAlerts")]
    public bool receiveAlerts { get; set; } = true;

    [Required, StringLength(20)]
    [Column("status")]
    public string status { get; set; } = "Active";
}

