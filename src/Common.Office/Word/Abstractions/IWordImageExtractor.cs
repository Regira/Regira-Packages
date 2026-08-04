using Regira.Office.Word.Models;

namespace Regira.Office.Word.Abstractions;

public interface IWordImageExtractor
{
    Task<IEnumerable<WordImage>> GetImages(WordTemplateInput input, CancellationToken cancellationToken = default);
}