using Regira.IO.Abstractions;
using Regira.Office.Models;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Abstractions;

public interface IWordConverter
{
    Task<IMemoryFile> Convert(WordTemplateInput input, FileFormat format, CancellationToken cancellationToken = default);
    Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default);
}