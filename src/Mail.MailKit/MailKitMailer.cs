using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Exceptions;
using Regira.Office.Mail.MailKit.Extensions;
using Regira.Office.Mail.Models;

namespace Regira.Office.Mail.MailKit;

/// <summary>
/// Sends through an SMTP server with MailKit. Each send opens its own connection: connect, authenticate when
/// <see cref="MailKitConfig.UserName"/> is set, send, quit.
/// </summary>
public class MailKitMailer(MailKitConfig config) : MailerBase
{
    protected override async Task<IMailResponse> OnSend(IMessageObject message, CancellationToken cancellationToken = default)
    {
        var mail = message.ToMimeMessage();

        using var client = new SmtpClient();
        client.Timeout = config.Timeout;
        string reply;
        try
        {
            await client.ConnectAsync(config.Host, config.Port, config.Security, cancellationToken);
            if (!string.IsNullOrEmpty(config.UserName))
            {
                await client.AuthenticateAsync(config.UserName, config.Password ?? string.Empty, cancellationToken);
            }
            // One refused recipient aborts the whole message: nothing is delivered, and the exception names the address
            reply = await client.SendAsync(mail, cancellationToken);
        }
        catch (SmtpCommandException ex)
        {
            throw new MailException($"Sending message failed. Status: {ex.StatusCode}", ex)
            {
                MessageObject = message,
                ResponseContent = ex.Mailbox != null ? $"{ex.Message} ({ex.Mailbox.Address})" : ex.Message
            };
        }
        catch (Exception ex) when (ex is SmtpProtocolException or AuthenticationException or ServiceNotAuthenticatedException)
        {
            // The server refused the session: the credentials, or the protocol exchange
            throw new MailException($"Sending message failed. {ex.GetType().Name}", ex)
            {
                MessageObject = message,
                ResponseContent = ex.Message
            };
        }

        try
        {
            await client.DisconnectAsync(true, CancellationToken.None);
        }
        catch (Exception)
        {
            // The server has accepted the message; a failed QUIT must not report it as unsent
        }

        return new MailResponse
        {
            Success = true,
            Status = "OK",
            Content = reply
        };
    }
}
