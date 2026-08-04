using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Regira.Web.ExceptionHandling.Abstractions;

namespace Regira.Web.ExceptionHandling;

public class GlobalExceptionHandler(ILoggerFactory loggerFactory) : IExceptionHandler
{
    public Task HandleException(HttpContext context, Exception ex)
    {
        var logger = loggerFactory.CreateLogger(GetType());
        logger.LogError(ex, ex.Message);

        return Task.CompletedTask;
    }
}