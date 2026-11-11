using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using IMS.Models;

namespace IMS.Services;

/// <summary>
/// Development-only UI preview support: a fixed in-memory catalogue of companies,
/// insurance types and products so the browse/quote screens render before the database
/// is configured. Enabled by "PreviewMode": true in appsettings.Development.json only.
/// </summary>
public static class PreviewCatalog
{
    private static readonly object Gate = new();
    private static readonly List<Quotation> SubmittedQuotes = new();

    public static bool IsEnabled(IConfiguration config, IWebHostEnvironment env)
        => env.IsDevelopment() && config.GetValue<bool>("PreviewMode");

    // The demo principal lives on the request only — no auth cookie is issued — so _Layout
    // renders the signed-in Client Portal chrome while every action still reads demo data.
    public static void AdoptPreviewIdentity(HttpContext http, Customer customer)
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, customer.userId.ToString()),
                new System.Security.Claims.Claim(ClaimTypes.Name, customer.UserAccount?.username ?? "preview.customer"),
                new System.Security.Claims.Claim(ClaimTypes.Email, customer.UserAccount?.email ?? "preview@example.com"),
                new System.Security.Claims.Claim("FullName", customer.fullName),
                new System.Security.Claims.Claim(ClaimTypes.Role, "Customer")
            },
            CookieAuthenticationDefaults.AuthenticationScheme);

        http.User = new ClaimsPrincipal(identity);
    }

    public static List<InsuranceType> Types() => _types;

    public static List<Company> Companies() => _companies;

    public static List<InsuranceProduct> Products() => _products;

    public static InsuranceProduct? FindProduct(int inProId)
        => _products.FirstOrDefault(p => p.inProId == inProId);

    public static Customer DemoCustomer() => _customer;

    /// <summary>Adds a preview quotation to the demo customer's list and returns its reference.</summary>
    public static int Submit(Quotation quotation)
    {
        lock (Gate)
        {
            quotation.quoteId = 1000 + SubmittedQuotes.Count + 1;
            SubmittedQuotes.Add(quotation);
            return quotation.quoteId;
        }
    }

    public static List<Quotation> Submitted()
    {
        lock (Gate)
        {
            return SubmittedQuotes.OrderByDescending(q => q.requestDate).ToList();
        }
    }

    private static readonly List<Company> _companies = new()
    {
        new Company
        {
            companyId = 1,
            name = "InsureHub Assurance",
            regNo = "KH-2019-0001",
            email = "care@insurehub.com",
            phone = "+855 23 555 010",
            address = "No. 45, Khandolk St., Phnom Penh",
            isHostCompany = true,
            approvalStatus = "Approved",
            commissionRate = 10m,
            createdAt = DateTime.UtcNow.AddYears(-2)
        },
        new Company
        {
            companyId = 2,
            name = "Forte Insurance Plc",
            regNo = "KH-2024-001",
            email = "underwriting@forte.com.kh",
            phone = "+855 97 800 220",
            address = "Street 310, Tonle Bassac, Phnom Penh",
            isHostCompany = false,
            approvalStatus = "Approved",
            commissionRate = 12m,
            createdAt = DateTime.UtcNow.AddMonths(-20)
        }
    };

    private static Company Host => _companies[0];
    private static Company Forte => _companies[1];

    private static readonly List<InsuranceType> _types = new()
    {
        new InsuranceType { inTypeId = 1, name = "Health Insurance", description = "Medical & hospitalisation cover" },
        new InsuranceType { inTypeId = 2, name = "Motor / Vehicle Insurance", description = "Vehicle damage & third-party liability" },
        new InsuranceType { inTypeId = 3, name = "Life Insurance", description = "Term & whole life protection" },
        new InsuranceType { inTypeId = 4, name = "Home & Property Insurance", description = "Buildings, contents & fire" },
        new InsuranceType { inTypeId = 5, name = "Travel Insurance", description = "Trip, medical & baggage cover" },
        new InsuranceType { inTypeId = 6, name = "Business Insurance", description = "Commercial liability & assets" }
    };

    private static readonly List<InsuranceProduct> _products = new()
    {
        new InsuranceProduct
        {
            inProId = 1, companyId = 1, inTypeId = 1,
            name = "Executive Health Shield",
            detail = "Full inpatient and outpatient cover with 24/7 telehealth, private hospital access and cashless claims across the InsureHub provider network.",
            basePremium = 145m, status = "Active",
            Company = Host, InsuranceType = _types[0]
        },
        new InsuranceProduct
        {
            inProId = 2, companyId = 1, inTypeId = 2,
            name = "InsureHub Total Auto Shield",
            detail = "Own-damage, theft, fire and third-party liability for private vehicles, with roadside assistance and a courtesy car during workshop repairs.",
            basePremium = 280m, status = "Active",
            Company = Host, InsuranceType = _types[1]
        },
        new InsuranceProduct
        {
            inProId = 3, companyId = 2, inTypeId = 1,
            name = "Forte Family Wellness Plan",
            detail = "Shared family hospitalisation limit with maternity, day-care procedures and annual health screenings for up to six members.",
            basePremium = 380m, status = "Active",
            Company = Forte, InsuranceType = _types[0]
        },
        new InsuranceProduct
        {
            inProId = 4, companyId = 1, inTypeId = 3,
            name = "SecureTerm Life Assurance",
            detail = "Level term protection to 180,000 USD with terminal illness acceleration and an optional weekly accident benefit.",
            basePremium = 210m, status = "Active",
            Company = Host, InsuranceType = _types[2]
        },
        new InsuranceProduct
        {
            inProId = 5, companyId = 2, inTypeId = 4,
            name = "Home Protector Plus",
            detail = "Buildings and contents cover against fire, flood, theft and water damage, including temporary accommodation if your home becomes uninhabitable.",
            basePremium = 120m, status = "Active",
            Company = Forte, InsuranceType = _types[3]
        },
        new InsuranceProduct
        {
            inProId = 6, companyId = 2, inTypeId = 5,
            name = "Global Travel Guard",
            detail = "Medical evacuation, trip cancellation, delayed baggage and personal liability cover for single trips or annual multi-trip travel.",
            basePremium = 65m, status = "Active",
            Company = Forte, InsuranceType = _types[4]
        },
        new InsuranceProduct
        {
            inProId = 7, companyId = 2, inTypeId = 6,
            name = "SME Business Liability Cover",
            detail = "Public liability, stock and equipment protection for workshops, retail and light manufacturing, with optional business interruption.",
            basePremium = 520m, status = "Active",
            Company = Forte, InsuranceType = _types[5]
        }
    };

    private static readonly Customer _customer = new()
    {
        customerId = 1,
        userId = 1,
        fullName = "Sok Chanleap",
        phone = "011 234 5678",
        address = "Phnom Penh",
        dateOfBirth = new DateTime(1994, 7, 22),
        nationalId = "112345678",
        UserAccount = new UserAccount
        {
            userId = 1,
            username = "chanleap.demo",
            email = "chanleap@example.com",
            role = "Customer",
            status = "Active",
            createdAt = DateTime.UtcNow.AddYears(-2),
            lastLogin = DateTime.UtcNow.AddHours(-3)
        }
    };
}
