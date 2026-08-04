using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Utilities;
using SendGrid.Helpers.Mail;

namespace Regira.Office.Mail.SendGrid.Extensions;

internal static class BinaryFileExtensions
{
    internal static Attachment ToAttachment(this INamedFile file)
    {
        var bytes = file.GetBytes()!;
        var base64Content = FileUtility.GetBase64String(bytes);

        return new Attachment
        {
            Filename = file.FileName,
            Type = file.ContentType ?? ContentTypeUtility.GetContentType(file.FileName!),
            Disposition = "attachment",
            Content = base64Content
        };
    }
}