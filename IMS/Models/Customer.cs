using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IMS.Models;

[Table("Customer")]
public class Customer
{
    [Key]
    [Column("customerId")]
    public int customerId { get; set; }

    [Column("userId")]
    public int userId { get; set; }

    [ForeignKey(nameof(userId))]
    public virtual UserAccount? UserAccount { get; set; }

    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100)]
    [Column("fullName")]
    public string fullName { get; set; } = string.Empty;

    [StringLength(255)]
    [Column("address")]
    public string? address { get; set; }

    [Required, StringLength(20)]
    [Column("phone")]
    public string phone { get; set; } = string.Empty;

    [Column("dateOfBirth")]
    public DateTime? dateOfBirth { get; set; }

    [StringLength(50)]
    [Display(Name = "National ID / Passport")]
    [Column("nationalId")]
    public string? nationalId { get; set; }

    // Navigations
    public virtual ICollection<Quotation> Quotations { get; set; } = new List<Quotation>();
}

