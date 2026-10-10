using Regira.IO.Extensions;
using Regira.IO.Utilities;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Exceptions;
using Regira.Office.Mail.Models;
using RestSharp;
using RestSharp.Authenticators;

namespace Regira.Office.Mail.MailGun;

public class MailGunMailer(MailgunConfig config) : MailerBase
{
    private readonly string _mailgunApi = config.Api;
    private readonly string _mailgunKey = config.Key;
    private readonly string _domain = config.Domain;
    private readonly bool _testMode = config.TestMode;


    protected override async Task<IMailResponse> OnSend(IMessageObject message, CancellationToken cancellationToken = default)
    {
        var options = new RestClientOptions(_mailgunApi) { Authenticator = new HttpBasicAuthenticator("api", _mailgunKey) };
        // The mailer is transient, so a client per send is the norm. useClientFactory has RestSharp keep one HttpClient
        // per API endpoint and reuse its connections, where a new HttpClient per send opened new ones every time.
        using var client = new RestClient(options, useClientFactory: true);
        var request = BuildRequest(message);

        // response
        var mailerResponse = await client.ExecuteAsync(request, cancellationToken);

        // errors?
        if (!mailerResponse.IsSuccessful)
        {
            // Mailgun describes the refusal in the response body ({"message": "..."}); without it the status
            // code alone leaves a caller with nothing to act on.
            throw new MailException($"Sending message failed. Status: {mailerResponse.StatusCode}", mailerResponse.ErrorException)
            {
                MessageObject = message,
                ResponseContent = mailerResponse.Content
            };
        }

        // result
        var responseMessage = mailerResponse.Content;
        return new MailResponse
        {
            Success = mailerResponse.IsSuccessful,
            Status = mailerResponse.StatusCode.ToString(),
            Content = responseMessage
        };
    }

    internal RestRequest BuildRequest(IMessageObject message)
    {
        var request = new RestRequest
        {
            Resource = $"{_domain}/messages",
            Method = Method.Post
        };
        // parameters
        request.AddParameter("domain", _domain, ParameterType.UrlSegment);
        request.AddParameter("from", FormatAddress(message.From!));
        foreach (var to in message.To)
        {
            string toName;
            switch (to.RecipientType)
            {
                case RecipientTypes.Cc:
                    toName = "cc";
                    break;
                case RecipientTypes.Bcc:
                    toName = "bcc";
                    break;
                default:
                    toName = "to";
                    break;
            }
            request.AddParameter(toName, FormatAddress(to));
        }
        if (!string.IsNullOrEmpty(message.ReplyTo?.Email))
        {
            request.AddParameter("h:Reply-To", FormatAddress(message.ReplyTo!));
        }
        request.AddParameter("subject", message.Subject);
        request.AddParameter(message.IsHtml ? "html" : "text", message.Body);
        if (_testMode)
        {
            // Mailgun accepts and logs the message but does not deliver it. The response is indistinguishable
            // from a real send, so callers (and the tests) still assert on Success as usual.
            request.AddParameter("o:testmode", "yes");
        }
        // attachments
        if (message.Attachments != null)
        {
            foreach (var file in message.Attachments)
            {
                var contentType = file.ContentType ?? ContentTypeUtility.GetContentType(file.FileName!);
                request.AddFile("attachment", file.GetBytes()!, file.FileName!, contentType);
            }
        }
        return request;
    }

    /// <summary>
    /// <c>Display Name &lt;email&gt;</c>, or the bare email without a display name. A name holding an RFC 5322
    /// special is quoted: Mailgun splits a recipient parameter on commas, so <c>Doe, John</c> would read as two
    /// addresses. Line breaks become spaces, keeping the name on its header line.
    /// </summary>
    internal static string FormatAddress(IMailAddress address)
    {
        var name = address.DisplayName?.ReplaceLineEndings(" ").Trim();
        if (string.IsNullOrEmpty(name))
        {
            return address.Email;
        }
        if (name.IndexOfAny(AddressSpecials) >= 0)
        {
            name = $"\"{name.Replace("\\", "\\\\").Replace("\"", "\\\"")}\"";
        }
        return $"{name} <{address.Email}>";
    }
    private static readonly char[] AddressSpecials = ['(', ')', '<', '>', '[', ']', ':', ';', '@', '\\', ',', '.', '"'];
}