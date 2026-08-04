using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Word.Models;

namespace Regira.Office.Word.Abstractions;

/// <summary>
/// Converts a document to a collection of images (1 image per page)
/// </summary>
public interface IWordToImagesService
{
    Task<IEnumerable<IImageFile>> ToImages(WordTemplateInput input, CancellationToken cancellationToken = default);
}