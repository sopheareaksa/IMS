using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IMS.Models;
using IMS.ViewModels;
using IMS.Services;

namespace IMS.Controllers;

[Authorize(Roles = "Agent,Staff")]
public class AgentController : Controller
{
    private readonly InsuranceContext _context;
    private readonly INotificationService _notificationService;

    public AgentController(InsuranceContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Dashboard()
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null)
        {
            TempData["ErrorMessage"] = "No company affiliation found for your account.";
            return RedirectToAction("Index", "Home");
        }

        var companyId = company.companyId;

        ViewBag.Company = company;
        ViewBag.TotalProducts = await _context.InsuranceProducts.CountAsync(p => p.companyId == companyId);
        ViewBag.PendingQuotes = await _context.Quotations.CountAsync(q => q.InsuranceProduct!.companyId == companyId && q.status == "Pending");
        ViewBag.TotalQuotes = await _context.Quotations.CountAsync(q => q.InsuranceProduct!.companyId == companyId);
        ViewBag.ActivePolicies = await _context.InsurancePolicies.CountAsync(p => p.Quotation!.InsuranceProduct!.companyId == companyId && p.status == "Active");
        ViewBag.PendingClaims = await _context.Claims.CountAsync(c => c.InsurancePolicy!.Quotation!.InsuranceProduct!.companyId == companyId && (c.status == "Submitted" || c.status == "In Review"));

        var recentQuotes = await _context.Quotations
            .Include(q => q.Customer)
            .Include(q => q.InsuranceProduct)
            .Where(q => q.InsuranceProduct!.companyId == companyId)
            .OrderByDescending(q => q.requestDate)
            .Take(5)
            .ToListAsync();

        var recentClaims = await _context.Claims
            .Include(c => c.InsurancePolicy)
                .ThenInclude(p => p!.Quotation)
                    .ThenInclude(q => q!.Customer)
            .Include(c => c.InsurancePolicy)
                .ThenInclude(p => p!.Quotation)
                    .ThenInclude(q => q!.InsuranceProduct)
            .Where(c => c.InsurancePolicy!.Quotation!.InsuranceProduct!.companyId == companyId)
            .OrderByDescending(c => c.claimDate)
            .Take(5)
            .ToListAsync();

        ViewBag.RecentQuotes = recentQuotes;
        ViewBag.RecentClaims = recentClaims;

