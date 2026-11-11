using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using IMS.Models;
using IMS.ViewModels;
using IMS.Services;

namespace IMS.Controllers;

[Authorize(Roles = "Admin")]
public class AdminController : Controller
{
    private readonly InsuranceContext _context;
    private readonly INotificationService _notificationService;

    public AdminController(InsuranceContext context, INotificationService notificationService)
    {
        _context = context;
        _notificationService = notificationService;
    }

    public async Task<IActionResult> Dashboard()
    {
        var model = new AdminDashboardViewModel
        {
            TotalCompanies = await _context.Companies.CountAsync(),
            PendingCompaniesCount = await _context.Companies.CountAsync(c => c.approvalStatus == "Pending"),
            TotalProducts = await _context.InsuranceProducts.CountAsync(),
            TotalQuotations = await _context.Quotations.CountAsync(),
            ActivePoliciesCount = await _context.InsurancePolicies.CountAsync(p => p.status == "Active"),
            PendingClaimsCount = await _context.Claims.CountAsync(c => c.status == "Submitted" || c.status == "In Review"),
            TotalStaffCount = await _context.StaffMembers.CountAsync(),
            TotalCustomersCount = await _context.Customers.CountAsync(),
            TotalPremiumsCollected = await _context.Payments.Where(p => p.payStatus == "Completed").SumAsync(p => (decimal?)p.payAmount) ?? 0m,

            PendingCompanies = await _context.Companies
                .Include(c => c.Agents)
                .Where(c => c.approvalStatus == "Pending")
                .OrderByDescending(c => c.createdAt)
                .ToListAsync(),

            PendingAdminQuotations = await _context.Quotations
                .Include(q => q.Customer)
                .Include(q => q.InsuranceProduct)
                    .ThenInclude(p => p!.Company)
                .Where(q => q.status == "Pending" && q.InsuranceProduct!.Company!.isHostCompany == true)
                .OrderByDescending(q => q.requestDate)
                .ToListAsync(),

            PendingAdminQuotationsCount = await _context.Quotations
                .CountAsync(q => q.status == "Pending" && q.InsuranceProduct!.Company!.isHostCompany == true),

            RecentQuotations = await _context.Quotations
                .Include(q => q.Customer)
                .Include(q => q.InsuranceProduct)
                    .ThenInclude(p => p!.Company)
                .OrderByDescending(q => q.requestDate)
                .Take(5)
                .ToListAsync(),

            RecentClaims = await _context.Claims
                .Include(c => c.InsurancePolicy)
                    .ThenInclude(p => p!.Quotation)
                        .ThenInclude(q => q!.Customer)
                .OrderByDescending(c => c.claimDate)
                .Take(5)
                .ToListAsync()
        };

        return View(model);
    }

    // ==========================================
    // COMPANIES MANAGEMENT & APPROVAL WORKFLOW
    // ==========================================
    public async Task<IActionResult> Companies(string? status = null)
    {
        var query = _context.Companies
            .Include(c => c.Agents)
            .Include(c => c.StaffMembers)
            .Include(c => c.Products)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(c => c.approvalStatus == status);
        }

