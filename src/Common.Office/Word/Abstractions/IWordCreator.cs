using Regira.IO.Abstractions;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Abstractions;

public interface IWordCreator
{
    Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default);
}