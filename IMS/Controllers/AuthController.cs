using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using IMS.Models;
using IMS.ViewModels;
using IMS.DTOs;
using IMS.Services;

namespace IMS.Controllers;

public class AuthController : Controller
{
    private readonly InsuranceContext _context;
    private readonly IMemoryCache _cache;
    private readonly IEmailService _emailService;
    private readonly INotificationService _notificationService;

    public AuthController(
        InsuranceContext context,
        IMemoryCache cache,
        IEmailService emailService,
        INotificationService notificationService)
    {
        _context = context;
        _cache = cache;
        _emailService = emailService;
        _notificationService = notificationService;
    }

    public IActionResult Index()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");
            if (User.IsInRole("Agent") || User.IsInRole("Staff")) return RedirectToAction("Dashboard", "Agent");
            if (User.IsInRole("Customer")) return RedirectToAction("Index", "Products");
        }
        return RedirectToAction(nameof(Login));
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    public IActionResult Profile()
    {
        if (User.IsInRole("Customer")) return RedirectToAction("Profile", "Customer");
        if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");
        if (User.IsInRole("Agent") || User.IsInRole("Staff")) return RedirectToAction("Dashboard", "Agent");
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult RegisterCustomer(string? username = null, string? email = null)
    {
        var model = new RegisterCustomerViewModel
        {
            Username = username ?? string.Empty,
            Email = email ?? string.Empty
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterCustomer(RegisterCustomerViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
            var parameters = new[]
            {
                new SqlParameter("@Username", model.Username),
                new SqlParameter("@Email", model.Email),
                new SqlParameter("@PasswordHash", passwordHash),
                new SqlParameter("@FullName", model.FullName),
                new SqlParameter("@Phone", model.Phone),
                new SqlParameter("@Address", (object?)model.Address ?? DBNull.Value),
                new SqlParameter("@DateOfBirth", (object?)model.DateOfBirth ?? DBNull.Value),
                new SqlParameter("@NationalId", (object?)model.NationalId ?? DBNull.Value)
            };
            var result = await _context.Database
                .SqlQueryRaw<CustomerRegisterResult>(
                    "EXEC dbo.sp_RegisterCustomer @Username, @Email, @PasswordHash, @FullName, @Phone, @Address, @DateOfBirth, @NationalId",
                    parameters)
                .ToListAsync();

            var registeredCustomer = result.FirstOrDefault();

            TempData["SuccessMessage"] = registeredCustomer?.Message ?? "Registration successful! Please login with your credentials.";
            return RedirectToAction(nameof(Login));
        }
        catch (SqlException ex)
        {
            if (ex.Number == 2627 || ex.Number == 2601)
            {
                ModelState.AddModelError(string.Empty, "Username, Email, or National ID already exists.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"Registration failed: {ex.Message}");
            }

            return View(model);
        }
    }

    [HttpGet]
    public IActionResult RegisterSeller(string? username = null, string? email = null)
    {
        var model = new RegisterSellerViewModel
        {
            Username = username ?? string.Empty,
            Email = email ?? string.Empty
        };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterSeller(RegisterSellerViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
            var parameters = new[]
            {
                new SqlParameter("@Username", model.Username),
                new SqlParameter("@Email", model.Email),
                new SqlParameter("@PasswordHash", passwordHash),
                new SqlParameter("@CompanyName", model.CompanyName),
                new SqlParameter("@CompanyEmail", model.CompanyEmail),
                new SqlParameter("@CompanyPhone", model.CompanyPhone),
                new SqlParameter("@CompanyAddress", (object?)model.CompanyAddress ?? DBNull.Value),
                new SqlParameter("@CommissionRate", 10.00m),
                new SqlParameter("@ApprovalStatus", "Pending"),
                new SqlParameter("@LeadAgentFullName", model.LeadAgentFullName),
                new SqlParameter("@Branch", string.IsNullOrWhiteSpace(model.Branch) ? "Main Branch" : model.Branch)
            };
            var result = await _context.Database
                .SqlQueryRaw<SellerRegisterResult>(
                    "EXEC dbo.sp_RegisterSellerCompany @Username, @Email, @PasswordHash, @CompanyName, @CompanyEmail, @CompanyPhone, @CompanyAddress, @CommissionRate, @ApprovalStatus, @LeadAgentFullName, @Branch",
                    parameters)
                .ToListAsync();

            var registeredSeller = result.FirstOrDefault();

            // ALERT ADMIN DASHBOARD: Trigger notification for newly registered partner company
            await _notificationService.NotifyAdminAsync(
                title: "New Partner Company Registration",
                message: $"Company '{model.CompanyName}' (Rep: {model.LeadAgentFullName}, Code: {registeredSeller?.RegNo}) has registered and is pending your approval.",
                linkUrl: "/Admin/Companies",
                type: "CompanyRegistration"
            );

            TempData["SuccessMessage"] = $"Company '{model.CompanyName}' registered successfully under status 'Pending'. Your account is currently awaiting Admin approval. You will be able to log in once approved.";
            return RedirectToAction(nameof(Login));
        }
        catch (SqlException ex)
        {
            if (ex.Number == 2627 || ex.Number == 2601)
            {
                ModelState.AddModelError(string.Empty, "Username, Email, or Company details already exist.");
            }
            else
            {
                ModelState.AddModelError(string.Empty, $"Registration failed: {ex.Message}");
            }

            return View(model);
        }
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity != null && User.Identity.IsAuthenticated)
        {
            if (User.IsInRole("Admin")) return RedirectToAction("Dashboard", "Admin");
            if (User.IsInRole("Agent") || User.IsInRole("Staff")) return RedirectToAction("Dashboard", "Agent");
            if (User.IsInRole("Customer")) return RedirectToAction("Index", "Products");
        }
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            TempData["ErrorMessage"] = "Please provide both Email/Username and Password.";
            return RedirectToAction(nameof(Login));
        }

        var user = await _context.UserAccounts
            .FirstOrDefaultAsync(u => u.email == email || u.username == email);

        if (user == null)
        {
            TempData["ErrorMessage"] = "Invalid credentials. Please verify your email/username and password.";
            return RedirectToAction(nameof(Login));
        }

        bool isPasswordValid = false;
        try
        {
            isPasswordValid = BCrypt.Net.BCrypt.Verify(password, user.passwordHash);
        }
        catch
        {
            // Backward-compatibility / test fallback
            if (password == user.passwordHash)
            {
                isPasswordValid = true;
                user.passwordHash = BCrypt.Net.BCrypt.HashPassword(password);
                await _context.SaveChangesAsync();
            }
        }

        if (!isPasswordValid)
        {
            TempData["ErrorMessage"] = "Invalid credentials. Please verify your email/username and password.";
            return RedirectToAction(nameof(Login));
        }

        if (user.status != "Active")
        {
            TempData["ErrorMessage"] = $"Account status is '{user.status}'. Please contact the administrator.";
            return RedirectToAction(nameof(Login));
        }

        // ==============================================================
        // AGENT / PARTNER COMPANY APPROVAL GATE CHECK
        // ==============================================================
        if (user.role == "Agent")
        {
            var agent = await _context.Agents
                .Include(a => a.Company)
                .FirstOrDefaultAsync(a => a.userId == user.userId);

            var company = agent?.Company ?? await _context.Companies.FirstOrDefaultAsync(c => c.userId == user.userId);

            if (company != null && !company.isHostCompany)
            {
                if (company.approvalStatus == "Pending")
                {
                    TempData["ErrorMessage"] = "Your company registration is pending approval by the Admin. Please wait until your company has been approved before logging in.";
                    return RedirectToAction(nameof(Login));
                }
                if (company.approvalStatus == "Rejected")
                {
                    TempData["ErrorMessage"] = "Your company registration was rejected. Please contact the administrator.";
                    return RedirectToAction(nameof(Login));
                }
                if (company.approvalStatus == "Suspended")
                {
                    TempData["ErrorMessage"] = "Your company account is currently suspended. Please contact the administrator.";
                    return RedirectToAction(nameof(Login));
                }
            }
        }
        else if (user.role == "Staff")
        {
            var staff = await _context.StaffMembers
                .Include(s => s.Company)
                .FirstOrDefaultAsync(s => s.userId == user.userId);

            if (staff != null && staff.Company != null && !staff.Company.isHostCompany && staff.Company.approvalStatus != "Approved")
            {
                TempData["ErrorMessage"] = $"Your associated company '{staff.Company.name}' is currently {staff.Company.approvalStatus}. Please wait until the company is approved.";
                return RedirectToAction(nameof(Login));
            }
        }

        // Update last login
        user.lastLogin = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        // Build Claims
        var claims = new List<System.Security.Claims.Claim>
        {
            new System.Security.Claims.Claim(ClaimTypes.NameIdentifier, user.userId.ToString()),
            new System.Security.Claims.Claim(ClaimTypes.Name, user.username),
            new System.Security.Claims.Claim(ClaimTypes.Email, user.email),
            new System.Security.Claims.Claim(ClaimTypes.Role, user.role)
        };

        if (user.role == "Customer")
        {
            var cust = await _context.Customers.FirstOrDefaultAsync(c => c.userId == user.userId);
            if (cust != null)
            {
                claims.Add(new System.Security.Claims.Claim("CustomerId", cust.customerId.ToString()));
                claims.Add(new System.Security.Claims.Claim("FullName", cust.fullName));
            }
        }
        else if (user.role == "Agent")
        {
            var ag = await _context.Agents.Include(a => a.Company).FirstOrDefaultAsync(a => a.userId == user.userId);
            if (ag != null)
            {
                claims.Add(new System.Security.Claims.Claim("AgentId", ag.agentId.ToString()));
                claims.Add(new System.Security.Claims.Claim("CompanyId", ag.companyId.ToString()));
                claims.Add(new System.Security.Claims.Claim("FullName", ag.fullName));
                if (ag.Company != null)
                {
                    claims.Add(new System.Security.Claims.Claim("CompanyName", ag.Company.name));
                    claims.Add(new System.Security.Claims.Claim("IsHostCompany", ag.Company.isHostCompany ? "True" : "False"));
                }
            }
        }
        else if (user.role == "Staff")
        {
            var st = await _context.StaffMembers.Include(s => s.Company).FirstOrDefaultAsync(s => s.userId == user.userId);
            if (st != null)
            {
                claims.Add(new System.Security.Claims.Claim("StaffId", st.staffId.ToString()));
                claims.Add(new System.Security.Claims.Claim("CompanyId", st.companyId.ToString()));
                claims.Add(new System.Security.Claims.Claim("FullName", st.fullName));
                if (st.Company != null)
                {
                    claims.Add(new System.Security.Claims.Claim("CompanyName", st.Company.name));
                }
            }
        }
        else if (user.role == "Admin")
        {
            claims.Add(new System.Security.Claims.Claim("FullName", "System Administrator"));
            claims.Add(new System.Security.Claims.Claim("CompanyId", "1"));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

        TempData["SuccessMessage"] = $"Welcome back, {user.username}! Signed in as {user.role}.";

        // Role-based destination
        if (user.role == "Admin")
        {
            return RedirectToAction("Dashboard", "Admin");
        }
        if (user.role == "Agent" || user.role == "Staff")
        {
            return RedirectToAction("Dashboard", "Agent");
        }
        return RedirectToAction("Index", "Products");
    }

    [HttpGet]
    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["SuccessMessage"] = "You have been logged out successfully.";
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendOTP(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("ForgotPassword", model);
        }

        var normalizedEmail = model.Email.Trim().ToLower();
        var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.email.ToLower() == normalizedEmail);
        if (user == null)
        {
            ModelState.AddModelError(nameof(model.Email), "No account registered with this email address.");
            return View("ForgotPassword", model);
        }
        string otp = Random.Shared.Next(100000, 999999).ToString();
        string cacheKey = $"OTP_{normalizedEmail}";
        _cache.Set(cacheKey, otp, TimeSpan.FromMinutes(5));

        try
        {
            await _emailService.SendOtpEmailAsync(model.Email.Trim(), otp);
            TempData["SuccessMessage"] = $"A 6-digit OTP code has been sent to {model.Email}. Please check your inbox.";
            return RedirectToAction(nameof(VerifyOTP), new { email = model.Email.Trim() });
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Failed to send email: {ex.Message}");
            return View("ForgotPassword", model);
        }
    }

    [HttpGet]
    public IActionResult VerifyOTP(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new VerifyOtpViewModel { Email = email });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult VerifyOTP(VerifyOtpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string normalizedEmail = model.Email.Trim().ToLower();
        string cacheKey = $"OTP_{normalizedEmail}";

        if (_cache.TryGetValue(cacheKey, out string? cachedOtp) && cachedOtp == model.Otp.Trim())
        {
            _cache.Remove(cacheKey);
            string resetToken = Guid.NewGuid().ToString("N");
            string resetTokenKey = $"ResetAllowed_{normalizedEmail}";
            _cache.Set(resetTokenKey, resetToken, TimeSpan.FromMinutes(10));

            TempData["SuccessMessage"] = "OTP verified successfully! Please set your new password.";
            return RedirectToAction(nameof(SetNewPassword), new { email = model.Email.Trim(), token = resetToken });
        }

        ModelState.AddModelError(nameof(model.Otp), "Invalid or expired OTP code. Please check your email or request a new code.");
        return View(model);
    }

    [HttpGet]
    public IActionResult SetNewPassword(string email, string token)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
        {
            TempData["ErrorMessage"] = "Invalid request. Please start the password reset process again.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        string normalizedEmail = email.Trim().ToLower();
        string resetTokenKey = $"ResetAllowed_{normalizedEmail}";

        if (!_cache.TryGetValue(resetTokenKey, out string? cachedToken) || cachedToken != token)
        {
            TempData["ErrorMessage"] = "Password reset session has expired or is invalid. Please request a new OTP.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new SetNewPasswordViewModel { Email = email.Trim(), Token = token });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetNewPassword(SetNewPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        string normalizedEmail = model.Email.Trim().ToLower();
        string resetTokenKey = $"ResetAllowed_{normalizedEmail}";

        if (!_cache.TryGetValue(resetTokenKey, out string? cachedToken) || cachedToken != model.Token)
        {
            TempData["ErrorMessage"] = "Password reset session has expired. Please request a new OTP.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var user = await _context.UserAccounts.FirstOrDefaultAsync(u => u.email.ToLower() == normalizedEmail);
        if (user == null)
        {
            TempData["ErrorMessage"] = "User account not found.";
            return RedirectToAction(nameof(Login));
        }
        user.passwordHash = BCrypt.Net.BCrypt.HashPassword(model.NewPassword);
        await _context.SaveChangesAsync();
        _cache.Remove(resetTokenKey);

        TempData["SuccessMessage"] = "Your password has been reset successfully! You can now sign in with your new password.";
        return RedirectToAction(nameof(Login));
    }
}

