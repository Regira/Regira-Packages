using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Exceptions;
using Regira.Office.Mail.Models;
using Regira.Office.Mail.SendGrid.Extensions;
using SendGrid;

namespace Regira.Office.Mail.SendGrid;

public class SendGridMailer(SendGridConfig config) : MailerBase
{
    // every instance shares one connection pool, recycled so a change to SendGrid's DNS reaches a long-running process;
    // no cookies, so nothing one instance receives rides on another's requests
    private static readonly SocketsHttpHandler SharedHandler = new() { PooledConnectionLifetime = TimeSpan.FromMinutes(2), UseCookies = false };
    // the handler every client goes through; a test points it at a stub
    internal HttpMessageHandler Handler { get; init; } = SharedHandler;
    // the check SendGrid's client makes, at construction as before
    private readonly string _key = string.IsNullOrWhiteSpace(config.Key) ? throw new ArgumentNullException("apiKey") : config.Key;

    protected override async Task<IMailResponse> OnSend(IMessageObject message, CancellationToken cancellationToken = default)
    {
        var mail = message.ToMailMessage();
        var client = new SendGridClient(new HttpClient(Handler, disposeHandler: false), _key);
        var mailerResponse = await client.SendEmailAsync(mail, cancellationToken);

        var responseContent = await mailerResponse.Body.ReadAsStringAsync();
        if (!mailerResponse.IsSuccessStatusCode)
        {
            throw new MailException($"Sending message failed. Status: {mailerResponse.StatusCode}")
            {
                MessageObject = message,
                ResponseContent = responseContent
            };
        }

        var response = new MailResponse
        {
            Status = mailerResponse.StatusCode.ToString(),
            Content = responseContent,
            Success = mailerResponse.IsSuccessStatusCode
        };

        return response;
    }
}