using Microsoft.AspNetCore.Mvc.Formatters;

namespace Regira.Web.Formatters;

/*
// https://peterdaugaardrasmussen.com/2020/02/29/asp-net-core-how-to-make-a-controller-endpoint-that-accepts-text-plain/
Usage:
Startup
    services
        .AddControllers(o =>
        {
            o.InputFormatters.Insert(o.InputFormatters.Count, new TextPlainInputFormatter());
        })
Controller
    public IActionResult Parse([FromBody] string input)
*/
public class TextPlainInputFormatter : InputFormatter
{
    private const string ContentType = "text/plain";
    public TextPlainInputFormatter()
    {
        SupportedMediaTypes.Add(ContentType);
    }


    public override async Task<InputFormatterResult> ReadRequestBodyAsync(InputFormatterContext context)
    {
        var request = context.HttpContext.Request;
        using var reader = new StreamReader(request.Body);
        var content = await reader.ReadToEndAsync();
        return await InputFormatterResult.SuccessAsync(content);
    }

    protected override bool CanReadType(Type type)
    {
        return type == typeof(string);
    }
}