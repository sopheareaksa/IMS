using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IMS.Models;
using IMS.Services;

namespace IMS.Controllers;

// [Authorize] is commented out so the Development PreviewMode below can render this page
// without login. Re-enable before publishing; with PreviewMode off every action still
// redirects anonymous users (or users without a Customer profile) to login.
//[Authorize(Roles = "Customer")]
public class QuotationsController : Controller
{
    private readonly InsuranceContext _context;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public QuotationsController(
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

    private bool PreviewMode => PreviewCatalog.IsEnabled(_config, _env);

    [HttpGet]
    public async Task<IActionResult> RequestQuote(int inProId)
    {
        if (PreviewMode)
        {
            var demoProduct = PreviewCatalog.FindProduct(inProId);
            if (demoProduct == null || demoProduct.status != "Active")
            {
                TempData["ErrorMessage"] = "The requested insurance product is currently unavailable.";
                return RedirectToAction("Index", "Products");
            }

            var demoCustomer = PreviewCatalog.DemoCustomer();
            PreviewCatalog.AdoptPreviewIdentity(HttpContext, demoCustomer);
            ViewBag.Customer = demoCustomer;
            return View(demoProduct);
        }

        var product = await _context.InsuranceProducts
            .Include(p => p.Company)
            .Include(p => p.InsuranceType)
            .FirstOrDefaultAsync(p => p.inProId == inProId);

        if (product == null || product.status != "Active")
        {
            TempData["ErrorMessage"] = "The requested insurance product is currently unavailable.";
            return RedirectToAction("Index", "Products");
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            return RedirectToAction("Login", "Auth");
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.userId == userId);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "Please sign in with a Customer account to request an insurance quotation.";
            return RedirectToAction("Index", "Products");
        }

        ViewBag.Customer = customer;
        return View(product);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestQuote(int inProId, decimal estimatedPremium, string? message)
    {
        if (PreviewMode)
        {
            var demoProduct = PreviewCatalog.FindProduct(inProId);
            var demoCustomer = PreviewCatalog.DemoCustomer();
            PreviewCatalog.AdoptPreviewIdentity(HttpContext, demoCustomer);

            if (demoProduct == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction("Index", "Products");
            }

            var demoQuotation = new Quotation
            {
                customerId = demoCustomer.customerId,
                inProId = demoProduct.inProId,
                InsuranceProduct = demoProduct,
                requestDate = DateTime.UtcNow,
                message = message?.Trim(),
                estimatedPremium = estimatedPremium > 0 ? estimatedPremium : demoProduct.basePremium,
                status = "Pending"
            };

            var demoReference = PreviewCatalog.Submit(demoQuotation);
            TempData["QuoteReference"] = demoReference.ToString();
            return RedirectToAction("MyQuotations", "Customer");
        }

        var product = await _context.InsuranceProducts
            .Include(p => p.Company)
            .FirstOrDefaultAsync(p => p.inProId == inProId);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction("Index", "Products");
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdString) || !int.TryParse(userIdString, out int userId))
        {
            return RedirectToAction("Login", "Auth");
        }

        var customer = await _context.Customers.FirstOrDefaultAsync(c => c.userId == userId);
        if (customer == null)
        {
            TempData["ErrorMessage"] = "A valid Customer profile is required to submit a quotation request.";
            return RedirectToAction("Index", "Products");
        }

        var assignedAgent = await _context.Agents
            .Where(a => a.companyId == product.companyId && a.status == "Active")
            .OrderBy(a => a.agentId)
            .FirstOrDefaultAsync();

        var quotation = new Quotation
        {
            customerId = customer.customerId,
            inProId = product.inProId,
            agentId = assignedAgent?.agentId,
            requestDate = DateTime.UtcNow,
            message = message?.Trim(),
            estimatedPremium = estimatedPremium > 0 ? estimatedPremium : product.basePremium,
            status = "Pending"
        };

        _context.Quotations.Add(quotation);
        await _context.SaveChangesAsync();

        // ==============================================================
        // DISPATCH ALERTS TO UNDERWRITERS & ADMIN
        // ==============================================================
        bool isHostProduct = product.Company?.isHostCompany == true;

        if (isHostProduct)
        {
            // High-priority alert to Platform Administrator to accept or reject
            await _notificationService.NotifyAdminAsync(
                title: "Action Required: New Quotation for Admin Plan",
                message: $"Customer '{customer.fullName}' has requested a quotation for your official plan '{product.name}' (Est. Premium: ${quotation.estimatedPremium:N2}). Please review and accept or reject.",
                linkUrl: "/Admin/Quotations?status=Pending",
                type: "AdminQuotationRequest"
            );

            // Also alert internal staff under host company
            await _notificationService.NotifyCompanyAsync(
                companyId: product.companyId,
                title: "New Quotation Request (Admin System)",
                message: $"Customer '{customer.fullName}' requested a quote for Admin plan '{product.name}' (Est. Premium: ${quotation.estimatedPremium:N2}).",
                linkUrl: "/Agent/Quotations",
                type: "QuotationRequest",
                alsoNotifyAdmin: false
            );
        }
        else
        {
            // Partner Company Product: alert company agents & staff, and also notify Admin for platform oversight
            await _notificationService.NotifyCompanyAsync(
                companyId: product.companyId,
                title: "New Quotation Request",
                message: $"Customer '{customer.fullName}' has requested a quotation for partner plan '{product.name}' ({product.Company?.name}) (Est. Premium: ${quotation.estimatedPremium:N2}).",
                linkUrl: "/Agent/Quotations",
                type: "QuotationRequest",
                alsoNotifyAdmin: true
            );
        }

        // Confirmation notification to the customer
        await _notificationService.NotifyUserAsync(
            userId: customer.userId,
            title: "Quotation Request Submitted",
            message: $"Your quote request for '{product.name}' has been submitted to {(isHostProduct ? "InsureHub Administration" : product.Company?.name)}. Assigned Quote Reference: #{quotation.quoteId}.",
            linkUrl: "/Customer/MyQuotations",
            type: "QuotationSubmitted"
        );

        // MyQuotations renders the success dialog from this reference.
        TempData["QuoteReference"] = quotation.quoteId.ToString();
        return RedirectToAction("MyQuotations", "Customer");
    }
}