        return View();
    }

    // ==========================================
    // QUOTATION REVIEW & POLICY CONVERSION
    // ==========================================
    public async Task<IActionResult> Quotations(string? status = null)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var query = _context.Quotations
            .Include(q => q.Customer)
            .Include(q => q.InsuranceProduct)
            .Include(q => q.HandlingAgent)
            .Include(q => q.InsurancePolicy)
            .Where(q => q.InsuranceProduct!.companyId == company.companyId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(q => q.status == status);
        }

        ViewBag.SelectedStatus = status ?? "All";
        var quotes = await query.OrderByDescending(q => q.requestDate).ToListAsync();
        return View(quotes);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AcceptQuote(int id)
    {
        var quote = await _context.Quotations
            .Include(q => q.Customer)
            .Include(q => q.InsuranceProduct)
            .FirstOrDefaultAsync(q => q.quoteId == id);

        if (quote == null)
        {
            TempData["ErrorMessage"] = "Quotation not found.";
            return RedirectToAction(nameof(Quotations));
        }

        var agent = await GetCurrentAgentAsync();
        if (agent != null)
        {
            quote.agentId = agent.agentId;
        }
        quote.status = "Accepted";
        await _context.SaveChangesAsync();

        if (quote.Customer != null)
        {
            await _notificationService.NotifyUserAsync(
                quote.Customer.userId,
                "Quotation Accepted!",
                $"Great news! Your quotation request #{quote.quoteId} for '{quote.InsuranceProduct?.name}' has been ACCEPTED by the insurer.",
                "/Customer/MyQuotations",
                "QuotationAccepted"
            );
        }

        TempData["SuccessMessage"] = $"Quotation #{quote.quoteId} has been Accepted. You can now issue an official Insurance Policy.";
        return RedirectToAction(nameof(Quotations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectQuote(int id)
    {
        var quote = await _context.Quotations
            .Include(q => q.Customer)
            .Include(q => q.InsuranceProduct)
            .FirstOrDefaultAsync(q => q.quoteId == id);

        if (quote == null)
        {
            TempData["ErrorMessage"] = "Quotation not found.";
            return RedirectToAction(nameof(Quotations));
        }

        quote.status = "Rejected";
        await _context.SaveChangesAsync();

        if (quote.Customer != null)
        {
            await _notificationService.NotifyUserAsync(
                quote.Customer.userId,
                "Quotation Status Update",
                $"Your quote request #{quote.quoteId} for '{quote.InsuranceProduct?.name}' was declined. Please contact the company for more details.",
                "/Customer/MyQuotations",
                "QuotationRejected"
            );
        }

        TempData["SuccessMessage"] = $"Quotation #{quote.quoteId} marked as Rejected.";
        return RedirectToAction(nameof(Quotations));
    }

    [HttpGet]
    public IActionResult IssuePolicy(int quoteId)
    {
        return RedirectToAction("IssuePolicy", "Payment", new { quoteId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IssuePolicy(InsurancePolicy model, string? beneficiaryName, string? beneficiaryRelation, string? beneficiaryPhone)
    {
        var quote = await _context.Quotations
            .Include(q => q.Customer)
            .Include(q => q.InsuranceProduct)
            .FirstOrDefaultAsync(q => q.quoteId == model.quoteId);

        if (quote == null)
        {
            TempData["ErrorMessage"] = "Quotation reference not found.";
            return RedirectToAction(nameof(Quotations));
        }

        var currentAgent = await GetCurrentAgentAsync();
        if (model.agentId == null || model.agentId == 0)
        {
            model.agentId = quote.agentId ?? currentAgent?.agentId;
        }
        if (quote.agentId == null && model.agentId != null)
        {
            quote.agentId = model.agentId;
        }

        quote.status = "Accepted";

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Save Policy
            model.status = "Active";
            _context.InsurancePolicies.Add(model);
            await _context.SaveChangesAsync();

            // 2. Beneficiary
            if (!string.IsNullOrWhiteSpace(beneficiaryName))
            {
                var beneficiary = new Beneficiary
                {
                    inPoId = model.inPoId,
                    name = beneficiaryName.Trim(),
                    relation = string.IsNullOrWhiteSpace(beneficiaryRelation) ? "Spouse" : beneficiaryRelation.Trim(),
                    phone = beneficiaryPhone?.Trim(),
                    sharePercentage = 100.00m
                };
                _context.Beneficiaries.Add(beneficiary);
            }

            // 3. Initial payment record
            var payment = new Payment
            {
                inPoId = model.inPoId,
                transactionRef = "TXN-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Random.Shared.Next(100, 999),
                payDate = DateTime.UtcNow,
                payAmount = model.premiumAmount,
                payMethod = "Credit / Debit Card",
                payStatus = "Completed"
            };
            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // Notify Customer
            if (quote.Customer != null)
            {
                await _notificationService.NotifyUserAsync(
                    quote.Customer.userId,
                    "Insurance Policy Issued!",
                    $"Your policy #{model.policyNumber} for '{quote.InsuranceProduct?.name}' has been successfully issued and is now active!",
                    "/Customer/MyPolicies",
                    "PolicyIssued"
                );
            }

            TempData["SuccessMessage"] = $"Policy #{model.policyNumber} issued successfully!";
            return RedirectToAction(nameof(Policies));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, $"Error issuing policy: {ex.Message}");
            ViewBag.Quotation = quote;
            return View(model);
        }
    }

    // ==========================================
    // COMPANY PRODUCTS INVENTORY
    // ==========================================
    public async Task<IActionResult> Products()
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var products = await _context.InsuranceProducts
            .Include(p => p.InsuranceType)
            .Where(p => p.companyId == company.companyId)
            .OrderByDescending(p => p.inProId)
            .ToListAsync();

        ViewBag.Company = company;
        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> CreateProduct()
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        ViewBag.InsuranceTypes = new SelectList(await _context.InsuranceTypes.OrderBy(t => t.name).ToListAsync(), "inTypeId", "name");
        return View(new InsuranceProduct { companyId = company.companyId, status = "Active" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(InsuranceProduct model)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        model.companyId = company.companyId;

        if (string.IsNullOrWhiteSpace(model.name) || model.basePremium <= 0)
        {
            ModelState.AddModelError(string.Empty, "Please enter a valid product name and base premium.");
            ViewBag.InsuranceTypes = new SelectList(await _context.InsuranceTypes.OrderBy(t => t.name).ToListAsync(), "inTypeId", "name", model.inTypeId);
            return View(model);
        }

        _context.InsuranceProducts.Add(model);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Product '{model.name}' created successfully.";
        return RedirectToAction(nameof(Products));
    }

    [HttpGet]
    public async Task<IActionResult> EditProduct(int id)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var product = await _context.InsuranceProducts
            .Include(p => p.InsuranceType)
            .FirstOrDefaultAsync(p => p.inProId == id && p.companyId == company.companyId);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Unauthorized: Product not found or does not belong to your company.";
            return RedirectToAction(nameof(Products));
        }

        ViewBag.InsuranceTypes = new SelectList(await _context.InsuranceTypes.OrderBy(t => t.name).ToListAsync(), "inTypeId", "name", product.inTypeId);
        ViewBag.Company = company;

        var model = new EditProductViewModel
        {
            InProId = product.inProId,
            CompanyId = product.companyId,
            CompanyName = company.name,
            IsHostCompany = company.isHostCompany,
            InTypeId = product.inTypeId,
            Name = product.name,
            BasePremium = product.basePremium,
            Detail = product.detail,
            Status = product.status
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProduct(EditProductViewModel model)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var product = await _context.InsuranceProducts
            .FirstOrDefaultAsync(p => p.inProId == model.InProId && p.companyId == company.companyId);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Unauthorized: Product not found or does not belong to your company.";
            return RedirectToAction(nameof(Products));
        }

        if (!ModelState.IsValid)
        {
            ViewBag.InsuranceTypes = new SelectList(await _context.InsuranceTypes.OrderBy(t => t.name).ToListAsync(), "inTypeId", "name", model.InTypeId);
            ViewBag.Company = company;
            model.CompanyName = company.name;
            return View(model);
        }

        product.name = model.Name.Trim();
        product.inTypeId = model.InTypeId;
        product.basePremium = model.BasePremium;
        product.detail = model.Detail?.Trim();
        product.status = model.Status;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Product '{product.name}' updated successfully.";
        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleProductStatus(int id)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var product = await _context.InsuranceProducts
            .FirstOrDefaultAsync(p => p.inProId == id && p.companyId == company.companyId);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Unauthorized: Product not found or does not belong to your company.";
            return RedirectToAction(nameof(Products));
        }

        product.status = product.status == "Active" ? "Inactive" : "Active";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Product '{product.name}' status updated to {product.status}.";
        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var product = await _context.InsuranceProducts
            .FirstOrDefaultAsync(p => p.inProId == id && p.companyId == company.companyId);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Unauthorized: Product not found or does not belong to your company.";
            return RedirectToAction(nameof(Products));
        }

        bool hasQuotes = await _context.Quotations.AnyAsync(q => q.inProId == id);
        if (hasQuotes)
        {
            product.status = "Inactive";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Product '{product.name}' has associated quotations/policies, so it was set to Inactive instead of deleted.";
            return RedirectToAction(nameof(Products));
        }

        _context.InsuranceProducts.Remove(product);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Product '{product.name}' deleted successfully.";
        return RedirectToAction(nameof(Products));
    }

    // ==========================================
    // COMPANY POLICIES & CLAIMS
    // ==========================================
    public async Task<IActionResult> Policies()
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var policies = await _context.InsurancePolicies
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.Customer)
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.InsuranceProduct)
            .Include(p => p.Agent)
            .Include(p => p.Beneficiaries)
            .Include(p => p.Payments)
            .Include(p => p.Claims)
            .Where(p => p.Quotation!.InsuranceProduct!.companyId == company.companyId)
            .OrderByDescending(p => p.startDate)
            .ToListAsync();

        return View(policies);
    }

    public async Task<IActionResult> Claims()
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var claims = await _context.Claims
            .Include(c => c.InsurancePolicy)
                .ThenInclude(p => p!.Quotation)
                    .ThenInclude(q => q!.Customer)
            .Include(c => c.InsurancePolicy)
                .ThenInclude(p => p!.Quotation)
                    .ThenInclude(q => q!.InsuranceProduct)
            .Where(c => c.InsurancePolicy!.Quotation!.InsuranceProduct!.companyId == company.companyId)
            .OrderByDescending(c => c.claimDate)
            .ToListAsync();

        return View(claims);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateClaimStatus(int id, string status)
    {
        var claim = await _context.Claims
            .Include(c => c.InsurancePolicy)
                .ThenInclude(p => p!.Quotation)
                    .ThenInclude(q => q!.Customer)
            .FirstOrDefaultAsync(c => c.claimId == id);

        if (claim == null)
        {
            TempData["ErrorMessage"] = "Claim not found.";
            return RedirectToAction(nameof(Claims));
        }

        claim.status = status;
        await _context.SaveChangesAsync();

        if (claim.InsurancePolicy?.Quotation?.Customer != null)
        {
            await _notificationService.NotifyUserAsync(
                claim.InsurancePolicy.Quotation.Customer.userId,
                "Claim Status Updated",
                $"Your claim #{claim.claimId} has been updated to: {status}.",
                "/Customer/MyPolicies",
                "ClaimStatusUpdated"
            );
        }

        TempData["SuccessMessage"] = $"Claim #{claim.claimId} status updated to '{status}'.";
        return RedirectToAction(nameof(Claims));
    }

    // ==========================================
    // MY STAFF (Agent can add staff under their agency)
    // ==========================================
    public async Task<IActionResult> MyStaff()
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var staff = await _context.StaffMembers
            .Include(s => s.UserAccount)
            .Include(s => s.SupervisingAgent)
            .Where(s => s.companyId == company.companyId)
            .ToListAsync();

        ViewBag.Company = company;
        return View(staff);
    }

    [HttpGet]
    public async Task<IActionResult> AddStaff()
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var agents = await _context.Agents.Where(a => a.companyId == company.companyId && a.status == "Active").ToListAsync();
        ViewBag.Agents = new SelectList(agents, "agentId", "fullName");
        ViewBag.Company = company;

        return View(new AddStaffViewModel
        {
            CompanyId = company.companyId,
            ReceiveAlerts = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStaff(AddStaffViewModel model)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        model.CompanyId = company.companyId;

        bool userExists = await _context.UserAccounts
            .AnyAsync(u => u.username.ToLower() == model.Username.ToLower() || u.email.ToLower() == model.Email.ToLower());

        if (userExists)
        {
            ModelState.AddModelError(string.Empty, "Username or Email already in use.");
            var agents = await _context.Agents.Where(a => a.companyId == company.companyId && a.status == "Active").ToListAsync();
            ViewBag.Agents = new SelectList(agents, "agentId", "fullName");
            ViewBag.Company = company;
            return View(model);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var userAccount = new UserAccount
            {
                username = model.Username.Trim(),
                email = model.Email.Trim().ToLower(),
                passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                role = "Staff",
                status = "Active",
                createdAt = DateTime.UtcNow
            };
            _context.UserAccounts.Add(userAccount);
            await _context.SaveChangesAsync();

            var staff = new Staff
            {
                userId = userAccount.userId,
                companyId = company.companyId,
                agentId = model.AgentId,
                fullName = model.FullName.Trim(),
                phone = model.Phone?.Trim(),
                receiveAlerts = model.ReceiveAlerts,
                status = "Active"
            };
            _context.StaffMembers.Add(staff);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            TempData["SuccessMessage"] = $"Staff member '{model.FullName}' added successfully under your agency.";
            return RedirectToAction(nameof(MyStaff));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, $"Failed to add staff: {ex.Message}");
            var agents = await _context.Agents.Where(a => a.companyId == company.companyId && a.status == "Active").ToListAsync();
            ViewBag.Agents = new SelectList(agents, "agentId", "fullName");
            ViewBag.Company = company;
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> EditStaff(int id)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var staff = await _context.StaffMembers
            .Include(s => s.UserAccount)
            .Include(s => s.SupervisingAgent)
            .FirstOrDefaultAsync(s => s.staffId == id && s.companyId == company.companyId);

        if (staff == null)
        {
            TempData["ErrorMessage"] = "Unauthorized: Staff member not found or does not belong to your company.";
            return RedirectToAction(nameof(MyStaff));
        }

        var agents = await _context.Agents.Where(a => a.companyId == company.companyId && a.status == "Active").ToListAsync();
        ViewBag.Agents = new SelectList(agents, "agentId", "fullName", staff.agentId);
        ViewBag.Company = company;

        var model = new EditStaffViewModel
        {
            StaffId = staff.staffId,
            UserId = staff.userId,
            CompanyId = staff.companyId,
            CompanyName = company.name,
            IsHostCompany = company.isHostCompany,
            Username = staff.UserAccount?.username ?? "",
            Email = staff.UserAccount?.email ?? "",
            FullName = staff.fullName,
            Phone = staff.phone,
            AgentId = staff.agentId,
            ReceiveAlerts = staff.receiveAlerts,
            Status = staff.status
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStaff(EditStaffViewModel model)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var staff = await _context.StaffMembers
            .Include(s => s.UserAccount)
            .FirstOrDefaultAsync(s => s.staffId == model.StaffId && s.companyId == company.companyId);

        if (staff == null)
        {
            TempData["ErrorMessage"] = "Unauthorized: Staff member not found or does not belong to your company.";
            return RedirectToAction(nameof(MyStaff));
        }

        if (!ModelState.IsValid)
        {
            var agents = await _context.Agents.Where(a => a.companyId == company.companyId && a.status == "Active").ToListAsync();
            ViewBag.Agents = new SelectList(agents, "agentId", "fullName", model.AgentId);
            ViewBag.Company = company;
            model.CompanyName = company.name;
            model.Username = staff.UserAccount?.username ?? "";
            return View(model);
        }

        if (staff.UserAccount != null && !string.Equals(staff.UserAccount.email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            bool emailExists = await _context.UserAccounts
                .AnyAsync(u => u.userId != staff.userId && u.email.ToLower() == model.Email.ToLower());

            if (emailExists)
            {
                ModelState.AddModelError("Email", "This email address is already in use by another account.");
                var agents = await _context.Agents.Where(a => a.companyId == company.companyId && a.status == "Active").ToListAsync();
                ViewBag.Agents = new SelectList(agents, "agentId", "fullName", model.AgentId);
                ViewBag.Company = company;
                model.CompanyName = company.name;
                model.Username = staff.UserAccount?.username ?? "";
                return View(model);
            }

            staff.UserAccount.email = model.Email.Trim().ToLower();
        }

        staff.fullName = model.FullName.Trim();
        staff.phone = model.Phone?.Trim();
        staff.agentId = model.AgentId;
        staff.receiveAlerts = model.ReceiveAlerts;
        staff.status = model.Status;

        if (staff.UserAccount != null)
        {
            staff.UserAccount.status = model.Status;
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                staff.UserAccount.passwordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
            }
        }

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Staff member '{staff.fullName}' updated successfully.";
        return RedirectToAction(nameof(MyStaff));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStaffAlerts(int id)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var staff = await _context.StaffMembers.FirstOrDefaultAsync(s => s.staffId == id && s.companyId == company.companyId);
        if (staff == null)
        {
            TempData["ErrorMessage"] = "Unauthorized: Staff member not found or does not belong to your company.";
            return RedirectToAction(nameof(MyStaff));
        }

        staff.receiveAlerts = !staff.receiveAlerts;
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Alert preferences for '{staff.fullName}' updated: Receive Alerts = {staff.receiveAlerts}.";
        return RedirectToAction(nameof(MyStaff));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStaff(int id)
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null) return RedirectToAction("Login", "Auth");

        var staff = await _context.StaffMembers
            .Include(s => s.UserAccount)
            .FirstOrDefaultAsync(s => s.staffId == id && s.companyId == company.companyId);

        if (staff == null)
        {
            TempData["ErrorMessage"] = "Unauthorized: Staff member not found or does not belong to your company.";
            return RedirectToAction(nameof(MyStaff));
        }

        if (staff.UserAccount != null)
        {
            _context.UserAccounts.Remove(staff.UserAccount);
        }
        else
        {
            _context.StaffMembers.Remove(staff);
        }
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Staff member '{staff.fullName}' removed successfully.";
        return RedirectToAction(nameof(MyStaff));
    }

    private async Task<Company?> GetCurrentCompanyAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId)) return null;

        var agent = await _context.Agents.Include(a => a.Company).FirstOrDefaultAsync(a => a.userId == userId);
        if (agent?.Company != null) return agent.Company;

        var staff = await _context.StaffMembers.Include(s => s.Company).FirstOrDefaultAsync(s => s.userId == userId);
        if (staff?.Company != null) return staff.Company;

        return await _context.Companies.FirstOrDefaultAsync(c => c.userId == userId);
    }

    private async Task<Agent?> GetCurrentAgentAsync()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdStr, out int userId)) return null;

        var agent = await _context.Agents.FirstOrDefaultAsync(a => a.userId == userId);
        if (agent != null) return agent;

        var staff = await _context.StaffMembers.Include(s => s.SupervisingAgent).FirstOrDefaultAsync(s => s.userId == userId);
        if (staff?.SupervisingAgent != null) return staff.SupervisingAgent;
        if (staff != null)
        {
            return await _context.Agents.FirstOrDefaultAsync(a => a.companyId == staff.companyId && a.status == "Active");
        }

        var company = await _context.Companies.FirstOrDefaultAsync(c => c.userId == userId);
        if (company != null)
        {
            return await _context.Agents.FirstOrDefaultAsync(a => a.companyId == company.companyId && a.status == "Active");
        }

        return null;
    }

    // GET: Agent/Reports
    [HttpGet]
    public async Task<IActionResult> Reports(string timeframe = "monthly")
    {
        var company = await GetCurrentCompanyAsync();
        if (company == null)
        {
            TempData["ErrorMessage"] = "No company affiliation found for your account.";
            return RedirectToAction(nameof(Dashboard));
        }

        timeframe = (timeframe ?? "monthly").ToLower().Trim();
        if (timeframe != "daily" && timeframe != "weekly" && timeframe != "monthly" && timeframe != "yearly")
        {
            timeframe = "monthly";
        }

        var companyId = company.companyId;

        var paymentsQuery = _context.Payments
            .Include(p => p.InsurancePolicy)
                .ThenInclude(ip => ip!.Quotation)
                    .ThenInclude(q => q!.Customer)
            .Include(p => p.InsurancePolicy)
                .ThenInclude(ip => ip!.Quotation)
                    .ThenInclude(q => q!.InsuranceProduct)
                        .ThenInclude(prod => prod!.InsuranceType)
            .Where(p => p.payStatus == "Completed" && p.InsurancePolicy!.Quotation!.InsuranceProduct!.companyId == companyId);

        var claimsQuery = _context.Claims
            .Include(c => c.InsurancePolicy)
                .ThenInclude(ip => ip!.Quotation)
                    .ThenInclude(q => q!.InsuranceProduct)
            .Where(c => (c.status == "Approved" || c.status == "Paid") && c.InsurancePolicy!.Quotation!.InsuranceProduct!.companyId == companyId);

        var policiesQuery = _context.InsurancePolicies
            .Where(ip => ip.Quotation!.InsuranceProduct!.companyId == companyId);

        var allPayments = await paymentsQuery.ToListAsync();
        var allClaims = await claimsQuery.ToListAsync();
        var totalPoliciesCount = await policiesQuery.CountAsync();

        // Staff count for this partner company
        var staffCount = await _context.StaffMembers.CountAsync(s => s.companyId == companyId && s.status == "Active");

        // Customer buyers for this partner company
        var customerBuyersQuery = _context.InsurancePolicies
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.Customer)
                    .ThenInclude(c => c!.UserAccount)
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.InsuranceProduct)
            .Include(p => p.Payments)
            .Where(p => p.Quotation!.InsuranceProduct!.companyId == companyId && p.Quotation.Customer != null);

        var allCompPolicies = await customerBuyersQuery.ToListAsync();

        var distinctCustomersCount = allCompPolicies
            .Select(p => p.Quotation!.customerId)
            .Distinct()
            .Count();

        var customerBuyers = allCompPolicies
            .GroupBy(p => p.Quotation!.customerId)
            .Select(g =>
            {
                var first = g.First();
                var customer = first.Quotation!.Customer!;
                var latestPolicy = g.OrderByDescending(x => x.startDate).First();
                var totalSpent = g.SelectMany(x => x.Payments)
                    .Where(pay => pay.payStatus == "Completed")
                    .Sum(pay => pay.payAmount);
                if (totalSpent == 0m)
                {
                    totalSpent = g.Sum(x => x.premiumAmount);
                }

                return new ReportCustomerBuyerItem
                {
                    CustomerId = customer.customerId,
                    CustomerName = customer.fullName,
                    Email = customer.UserAccount?.email,
                    Phone = customer.phone,
                    NationalId = customer.nationalId,
                    PoliciesCount = g.Count(),
                    TotalSpent = totalSpent,
                    LatestProductName = latestPolicy.Quotation?.InsuranceProduct?.name ?? "Insurance Plan",
                    CompanyName = company.name,
                    LatestPurchaseDate = latestPolicy.startDate,
                    PolicyStatus = latestPolicy.status
                };
            })
            .OrderByDescending(c => c.TotalSpent)
            .ToList();

        var model = new FinancialReportViewModel
        {
            Timeframe = timeframe,
            SelectedCompanyId = companyId,
            SelectedCompanyName = company.name,
            TotalRevenue = allPayments.Sum(p => p.payAmount),
            TotalClaimsPaid = allClaims.Sum(c => c.claimAmount),
            TotalPoliciesCount = totalPoliciesCount,
            TotalTransactionsCount = allPayments.Count,
            TotalStaffCount = staffCount,
            TotalCustomersCount = distinctCustomersCount,
            CustomerBuyers = customerBuyers
        };

        BuildTimeSeriesData(model, allPayments, allClaims, timeframe);

        var totalRev = model.TotalRevenue > 0 ? model.TotalRevenue : 1m;

        // Category breakdown
        model.CategoryBreakdown = allPayments
            .GroupBy(p => p.InsurancePolicy?.Quotation?.InsuranceProduct?.InsuranceType?.name ?? "General")
            .Select(g => new ReportBreakdownItem
            {
                Name = g.Key,
                Amount = g.Sum(x => x.payAmount),
                Count = g.Count(),
                Percentage = Math.Round((g.Sum(x => x.payAmount) / totalRev) * 100, 1)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        // Product breakdown for this agency
        model.ProductBreakdown = allPayments
            .GroupBy(p => new
            {
                Id = p.InsurancePolicy?.Quotation?.InsuranceProduct?.inProId ?? 0,
                Name = p.InsurancePolicy?.Quotation?.InsuranceProduct?.name ?? "General Plan"
            })
            .Select(g => new ReportBreakdownItem
            {
                Id = g.Key.Id,
                Name = g.Key.Name,
                Amount = g.Sum(x => x.payAmount),
                Count = g.Count(),
                Percentage = Math.Round((g.Sum(x => x.payAmount) / totalRev) * 100, 1)
            })
            .OrderByDescending(x => x.Amount)
            .ToList();

        // Recent 20 transactions
        model.RecentTransactions = allPayments
            .OrderByDescending(p => p.payDate)
            .Take(20)
            .Select(p => new ReportTransactionItem
            {
                PaymentId = p.payId,
                TransactionRef = p.transactionRef,
                PolicyNumber = p.InsurancePolicy?.policyNumber ?? "N/A",
                ProductName = p.InsurancePolicy?.Quotation?.InsuranceProduct?.name ?? "Insurance Plan",
                CategoryName = p.InsurancePolicy?.Quotation?.InsuranceProduct?.InsuranceType?.name ?? "General",
                CompanyName = company.name,
                CustomerName = p.InsurancePolicy?.Quotation?.Customer?.fullName ?? "Customer",
                PayDate = p.payDate,
                Amount = p.payAmount,
                PaymentMethod = p.payMethod,
                Status = p.payStatus
            })
            .ToList();

        ViewBag.Company = company;
        return View(model);
    }

    private static void BuildTimeSeriesData(FinancialReportViewModel model, List<Payment> payments, List<IMS.Models.Claim> claims, string timeframe)
    {
        model.ChartLabels.Clear();
        model.RevenueData.Clear();
        model.ClaimsData.Clear();
        model.NetData.Clear();

        var today = DateTime.Today;

        switch (timeframe)
        {
            case "daily":
                // Last 14 days
                for (int i = 13; i >= 0; i--)
                {
                    var targetDay = today.AddDays(-i);
                    var label = targetDay.ToString("MMM dd");
                    var rev = payments.Where(p => p.payDate.Date == targetDay).Sum(p => p.payAmount);
                    var clm = claims.Where(c => c.claimDate.Date == targetDay).Sum(c => c.claimAmount);

                    model.ChartLabels.Add(label);
                    model.RevenueData.Add(rev);
                    model.ClaimsData.Add(clm);
                    model.NetData.Add(rev - clm);
                }
                break;

            case "weekly":
                // Last 8 weeks
                var diff = (7 + (today.DayOfWeek - DayOfWeek.Monday)) % 7;
                var currentWeekMonday = today.AddDays(-1 * diff);
                for (int i = 7; i >= 0; i--)
                {
                    var weekStart = currentWeekMonday.AddDays(-i * 7);
                    var weekEnd = weekStart.AddDays(7);
                    var label = $"{weekStart:MMM dd} - {weekStart.AddDays(6):MMM dd}";
                    var rev = payments.Where(p => p.payDate.Date >= weekStart && p.payDate.Date < weekEnd).Sum(p => p.payAmount);
                    var clm = claims.Where(c => c.claimDate.Date >= weekStart && c.claimDate.Date < weekEnd).Sum(c => c.claimAmount);

                    model.ChartLabels.Add(label);
                    model.RevenueData.Add(rev);
                    model.ClaimsData.Add(clm);
                    model.NetData.Add(rev - clm);
                }
                break;

            case "yearly":
                // Last 5 years
                int currentYear = today.Year;
                for (int i = 4; i >= 0; i--)
                {
                    var yr = currentYear - i;
                    var label = yr.ToString();
                    var rev = payments.Where(p => p.payDate.Year == yr).Sum(p => p.payAmount);
                    var clm = claims.Where(c => c.claimDate.Year == yr).Sum(c => c.claimAmount);

                    model.ChartLabels.Add(label);
                    model.RevenueData.Add(rev);
                    model.ClaimsData.Add(clm);
                    model.NetData.Add(rev - clm);
                }
                break;

            case "monthly":
            default:
                // Last 12 months
                for (int i = 11; i >= 0; i--)
                {
                    var targetMonth = today.AddMonths(-i);
                    var yr = targetMonth.Year;
                    var m = targetMonth.Month;
                    var label = targetMonth.ToString("MMM yyyy");
                    var rev = payments.Where(p => p.payDate.Year == yr && p.payDate.Month == m).Sum(p => p.payAmount);
                    var clm = claims.Where(c => c.claimDate.Year == yr && c.claimDate.Month == m).Sum(c => c.claimAmount);

                    model.ChartLabels.Add(label);
                    model.RevenueData.Add(rev);
                    model.ClaimsData.Add(clm);
                    model.NetData.Add(rev - clm);
                }
                break;
        }
    }
}


