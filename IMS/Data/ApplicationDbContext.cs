using IMS.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IMS.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : IdentityDbContext<ApplicationUser, IdentityRole<int>, int>(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Configure ApplicationUser / UserAccount table
            builder.Entity<ApplicationUser>(b =>
            {
                b.ToTable("UserAccount");

                b.Property(u => u.UserName).HasMaxLength(50);
                b.Property(u => u.Email).HasMaxLength(100);
                b.Property(u => u.Status).HasMaxLength(20).HasDefaultValue("Active");
                b.Property(u => u.CreatedAt).HasDefaultValueSql("GETDATE()");
            });

            // Configure IdentityRole<int> table
            builder.Entity<IdentityRole<int>>(b =>
            {
                b.ToTable("Roles");
            });
        }
    }
}

