using System.IO.Compression;
using System.Xml.Linq;
using Office.Word.testing.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Word.Spire.Internal;

namespace Office.Word.testing;

/// <summary>
/// Word.Spire rewrites a part whose text boxes' fallback copies fell behind their DrawingML copies
/// (<see cref="TextBoxFallbacks"/>); the rest of the part stays as Spire wrote it.
/// </summary>
[TestFixture]
public class TextBoxFallbacksTests
{
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>
    /// A carriage return in a part's text, which XML holds only as a character reference — a written one reads as a
    /// line feed — is one still.
    /// </summary>
    [Test]
    public void A_Rewritten_Part_Keeps_A_Carriage_Return()
    {
        // the DrawingML copy filled in and the fallback copy not, as Spire writes a filled template
        using var package = Package(Docx.Document([Docx.TextBoxWithFallback("Name"), Docx.Paragraph("CR")]).GetBytes()!,
            xml => ReplaceFirst(xml, ">Name<", ">Filled<").Replace(">CR<", ">a&#xD;b<"));

        TextBoxFallbacks.Synchronize(package);

        var texts = Part(package).Descendants(W + "t").Select(text => text.Value).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(texts, Has.Exactly(2).EqualTo("Filled"), "both copies of the text box");
            Assert.That(texts, Does.Contain("a\rb"));
        });
    }

    private static MemoryStream Package(byte[] docx, Func<string, string> edit)
    {
        var package = new MemoryStream();
        package.Write(docx);
        using (var zip = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = zip.GetEntry("word/document.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open()))
            {
                xml = reader.ReadToEnd();
            }
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            writer.Write(edit(xml));
        }
        return package;
    }

    private static XDocument Part(Stream package)
    {
        package.Position = 0;
        using var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true);
        using var stream = zip.GetEntry("word/document.xml")!.Open();
        return XDocument.Load(stream);
    }

    private static string ReplaceFirst(string text, string value, string replacement)
    {
        var index = text.IndexOf(value, StringComparison.Ordinal);
        return index < 0 ? text : text[..index] + replacement + text[(index + value.Length)..];
    }
}
