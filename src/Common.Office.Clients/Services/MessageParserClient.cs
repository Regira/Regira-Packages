using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Clients.Abstractions;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;

namespace Regira.Office.Clients.Services;

public class MessageParserClient(HttpClient client) : OfficeClientBase(client), IMessageParser
{
    private const string MsgPath = "mail/msg";
    private const string EmlPath = "mail/eml";

    public async Task<IMessageObject> Parse(IMemoryFile file, CancellationToken cancellationToken = default)
    {
        var path = IsEml(file) ? EmlPath : MsgPath;
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(file.GetBytes() ?? throw new ArgumentException("File has no content.", nameof(file))), "file", "message");
        return await PostMultipartAsync<MessageObject>(path, content, cancellationToken) ?? new MessageObject();
    }

    private static bool IsEml(IMemoryFile file)
        => file.ContentType is "message/rfc822"
           || (file is INamedFile namedFile && namedFile.FileName?.EndsWith(".eml", StringComparison.OrdinalIgnoreCase) == true);
}
