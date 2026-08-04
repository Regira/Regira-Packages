namespace Regira.Entities.Attachments.Abstractions;

public interface IFileIdentifierGenerator
{
    Task<string> Generate(IEntityAttachment entity, CancellationToken token = default);
}