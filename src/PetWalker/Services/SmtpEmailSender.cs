using System.Net;
using System.Net.Mail;

namespace PetWalker.Services;

public class EmailOptions
{
    public const string Section = "Email";

    // SMTP server. For SendGrid: smtp.sendgrid.net, port 587, user name "apikey", password = the API key.
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "";
    public string FromName { get; set; } = "PetWalker";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(From);
}

// Sends real email over SMTP (SendGrid, Gmail, Brevo, ...). Used when the "Email" settings are filled in.
public class SmtpEmailSender(EmailOptions options, ILogger<SmtpEmailSender> logger) : IEmailSender
{
    public async Task SendAsync(string to, string subject, string body)
    {
        using var client = new SmtpClient(options.Host, options.Port)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(options.UserName, options.Password),
        };
        using var message = new MailMessage(new MailAddress(options.From, options.FromName), new MailAddress(to))
        {
            Subject = subject,
            Body = body,
        };

        try
        {
            await client.SendMailAsync(message);
        }
        catch (SmtpException ex)
        {
            // Don't break the page for the user; the failure is in the log for us.
            logger.LogError(ex, "Email to {To} could not be sent", to);
        }
    }
}