        ViewBag.SelectedStatus = status ?? "All";
        var companies = await query.OrderByDescending(c => c.isHostCompany).ThenByDescending(c => c.createdAt).ToListAsync();
        return View(companies);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveCompany(int id, decimal commissionRate = 10.00m)
    {
        var company = await _context.Companies
            .Include(c => c.Agents)
            .FirstOrDefaultAsync(c => c.companyId == id);

        if (company == null)
        {
            TempData["ErrorMessage"] = "Company not found.";
            return RedirectToAction(nameof(Companies));
        }

        company.approvalStatus = "Approved";
        company.commissionRate = commissionRate;

        // Notify company agents
        await _notificationService.NotifyCompanyAsync(
            company.companyId,
            "Company Approved!",
            $"Congratulations! Your company '{company.name}' has been approved by the platform Administrator with a commission rate of {commissionRate:F1}%. You can now log in and manage your products.",
            "/Agent/Dashboard",
            "CompanyApproved",
            alsoNotifyAdmin: false
        );

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Company '{company.name}' has been Approved successfully. Representative agents can now log in.";
        return RedirectToAction(nameof(Companies));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectCompany(int id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
        {
            TempData["ErrorMessage"] = "Company not found.";
            return RedirectToAction(nameof(Companies));
        }

        company.approvalStatus = "Rejected";
        await _context.SaveChangesAsync();

        await _notificationService.NotifyCompanyAsync(
            company.companyId,
            "Company Registration Rejected",
            $"Your company registration for '{company.name}' was rejected by the administrator.",
            null,
            "CompanyRejected",
            alsoNotifyAdmin: false
        );

        TempData["SuccessMessage"] = $"Company '{company.name}' registration marked as Rejected.";
        return RedirectToAction(nameof(Companies));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendCompany(int id)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
        {
            TempData["ErrorMessage"] = "Company not found.";
            return RedirectToAction(nameof(Companies));
        }

        company.approvalStatus = company.approvalStatus == "Suspended" ? "Approved" : "Suspended";
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Company '{company.name}' status changed to {company.approvalStatus}.";
        return RedirectToAction(nameof(Companies));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateCommission(int id, decimal commissionRate)
    {
        var company = await _context.Companies.FindAsync(id);
        if (company == null)
        {
            TempData["ErrorMessage"] = "Company not found.";
            return RedirectToAction(nameof(Companies));
        }

        company.commissionRate = commissionRate;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Commission rate for '{company.name}' updated to {commissionRate:F2}%.";
        return RedirectToAction(nameof(Companies));
    }

    // ==========================================
    // STAFF MANAGEMENT (Admin manages host company staff)
    // ==========================================
    public async Task<IActionResult> Staff(int? companyId = null)
    {
        var hostCompany = await GetHostCompanyAsync();
        var query = _context.StaffMembers
            .Include(s => s.Company)
            .Include(s => s.SupervisingAgent)
            .Include(s => s.UserAccount)
            .AsQueryable();

        if (companyId.HasValue && companyId.Value > 0)
        {
            query = query.Where(s => s.companyId == companyId.Value);
        }

        ViewBag.Companies = await _context.Companies.OrderBy(c => c.name).ToListAsync();
        ViewBag.SelectedCompanyId = companyId;
        ViewBag.HostCompany = hostCompany;

        var staffList = await query.OrderBy(s => s.fullName).ToListAsync();
        return View(staffList);
    }

    [HttpGet]
    public async Task<IActionResult> AddStaff(int? defaultCompanyId = null)
    {
        var hostCompany = await GetHostCompanyAsync();
        int targetCompanyId = hostCompany?.companyId ?? defaultCompanyId ?? 1;
        await PopulateStaffDropdowns(targetCompanyId);
        return View(new AddStaffViewModel
        {
            CompanyId = targetCompanyId,
            ReceiveAlerts = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStaff(AddStaffViewModel model)
    {
        var hostCompany = await GetHostCompanyAsync();
        int hostCompanyId = hostCompany?.companyId ?? 1;

        if (model.CompanyId != hostCompanyId)
        {
            ModelState.AddModelError(string.Empty, "Administrators can only add staff members under the official Host Company.");
            await PopulateStaffDropdowns(hostCompanyId);
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            await PopulateStaffDropdowns(model.CompanyId);
            return View(model);
        }

        // Check if username or email already taken
        bool userExists = await _context.UserAccounts
            .AnyAsync(u => u.username.ToLower() == model.Username.ToLower() || u.email.ToLower() == model.Email.ToLower());

        if (userExists)
        {
            ModelState.AddModelError(string.Empty, "Username or Email address is already in use.");
            await PopulateStaffDropdowns(model.CompanyId);
            return View(model);
        }

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            // 1. Create UserAccount
            var userAccount = new UserAccount
            {
                username = model.Username.Trim(),
                email = model.Email.Trim().ToLower(),
                passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                role = "Staff",
                status = model.Status,
                createdAt = DateTime.UtcNow
            };
            _context.UserAccounts.Add(userAccount);
            await _context.SaveChangesAsync();

            // 2. Create Staff Profile
            var staff = new Staff
            {
                userId = userAccount.userId,
                companyId = model.CompanyId,
                agentId = model.AgentId,
                fullName = model.FullName.Trim(),
                phone = model.Phone?.Trim(),
                receiveAlerts = model.ReceiveAlerts,
                status = model.Status
            };
            _context.StaffMembers.Add(staff);
            await _context.SaveChangesAsync();

            await transaction.CommitAsync();

            // Notify staff member and company
            await _notificationService.NotifyCompanyAsync(
                model.CompanyId,
                "New Staff Member Added",
                $"Staff member '{model.FullName}' has been registered under your agency.",
                "/Admin/Staff",
                "StaffRegistered",
                alsoNotifyAdmin: false
            );

            TempData["SuccessMessage"] = $"Staff member '{model.FullName}' added successfully under Host Company.";
            return RedirectToAction(nameof(Staff), new { companyId = model.CompanyId });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            ModelState.AddModelError(string.Empty, $"Failed to add staff: {ex.Message}");
            await PopulateStaffDropdowns(model.CompanyId);
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> EditStaff(int id)
    {
        var staff = await _context.StaffMembers
            .Include(s => s.Company)
            .Include(s => s.UserAccount)
            .Include(s => s.SupervisingAgent)
            .FirstOrDefaultAsync(s => s.staffId == id);

        if (staff == null)
        {
            TempData["ErrorMessage"] = "Staff member not found.";
            return RedirectToAction(nameof(Staff));
        }

        // RESTRICTION: Admin can only update their own (Host Company) staff
        if (staff.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized: Administrators can only update staff members belonging to the host company. Partner company staff must be managed by the partner company.";
            return RedirectToAction(nameof(Staff));
        }

        var agents = await _context.Agents
            .Where(a => a.companyId == staff.companyId && a.status == "Active")
            .OrderBy(a => a.fullName)
            .ToListAsync();
        ViewBag.Agents = new SelectList(agents, "agentId", "fullName", staff.agentId);

        var model = new EditStaffViewModel
        {
            StaffId = staff.staffId,
            UserId = staff.userId,
            CompanyId = staff.companyId,
            CompanyName = staff.Company?.name,
            IsHostCompany = true,
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
        var staff = await _context.StaffMembers
            .Include(s => s.Company)
            .Include(s => s.UserAccount)
            .FirstOrDefaultAsync(s => s.staffId == model.StaffId);

        if (staff == null)
        {
            TempData["ErrorMessage"] = "Staff member not found.";
            return RedirectToAction(nameof(Staff));
        }

        // RESTRICTION: Admin can only update their own (Host Company) staff
        if (staff.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized: Administrators can only update staff members belonging to the host company.";
            return RedirectToAction(nameof(Staff));
        }

        if (!ModelState.IsValid)
        {
            var agents = await _context.Agents
                .Where(a => a.companyId == staff.companyId && a.status == "Active")
                .OrderBy(a => a.fullName)
                .ToListAsync();
            ViewBag.Agents = new SelectList(agents, "agentId", "fullName", model.AgentId);
            model.CompanyName = staff.Company?.name;
            model.IsHostCompany = true;
            model.Username = staff.UserAccount?.username ?? "";
            return View(model);
        }

        // Check email uniqueness if modified
        if (staff.UserAccount != null && !string.Equals(staff.UserAccount.email, model.Email, StringComparison.OrdinalIgnoreCase))
        {
            bool emailExists = await _context.UserAccounts
                .AnyAsync(u => u.userId != staff.userId && u.email.ToLower() == model.Email.ToLower());

            if (emailExists)
            {
                ModelState.AddModelError("Email", "This email address is already in use by another account.");
                var agents = await _context.Agents
                    .Where(a => a.companyId == staff.companyId && a.status == "Active")
                    .OrderBy(a => a.fullName)
                    .ToListAsync();
                ViewBag.Agents = new SelectList(agents, "agentId", "fullName", model.AgentId);
                model.CompanyName = staff.Company?.name;
                model.IsHostCompany = true;
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
        return RedirectToAction(nameof(Staff), new { companyId = staff.companyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStaffAlerts(int id)
    {
        var staff = await _context.StaffMembers
            .Include(s => s.Company)
            .FirstOrDefaultAsync(s => s.staffId == id);

        if (staff == null)
        {
            TempData["ErrorMessage"] = "Staff not found.";
            return RedirectToAction(nameof(Staff));
        }

        // RESTRICTION: Admin can only toggle alerts on their own staff
        if (staff.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized: Administrators can only manage alert preferences for host company staff. Partner staff must be managed by the partner company.";
            return RedirectToAction(nameof(Staff));
        }

        staff.receiveAlerts = !staff.receiveAlerts;
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Alert preferences for '{staff.fullName}' updated: Receive Alerts = {staff.receiveAlerts}.";
        return RedirectToAction(nameof(Staff), new { companyId = staff.companyId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteStaff(int id)
    {
        var staff = await _context.StaffMembers
            .Include(s => s.Company)
            .Include(s => s.UserAccount)
            .FirstOrDefaultAsync(s => s.staffId == id);

        if (staff == null)
        {
            TempData["ErrorMessage"] = "Staff member not found.";
            return RedirectToAction(nameof(Staff));
        }

        // RESTRICTION: Admin can only delete their own staff
        if (staff.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized: Administrators can only remove staff members belonging to the host company. Partner company staff can only be deleted by the partner company.";
            return RedirectToAction(nameof(Staff));
        }

        int compId = staff.companyId;
        if (staff.UserAccount != null)
        {
            _context.UserAccounts.Remove(staff.UserAccount);
        }
        else
        {
            _context.StaffMembers.Remove(staff);
        }
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = "Staff member removed successfully.";
        return RedirectToAction(nameof(Staff), new { companyId = compId });
    }

    // ==========================================
    // PRODUCTS MANAGEMENT (Admin manages host company products)
    // ==========================================
    public async Task<IActionResult> Products()
    {
        var hostCompany = await GetHostCompanyAsync();
        var products = await _context.InsuranceProducts
            .Include(p => p.Company)
            .Include(p => p.InsuranceType)
            .OrderByDescending(p => p.inProId)
            .ToListAsync();

        ViewBag.HostCompany = hostCompany;
        return View(products);
    }

    [HttpGet]
    public async Task<IActionResult> CreateProduct()
    {
        var hostCompany = await GetHostCompanyAsync();
        var hostCompanyList = hostCompany != null ? new List<Company> { hostCompany } : await _context.Companies.Where(c => c.isHostCompany).ToListAsync();
        ViewBag.Companies = new SelectList(hostCompanyList, "companyId", "name", hostCompany?.companyId);
        ViewBag.InsuranceTypes = new SelectList(await _context.InsuranceTypes.OrderBy(t => t.name).ToListAsync(), "inTypeId", "name");
        return View(new CreateProductViewModel
        {
            CompanyId = hostCompany?.companyId ?? 1
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateProduct(CreateProductViewModel model)
    {
        var hostCompany = await GetHostCompanyAsync();
        int hostCompanyId = hostCompany?.companyId ?? 1;

        if (model.CompanyId != hostCompanyId)
        {
            ModelState.AddModelError(string.Empty, "Administrators can only create products under the official Host Company.");
            var hostCompanyList = hostCompany != null ? new List<Company> { hostCompany } : await _context.Companies.Where(c => c.isHostCompany).ToListAsync();
            ViewBag.Companies = new SelectList(hostCompanyList, "companyId", "name", hostCompanyId);
            ViewBag.InsuranceTypes = new SelectList(await _context.InsuranceTypes.OrderBy(t => t.name).ToListAsync(), "inTypeId", "name", model.InTypeId);
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            var hostCompanyList = hostCompany != null ? new List<Company> { hostCompany } : await _context.Companies.Where(c => c.isHostCompany).ToListAsync();
            ViewBag.Companies = new SelectList(hostCompanyList, "companyId", "name", hostCompanyId);
            ViewBag.InsuranceTypes = new SelectList(await _context.InsuranceTypes.OrderBy(t => t.name).ToListAsync(), "inTypeId", "name", model.InTypeId);
            return View(model);
        }

        var product = new InsuranceProduct
        {
            companyId = model.CompanyId,
            inTypeId = model.InTypeId,
            name = model.Name.Trim(),
            basePremium = model.BasePremium,
            detail = model.Detail?.Trim(),
            status = model.Status
        };

        _context.InsuranceProducts.Add(product);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Insurance Product '{model.Name}' created successfully under Host Company.";
        return RedirectToAction(nameof(Products));
    }

    [HttpGet]
    public async Task<IActionResult> EditProduct(int id)
    {
        var product = await _context.InsuranceProducts
            .Include(p => p.Company)
            .Include(p => p.InsuranceType)
            .FirstOrDefaultAsync(p => p.inProId == id);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction(nameof(Products));
        }

        // RESTRICTION: Admin can only edit their own products
        if (product.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized: Administrators can only edit products belonging to the host company. Partner company products can only be updated by the partner company.";
            return RedirectToAction(nameof(Products));
        }

        ViewBag.InsuranceTypes = new SelectList(await _context.InsuranceTypes.OrderBy(t => t.name).ToListAsync(), "inTypeId", "name", product.inTypeId);

        var model = new EditProductViewModel
        {
            InProId = product.inProId,
            CompanyId = product.companyId,
            CompanyName = product.Company?.name,
            IsHostCompany = true,
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
        var product = await _context.InsuranceProducts
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.inProId == model.InProId);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction(nameof(Products));
        }

        // RESTRICTION: Admin can only edit their own products
        if (product.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized: Administrators can only update products belonging to the host company.";
            return RedirectToAction(nameof(Products));
        }

        if (!ModelState.IsValid)
        {
            ViewBag.InsuranceTypes = new SelectList(await _context.InsuranceTypes.OrderBy(t => t.name).ToListAsync(), "inTypeId", "name", model.InTypeId);
            model.CompanyName = product.Company?.name;
            model.IsHostCompany = true;
            return View(model);
        }

        product.name = model.Name.Trim();
        product.inTypeId = model.InTypeId;
        product.basePremium = model.BasePremium;
        product.detail = model.Detail?.Trim();
        product.status = model.Status;

        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Insurance Product '{product.name}' updated successfully.";
        return RedirectToAction(nameof(Products));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleProductStatus(int id)
    {
        var product = await _context.InsuranceProducts
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.inProId == id);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction(nameof(Products));
        }

        // RESTRICTION: Admin can only toggle status of their own products
        if (product.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized: Administrators can only change the status of products belonging to the host company. Partner company products are managed by the partner company.";
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
        var product = await _context.InsuranceProducts
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.inProId == id);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction(nameof(Products));
        }

        // RESTRICTION: Admin can only delete their own products
        if (product.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized: Administrators can only delete products belonging to the host company. Partner company products can only be deleted by the partner company.";
            return RedirectToAction(nameof(Products));
        }

        bool hasQuotes = await _context.Quotations.AnyAsync(q => q.inProId == id);
        if (hasQuotes)
        {
            product.status = "Inactive";
            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = $"Product '{product.name}' has associated customer quotations/policies, so it was marked as Inactive instead of permanently deleted.";
            return RedirectToAction(nameof(Products));
        }

        _context.InsuranceProducts.Remove(product);
        await _context.SaveChangesAsync();
        TempData["SuccessMessage"] = $"Insurance Product '{product.name}' deleted successfully.";
        return RedirectToAction(nameof(Products));
    }

    // ==========================================
    // INSURANCE TYPES MANAGEMENT
    // ==========================================
    public async Task<IActionResult> InsuranceTypes()
    {
        var types = await _context.InsuranceTypes
            .Include(t => t.Products)
            .ToListAsync();
        return View(types);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateInsuranceType(InsuranceType model)
    {
        if (string.IsNullOrWhiteSpace(model.name))
        {
            TempData["ErrorMessage"] = "Insurance Type Name is required.";
            return RedirectToAction(nameof(InsuranceTypes));
        }

        _context.InsuranceTypes.Add(model);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Insurance category '{model.name}' created successfully.";
        return RedirectToAction(nameof(InsuranceTypes));
    }

    // ==========================================
    // QUOTATIONS, POLICIES & CLAIMS OVERSIGHT
    // ==========================================
    public async Task<IActionResult> Quotations(string? status = null, string? scope = null)
    {
        var query = _context.Quotations
            .Include(q => q.Customer)
            .Include(q => q.InsuranceProduct)
                .ThenInclude(p => p!.Company)
            .Include(q => q.HandlingAgent)
            .Include(q => q.InsurancePolicy)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status) && status != "All")
        {
            query = query.Where(q => q.status == status);
        }

        if (scope == "AdminPlans")
        {
            query = query.Where(q => q.InsuranceProduct!.Company!.isHostCompany == true);
        }
        else if (scope == "PartnerPlans")
        {
            query = query.Where(q => q.InsuranceProduct!.Company!.isHostCompany == false);
        }

        ViewBag.SelectedStatus = status ?? "All";
        ViewBag.SelectedScope = scope ?? "All";

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
                .ThenInclude(p => p!.Company)
            .FirstOrDefaultAsync(q => q.quoteId == id);

        if (quote == null)
        {
            TempData["ErrorMessage"] = "Quotation record not found.";
            return RedirectToAction(nameof(Quotations));
        }

        // RESTRICTION: Admin can only approve quotations for Admin's products (host company)
        if (quote.InsuranceProduct?.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized Action: Platform Administrators can only approve quotes for official Admin system products. Quotes for partner carrier plans must be reviewed and approved by the partner company's authorized agent.";
            return RedirectToAction(nameof(Quotations));
        }

        // Assign default agent if not assigned
        if (quote.agentId == null)
        {
            var hostAgent = await _context.Agents.FirstOrDefaultAsync(a => a.companyId == quote.InsuranceProduct!.companyId && a.status == "Active")
                ?? await _context.Agents.FirstOrDefaultAsync(a => a.companyId == 1 && a.status == "Active");
            if (hostAgent != null)
            {
                quote.agentId = hostAgent.agentId;
            }
        }

        quote.status = "Accepted";
        await _context.SaveChangesAsync();

        if (quote.Customer != null)
        {
            await _notificationService.NotifyUserAsync(
                quote.Customer.userId,
                "Quotation Accepted by InsureHub Admin!",
                $"Great news! Your quotation request #{quote.quoteId} for '{quote.InsuranceProduct?.name}' has been ACCEPTED by the Platform Administrator. Policy issuance can now proceed.",
                "/Customer/MyQuotations",
                "QuotationAccepted"
            );
        }

        TempData["SuccessMessage"] = $"Quotation #{quote.quoteId} has been Accepted by Admin.";
        return RedirectToAction(nameof(Quotations));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectQuote(int id)
    {
        var quote = await _context.Quotations
            .Include(q => q.Customer)
            .Include(q => q.InsuranceProduct)
                .ThenInclude(p => p!.Company)
            .FirstOrDefaultAsync(q => q.quoteId == id);

        if (quote == null)
        {
            TempData["ErrorMessage"] = "Quotation record not found.";
            return RedirectToAction(nameof(Quotations));
        }

        // RESTRICTION: Admin can only reject quotations for Admin's products (host company)
        if (quote.InsuranceProduct?.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized Action: Platform Administrators can only reject quotes for official Admin system products. Partner carrier quotes are managed by the respective carrier.";
            return RedirectToAction(nameof(Quotations));
        }

        quote.status = "Rejected";
        await _context.SaveChangesAsync();

        if (quote.Customer != null)
        {
            await _notificationService.NotifyUserAsync(
                quote.Customer.userId,
                "Quotation Status Update",
                $"Your quotation request #{quote.quoteId} for '{quote.InsuranceProduct?.name}' was declined by the Platform Administrator.",
                "/Customer/MyQuotations",
                "QuotationRejected"
            );
        }

        TempData["SuccessMessage"] = $"Quotation #{quote.quoteId} has been marked as Rejected.";
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
                .ThenInclude(p => p!.Company)
            .FirstOrDefaultAsync(q => q.quoteId == model.quoteId);

        if (quote == null)
        {
            TempData["ErrorMessage"] = "Quotation reference not found.";
            return RedirectToAction(nameof(Quotations));
        }

        // RESTRICTION: Admin can only issue policy for Admin's products (host company)
        if (quote.InsuranceProduct?.Company?.isHostCompany != true)
        {
            TempData["ErrorMessage"] = "Unauthorized Action: Platform Administrators can only issue policies for official Admin system products.";
            return RedirectToAction(nameof(Quotations));
        }

        var hostAgent = await _context.Agents.FirstOrDefaultAsync(a => a.companyId == quote.InsuranceProduct!.companyId && a.status == "Active")
            ?? await _context.Agents.FirstOrDefaultAsync(a => a.companyId == 1);

        if (model.agentId == null || model.agentId == 0)
        {
            model.agentId = quote.agentId ?? hostAgent?.agentId;
        }
        if (quote.agentId == null && model.agentId != null)
        {
            quote.agentId = model.agentId;
        }

        quote.status = "Accepted";

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            _context.InsurancePolicies.Add(model);
            await _context.SaveChangesAsync();

            if (!string.IsNullOrWhiteSpace(beneficiaryName))
            {
                var beneficiary = new Beneficiary
                {
                    inPoId = model.inPoId,
                    name = beneficiaryName.Trim(),
                    relation = string.IsNullOrWhiteSpace(beneficiaryRelation) ? "Primary" : beneficiaryRelation.Trim(),
                    phone = beneficiaryPhone?.Trim(),
                    sharePercentage = 100.00m
                };
                _context.Beneficiaries.Add(beneficiary);
                await _context.SaveChangesAsync();
            }

            await transaction.CommitAsync();

            if (quote.Customer != null)
            {
                await _notificationService.NotifyUserAsync(
                    quote.Customer.userId,
                    "Policy Issued Successfully!",
                    $"Your official insurance policy #{model.policyNumber} for '{quote.InsuranceProduct?.name}' has been ISSUED and is now ACTIVE.",
                    "/Customer/MyPolicies",
                    "PolicyIssued"
                );
            }

            TempData["SuccessMessage"] = $"Policy '{model.policyNumber}' issued successfully for {quote.Customer?.fullName}!";
            return RedirectToAction(nameof(Policies));
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            TempData["ErrorMessage"] = $"Failed to issue policy: {ex.Message}";
            return RedirectToAction(nameof(Quotations));
        }
    }

    public async Task<IActionResult> Policies()
    {
        var policies = await _context.InsurancePolicies
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.Customer)
            .Include(p => p.Quotation)
                .ThenInclude(q => q!.InsuranceProduct)
                    .ThenInclude(pr => pr!.Company)
            .Include(p => p.Agent)
            .OrderByDescending(p => p.startDate)
            .ToListAsync();

        return View(policies);
    }

    public async Task<IActionResult> Claims()
    {
        var claims = await _context.Claims
            .Include(c => c.InsurancePolicy)
                .ThenInclude(p => p!.Quotation)
                    .ThenInclude(q => q!.Customer)
            .Include(c => c.InsurancePolicy)
                .ThenInclude(p => p!.Quotation)
                    .ThenInclude(q => q!.InsuranceProduct)
                        .ThenInclude(pr => pr!.Company)
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

    // Helper for staff dropdowns
    private async Task PopulateStaffDropdowns(int? companyId)
    {
        var hostCompany = await GetHostCompanyAsync();
        var companies = await _context.Companies.OrderBy(c => c.name).ToListAsync();
        ViewBag.Companies = new SelectList(companies, "companyId", "name", companyId);

        var targetCompanyId = companyId ?? hostCompany?.companyId ?? companies.FirstOrDefault()?.companyId ?? 1;
        var agents = await _context.Agents
            .Where(a => a.companyId == targetCompanyId && a.status == "Active")
            .OrderBy(a => a.fullName)
            .ToListAsync();

        if (!agents.Any())
        {
            agents = await _context.Agents.Where(a => a.status == "Active").ToListAsync();
        }

        ViewBag.Agents = new SelectList(agents, "agentId", "fullName");
    }

    private async Task<Company?> GetHostCompanyAsync()
    {
        return await _context.Companies.FirstOrDefaultAsync(c => c.isHostCompany)
            ?? await _context.Companies.FirstOrDefaultAsync(c => c.companyId == 1);
    }

    // GET: Admin/Reports
    [HttpGet]
    public async Task<IActionResult> Reports(string timeframe = "monthly", int? companyId = null, string scope = "all")
    {
        timeframe = (timeframe ?? "monthly").ToLower().Trim();
        if (timeframe != "daily" && timeframe != "weekly" && timeframe != "monthly" && timeframe != "yearly")
        {
            timeframe = "monthly";
        }

        scope = (scope ?? "all").ToLower().Trim();
        if (scope != "admin" && scope != "partner" && scope != "all")
        {
            scope = "all";
        }

        var hostCompany = await GetHostCompanyAsync();
        var hostCompanyId = hostCompany?.companyId ?? 1;
        var hostCompanyName = hostCompany?.name ?? "InsureHub Direct Protection";

        var allCompanies = await _context.Companies.OrderBy(c => c.name).ToListAsync();
        var partnerCompanies = allCompanies.Where(c => c.companyId != hostCompanyId).ToList();

        var allStaff = await _context.StaffMembers.Where(s => s.status == "Active").ToListAsync();
        var allPolicies = await _context.InsurancePolicies
            .Include(ip => ip.Quotation)
                .ThenInclude(q => q!.Customer)
                    .ThenInclude(c => c!.UserAccount)
            .Include(ip => ip.Quotation)
                .ThenInclude(q => q!.InsuranceProduct)
                    .ThenInclude(prod => prod!.Company)
            .Include(ip => ip.Quotation)
                .ThenInclude(q => q!.InsuranceProduct)
                    .ThenInclude(prod => prod!.InsuranceType)
            .Include(ip => ip.Payments)
            .ToListAsync();

        var allPayments = await _context.Payments
            .Include(p => p.InsurancePolicy)
                .ThenInclude(ip => ip!.Quotation)
                    .ThenInclude(q => q!.Customer)
            .Include(p => p.InsurancePolicy)
                .ThenInclude(ip => ip!.Quotation)
                    .ThenInclude(q => q!.InsuranceProduct)
                        .ThenInclude(prod => prod!.Company)
            .Include(p => p.InsurancePolicy)
                .ThenInclude(ip => ip!.Quotation)
                    .ThenInclude(q => q!.InsuranceProduct)
                        .ThenInclude(prod => prod!.InsuranceType)
            .Where(p => p.payStatus == "Completed")
            .ToListAsync();

        var allClaims = await _context.Claims
            .Include(c => c.InsurancePolicy)
                .ThenInclude(ip => ip!.Quotation)
                    .ThenInclude(q => q!.InsuranceProduct)
            .Where(c => c.status == "Approved" || c.status == "Paid")
            .ToListAsync();

        // 1. Host Company (Admin Owner) Metrics
        var hostStaffCount = allStaff.Count(s => s.companyId == hostCompanyId);
        var hostPols = allPolicies.Where(p => p.Quotation?.InsuranceProduct?.companyId == hostCompanyId).ToList();
        var hostPayments = allPayments.Where(p => p.InsurancePolicy?.Quotation?.InsuranceProduct?.companyId == hostCompanyId).ToList();
        var hostClaims = allClaims.Where(c => c.InsurancePolicy?.Quotation?.InsuranceProduct?.companyId == hostCompanyId).ToList();
        var hostRev = hostPayments.Sum(p => p.payAmount);
        var hostClm = hostClaims.Sum(c => c.claimAmount);
        var hostCustCount = hostPols
            .Where(p => p.Quotation != null)
            .Select(p => p.Quotation!.customerId)
            .Distinct()
            .Count();

        var totalRevForPercent = allPayments.Sum(p => p.payAmount);
        if (totalRevForPercent <= 0) totalRevForPercent = 1m;

        var hostCompanyReport = new CompanyReportItem
        {
            CompanyId = hostCompanyId,
            CompanyName = hostCompanyName,
            CompanyEmail = hostCompany?.email,
            CompanyPhone = hostCompany?.phone,
            IsHostCompany = true,
            StaffCount = hostStaffCount,
            CustomerCount = hostCustCount,
            PoliciesCount = hostPols.Count,
            TotalRevenue = hostRev,
            TotalClaimsPaid = hostClm,
            RevenuePercentage = Math.Round((hostRev / totalRevForPercent) * 100, 1)
        };

        // 2. Partner Companies Metrics
        var partnerStaffCount = allStaff.Count(s => s.companyId != hostCompanyId);
        var partnerPols = allPolicies.Where(p => p.Quotation?.InsuranceProduct?.companyId != hostCompanyId).ToList();
        var partnerPayments = allPayments.Where(p => p.InsurancePolicy?.Quotation?.InsuranceProduct?.companyId != hostCompanyId).ToList();
        var partnerClaims = allClaims.Where(c => c.InsurancePolicy?.Quotation?.InsuranceProduct?.companyId != hostCompanyId).ToList();
        var partnerRev = partnerPayments.Sum(p => p.payAmount);
        var partnerClm = partnerClaims.Sum(c => c.claimAmount);
        var partnerCustCount = partnerPols
            .Where(p => p.Quotation != null)
            .Select(p => p.Quotation!.customerId)
            .Distinct()
            .Count();

        var companyReports = new List<CompanyReportItem>();
        foreach (var comp in partnerCompanies)
        {
            var compStaff = allStaff.Count(s => s.companyId == comp.companyId);
            var compPols = allPolicies.Where(p => p.Quotation?.InsuranceProduct?.companyId == comp.companyId).ToList();
            var compCusts = compPols
                .Where(p => p.Quotation != null)
                .Select(p => p.Quotation!.customerId)
                .Distinct()
                .Count();

            var compPayments = allPayments.Where(p => p.InsurancePolicy?.Quotation?.InsuranceProduct?.companyId == comp.companyId).ToList();
            var compClaims = allClaims.Where(c => c.InsurancePolicy?.Quotation?.InsuranceProduct?.companyId == comp.companyId).ToList();

            var cRev = compPayments.Sum(p => p.payAmount);
            var cClm = compClaims.Sum(c => c.claimAmount);

            companyReports.Add(new CompanyReportItem
            {
                CompanyId = comp.companyId,
                CompanyName = comp.name,
                CompanyEmail = comp.email,
                CompanyPhone = comp.phone,
                IsHostCompany = false,
                StaffCount = compStaff,
                CustomerCount = compCusts,
                PoliciesCount = compPols.Count,
                TotalRevenue = cRev,
                TotalClaimsPaid = cClm,
                RevenuePercentage = Math.Round((cRev / totalRevForPercent) * 100, 1)
            });
        }

        // 3. Resolve active dataset based on Scope and Company Filter
        List<Payment> activePayments;
        List<Claim> activeClaims;
        List<InsurancePolicy> activePolicies;
        int activeStaffCount;
        int activeCustomersCount;
        string? selectedCompanyName = null;

        if (scope == "admin")
        {
            companyId = hostCompanyId;
            activePayments = hostPayments;
            activeClaims = hostClaims;
            activePolicies = hostPols;
            activeStaffCount = hostStaffCount;
            activeCustomersCount = hostCustCount;
            selectedCompanyName = hostCompanyName;
        }
        else if (scope == "partner")
        {
            if (companyId.HasValue && companyId.Value > 0 && companyId.Value != hostCompanyId)
            {
                activePayments = allPayments.Where(p => p.InsurancePolicy?.Quotation?.InsuranceProduct?.companyId == companyId.Value).ToList();
                activeClaims = allClaims.Where(c => c.InsurancePolicy?.Quotation?.InsuranceProduct?.companyId == companyId.Value).ToList();
                activePolicies = allPolicies.Where(p => p.Quotation?.InsuranceProduct?.companyId == companyId.Value).ToList();
                activeStaffCount = allStaff.Count(s => s.companyId == companyId.Value);
                activeCustomersCount = activePolicies.Where(p => p.Quotation != null).Select(p => p.Quotation!.customerId).Distinct().Count();
                selectedCompanyName = partnerCompanies.FirstOrDefault(c => c.companyId == companyId.Value)?.name;
            }
            else
            {
                companyId = null;
                activePayments = partnerPayments;
                activeClaims = partnerClaims;
                activePolicies = partnerPols;
                activeStaffCount = partnerStaffCount;
                activeCustomersCount = partnerCustCount;
                selectedCompanyName = "All Partner Companies";
            }
        }
        else // "all"
        {
            if (companyId.HasValue && companyId.Value > 0)
            {
                activePayments = allPayments.Where(p => p.InsurancePolicy?.Quotation?.InsuranceProduct?.companyId == companyId.Value).ToList();
                activeClaims = allClaims.Where(c => c.InsurancePolicy?.Quotation?.InsuranceProduct?.companyId == companyId.Value).ToList();
                activePolicies = allPolicies.Where(p => p.Quotation?.InsuranceProduct?.companyId == companyId.Value).ToList();
                activeStaffCount = allStaff.Count(s => s.companyId == companyId.Value);
                activeCustomersCount = activePolicies.Where(p => p.Quotation != null).Select(p => p.Quotation!.customerId).Distinct().Count();
                selectedCompanyName = allCompanies.FirstOrDefault(c => c.companyId == companyId.Value)?.name;
            }
            else
            {
                activePayments = allPayments;
                activeClaims = allClaims;
                activePolicies = allPolicies;
                activeStaffCount = allStaff.Count;
                activeCustomersCount = allPolicies.Where(p => p.Quotation != null).Select(p => p.Quotation!.customerId).Distinct().Count();
                selectedCompanyName = null;
            }
        }

        // 4. Build Customer Buyers
        var customerBuyers = activePolicies
            .Where(p => p.Quotation?.Customer != null)
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
                    CompanyName = latestPolicy.Quotation?.InsuranceProduct?.Company?.name ?? "Carrier",
                    LatestPurchaseDate = latestPolicy.startDate,
                    PolicyStatus = latestPolicy.status
                };
            })
            .OrderByDescending(c => c.TotalSpent)
            .Take(30)
            .ToList();

        // If scope is admin and there are quotes for host products, include them so admin owner report shows customers
        if (scope == "admin" && !customerBuyers.Any())
        {
            var hostQuotes = await _context.Quotations
                .Include(q => q.Customer).ThenInclude(c => c!.UserAccount)
                .Include(q => q.InsuranceProduct)
                .Where(q => q.InsuranceProduct!.companyId == hostCompanyId && q.Customer != null)
                .ToListAsync();

            customerBuyers = hostQuotes
                .GroupBy(q => q.customerId)
                .Select(g =>
                {
                    var first = g.First();
                    var customer = first.Customer!;
                    var latest = g.OrderByDescending(x => x.requestDate).First();
                    return new ReportCustomerBuyerItem
                    {
                        CustomerId = customer.customerId,
                        CustomerName = customer.fullName,
                        Email = customer.UserAccount?.email,
                        Phone = customer.phone,
                        NationalId = customer.nationalId,
                        PoliciesCount = 0,
                        TotalSpent = g.Sum(x => x.estimatedPremium),
                        LatestProductName = latest.InsuranceProduct?.name ?? "InsureHub Plan",
                        CompanyName = hostCompanyName,
                        LatestPurchaseDate = latest.requestDate,
                        PolicyStatus = latest.status
                    };
                })
                .ToList();
        }

        var model = new FinancialReportViewModel
        {
            Timeframe = timeframe,
            Scope = scope,
            SelectedCompanyId = companyId,
            SelectedCompanyName = selectedCompanyName,
            AvailableCompanies = allCompanies,
            PartnerCompanies = partnerCompanies,
            HostCompanyId = hostCompanyId,
            HostCompanyName = hostCompanyName,
            HostCompanyReport = hostCompanyReport,

            TotalRevenue = activePayments.Sum(p => p.payAmount),
            TotalClaimsPaid = activeClaims.Sum(c => c.claimAmount),
            TotalPoliciesCount = activePolicies.Count,
            TotalTransactionsCount = activePayments.Count,
            TotalStaffCount = activeStaffCount,
            TotalCustomersCount = activeCustomersCount > 0 ? activeCustomersCount : customerBuyers.Count,

            AdminRevenue = hostRev,
            AdminPoliciesCount = hostPols.Count,
            AdminStaffCount = hostStaffCount,
            AdminCustomersCount = hostCustCount,

            PartnerRevenue = partnerRev,
            PartnerPoliciesCount = partnerPols.Count,
            PartnerStaffCount = partnerStaffCount,
            PartnerCustomersCount = partnerCustCount,

            CustomerBuyers = customerBuyers,
            CompanyReports = companyReports
        };

        BuildTimeSeriesData(model, activePayments, activeClaims, timeframe);

        var totalRev = model.TotalRevenue > 0 ? model.TotalRevenue : 1m;

        // Category breakdown
        model.CategoryBreakdown = activePayments
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

        // Recent transactions
        model.RecentTransactions = activePayments
            .OrderByDescending(p => p.payDate)
            .Take(25)
            .Select(p => new ReportTransactionItem
            {
                PaymentId = p.payId,
                TransactionRef = p.transactionRef,
                PolicyNumber = p.InsurancePolicy?.policyNumber ?? "N/A",
                ProductName = p.InsurancePolicy?.Quotation?.InsuranceProduct?.name ?? "Insurance Plan",
                CategoryName = p.InsurancePolicy?.Quotation?.InsuranceProduct?.InsuranceType?.name ?? "General",
                CompanyName = p.InsurancePolicy?.Quotation?.InsuranceProduct?.Company?.name ?? "Carrier",
                CustomerName = p.InsurancePolicy?.Quotation?.Customer?.fullName ?? "Customer",
                PayDate = p.payDate,
                Amount = p.payAmount,
                PaymentMethod = p.payMethod,
                Status = p.payStatus
            })
            .ToList();

        return View(model);
    }

    private static void BuildTimeSeriesData(FinancialReportViewModel model, List<Payment> payments, List<Claim> claims, string timeframe)
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


