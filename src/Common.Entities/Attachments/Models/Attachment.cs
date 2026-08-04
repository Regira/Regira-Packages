using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Attachments.Models;

public class Attachment : Attachment<int>, IEntityWithSerial;
public class Attachment<TKey> : IAttachment<TKey>
{
    public TKey Id { get; set; } = default!;

    /// <summary>
    /// Client's filename (public)
    /// </summary>
    [MaxLength(256)]
    public string? FileName { get; set; }
    [MaxLength(128)]
    public string? ContentType { get; set; }
    public long Length { get; set; }
    /// <summary>
    /// Internal storage path
    /// </summary>
    [MaxLength(1024)]
    public string? Path { get; set; }


    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }


    [NotMapped]
    public string? Identifier { get; set; }
    [NotMapped]
    public string? Prefix { get; set; }
    [NotMapped]
    public byte[]? Bytes { get; set; }
    [NotMapped]
    public Stream? Stream { get; set; }

    public void Dispose()
    {
        Stream?.Dispose();
    }
}