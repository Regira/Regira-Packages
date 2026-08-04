using Regira.Office.Word.Models;

namespace Regira.Office.Word.Abstractions;

public interface IWordTextExtractor
{
    Task<string> GetText(WordTemplateInput input, CancellationToken cancellationToken = default);
}