using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;

namespace Regira.Office.Mail.Services;

/// <summary>
/// Mailer that does not really send an email, but just returns an empty MailResponse
/// </summary>
public class DummyMailer : MailerBase
{
    protected override Task<IMailResponse> OnSend(IMessageObject message, CancellationToken cancellationToken = default)
    {
        return Task.FromResult((IMailResponse)new MailResponse());
    }
}