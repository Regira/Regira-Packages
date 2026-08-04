using Regira.Office.Mail.Models;

namespace Regira.Security.Authentication.Web.Mail;

public class IdentityMailerOptions
{
    public MailAddress Sender { get; set; } = null!;
}
