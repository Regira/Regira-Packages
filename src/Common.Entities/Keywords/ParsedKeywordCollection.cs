namespace Regira.Entities.Keywords;

public class ParsedKeywordCollection(IEnumerable<QKeyword> items, string? input) : List<QKeyword>(items)
{
    public string? Input { get; } = input;
}