namespace Regira.Web.HTML.Abstractions;

public interface IHtmlParser
{
    Task<string> Parse<T>(string html, T model);
}