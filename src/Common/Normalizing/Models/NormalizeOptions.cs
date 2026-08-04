namespace Regira.Normalizing.Models;

public enum TextTransform
{
    NoChanges,
    ToUpperCase,
    ToLowerCase,
}

public class NormalizeOptions
{
    /// <summary>
    /// Chars that should be replaced by a space<br />
    /// Some chars might need escaping (e.g. \-) as in Regular Expressions
    /// </summary>
    public string CharsToSpace = @"\-,!;&'";
    public TextTransform Transform { get; set; } = TextTransform.NoChanges;
    public bool RemoveDiacritics { get; set; } = true;
}