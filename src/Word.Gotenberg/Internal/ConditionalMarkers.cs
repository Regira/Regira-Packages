using System.Text;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Regira.Office.Word.Templating;

namespace Regira.Office.Word.Gotenberg.Internal;

/// <summary>
/// Finds the conditional blocks of an OOXML template. A template holding one needs an <c>IWordCreator</c> even
/// without a single parameter: a condition on a key the input does not give is false, and its block has to go.
/// </summary>
internal static class ConditionalMarkers
{
    /// <summary>
    /// The most XML the scan reads, in bytes, across the document's parts. Every OOXML conversion is scanned in-process,
    /// and a document that opens a block is then created in-process, where its XML parts are loaded whole: a document
    /// holding more — a zip bomb, or one far larger than any template — stops the scan, and goes to Gotenberg as it is.
    /// </summary>
    internal const long MaxBytes = 32 * 1024 * 1024;
    /// <summary>The most text a paragraph can hold and still be read as a marker; the scan keeps no more of it.</summary>
    private const int MaxMarkerLength = 1024;

    /// <summary>
    /// Whether the body, a header or a footer holds a paragraph that opens a block — what makes a document use blocks,
    /// as the creators decide it. Nothing else does, marker text among other text included: that document converts as
    /// it is. A package that cannot be read holds none, and so does one whose parts exceed <see cref="MaxBytes"/>
    /// together: it goes to Gotenberg as it is, which reports what is wrong with it.
    /// </summary>
    public static bool Any(byte[] source)
        => Any(source, MaxBytes);

    internal static bool Any(byte[] source, long maxBytes)
    {
        try
        {
            return Find(source, maxBytes);
        }
        catch (Exception ex)
            when (ex is OpenXmlPackageException or InvalidDataException or FileFormatException or XmlException or ScanBudgetExceededException)
        {
            return false;
        }
    }

    private static bool Find(byte[] source, long maxBytes)
    {
        using var stream = new MemoryStream(source, false);
        using var doc = WordprocessingDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = maxBytes });
        var mainPart = doc.MainDocumentPart;
        if (mainPart == null)
        {
            return false;
        }

        // every part to its end, whatever the first ones held: the budget has to cover all that the creator would load
        var budget = new ScanBudget(maxBytes);
        var opensBlock = false;
        OpenXmlPart[] parts = [mainPart, .. mainPart.HeaderParts, .. mainPart.FooterParts];
        var scanned = parts.Distinct().ToArray();
        foreach (var part in scanned)
        {
            opensBlock |= PartOpensBlock(part, budget);
        }

        if (opensBlock)
        {
            // a creator other than Word.Mini loads every XML part — styles, numbering, footnotes, comments — so the
            // document it gets has to fit the budget as a whole; images are not parsed, and do not count
            foreach (var part in doc.GetAllParts().Except(scanned).Where(x => x.ContentType.EndsWith("xml", StringComparison.OrdinalIgnoreCase)))
            {
                using var partStream = new BudgetedStream(part.GetStream(FileMode.Open, FileAccess.Read), budget);
                partStream.CopyTo(Stream.Null);
            }
        }
        return opensBlock;
    }

    /// <summary>
    /// Streams <paramref name="part"/> rather than loading it, so the scan holds no document model whatever the part
    /// holds. A paragraph's text is its visible text, as the backends that resolve the blocks read it: only its
    /// <see cref="Text"/> runs, so field codes and deleted revisions do not count, and not those of a text box inside it,
    /// whose paragraphs are read on their own.
    /// </summary>
    private static bool PartOpensBlock(OpenXmlPart part, ScanBudget budget)
    {
        using var reader = new OpenXmlPartReader(new BudgetedStream(part.GetStream(FileMode.Open, FileAccess.Read), budget), part.Features,
            new OpenXmlPartReaderOptions { CloseStream = true });
        var opensBlock = false;
        // the text of each open paragraph, innermost on top: a text box's paragraphs sit inside the one that anchors it;
        // null once it is too long to be a marker
        var paragraphs = new Stack<StringBuilder?>();
        while (reader.Read())
        {
            if (reader.ElementType == typeof(Paragraph))
            {
                if (reader.IsStartElement)
                {
                    paragraphs.Push(new StringBuilder());
                }
                else if (reader.IsEndElement && paragraphs.Pop() is { } text && ConditionalBlocks.OpensBlock(text.ToString()))
                {
                    opensBlock = true;
                }
            }
            else if (reader.ElementType == typeof(Text) && reader.IsStartElement && paragraphs.TryPeek(out var text) && text != null)
            {
                var value = reader.GetText();
                if (text.Length + value.Length > MaxMarkerLength)
                {
                    paragraphs.Pop();
                    paragraphs.Push(null);
                }
                else
                {
                    text.Append(value);
                }
            }
        }
        return opensBlock;
    }

    private sealed class ScanBudget(long bytes)
    {
        public void Spend(int count)
        {
            bytes -= count;
            if (bytes < 0)
            {
                throw new ScanBudgetExceededException();
            }
        }
    }

    private sealed class ScanBudgetExceededException : Exception;

    /// <summary>A part's stream that spends <see cref="ScanBudget"/> on every byte read.</summary>
    private sealed class BudgetedStream(Stream inner, ScanBudget budget) : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = inner.Read(buffer, offset, count);
            budget.Spend(read);
            return read;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                inner.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
