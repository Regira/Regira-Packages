using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Regira.Office.Word.Packaging;
using Spire.Doc;

namespace Regira.Office.Word.Spire.Internal;

/// <summary>
/// Word writes a text box twice: as DrawingML in an <c>mc:Choice</c>, and as a VML copy in the <c>mc:Fallback</c>
/// beside it, for readers that do not know DrawingML shapes — plain Open XML text extraction among them. Spire
/// loads the fallback into a copy of its own that nothing in its document model reaches, and writes that copy back
/// as it was read, so parameters, conditional blocks and nested documents would leave the template's tags in it.
/// Rewriting each fallback text box from its DrawingML copy in the written package keeps the two the same, as Word
/// writes them.
/// </summary>
internal static class TextBoxFallbacks
{
    private static readonly XNamespace Mc = "http://schemas.openxmlformats.org/markup-compatibility/2006";
    private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    /// <summary>Whether Spire writes the format as an Open XML package: Docx, Dotx, Docm or Dotm, in any version.</summary>
    public static bool InPackage(FileFormat format)
        => format is FileFormat.Docx or FileFormat.Docx2010 or FileFormat.Docx2013 or FileFormat.Docx2016 or FileFormat.Docx2019
            or FileFormat.Dotx or FileFormat.Dotx2010 or FileFormat.Dotx2013 or FileFormat.Dotx2016 or FileFormat.Dotx2019
            or FileFormat.Docm or FileFormat.Docm2010 or FileFormat.Docm2013 or FileFormat.Docm2016 or FileFormat.Docm2019
            or FileFormat.Dotm or FileFormat.Dotm2010 or FileFormat.Dotm2013 or FileFormat.Dotm2016 or FileFormat.Dotm2019;

    /// <summary>
    /// Rewrites the fallback copy of every text box in the package's stories from its DrawingML copy, and gives each
    /// VML shape an id of its own (<see cref="VmlShapeIds"/>): Spire writes a copied shape — a template loop's — under
    /// its original's. A part whose copies already match and whose ids are unique is left as it is.
    /// </summary>
    /// <param name="package">The package as Spire wrote it; rewritten in place</param>
    public static void Synchronize(Stream package)
    {
        var rewritten = new Dictionary<string, XDocument>();
        package.Position = 0;
        using (var zip = new ZipArchive(package, ZipArchiveMode.Read, leaveOpen: true))
        {
            // no other part's copies can differ
            var stories = WordPackageStories.Of(zip);
            foreach (var entry in zip.Entries.Where(entry => stories.Contains(entry.FullName)))
            {
                string xml;
                using (var reader = new StreamReader(entry.Open()))
                {
                    xml = reader.ReadToEnd();
                }
                var hasFallbacks = xml.Contains("txbxContent") && xml.Contains("AlternateContent");
                var repeatsVml = VmlShapeIds.HasRepeated(xml);
                if (!hasFallbacks && !repeatsVml)
                {
                    continue;
                }

                var part = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
                // the fallbacks first: a rewritten one holds what its DrawingML copy does, ids included
                var synchronized = hasFallbacks && Synchronize(part);
                if (VmlShapeIds.MakeUnique(part) | synchronized)
                {
                    rewritten[entry.FullName] = part;
                }
            }
        }

        if (rewritten.Count > 0)
        {
            package.Position = 0;
            using var zip = new ZipArchive(package, ZipArchiveMode.Update, leaveOpen: true);
            foreach (var (name, part) in rewritten)
            {
                WordPackageStories.Save(zip.GetEntry(name)!, part);
            }
        }
        package.Position = 0;
    }

    private static bool Synchronize(XDocument part)
    {
        var changed = false;
        // innermost first, so a box's content is copied once whatever it holds is
        foreach (var alternate in part.Descendants(Mc + "AlternateContent").Reverse().ToArray())
        {
            var fallback = alternate.Element(Mc + "Fallback");
            var choice = alternate.Elements(Mc + "Choice").FirstOrDefault(copy => TextBoxes(copy, alternate).Any());
            if (fallback == null || choice == null)
            {
                continue;
            }

            var sources = TextBoxes(choice, alternate).ToArray();
            var targets = TextBoxes(fallback, alternate).ToArray();
            // a group holds its boxes in the same order in both copies; a fallback shaped otherwise is not a copy
            if (sources.Length != targets.Length)
            {
                continue;
            }

            for (var i = 0; i < sources.Length; i++)
            {
                if (!XNode.DeepEquals(sources[i], targets[i]))
                {
                    targets[i].ReplaceNodes(sources[i].Nodes());
                    changed = true;
                }
            }
        }
        return changed;
    }

    /// <summary>The text boxes of one copy of <paramref name="alternate"/>, leaving out those of an alternate content inside it.</summary>
    private static IEnumerable<XElement> TextBoxes(XElement copy, XElement alternate)
        => copy.Descendants(W + "txbxContent").Where(box => box.Ancestors(Mc + "AlternateContent").First() == alternate);
}
