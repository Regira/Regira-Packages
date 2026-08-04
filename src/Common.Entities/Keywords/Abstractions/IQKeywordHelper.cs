namespace Regira.Entities.Keywords.Abstractions;

public interface IQKeywordHelper
{
    ParsedKeywordCollection Parse(string? input);
    QKeyword ParseKeyword(string? input);
}