using Regira.TreeList;
using Regira.Utilities;
using Spire.Doc;
using Spire.Doc.Collections;
using Spire.Doc.Documents;
using Spire.Doc.Fields;
using HeaderFooterType = Regira.Office.Word.Models.HeaderFooterType;

namespace Regira.Office.Word.Spire.Extensions;

internal static class WordDocumentExtensions
{
    public static Stream ToStream(this Document doc, FileFormat format = FileFormat.Docx)
    {
        var ms = new MemoryStream();
        doc.SaveToStream(ms, format);
        ms.Position = 0;
        return ms;
    }

    public static TreeList<DocumentObject> ToTreeList(this DocumentObject obj)
    {
        var tree = new TreeList<DocumentObject>(obj.ChildObjects.Count);
        AddObject(tree, obj);
        return tree;
    }
    private static void AddObject(TreeList<DocumentObject> tree, DocumentObject obj, TreeNode<DocumentObject>? parent = null)
    {
        var node = tree.AddValue(obj, parent);
        foreach (var child in obj.ChildObjects.Cast<DocumentObject>())
        {
            AddObject(tree, child, node);
        }
    }

    public static IEnumerable<TreeNode<DocumentObject>> WithOffspring(this IEnumerable<TreeNode<DocumentObject>> collection)
    {
        var nodes = collection.AsList();
        return nodes
            .Concat(nodes.GetOffspring())
            .DistinctBy(n => n.Value);
    }
    public static IEnumerable<DocPicture> FindAllPictures(this IEnumerable<TreeNode<DocumentObject>> collection, string? name = null)
    {
        return collection
            .WithOffspring()
            .Where(n => n.Value.DocumentObjectType == DocumentObjectType.Picture)
            .Select(n => (DocPicture)n.Value)
            .Where(p => string.IsNullOrWhiteSpace(name) || p.Title == name || p.AlternativeText == name);
    }
    public static IEnumerable<DocPicture> FindAllPictures(this TreeNode<DocumentObject> item, string? name = null)
    {
        return new[] { item }.FindAllPictures(name);
    }

    public static IEnumerable<Table> FindAllTables(this IEnumerable<TreeNode<DocumentObject>> collection)
    {
        return collection
            .WithOffspring()
            .Where(n => n.Value.DocumentObjectType == DocumentObjectType.Table)
            .Select(n => (Table)n.Value);
    }
    public static IEnumerable<Table> FindAllTables(this TreeNode<DocumentObject> item)
    {
        return new[] { item }.FindAllTables();
    }
    public static Table? FindTable(this IEnumerable<TreeNode<DocumentObject>> collection, string tableName)
    {
        // find table by title (Table properties -> Alt Text -> Title)
        return FindAllTables(collection)
            .FirstOrDefault(t => t.Title == tableName);
    }

    public static IEnumerable<Paragraph> FindAllParagraphs(this IEnumerable<TreeNode<DocumentObject>> collection)
    {
        return collection
            .WithOffspring()
            .Where(x => x.Value.DocumentObjectType == DocumentObjectType.Paragraph)
            .Select(n => (Paragraph)n.Value);
    }

    public static HeaderFooter GetHeader(this Document doc, HeaderFooterType type = HeaderFooterType.Default)
    {
        var headersFooters = doc.Sections[0].HeadersFooters;
        switch (type)
        {
            case HeaderFooterType.FirstPage:
                return headersFooters.FirstPageHeader;
            case HeaderFooterType.Even:
                return headersFooters.EvenHeader;
            case HeaderFooterType.Odd:
                return headersFooters.OddHeader;
            default:
                return headersFooters.Header;
        }
    }

    public static string GetObjectText(this IEnumerable<TreeNode<DocumentObject>> collection, DocumentObject obj)
    {
        var texts = collection
            .GetSelf(obj)
            .WithOffspring()
            .FindAllParagraphs()
            .Select(p => p.Text)
            .ToArray();
        return string.Join(Environment.NewLine, texts);
    }
    public static string GetHeaderText(this Document doc, HeaderFooterType type = HeaderFooterType.Default)
    {
        var header = doc.GetHeader(type);
        return doc.ToTreeList().GetObjectText(header);
    }
    public static HeaderFooter GetFooter(this Document doc, HeaderFooterType type = HeaderFooterType.Default)
    {
        var headersFooters = doc.Sections[0].HeadersFooters;
        switch (type)
        {
            case HeaderFooterType.FirstPage:
                return headersFooters.FirstPageFooter;
            case HeaderFooterType.Even:
                return headersFooters.EvenFooter;
            case HeaderFooterType.Odd:
                return headersFooters.OddFooter;
            default:
                return headersFooters.Footer;
        }
    }
    public static string GetFooterText(this Document doc, HeaderFooterType type = HeaderFooterType.Default)
    {
        var footer = doc.GetFooter(type);
        return doc.ToTreeList().GetObjectText(footer);
    }


    public static void ImportChildObjects(this DocumentObject target, DocumentObjectCollection childObjects)
    {
        foreach (DocumentObject child in childObjects)
        {
            target.ChildObjects.Add(child.Clone());
        }
    }
    public static void ReplaceChildObjects(this DocumentObject target, DocumentObjectCollection childObjects)
    {
        target.ChildObjects.Clear();
        ImportChildObjects(target, childObjects);
    }
    public static bool HasEmptyBody(this TreeList<DocumentObject> collection)
    {
        var body = collection.FirstOrDefault(n => n.Value is Body);
        return body?.GetOffspring()
                   .Where(n => n.Value is Paragraph)
                   .Select(n => (Paragraph)n.Value)
                   .All(p => string.IsNullOrWhiteSpace(p.Text))
               == true;
    }
}