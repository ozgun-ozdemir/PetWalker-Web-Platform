namespace PetWalker.Services;

public interface IEmailSender
{
    Task SendAsync(string to, string subject, string body);
}

// Development stand-in: writes the email to the log instead of sending it.
// Replace with a real SMTP / SendGrid implementation before going live.
public class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender
{
    public Task SendAsync(string to, string subject, string body)
    {
        logger.LogWarning("EMAIL to {To} | {Subject}\n{Body}", to, subject, body);
        return Task.CompletedTask;
    }
}
