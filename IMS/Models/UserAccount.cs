using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("UserAccount")]
public class UserAccount
{
    [Key]
    [Column("userId")]
    public int userId { get; set; }

    [Required(ErrorMessage = "Username is required.")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "Username must be between 3 and 50 characters.")]
    [Column("username")]
    public string username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address.")]
    [Column("email")]
    public string email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(255)]
    [Column("passwordHash")]
    public string passwordHash { get; set; } = string.Empty;

    [Required(ErrorMessage = "Role is required.")]
    [StringLength(20)]
    [Column("role")]
    public string role { get; set; } = string.Empty; // Admin, Agent, Staff, Customer

    [Required, StringLength(20)]
    [Column("status")]
    public string status { get; set; } = "Active";

    [Column("lastLogin")]
    public DateTime? lastLogin { get; set; }

    [Column("createdAt")]
    public DateTime createdAt { get; set; } = DateTime.UtcNow;

    // Navigations
    public virtual Customer? Customer { get; set; }
    public virtual Company? Company { get; set; }
    public virtual Agent? Agent { get; set; }
    public virtual Staff? Staff { get; set; }
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}

