using System.Runtime.CompilerServices;
using Regira.Entities.Attachments.Abstractions;
using Regira.IO.Extensions;

namespace Regira.Entities.EFcore.Attachments;

/// <summary>
/// The content an attachment holds that a file already holds, with the path of that file: the bytes
/// <see cref="AttachmentProcessor{TAttachment, TKey}"/> loaded, or what <see cref="AttachmentPrimer{TAttachment, TKey}"/>
/// stored. Bytes or a stream given in its place are new content; the same instance is not while the attachment names
/// that file, so a save of a metadata edit leaves the file where it is, and a retry of a failed save saves the file it
/// wrote. An attachment that names another path again, reloaded after a failed save, holds new content.
/// </summary>
internal static class StoredContent
{
    private sealed record HeldFile(object Content, string? Path);

    private static readonly ConditionalWeakTable<IAttachment, HeldFile> Held = new();

    public static void Mark(IAttachment item)
    {
        if (ContentOf(item) is { } content)
        {
            Held.AddOrUpdate(item, new HeldFile(content, item.Path));
        }
        else
        {
            Held.Remove(item);
        }
    }

    /// <summary>Whether the attachment holds content the file it names does not have yet.</summary>
    public static bool HasNew(IAttachment item)
        => item.HasContent() && !(Held.TryGetValue(item, out var held) && ReferenceEquals(held.Content, ContentOf(item)) && held.Path == item.Path);

    // what SaveFile stores: the stream, else the bytes
    private static object? ContentOf(IAttachment item) => item.HasStream() ? item.Stream : item.Bytes;
}
