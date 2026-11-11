using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using kh.gov.nbc.bakong_khqr;
using kh.gov.nbc.bakong_khqr.model;
using IMS.DTOs;
using IMS.Models;
using IMS.Services;
using IMS.ViewModels;

namespace IMS.Controllers;

[Authorize]
public class PaymentController : Controller
{
    private readonly InsuranceContext _context;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly INotificationService _notificationService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        InsuranceContext context,
        IConfiguration config,
        IHttpClientFactory httpClientFactory,
        INotificationService notificationService,
        ILogger<PaymentController> logger)
    {
        _context = context;
        _config = config;
        _httpClientFactory = httpClientFactory;
        _notificationService = notificationService;
        _logger = logger;
    }

    // =========================================================================
    // 1. GET: IssuePolicy & Generate Bakong KHQR
    // =========================================================================
    [HttpGet]
    public async Task<IActionResult> IssuePolicy(int quoteId, string currency = "USD")
    {
        var quote = await _context.Quotations
            .Include(q => q.Customer)
                .ThenInclude(c => c!.UserAccount)
            .Include(q => q.InsuranceProduct)
                .ThenInclude(p => p!.Company)
            .Include(q => q.InsuranceProduct)
                .ThenInclude(p => p!.InsuranceType)
            .Include(q => q.InsurancePolicy)
            .FirstOrDefaultAsync(q => q.quoteId == quoteId);

        if (quote == null)
        {
            TempData["ErrorMessage"] = "Quotation not found.";
            return RedirectToAction("MyQuotations", "Customer");
        }

        // If policy already issued and active
        if (quote.InsurancePolicy != null && quote.InsurancePolicy.status == "Active")
        {
            TempData["InfoMessage"] = $"Policy #{quote.InsurancePolicy.policyNumber} is already active.";
            return RedirectToAction("MyPolicies", "Customer");
        }

        // Assign handling agent if not set
        if (quote.agentId == null)
        {
            var hostOrCompanyAgent = await _context.Agents
                .FirstOrDefaultAsync(a => a.companyId == quote.InsuranceProduct!.companyId && a.status == "Active")
                ?? await _context.Agents.FirstOrDefaultAsync(a => a.companyId == 1);

            if (hostOrCompanyAgent != null)
            {
                quote.agentId = hostOrCompanyAgent.agentId;
                await _context.SaveChangesAsync();
            }
        }

        // Generate Policy & KHQR details
        string policyNumber = quote.InsurancePolicy?.policyNumber 
            ?? ("POL-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" + Random.Shared.Next(1000, 9999));
        decimal premiumAmount = quote.estimatedPremium;
        decimal defaultCoverage = quote.estimatedPremium * 50;

        string bakongAccountId = _config["Bakong:AccountId"] ?? "sopheareaksa_pheak@bkrt";
        string merchantName = _config["Bakong:MerchantName"] ?? "Sopheareaksa Pheak";
        string merchantCity = _config["Bakong:MerchantCity"] ?? "Phnom Penh";
        bool isKhr = string.Equals(currency, "KHR", StringComparison.OrdinalIgnoreCase);
        KHQRCurrency khqrCurrency = isKhr ? KHQRCurrency.KHR : KHQRCurrency.USD;

        // 15-minute expiration timestamp
        long expirationTimestamp = DateTimeOffset.UtcNow.AddMinutes(15).ToUnixTimeMilliseconds();

        string storeLabel = _config["Bakong:StoreLabel"] ?? "Insurance POS";
        string terminalLabel = "POS-T1";

        var (qrString, md5, khqrError) = GenerateKhqr(
            bakongAccountId,
            merchantName,
            merchantCity,
            premiumAmount,
            currency,
            policyNumber,
            storeLabel,
            terminalLabel,
            expirationTimestamp);

        var viewModel = new IssuePolicyPaymentViewModel
        {
            ErrorMessage = khqrError,
            QuoteId = quote.quoteId,
            InProId = quote.inProId,
            AgentId = quote.agentId,
            CustomerId = quote.customerId,
            CustomerName = quote.Customer?.fullName ?? "Valued Customer",
            CustomerPhone = quote.Customer?.phone,
            CustomerNationalId = quote.Customer?.nationalId,
            ProductName = quote.InsuranceProduct?.name ?? "Insurance Plan",
            ProductType = quote.InsuranceProduct?.InsuranceType?.name ?? "General",
            CompanyName = quote.InsuranceProduct?.Company?.name ?? "InsureHub",
            PolicyNumber = policyNumber,
            CoverageAmount = defaultCoverage,
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddYears(1),
            PremiumAmount = premiumAmount,
            Currency = isKhr ? "KHR" : "USD",
            QrCodeString = qrString,
            Md5 = md5,
            ExpirationTimestamp = expirationTimestamp,
            BakongAccountId = bakongAccountId,
            MerchantName = merchantName,
            MerchantCity = merchantCity
        };

        return View("IssuePolicy", viewModel);
    }

    // =========================================================================
    // 2. POST: Confirm Policy Payment & Check with NBC Bakong API
    // =========================================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPolicyPayment(IssuePolicyPaymentViewModel model, string? submitAction)
    {
        var quote = await _context.Quotations
            .Include(q => q.Customer)
                .ThenInclude(c => c!.UserAccount)
            .Include(q => q.InsuranceProduct)
                .ThenInclude(p => p!.Company)
            .Include(q => q.InsurancePolicy)
            .FirstOrDefaultAsync(q => q.quoteId == model.QuoteId);

        if (quote == null)
        {
            TempData["ErrorMessage"] = "Quotation reference not found.";
            return RedirectToAction("MyQuotations", "Customer");
        }

        // -------------------------------------------------------------
        // Option A: User clicked Cancel Payment
        // -------------------------------------------------------------
        if (submitAction == "cancel")
        {
            var failureModel = new PaymentResultViewModel
            {
                Success = false,
                QuoteId = model.QuoteId,
                PolicyNumber = model.PolicyNumber,
                ProductName = model.ProductName,
                CompanyName = model.CompanyName,
                CustomerName = model.CustomerName,
                PremiumAmount = model.PremiumAmount,
                Currency = model.Currency,
                Md5 = model.Md5,
                PaymentMethod = model.PaymentMethod == "FieldForm" ? "Field Form" : "Bakong KHQR",
                ErrorMessage = "Payment process was cancelled by the user. The insurance policy has not been activated."
            };
            return View("PaymentFailed", failureModel);
        }

        // -------------------------------------------------------------
        // Option B: Dev / Sandbox Simulated Payment Failed
        // -------------------------------------------------------------
        if (submitAction == "simulate_failed")
        {
            var failureModel = new PaymentResultViewModel
            {
                Success = false,
                QuoteId = model.QuoteId,
                PolicyNumber = model.PolicyNumber,
                ProductName = model.ProductName,
                CompanyName = model.CompanyName,
                CustomerName = model.CustomerName,
                PremiumAmount = model.PremiumAmount,
                Currency = model.Currency,
                Md5 = model.Md5,
                PaymentMethod = model.PaymentMethod == "FieldForm" ? "Field Form" : "Bakong KHQR",
                ErrorMessage = "Simulated Payment Failed: The transaction was declined or timed out in the payment gateway."
            };
            return View("PaymentFailed", failureModel);
        }

        // -------------------------------------------------------------
        // Option C: Payment Processing (QR or Field Form)
        // -------------------------------------------------------------
        bool isPaymentVerified = false;
        string transactionHash = string.Empty;
        string payMethodUsed = "Bakong KHQR";
        string? maskedDetails = null;

        if (submitAction == "simulate_success")
        {
            isPaymentVerified = true;
            if (model.PaymentMethod == "FieldForm")
            {
                if (model.FieldPaymentType == "Card")
                {
                    string cardBrand = string.IsNullOrWhiteSpace(model.CardType) ? "Visa" : model.CardType;
                    payMethodUsed = $"Card ({cardBrand} - Demo)";
                    maskedDetails = "•••• •••• •••• 4242";
                    transactionHash = "SIM-CARD-" + Guid.NewGuid().ToString("N")[..12].ToUpper();
                }
                else
                {
                    string bank = string.IsNullOrWhiteSpace(model.BankName) ? "ABA Bank" : model.BankName;
                    payMethodUsed = $"Bank Transfer ({bank} - Demo)";
                    maskedDetails = $"{bank} (Acc •••• 9876)";
                    transactionHash = "SIM-BANK-" + Guid.NewGuid().ToString("N")[..12].ToUpper();
                }
            }
            else
            {
                payMethodUsed = "Bakong KHQR";
                maskedDetails = model.BakongAccountId;
                transactionHash = "SIM-KHQR-" + Guid.NewGuid().ToString("N")[..16].ToUpper();
            }
        }
        else
        {
            // Process according to selected method
            if (model.PaymentMethod == "FieldForm")
            {
                if (model.FieldPaymentType == "Card")
                {
                    // Validate Card Details
                    if (string.IsNullOrWhiteSpace(model.CardHolderName))
                    {
                        ModelState.AddModelError(nameof(model.CardHolderName), "Cardholder name is required.");
                    }

                    string rawCard = (model.CardNumber ?? "").Replace(" ", "").Replace("-", "");
                    if (string.IsNullOrWhiteSpace(rawCard) || rawCard.Length < 13 || !rawCard.All(char.IsDigit))
                    {
                        ModelState.AddModelError(nameof(model.CardNumber), "A valid card number with 13-19 digits is required.");
                    }

                    if (string.IsNullOrWhiteSpace(model.CardExpiry) || !System.Text.RegularExpressions.Regex.IsMatch(model.CardExpiry.Trim(), @"^(0[1-9]|1[0-2])\/?([0-9]{2})$"))
                    {
                        ModelState.AddModelError(nameof(model.CardExpiry), "Enter a valid expiration date (MM/YY).");
                    }

                    if (string.IsNullOrWhiteSpace(model.CardCvv) || model.CardCvv.Trim().Length < 3 || !model.CardCvv.Trim().All(char.IsDigit))
                    {
                        ModelState.AddModelError(nameof(model.CardCvv), "Enter a valid 3 or 4-digit CVV / CVC code.");
                    }

                    if (!ModelState.IsValid)
                    {
                        return View("IssuePolicy", model);
                    }

                    string last4 = rawCard.Length >= 4 ? rawCard[^4..] : rawCard;
                    string cardBrand = string.IsNullOrWhiteSpace(model.CardType) ? "Credit/Debit Card" : model.CardType;
                    payMethodUsed = $"Card ({cardBrand} ending {last4})";
                    maskedDetails = $"•••• •••• •••• {last4}";
                    transactionHash = "CARD-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Random.Shared.Next(1000, 9999);
                    isPaymentVerified = true;
                }
                else // Bank Transfer
                {
                    if (string.IsNullOrWhiteSpace(model.BankName))
                    {
                        ModelState.AddModelError(nameof(model.BankName), "Please select your bank.");
                    }

                    if (string.IsNullOrWhiteSpace(model.BankAccountNumber))
                    {
                        ModelState.AddModelError(nameof(model.BankAccountNumber), "Bank account or phone number is required.");
                    }

                    if (string.IsNullOrWhiteSpace(model.BankAccountHolder))
                    {
                        ModelState.AddModelError(nameof(model.BankAccountHolder), "Account holder name is required.");
                    }

                    if (!ModelState.IsValid)
                    {
                        return View("IssuePolicy", model);
                    }

                    string rawAcc = (model.BankAccountNumber ?? "").Trim();
                    string last4 = rawAcc.Length >= 4 ? rawAcc[^4..] : rawAcc;
                    payMethodUsed = $"Bank Transfer ({model.BankName} - {model.BankAccountHolder})";
                    maskedDetails = $"{model.BankName} (Acc •••• {last4})";
                    transactionHash = string.IsNullOrWhiteSpace(model.BankTransactionRef)
                        ? ("BANK-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss") + "-" + Random.Shared.Next(1000, 9999))
                        : model.BankTransactionRef.Trim();
                    isPaymentVerified = true;
                }
            }
            else
            {
                // Bakong KHQR verification
                payMethodUsed = "Bakong KHQR";
                maskedDetails = model.BakongAccountId;

                try
                {
                    string apiUrl = _config["Bakong:BAKONG_API_URL"] ?? "https://api-bakong.nbc.gov.kh";
                    string apiToken = _config["Bakong:BAKONG_API_TOKEN"] ?? string.Empty;

                    var httpClient = _httpClientFactory.CreateClient();
                    httpClient.Timeout = TimeSpan.FromSeconds(10);

                    if (!string.IsNullOrWhiteSpace(apiToken))
                    {
                        httpClient.DefaultRequestHeaders.Authorization =
                            new AuthenticationHeaderValue("Bearer", apiToken);
                    }

                    var payload = JsonSerializer.Serialize(new { md5 = model.Md5 });
                    var content = new StringContent(payload, Encoding.UTF8, "application/json");

                    var httpResponse = await httpClient.PostAsync($"{apiUrl}/v1/check_transaction_by_md5", content);

                    if (httpResponse.IsSuccessStatusCode)
                    {
                        var stream = await httpResponse.Content.ReadAsStreamAsync();
                        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var result = await JsonSerializer.DeserializeAsync<BakongCheckResponse>(stream, options);

                        if (result != null && result.ResponseCode == 0)
                        {
                            isPaymentVerified = true;
                            transactionHash = result.Data?.Hash ?? model.Md5;
                        }
                        else
                        {
                            _logger.LogInformation("Bakong API returned non-zero response code: {Code}, message: {Msg}",
                                result?.ResponseCode, result?.ResponseMessage);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Bakong API call failed with HTTP status {Status}", httpResponse.StatusCode);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking Bakong payment transaction by MD5");
                }
            }
        }

        // -------------------------------------------------------------
        // If Payment is Completed: Issue Active Policy & Record Payment
        // -------------------------------------------------------------
        if (isPaymentVerified)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                quote.status = "Accepted";

                // 1. Create or update Policy
                var policy = quote.InsurancePolicy;
                if (policy == null)
                {
                    policy = new InsurancePolicy
                    {
                        quoteId = quote.quoteId,
                        agentId = model.AgentId ?? quote.agentId,
                        policyNumber = model.PolicyNumber,
                        coverageAmount = model.CoverageAmount,
                        premiumAmount = model.PremiumAmount,
                        startDate = model.StartDate,
                        endDate = model.EndDate,
                        status = "Active"
                    };
                    _context.InsurancePolicies.Add(policy);
                }
                else
                {
                    policy.policyNumber = model.PolicyNumber;
                    policy.coverageAmount = model.CoverageAmount;
                    policy.premiumAmount = model.PremiumAmount;
                    policy.startDate = model.StartDate;
                    policy.endDate = model.EndDate;
                    policy.status = "Active";
                }

                await _context.SaveChangesAsync();

                // 2. Record Beneficiary if specified
                if (!string.IsNullOrWhiteSpace(model.BeneficiaryName))
                {
                    var beneficiary = new Beneficiary
                    {
                        inPoId = policy.inPoId,
                        name = model.BeneficiaryName.Trim(),
                        relation = string.IsNullOrWhiteSpace(model.BeneficiaryRelation) ? "Primary" : model.BeneficiaryRelation.Trim(),
                        phone = model.BeneficiaryPhone?.Trim(),
                        sharePercentage = 100.00m
                    };
                    _context.Beneficiaries.Add(beneficiary);
                    await _context.SaveChangesAsync();
                }

                // 3. Record Payment row
                string finalRef = string.IsNullOrWhiteSpace(transactionHash)
                    ? ((model.PaymentMethod == "FieldForm" ? (model.FieldPaymentType == "Card" ? "CARD-" : "BANK-") : "KHQR-") + Guid.NewGuid().ToString("N")[..12].ToUpper())
                    : transactionHash;

                var payment = new Payment
                {
                    inPoId = policy.inPoId,
                    transactionRef = finalRef,
                    payDate = DateTime.UtcNow,
                    payAmount = model.PremiumAmount,
                    payMethod = payMethodUsed,
                    payStatus = "Completed"
                };
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // 4. Send Notifications
                if (quote.Customer != null)
                {
                    await _notificationService.NotifyUserAsync(
                        quote.Customer.userId,
                        "Payment Successful & Policy Activated!",
                        $"Congratulations! Your payment of ${model.PremiumAmount:N2} via {payMethodUsed} for policy #{policy.policyNumber} ({quote.InsuranceProduct?.name}) has been verified and your policy is now ACTIVE.",
                        "/Customer/MyPolicies",
                        "PaymentSuccess"
                    );
                }

                if (quote.agentId != null)
                {
                    var agent = await _context.Agents.FindAsync(quote.agentId);
                    if (agent != null)
                    {
                        await _notificationService.NotifyUserAsync(
                            agent.userId,
                            $"New Policy Activated via {payMethodUsed}",
                            $"Policy #{policy.policyNumber} has been paid and activated for customer '{quote.Customer?.fullName}'.",
                            "/Agent/Policies",
                            "PolicyIssued"
                        );
                    }
                }

                var successModel = new PaymentResultViewModel
                {
                    Success = true,
                    QuoteId = quote.quoteId,
                    PolicyId = policy.inPoId,
                    PolicyNumber = policy.policyNumber,
                    ProductName = quote.InsuranceProduct?.name ?? "Insurance Plan",
                    CompanyName = quote.InsuranceProduct?.Company?.name ?? "InsureHub",
                    CustomerName = quote.Customer?.fullName ?? "Valued Customer",
                    PremiumAmount = model.PremiumAmount,
                    Currency = model.Currency,
                    CoverageAmount = model.CoverageAmount,
                    StartDate = model.StartDate,
                    EndDate = model.EndDate,
                    TransactionRef = payment.transactionRef,
                    PaymentDate = payment.payDate,
                    BeneficiaryName = model.BeneficiaryName,
                    BeneficiaryRelation = model.BeneficiaryRelation,
                    PaymentMethod = payMethodUsed,
                    MaskedAccountOrCard = maskedDetails
                };

                return View("PaymentSuccess", successModel);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error activating policy after payment verification");

                var errorModel = new PaymentResultViewModel
                {
                    Success = false,
                    QuoteId = model.QuoteId,
                    PolicyNumber = model.PolicyNumber,
                    ProductName = model.ProductName,
                    CompanyName = model.CompanyName,
                    CustomerName = model.CustomerName,
                    PremiumAmount = model.PremiumAmount,
                    Currency = model.Currency,
                    Md5 = model.Md5,
                    PaymentMethod = payMethodUsed,
                    ErrorMessage = $"Internal system error saving active policy: {ex.Message}"
                };
                return View("PaymentFailed", errorModel);
            }
        }

        // -------------------------------------------------------------
        // Payment NOT Verified / Not Found in Gateway
        // -------------------------------------------------------------
        var unverifiedModel = new PaymentResultViewModel
        {
            Success = false,
            QuoteId = model.QuoteId,
            PolicyNumber = model.PolicyNumber,
            ProductName = model.ProductName,
            CompanyName = model.CompanyName,
            CustomerName = model.CustomerName,
            PremiumAmount = model.PremiumAmount,
            Currency = model.Currency,
            Md5 = model.Md5,
            PaymentMethod = model.PaymentMethod == "FieldForm" ? "Field Form" : "Bakong KHQR",
            ErrorMessage = model.PaymentMethod == "FieldForm"
                ? "Payment processing failed. Please verify your card or bank account details and try again."
                : "Bakong Payment Not Received: No completed transaction was found matching this KHQR code yet. Please scan the QR code using your Bakong or mobile banking app and approve the transfer, then try again."
        };

        return View("PaymentFailed", unverifiedModel);
    }

    // =========================================================================
    // 3. Helper: Generate KHQR (SDK with EMV Standard Fallback)
    // =========================================================================
    private (string qrString, string md5, string? error) GenerateKhqr(
        string accountId,
        string merchantName,
        string merchantCity,
        decimal amount,
        string currency,
        string billNumber,
        string storeLabel,
        string terminalLabel,
        long expirationTimestamp)
    {
        bool isKhr = string.Equals(currency, "KHR", StringComparison.OrdinalIgnoreCase);
        KHQRCurrency khqrCurrency = isKhr ? KHQRCurrency.KHR : KHQRCurrency.USD;

        // 1. Try NBC Bakong SDK
        try
        {
            var individualInfo = new IndividualInfo
            {
                BakongAccountID = accountId,
                MerchantName = merchantName,
                MerchantCity = merchantCity,
                Currency = khqrCurrency,
                Amount = (double)amount,
                BillNumber = billNumber.Length > 25 ? billNumber[..25] : billNumber,
                StoreLabel = storeLabel.Length > 25 ? storeLabel[..25] : storeLabel,
                TerminalLabel = terminalLabel.Length > 25 ? terminalLabel[..25] : terminalLabel,
                ExpirationTimestamp = expirationTimestamp
            };

            var response = BakongKHQR.GenerateIndividual(individualInfo);
            if (response?.Status?.Code == 0 && response.Data != null && !string.IsNullOrWhiteSpace(response.Data.QR))
            {
                return (response.Data.QR, response.Data.MD5 ?? string.Empty, null);
            }

            _logger.LogWarning("BakongKHQR SDK Code: {Code}, Msg: {Msg}. Generating via EMV fallback.",
                response?.Status?.Code, response?.Status?.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BakongKHQR SDK threw exception. Generating via EMV fallback.");
        }

        // 2. EMVCo KHQR Standard Compliant Fallback Generator
        try
        {
            var sb = new StringBuilder();
            sb.Append("000201"); // Payload Format Indicator
            sb.Append("010212"); // Point of Initiation: Dynamic (with amount)

            // Tag 29: Individual Account Info
            string sub29 = $"00{accountId.Length:D2}{accountId}";
            sb.Append($"29{sub29.Length:D2}{sub29}");

            sb.Append("52045999"); // Merchant Category Code
            string currCode = isKhr ? "116" : "840";
            sb.Append($"53{currCode.Length:D2}{currCode}"); // Currency: 840 (USD), 116 (KHR)

            string amtStr = isKhr ? ((long)amount).ToString() : amount.ToString("0.##");
            sb.Append($"54{amtStr.Length:D2}{amtStr}"); // Amount

            sb.Append("5802KH"); // Country Code
            sb.Append($"59{merchantName.Length:D2}{merchantName}"); // Merchant Name
            sb.Append($"60{merchantCity.Length:D2}{merchantCity}"); // Merchant City

            // Tag 62: Additional Data (Bill Number, Store Label, Terminal Label)
            var sub62 = new StringBuilder();
            if (!string.IsNullOrEmpty(billNumber))
            {
                string bNum = billNumber.Length > 25 ? billNumber[..25] : billNumber;
                sub62.Append($"01{bNum.Length:D2}{bNum}");
            }
            if (!string.IsNullOrEmpty(storeLabel))
            {
                string sLbl = storeLabel.Length > 25 ? storeLabel[..25] : storeLabel;
                sub62.Append($"03{sLbl.Length:D2}{sLbl}");
            }
            if (!string.IsNullOrEmpty(terminalLabel))
            {
                string tLbl = terminalLabel.Length > 25 ? terminalLabel[..25] : terminalLabel;
                sub62.Append($"07{tLbl.Length:D2}{tLbl}");
            }
            string sub62Str = sub62.ToString();
            sb.Append($"62{sub62Str.Length:D2}{sub62Str}");

            // Tag 99: Timestamps (00: created, 01: expiration)
            long createdTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string sub99 = $"00{createdTimestamp.ToString().Length:D2}{createdTimestamp}01{expirationTimestamp.ToString().Length:D2}{expirationTimestamp}";
            sb.Append($"99{sub99.Length:D2}{sub99}");

            // Tag 63: CRC16
            string dataToCrc = sb.ToString() + "6304";
            byte[] bytes = Encoding.UTF8.GetBytes(dataToCrc);
            string crcString = kh.gov.nbc.bakong_khqr.core.CRC16.ComputeChecksumString(bytes);
            string finalQr = dataToCrc + crcString;

            // MD5 Hash
            byte[] md5Bytes = System.Security.Cryptography.MD5.HashData(Encoding.UTF8.GetBytes(finalQr));
            string finalMd5 = Convert.ToHexString(md5Bytes).ToLowerInvariant();

            return (finalQr, finalMd5, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate KHQR EMV fallback");
            return (string.Empty, string.Empty, ex.Message);
        }
    }
}

