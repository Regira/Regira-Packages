namespace Regira.Entities.Mapping.Models;

public record AttachmentDto : AttachmentDto<int>;
public record AttachmentDto<TKey>
{
    public TKey Id { get; set; } = default!;
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public long Length { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }
}