using Microsoft.AspNetCore.Identity.UI.Services;

namespace Web.Security.Testing.Infrastructure.Identity;

// Stand-in for IEmailSender that records messages instead of sending them.
// Avoids pulling a real IMailService (Common.Office) into the test host.
public class FakeEmailSender(SentEmailStore store) : IEmailSender
{
    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        store.Add(new SentEmail(email, subject, htmlMessage));
        return Task.CompletedTask;
    }
}
