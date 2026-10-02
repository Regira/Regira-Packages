using System.Runtime.CompilerServices;
using Regira.Entities.Attachments.Abstractions;
using Regira.IO.Extensions;

namespace Regira.Entities.EFcore.Attachments;

/// <summary>
/// The content an attachment holds that is already its stored file: the bytes <see cref="AttachmentProcessor{TAttachment, TKey}"/>
/// loaded, or what <see cref="AttachmentPrimer{TAttachment, TKey}"/> stored. Bytes or a stream given in its place are new
/// content; the same instance is not, so a save of a metadata edit leaves the file where it is.
/// </summary>
internal static class StoredContent
{
    private static readonly ConditionalWeakTable<IAttachment, object> Held = new();

    public static void Mark(IAttachment item)
    {
        if (ContentOf(item) is { } content)
        {
            Held.AddOrUpdate(item, content);
        }
        else
        {
            Held.Remove(item);
        }
    }

    public static void Unmark(IAttachment item) => Held.Remove(item);

    /// <summary>Whether the attachment holds content its stored file does not have yet.</summary>
    public static bool HasNew(IAttachment item)
        => item.HasContent() && !(Held.TryGetValue(item, out var held) && ReferenceEquals(held, ContentOf(item)));

    // what SaveFile stores: the stream, else the bytes
    private static object? ContentOf(IAttachment item) => item.HasStream() ? item.Stream : item.Bytes;
}
