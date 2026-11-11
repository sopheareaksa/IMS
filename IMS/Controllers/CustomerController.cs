using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IMS.Models;
using IMS.Services;
using IMS.ViewModels;
using ClaimEntity = IMS.Models.Claim;

namespace IMS.Controllers;

// [Authorize] is commented out so the Development PreviewMode below can render
// these pages without login. Re-enable before publishing; with PreviewMode off
// the null-customer guards below still redirect anonymous users to login.
//[Authorize(Roles = "Customer")]
public class CustomerController : Controller
{
    private readonly InsuranceContext _context;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public CustomerController(
        InsuranceContext context,
        INotificationService notificationService,
        IConfiguration config,
        IWebHostEnvironment env)
    {
        _context = context;
        _notificationService = notificationService;
        _config = config;
        _env = env;
    }

    public async Task<IActionResult> Dashboard()
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            if (PreviewMode)
            {
                var preview = BuildPreviewData();
                AdoptPreviewIdentity(preview.Customer);
                ViewBag.Customer = preview.Customer;
                ViewBag.Quotations = preview.Quotations;
                ViewBag.Policies = preview.Policies;
                ViewBag.Claims = preview.Claims;
                return View();
            }
            return RedirectToAction("Login", "Auth");
        }

        var quotations = await _context.Quotations
            .Include(q => q.InsuranceProduct)
                .ThenInclude(p => p!.Company)
            .Where(q => q.customerId == customer.customerId)
            .OrderByDescending(q => q.requestDate)
            .ToListAsync();

        var policies = await _context.InsurancePolicies
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.InsuranceProduct)
                    .ThenInclude(pr => pr!.Company)
            .Where(p => p.Quotation!.customerId == customer.customerId)
            .OrderByDescending(p => p.startDate)
            .ToListAsync();

        var policyIds = policies.Select(p => p.inPoId).ToList();
        var claims = await _context.Claims
            .Include(c => c.InsurancePolicy)
            .Where(c => policyIds.Contains(c.inPoId))
            .OrderByDescending(c => c.claimDate)
            .ToListAsync();

        ViewBag.Customer = customer;
        ViewBag.Quotations = quotations;
        ViewBag.Policies = policies;
        ViewBag.Claims = claims;

        return View();
    }

    public async Task<IActionResult> MyQuotations()
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            if (PreviewMode)
            {
                var preview = BuildPreviewData();
                AdoptPreviewIdentity(preview.Customer);
                return View(preview.Quotations);
            }
            return RedirectToAction("Login", "Auth");
        }

        var quotes = await _context.Quotations
            .Include(q => q.InsuranceProduct)
                .ThenInclude(p => p!.Company)
            .Include(q => q.InsuranceProduct!.InsuranceType)
            .Include(q => q.HandlingAgent)
            .Include(q => q.InsurancePolicy)
            .Where(q => q.customerId == customer.customerId)
            .OrderByDescending(q => q.requestDate)
            .ToListAsync();

        return View(quotes);
    }

    public async Task<IActionResult> MyPolicies()
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            if (PreviewMode)
            {
                var preview = BuildPreviewData();
                AdoptPreviewIdentity(preview.Customer);
                return View(preview.Policies);
            }
            return RedirectToAction("Login", "Auth");
        }

        var policies = await _context.InsurancePolicies
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.InsuranceProduct)
                    .ThenInclude(pr => pr!.Company)
            .Include(p => p.Quotation!.InsuranceProduct!.InsuranceType)
            .Include(p => p.Agent)
            .Include(p => p.Beneficiaries)
            .Include(p => p.Payments)
            .Include(p => p.Claims)
            .Where(p => p.Quotation!.customerId == customer.customerId)
            .OrderByDescending(p => p.startDate)
            .ToListAsync();

        return View(policies);
    }

    [HttpGet]
    public async Task<IActionResult> SubmitClaim(int? inPoId = null)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            if (PreviewMode)
            {
                var previewData = BuildPreviewData();
                var previewPolicies = previewData.Policies.Where(p => p.status == "Active").ToList();
                AdoptPreviewIdentity(previewData.Customer);
                ViewBag.Policies = new SelectList(
                    previewPolicies.Select(p => new {
                        p.inPoId,
                        Display = $"{p.policyNumber} - {p.Quotation?.InsuranceProduct?.name} (Coverage: ${p.coverageAmount:N2})"
                    }),
                    "inPoId",
                    "Display",
                    inPoId
                );
                return View(new ClaimEntity
                {
                    inPoId = inPoId ?? previewPolicies.First().inPoId,
                    incidentDate = DateTime.Today
                });
            }
            return RedirectToAction("Login", "Auth");
        }

        var activePolicies = await _context.InsurancePolicies
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.InsuranceProduct)
            .Where(p => p.Quotation!.customerId == customer.customerId && p.status == "Active")
            .ToListAsync();

        if (!activePolicies.Any())
        {
            TempData["ErrorMessage"] = "You do not have any active policies to file a claim against.";
            return RedirectToAction(nameof(MyPolicies));
        }

        ViewBag.Policies = new SelectList(
            activePolicies.Select(p => new {
                p.inPoId,
                Display = $"{p.policyNumber} - {p.Quotation?.InsuranceProduct?.name} (Coverage: ${p.coverageAmount:N2})"
            }),
            "inPoId",
            "Display",
            inPoId
        );

        return View(new ClaimEntity
        {
            inPoId = inPoId ?? activePolicies.First().inPoId,
            incidentDate = DateTime.Today
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitClaim(ClaimEntity model)
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null) return RedirectToAction("Login", "Auth");

        var policy = await _context.InsurancePolicies
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.InsuranceProduct)
                    .ThenInclude(pr => pr!.Company)
            .FirstOrDefaultAsync(p => p.inPoId == model.inPoId && p.Quotation!.customerId == customer.customerId);

        if (policy == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid policy selected.");
            return View(model);
        }

        if (model.claimAmount > policy.coverageAmount)
        {
            ModelState.AddModelError(nameof(model.claimAmount), $"Claim amount cannot exceed total policy coverage (${policy.coverageAmount:N2}).");
            return View(model);
        }

        model.claimDate = DateTime.UtcNow;
        model.status = "Submitted";

        _context.Claims.Add(model);
        await _context.SaveChangesAsync();

        // Alert company agent and staff
        int companyId = policy.Quotation!.InsuranceProduct!.companyId;
        await _notificationService.NotifyCompanyAsync(
            companyId,
            "New Insurance Claim Filed",
            $"Customer '{customer.fullName}' filed claim #{model.claimId} (${model.claimAmount:N2}) for policy #{policy.policyNumber}.",
            "/Agent/Claims",
            "ClaimSubmitted",
            alsoNotifyAdmin: true
        );

        TempData["SuccessMessage"] = $"Claim #{model.claimId} filed successfully! The claims department and assigned agents have been notified.";
        return RedirectToAction(nameof(MyPolicies));
    }

    // =========================================================================
    // Account Profile: View and Update Customer Information
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var customer = await GetCurrentCustomerWithUserAsync();
        if (customer == null)
        {
            if (PreviewMode)
            {
                AdoptPreviewIdentity(BuildPreviewData().Customer);
                return View(BuildPreviewProfileModel());
            }
            return RedirectToAction("Login", "Auth");
        }

        var model = await BuildProfileViewModelAsync(customer);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(CustomerProfileViewModel model)
    {
        var customer = await GetCurrentCustomerWithUserAsync();
        if (customer == null) return RedirectToAction("Login", "Auth");

        // Validate uniqueness of username and email
        var cleanUsername = model.Username.Trim();
        var cleanEmail = model.Email.Trim();

        if (await _context.UserAccounts.AnyAsync(u => u.userId != customer.userId && u.username == cleanUsername))
        {
            ModelState.AddModelError(nameof(model.Username), "This username is already taken by another account.");
        }

        if (await _context.UserAccounts.AnyAsync(u => u.userId != customer.userId && u.email == cleanEmail))
        {
            ModelState.AddModelError(nameof(model.Email), "This email address is already in use by another account.");
        }

        // Validate password change if user requested it
        bool isPasswordChanging = !string.IsNullOrWhiteSpace(model.NewPassword) || !string.IsNullOrWhiteSpace(model.CurrentPassword);
        if (isPasswordChanging)
        {
            if (string.IsNullOrWhiteSpace(model.CurrentPassword))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Current password is required to set a new password.");
            }
            else
            {
                bool isCurrentValid = false;
                try
                {
                    isCurrentValid = BCrypt.Net.BCrypt.Verify(model.CurrentPassword, customer.UserAccount!.passwordHash);
                }
                catch
                {
                    if (model.CurrentPassword == customer.UserAccount!.passwordHash)
                        isCurrentValid = true;
                }

                if (!isCurrentValid)
                {
                    ModelState.AddModelError(nameof(model.CurrentPassword), "The current password you entered is incorrect.");
                }
            }

            if (string.IsNullOrWhiteSpace(model.NewPassword))
            {
                ModelState.AddModelError(nameof(model.NewPassword), "New password is required.");
            }
            else if (model.NewPassword.Length < 6)
            {
                ModelState.AddModelError(nameof(model.NewPassword), "New password must be at least 6 characters.");
            }
            else if (model.NewPassword != model.ConfirmPassword)
            {
                ModelState.AddModelError(nameof(model.ConfirmPassword), "New password and confirmation do not match.");
            }
        }

        if (!ModelState.IsValid)
        {
            // Re-populate activity metrics
            var reloadedModel = await BuildProfileViewModelAsync(customer);
            reloadedModel.FullName = model.FullName;
            reloadedModel.Phone = model.Phone;
            reloadedModel.Address = model.Address;
            reloadedModel.DateOfBirth = model.DateOfBirth;
            reloadedModel.NationalId = model.NationalId;
            reloadedModel.Username = model.Username;
            reloadedModel.Email = model.Email;
            return View(reloadedModel);
        }

        // Apply Profile Updates
        customer.fullName = model.FullName.Trim();
        customer.phone = model.Phone.Trim();
        customer.address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();
        customer.dateOfBirth = model.DateOfBirth;
        customer.nationalId = string.IsNullOrWhiteSpace(model.NationalId) ? null : model.NationalId.Trim();

        if (customer.UserAccount != null)
        {
            customer.UserAccount.username = cleanUsername;
            customer.UserAccount.email = cleanEmail;

            if (isPasswordChanging && !string.IsNullOrWhiteSpace(model.NewPassword))
            {
                customer.UserAccount.passwordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            }
        }

        await _context.SaveChangesAsync();

        // Refresh authentication cookie with updated claims (e.g. FullName)
        var claims = new List<System.Security.Claims.Claim>
        {
            new(ClaimTypes.NameIdentifier, customer.userId.ToString()),
            new(ClaimTypes.Name, customer.UserAccount?.username ?? cleanUsername),
            new(ClaimTypes.Email, customer.UserAccount?.email ?? cleanEmail),
            new(ClaimTypes.Role, customer.UserAccount?.role ?? "Customer"),
            new("CustomerId", customer.customerId.ToString()),
            new("FullName", customer.fullName)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties
        );

        // Send confirmation notification
        await _notificationService.NotifyUserAsync(
            customer.userId,
            "Profile Updated",
            "Your account profile and personal contact details have been successfully saved.",
            "/Customer/Profile",
            "ProfileUpdated"
        );

        TempData["SuccessMessage"] = "Your account profile has been successfully updated!";
        return RedirectToAction(nameof(Profile));
    }

    // =========================================================================
    // Customer Payments History
    // =========================================================================
    public async Task<IActionResult> MyPayments()
    {
        var customer = await GetCurrentCustomerAsync();
        if (customer == null)
        {
            if (PreviewMode)
            {
                var preview = BuildPreviewData();
                AdoptPreviewIdentity(preview.Customer);
                return View(preview.Payments);
            }
            return RedirectToAction("Login", "Auth");
        }

        var policyIds = await _context.InsurancePolicies
            .Where(p => p.Quotation!.customerId == customer.customerId)
            .Select(p => p.inPoId)
            .ToListAsync();

        var payments = await _context.Payments
            .Include(p => p.InsurancePolicy)
                .ThenInclude(pol => pol!.Quotation)
                    .ThenInclude(q => q!.InsuranceProduct)
            .Where(p => policyIds.Contains(p.inPoId))
            .OrderByDescending(p => p.payDate)
            .ToListAsync();

        return View(payments);
    }

    // Development-only UI preview: serves demo data when PreviewMode is set in
    // appsettings.Development.json. Never enable outside Development.
    private bool PreviewMode => PreviewCatalog.IsEnabled(_config, _env);

    private void AdoptPreviewIdentity(Customer customer)
        => PreviewCatalog.AdoptPreviewIdentity(HttpContext, customer);

    private static (Customer Customer, List<Quotation> Quotations, List<InsurancePolicy> Policies, List<ClaimEntity> Claims, List<Payment> Payments) BuildPreviewData()
    {
        var company = PreviewCatalog.Companies()[0];
        var healthProduct = PreviewCatalog.FindProduct(1)!;
        var motorProduct = PreviewCatalog.FindProduct(2)!;

        var agent = new Agent
        {
            agentId = 1,
            userId = 2,
            companyId = 1,
            fullName = "Dara Pich",
            phone = "+855 12 555 778",
            branch = "Phnom Penh HQ",
            status = "Active",
            Company = company
        };

        var customer = PreviewCatalog.DemoCustomer();

        var healthQuote = new Quotation
        {
            quoteId = 1,
            customerId = 1,
            inProId = 1,
            agentId = 1,
            requestDate = DateTime.UtcNow.AddMonths(-3),
            message = "Requesting family coverage for 4 members.",
            estimatedPremium = 1450m,
            status = "Accepted",
            InsuranceProduct = healthProduct,
            HandlingAgent = agent
        };

        var motorQuote = new Quotation
        {
            quoteId = 2,
            customerId = 1,
            inProId = 2,
            agentId = 1,
            requestDate = DateTime.UtcNow.AddDays(-5),
            message = "Quote for Toyota Camry 2022.",
            estimatedPremium = 2100m,
            status = "Quoted",
            InsuranceProduct = motorProduct,
            HandlingAgent = agent
        };

        var healthPolicy = new InsurancePolicy
        {
            inPoId = 1,
            quoteId = 1,
            agentId = 1,
            policyNumber = "IMS-HL-2026-0001",
            startDate = DateTime.UtcNow.AddMonths(-2),
            endDate = DateTime.UtcNow.AddMonths(10),
            premiumAmount = 1450m,
            coverageAmount = 50000m,
            status = "Active",
            Quotation = healthQuote,
            Agent = agent,
            Beneficiaries = new List<Beneficiary>
            {
                new Beneficiary { benId = 1, inPoId = 1, name = "Dara Pich", relation = "Spouse", sharePercentage = 60m, phone = "+855 12 555 778" },
                new Beneficiary { benId = 2, inPoId = 1, name = "Maly Raksa", relation = "Child", sharePercentage = 40m, phone = "+855 98 111 222" }
            },
            Payments = new List<Payment>
            {
                new Payment { payId = 1, inPoId = 1, transactionRef = "TXN-9F3K2L-001", payDate = DateTime.UtcNow.AddMonths(-2), payAmount = 725m, payMethod = "Bakong KHQR", payStatus = "Completed" },
                new Payment { payId = 2, inPoId = 1, transactionRef = "TXN-7H1M4P-002", payDate = DateTime.UtcNow.AddDays(-7), payAmount = 725m, payMethod = "Bakong KHQR", payStatus = "Pending" }
            },
            Claims = new List<ClaimEntity>
            {
                new ClaimEntity
                {
                    claimId = 1,
                    inPoId = 1,
                    incidentDate = DateTime.UtcNow.AddDays(-20),
                    claimDate = DateTime.UtcNow.AddDays(-19),
                    incidentType = "Accident",
                    incidentLocation = "Phnom Penh",
                    claimAmount = 1200m,
                    status = "In Review"
                }
            }
        };

        var motorPolicy = new InsurancePolicy
        {
            inPoId = 2,
            quoteId = 2,
            agentId = 1,
            policyNumber = "IMS-MT-2025-0042",
            startDate = DateTime.UtcNow.AddYears(-1),
            endDate = DateTime.UtcNow.AddMonths(-1),
            premiumAmount = 2100m,
            coverageAmount = 30000m,
            status = "Lapsed",
            Quotation = motorQuote,
            Agent = agent,
            Beneficiaries = new List<Beneficiary>
            {
                new Beneficiary { benId = 3, inPoId = 2, name = "Maly Raksa", relation = "Child", sharePercentage = 100m, phone = "+855 98 111 222" }
            },
            Payments = new List<Payment>
            {
                new Payment { payId = 3, inPoId = 2, transactionRef = "TXN-2B8N6C-003", payDate = DateTime.UtcNow.AddYears(-1), payAmount = 2100m, payMethod = "ABA Bank Transfer", payStatus = "Completed" }
            }
        };

        healthQuote.InsurancePolicy = healthPolicy;

        var quotations = PreviewCatalog.Submitted()
            .Concat(new List<Quotation> { motorQuote, healthQuote })
            .ToList();
        var policies = new List<InsurancePolicy> { healthPolicy, motorPolicy };
        var claims = healthPolicy.Claims.ToList();
        var payments = policies.SelectMany(p => p.Payments).OrderByDescending(p => p.payDate).ToList();

        return (customer, quotations, policies, claims, payments);
    }

    private static CustomerProfileViewModel BuildPreviewProfileModel()
    {
        var preview = BuildPreviewData();
        return new CustomerProfileViewModel
        {
            CustomerId = preview.Customer.customerId,
            UserId = preview.Customer.userId,
            Username = preview.Customer.UserAccount?.username ?? string.Empty,
            Email = preview.Customer.UserAccount?.email ?? string.Empty,
            FullName = preview.Customer.fullName,
            Phone = preview.Customer.phone,
            Address = preview.Customer.address,
            DateOfBirth = preview.Customer.dateOfBirth,
            NationalId = preview.Customer.nationalId,
            MemberSince = preview.Customer.UserAccount?.createdAt ?? DateTime.UtcNow,
            LastLogin = preview.Customer.UserAccount?.lastLogin,
            Status = preview.Customer.UserAccount?.status ?? "Active",
            Role = "Customer",
            ActivePoliciesCount = preview.Policies.Count(p => p.status == "Active"),
            TotalQuotationsCount = preview.Quotations.Count,
            ClaimsCount = preview.Claims.Count,
            TotalAmountPaid = preview.Payments.Where(p => p.payStatus == "Completed").Sum(p => p.payAmount),
            RecentPolicies = preview.Policies.Take(5).ToList(),
            RecentQuotations = preview.Quotations.Take(5).ToList(),
            RecentPayments = preview.Payments.Take(5).ToList()
        };
    }

    private async Task<Customer?> GetCurrentCustomerAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int userId))
        {
            return await _context.Customers.FirstOrDefaultAsync(c => c.userId == userId);
        }
        return null;
    }

    private async Task<Customer?> GetCurrentCustomerWithUserAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdStr, out int userId))
        {
            return await _context.Customers
                .Include(c => c.UserAccount)
                .FirstOrDefaultAsync(c => c.userId == userId);
        }
        return null;
    }

    private async Task<CustomerProfileViewModel> BuildProfileViewModelAsync(Customer customer)
    {
        var policies = await _context.InsurancePolicies
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.InsuranceProduct)
                    .ThenInclude(pr => pr!.Company)
            .Where(p => p.Quotation!.customerId == customer.customerId)
            .OrderByDescending(p => p.startDate)
            .ToListAsync();

        var policyIds = policies.Select(p => p.inPoId).ToList();

        var quotations = await _context.Quotations
            .Include(q => q.InsuranceProduct)
                .ThenInclude(pr => pr!.Company)
            .Where(q => q.customerId == customer.customerId)
            .OrderByDescending(q => q.requestDate)
            .ToListAsync();

        var claims = await _context.Claims
            .Where(c => policyIds.Contains(c.inPoId))
            .ToListAsync();

        var payments = await _context.Payments
            .Include(p => p.InsurancePolicy)
            .Where(p => policyIds.Contains(p.inPoId))
            .OrderByDescending(p => p.payDate)
            .ToListAsync();

        decimal totalPaid = payments
            .Where(p => p.payStatus == "Completed")
            .Sum(p => p.payAmount);

        return new CustomerProfileViewModel
        {
            CustomerId = customer.customerId,
            UserId = customer.userId,
            Username = customer.UserAccount?.username ?? string.Empty,
            Email = customer.UserAccount?.email ?? string.Empty,
            FullName = customer.fullName,
            Phone = customer.phone,
            Address = customer.address,
            DateOfBirth = customer.dateOfBirth,
            NationalId = customer.nationalId,
            MemberSince = customer.UserAccount?.createdAt ?? DateTime.UtcNow,
            LastLogin = customer.UserAccount?.lastLogin,
            Status = customer.UserAccount?.status ?? "Active",
            Role = customer.UserAccount?.role ?? "Customer",
            ActivePoliciesCount = policies.Count(p => p.status == "Active"),
            TotalQuotationsCount = quotations.Count,
            ClaimsCount = claims.Count,
            TotalAmountPaid = totalPaid,
            RecentPolicies = policies.Take(5).ToList(),
            RecentQuotations = quotations.Take(5).ToList(),
            RecentPayments = payments.Take(5).ToList()
        };
    }
}

