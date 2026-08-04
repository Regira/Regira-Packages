using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Attachments.Abstractions;


public interface IAttachmentSearchObject<TKey> : ISearchObject<TKey>, IAttachmentSearchObject;
public interface IAttachmentSearchObject
{
    string? FileName { get; set; }
    string? Extension { get; set; }
    long? MinSize { get; set; }
    long? MaxSize { get; set; }
}