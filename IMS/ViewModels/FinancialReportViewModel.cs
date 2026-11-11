using IMS.Models;

namespace IMS.ViewModels;

public class FinancialReportViewModel
{
    public string Timeframe { get; set; } = "monthly"; // daily, weekly, monthly, yearly
    public string Scope { get; set; } = "all"; // "all", "admin", "partner"
    public int? SelectedCompanyId { get; set; }
    public string? SelectedCompanyName { get; set; }
    public List<Company> AvailableCompanies { get; set; } = new();
    public List<Company> PartnerCompanies { get; set; } = new();

    // Host Company (Admin Owner) Details
    public CompanyReportItem? HostCompanyReport { get; set; }
    public int? HostCompanyId { get; set; }
    public string HostCompanyName { get; set; } = "InsureHub Direct Protection";

    // Summary KPI metrics (for currently selected scope)
    public decimal TotalRevenue { get; set; }
    public decimal TotalClaimsPaid { get; set; }
    public decimal NetRevenue => TotalRevenue - TotalClaimsPaid;
    public int TotalPoliciesCount { get; set; }
    public int TotalTransactionsCount { get; set; }
    public decimal AverageTransactionAmount => TotalTransactionsCount > 0 ? TotalRevenue / TotalTransactionsCount : 0m;
    public decimal ClaimLossRatio => TotalRevenue > 0 ? Math.Round((TotalClaimsPaid / TotalRevenue) * 100, 1) : 0m;

    // Workforce & Customer Metrics
    public int TotalStaffCount { get; set; } // Amount of staff
    public int TotalCustomersCount { get; set; } // Amount of customers that bought product

    // Breakdown between Admin Owner and Partners
    public decimal AdminRevenue { get; set; }
    public int AdminPoliciesCount { get; set; }
    public int AdminStaffCount { get; set; }
    public int AdminCustomersCount { get; set; }

    public decimal PartnerRevenue { get; set; }
    public int PartnerPoliciesCount { get; set; }
    public int PartnerStaffCount { get; set; }
    public int PartnerCustomersCount { get; set; }

    // Chart Time-Series Data
    public List<string> ChartLabels { get; set; } = new();
    public List<decimal> RevenueData { get; set; } = new();
    public List<decimal> ClaimsData { get; set; } = new();
    public List<decimal> NetData { get; set; } = new();

    // Category / Product Breakdown
    public List<ReportBreakdownItem> CategoryBreakdown { get; set; } = new();
    public List<CompanyReportItem> CompanyReports { get; set; } = new(); // Used by Admin: Partner Company Report with staff and customers
    public List<ReportBreakdownItem> ProductBreakdown { get; set; } = new(); // Used by Agent

    // Customer buyers list (customers that bought product)
    public List<ReportCustomerBuyerItem> CustomerBuyers { get; set; } = new();

    // Recent Payments / Transactions
    public List<ReportTransactionItem> RecentTransactions { get; set; } = new();
}

public class CompanyReportItem
{
    public int CompanyId { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyEmail { get; set; }
    public string? CompanyPhone { get; set; }
    public bool IsHostCompany { get; set; }
    public int StaffCount { get; set; } // Amount of staff in partner company
    public int CustomerCount { get; set; } // Amount of customers that bought their product
    public int PoliciesCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public decimal TotalClaimsPaid { get; set; }
    public decimal NetRevenue => TotalRevenue - TotalClaimsPaid;
    public decimal RevenuePercentage { get; set; }
}

public class ReportCustomerBuyerItem
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    public int PoliciesCount { get; set; }
    public decimal TotalSpent { get; set; }
    public string LatestProductName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public DateTime LatestPurchaseDate { get; set; }
    public string PolicyStatus { get; set; } = "Active";
}

public class ReportBreakdownItem
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Count { get; set; }
    public decimal Percentage { get; set; }
}

public class ReportTransactionItem
{
    public int PaymentId { get; set; }
    public string TransactionRef { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public DateTime PayDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

