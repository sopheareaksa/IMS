using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IMS.Models;
using IMS.Services;

namespace IMS.Controllers;

public class ProductsController : Controller
{
    private readonly InsuranceContext _context;
    private readonly IConfiguration _config;
    private readonly IWebHostEnvironment _env;

    public ProductsController(InsuranceContext context, IConfiguration config, IWebHostEnvironment env)
    {
        _context = context;
        _config = config;
        _env = env;
    }

    // Development-only UI preview; see PreviewCatalog. Never enable outside Development.
    private bool PreviewMode => PreviewCatalog.IsEnabled(_config, _env);

    public async Task<IActionResult> Index(string? search = null, int? typeId = null, int? companyId = null)
    {
        ViewBag.SearchTerm = search;
        ViewBag.SelectedTypeId = typeId;
        ViewBag.SelectedCompanyId = companyId;

        if (PreviewMode)
        {
            PreviewCatalog.AdoptPreviewIdentity(HttpContext, PreviewCatalog.DemoCustomer());
            ViewBag.InsuranceTypes = PreviewCatalog.Types().OrderBy(t => t.name).ToList();
            ViewBag.Companies = PreviewCatalog.Companies()
                .Where(c => c.isHostCompany || c.approvalStatus == "Approved")
                .OrderBy(c => c.name)
                .ToList();

            var demoQuery = PreviewCatalog.Products()
                .Where(p => p.status == "Active" && (p.Company!.isHostCompany || p.Company.approvalStatus == "Approved"));

            return View(ApplyFilters(demoQuery, search, typeId, companyId)
                .OrderBy(p => p.basePremium)
                .ToList());
        }

        var query = _context.InsuranceProducts
            .Include(p => p.Company)
            .Include(p => p.InsuranceType)
            .Where(p => p.status == "Active" && (p.Company!.isHostCompany || p.Company.approvalStatus == "Approved"))
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.name.Contains(search) || (p.detail != null && p.detail.Contains(search)));
        }

        if (typeId.HasValue && typeId.Value > 0)
        {
            query = query.Where(p => p.inTypeId == typeId.Value);
        }

        if (companyId.HasValue && companyId.Value > 0)
        {
            query = query.Where(p => p.companyId == companyId.Value);
        }

        ViewBag.InsuranceTypes = await _context.InsuranceTypes.OrderBy(t => t.name).ToListAsync();
        ViewBag.Companies = await _context.Companies.Where(c => c.isHostCompany || c.approvalStatus == "Approved").OrderBy(c => c.name).ToListAsync();

        var products = await query.OrderBy(p => p.basePremium).ToListAsync();
        return View(products);
    }

    public async Task<IActionResult> Details(int id)
    {
        if (PreviewMode)
        {
            PreviewCatalog.AdoptPreviewIdentity(HttpContext, PreviewCatalog.DemoCustomer());

            var demoProduct = PreviewCatalog.FindProduct(id);
            if (demoProduct == null)
            {
                TempData["ErrorMessage"] = "Product not found.";
                return RedirectToAction(nameof(Index));
            }

            return View(demoProduct);
        }

        var product = await _context.InsuranceProducts
            .Include(p => p.Company)
            .Include(p => p.InsuranceType)
            .FirstOrDefaultAsync(p => p.inProId == id);

        if (product == null)
        {
            TempData["ErrorMessage"] = "Product not found.";
            return RedirectToAction(nameof(Index));
        }

        return View(product);
    }

    private static IEnumerable<InsuranceProduct> ApplyFilters(
        IEnumerable<InsuranceProduct> products, string? search, int? typeId, int? companyId)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            products = products.Where(p =>
                p.name.Contains(search, StringComparison.OrdinalIgnoreCase)
                || (p.detail != null && p.detail.Contains(search, StringComparison.OrdinalIgnoreCase)));
        }

        if (typeId.HasValue && typeId.Value > 0)
        {
            products = products.Where(p => p.inTypeId == typeId.Value);
        }

        if (companyId.HasValue && companyId.Value > 0)
        {
            products = products.Where(p => p.companyId == companyId.Value);
        }

        return products;
    }
}
