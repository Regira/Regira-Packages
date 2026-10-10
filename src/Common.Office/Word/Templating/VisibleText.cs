using System.Text;

namespace Regira.Office.Word.Templating;

/// <summary>
/// A paragraph's visible text, collected as a backend walks the paragraph's inline content in order — the text
/// <see cref="TemplateBlocks"/> reads. Word shows neither a field's code nor a deleted revision, so neither counts:
/// a marker there is not a block, and a key edited under track changes reads as edited. A field's result stays.
/// The Open XML backends get the same text by reading only a paragraph's <c>w:t</c> elements.
/// </summary>
internal sealed class VisibleText
{
    private readonly StringBuilder _text = new();
    // one entry per open field: true while its code is read, false once its result is
    private readonly Stack<bool> _fields = new();

    public void FieldStart() => _fields.Push(true);

    public void FieldSeparator()
    {
        if (_fields.Count > 0)
        {
            _fields.Pop();
            _fields.Push(false);
        }
    }

    public void FieldEnd()
    {
        if (_fields.Count > 0)
        {
            _fields.Pop();
        }
    }

    /// <param name="text">A run's text.</param>
    /// <param name="isDeleted">Whether the run is a deleted revision.</param>
    public void Append(string? text, bool isDeleted)
    {
        if (!isDeleted && !_fields.Contains(true))
        {
            _text.Append(text);
        }
    }

    public override string ToString() => _text.ToString();
}
