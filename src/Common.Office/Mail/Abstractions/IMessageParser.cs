using Regira.IO.Abstractions;

namespace Regira.Office.Mail.Abstractions;

public interface IMessageParser
{
    public Task<IMessageObject> Parse(IMemoryFile file, CancellationToken cancellationToken = default);
}