using Regira.TreeList;
using Spire.Doc;

namespace Regira.Office.Word.Spire;

internal class DocumentTree : TreeList<DocumentObject>
{
    public Document Document { get; set; }
    public DocumentTree(Document doc)
    {
        Document = doc;
        Refresh();
    }


    public void Refresh()
    {
        Clear();

        var roots = Document.ChildObjects;
        var tree = new TreeList<DocumentObject>(roots.Count);
        foreach (DocumentObject root in roots)
        {
            AddObject(tree, root);
        }
    }
    private void AddObject(TreeList<DocumentObject> tree, DocumentObject obj, TreeNode<DocumentObject>? parent = null)
    {
        var node = tree.AddValue(obj, parent);
        foreach (var child in obj.ChildObjects.Cast<DocumentObject>())
        {
            AddObject(tree, child, node);
        }
    }
}