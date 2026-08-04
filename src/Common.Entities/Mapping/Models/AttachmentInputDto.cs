using System.ComponentModel.DataAnnotations;

namespace Regira.Entities.Mapping.Models;

public record AttachmentInputDto : AttachmentInputDto<int>;
public record AttachmentInputDto<TKey>
{
    public TKey Id { get; set; } = default!;
    [MaxLength(256)]
    public string? FileName { get; set; }
    [MaxLength(128)]
    public string? ContentType { get; set; }
    public byte[]? Bytes { get; set; }
}