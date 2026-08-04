using Regira.IO.Abstractions;

namespace Regira.Office.Mail.Abstractions;

public interface IMailService
{
    Task<IMailResponse> Send(IMailAddress sender, IEnumerable<IMailRecipient> recipients, string? subject, string? message, bool isHtml = true, IEnumerable<INamedFile>? attachments = null, CancellationToken cancellationToken = default);
    Task<IMailResponse> Send(IMessageObject message, CancellationToken cancellationToken = default);
}