using Regira.IO.Abstractions;
using Regira.Office.PDF.Models;

namespace Regira.Office.PDF.Abstractions;

public interface IHtmlToPdfService
{
    Task<IMemoryFile> Create(HtmlInput template, CancellationToken cancellationToken = default);
}