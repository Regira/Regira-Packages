using Regira.IO.Abstractions;
using Regira.Office.PDF.Models;

namespace Regira.Office.PDF.Abstractions;

/// <summary>
/// Converts an office document — a Word document, a spreadsheet or a presentation — to PDF.
/// <para>
/// Which source formats a backend reads, and which page settings of <see cref="DocumentInput"/> it can apply to
/// each, differ per backend. A document or a setting the backend cannot handle throws
/// <see cref="NotSupportedException"/>; a setting is never ignored silently.
/// </para>
/// </summary>
public interface IDocumentToPdfService
{
    Task<IMemoryFile> Create(DocumentInput input, CancellationToken cancellationToken = default);
}
