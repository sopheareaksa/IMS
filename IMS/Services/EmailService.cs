using System.Net;
using System.Net.Mail;

namespace IMS.Services;

public interface IEmailService
{
    Task SendOtpEmailAsync(string toEmail, string otp);
}

public class EmailService : IEmailService
{
    private readonly IConfiguration _config;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IConfiguration config, ILogger<EmailService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task SendOtpEmailAsync(string toEmail, string otp)
    {
        var smtpServer = _config["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
        var portStr = _config["EmailSettings:Port"] ?? "587";
        var port = int.TryParse(portStr, out int p) ? p : 587;
        var senderEmail = _config["EmailSettings:Email"] ?? _config["EmailSettings:SenderEmail"]!;
        var appPassword = (_config["EmailSettings:AppPassword"] ?? _config["EmailSettings:Password"] ?? "").Replace(" ", "");
        var senderName = _config["EmailSettings:SenderName"] ?? "Insurance Management System";

        using var client = new SmtpClient(smtpServer, port)
        {
            Credentials = new NetworkCredential(senderEmail, appPassword),
            EnableSsl = true
        };

        var mailMessage = new MailMessage
        {
            From = new MailAddress(senderEmail, senderName),
            Subject = "Password Reset OTP Code - Insurance Management System",
            Body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 500px; margin: auto; padding: 25px; border: 1px solid #e0e0e0; border-radius: 12px; background-color: #ffffff;'>
                    <div style='text-align: center; margin-bottom: 20px;'>
                        <h2 style='color: #0d6efd; margin: 0;'>Insurance Management System</h2>
                        <p style='color: #6c757d; font-size: 14px;'>Password Reset Request</p>
                    </div>
                    <p style='font-size: 15px; color: #333333;'>Hello,</p>
                    <p style='font-size: 14px; color: #555555;'>We received a request to reset your password. Use the following One-Time Password (OTP) to proceed:</p>
                    <div style='text-align: center; margin: 30px 0;'>
                        <span style='display: inline-block; font-size: 32px; font-weight: bold; letter-spacing: 8px; color: #0d6efd; background-color: #f0f7ff; padding: 12px 24px; border-radius: 8px; border: 1px dashed #0d6efd;'>{otp}</span>
                    </div>
                    <p style='font-size: 13px; color: #dc3545;'><strong>Note:</strong> This OTP is valid for <strong>5 minutes</strong> only.</p>
                    <p style='font-size: 13px; color: #777777;'>If you did not request this password reset, please ignore this email and your password will remain unchanged.</p>
                    <hr style='border: none; border-top: 1px solid #eeeeee; margin: 25px 0;' />
                    <p style='font-size: 12px; color: #aaaaaa; text-align: center;'>&copy; 2026 Insurance Management System. All rights reserved.</p>
                </div>",
            IsBodyHtml = true
        };
        mailMessage.To.Add(toEmail);

        _logger.LogInformation("Sending OTP email to {Email}", toEmail);
        await client.SendMailAsync(mailMessage);
    }
}

