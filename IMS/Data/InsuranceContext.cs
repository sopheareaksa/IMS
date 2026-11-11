using Microsoft.EntityFrameworkCore;

namespace IMS.Models;

public class InsuranceContext : DbContext
{
    public InsuranceContext(DbContextOptions<InsuranceContext> options)
        : base(options)
    {
    }

    public DbSet<UserAccount> UserAccounts { get; set; }
    public DbSet<Company> Companies { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Agent> Agents { get; set; }
    public DbSet<Staff> StaffMembers { get; set; }
    public DbSet<InsuranceType> InsuranceTypes { get; set; }
    public DbSet<InsuranceProduct> InsuranceProducts { get; set; }
    public DbSet<Quotation> Quotations { get; set; }
    public DbSet<InsurancePolicy> InsurancePolicies { get; set; }
    public DbSet<Claim> Claims { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<Beneficiary> Beneficiaries { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Singular table names
        modelBuilder.Entity<UserAccount>().ToTable("UserAccount");
        modelBuilder.Entity<Company>().ToTable("Company");
        modelBuilder.Entity<Customer>().ToTable("Customer");
        modelBuilder.Entity<Agent>().ToTable("Agent");
        modelBuilder.Entity<Staff>().ToTable("Staff");
        modelBuilder.Entity<InsuranceType>().ToTable("InsuranceType");
        modelBuilder.Entity<InsuranceProduct>().ToTable("InsuranceProduct");
        modelBuilder.Entity<Quotation>().ToTable("Quotation");
        modelBuilder.Entity<InsurancePolicy>().ToTable("InsurancePolicy");
        modelBuilder.Entity<Claim>().ToTable("Claim");
        modelBuilder.Entity<Payment>().ToTable("Payment");
        modelBuilder.Entity<Beneficiary>().ToTable("Beneficiary");
        modelBuilder.Entity<Notification>().ToTable("Notification");

        // 1-to-1 UserAccount - Customer
        modelBuilder.Entity<Customer>()
            .HasOne(c => c.UserAccount)
            .WithOne(u => u.Customer)
            .HasForeignKey<Customer>(c => c.userId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-to-1 UserAccount - Company
        modelBuilder.Entity<Company>()
            .HasOne(c => c.UserAccount)
            .WithOne(u => u.Company)
            .HasForeignKey<Company>(c => c.userId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-to-1 UserAccount - Agent
        modelBuilder.Entity<Agent>()
            .HasOne(a => a.UserAccount)
            .WithOne(u => u.Agent)
            .HasForeignKey<Agent>(a => a.userId)
            .OnDelete(DeleteBehavior.Cascade);

        // 1-to-1 UserAccount - Staff
        modelBuilder.Entity<Staff>()
            .HasOne(s => s.UserAccount)
            .WithOne(u => u.Staff)
            .HasForeignKey<Staff>(s => s.userId)
            .OnDelete(DeleteBehavior.Cascade);

        // Company - Agent (1 to many)
        modelBuilder.Entity<Agent>()
            .HasOne(a => a.Company)
            .WithMany(c => c.Agents)
            .HasForeignKey(a => a.companyId)
            .OnDelete(DeleteBehavior.NoAction);

        // Company - Staff (1 to many)
        modelBuilder.Entity<Staff>()
            .HasOne(s => s.Company)
            .WithMany(c => c.StaffMembers)
            .HasForeignKey(s => s.companyId)
            .OnDelete(DeleteBehavior.NoAction);

        // Agent - Staff (1 to many)
        modelBuilder.Entity<Staff>()
            .HasOne(s => s.SupervisingAgent)
            .WithMany(a => a.StaffMembers)
            .HasForeignKey(s => s.agentId)
            .OnDelete(DeleteBehavior.NoAction);

        // Company - InsuranceProduct (1 to many)
        modelBuilder.Entity<InsuranceProduct>()
            .HasOne(p => p.Company)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.companyId)
            .OnDelete(DeleteBehavior.NoAction);

        // InsuranceType - InsuranceProduct (1 to many)
        modelBuilder.Entity<InsuranceProduct>()
            .HasOne(p => p.InsuranceType)
            .WithMany(t => t.Products)
            .HasForeignKey(p => p.inTypeId)
            .OnDelete(DeleteBehavior.NoAction);

        // Quotation relationships
        modelBuilder.Entity<Quotation>()
            .HasOne(q => q.Customer)
            .WithMany(c => c.Quotations)
            .HasForeignKey(q => q.customerId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Quotation>()
            .HasOne(q => q.InsuranceProduct)
            .WithMany(p => p.Quotations)
            .HasForeignKey(q => q.inProId)
            .OnDelete(DeleteBehavior.NoAction);

        modelBuilder.Entity<Quotation>()
            .HasOne(q => q.HandlingAgent)
            .WithMany(a => a.Quotations)
            .HasForeignKey(q => q.agentId)
            .OnDelete(DeleteBehavior.SetNull);

        // 1-to-1 Quotation - InsurancePolicy
        modelBuilder.Entity<InsurancePolicy>()
            .HasOne(p => p.Quotation)
            .WithOne(q => q.InsurancePolicy)
            .HasForeignKey<InsurancePolicy>(p => p.quoteId)
            .OnDelete(DeleteBehavior.NoAction);

        // InsurancePolicy - Agent (1 to many)
        modelBuilder.Entity<InsurancePolicy>()
            .HasOne(p => p.Agent)
            .WithMany(a => a.Policies)
            .HasForeignKey(p => p.agentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Policy - Claim (1 to many)
        modelBuilder.Entity<Claim>()
            .HasOne(c => c.InsurancePolicy)
            .WithMany(p => p.Claims)
            .HasForeignKey(c => c.inPoId)
            .OnDelete(DeleteBehavior.NoAction);

        // Policy - Payment (1 to many)
        modelBuilder.Entity<Payment>()
            .HasOne(p => p.InsurancePolicy)
            .WithMany(pol => pol.Payments)
            .HasForeignKey(p => p.inPoId)
            .OnDelete(DeleteBehavior.NoAction);

        // Policy - Beneficiary (1 to many with cascade)
        modelBuilder.Entity<Beneficiary>()
            .HasOne(b => b.InsurancePolicy)
            .WithMany(p => p.Beneficiaries)
            .HasForeignKey(b => b.inPoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Notification
        modelBuilder.Entity<Notification>()
            .HasOne(n => n.UserAccount)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.userId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Notification>()
            .HasOne(n => n.Company)
            .WithMany(c => c.Notifications)
            .HasForeignKey(n => n.companyId)
            .OnDelete(DeleteBehavior.NoAction);
    }
}