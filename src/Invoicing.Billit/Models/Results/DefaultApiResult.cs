using Regira.Invoicing.Billit.Models.Results.Abstractions;

namespace Regira.Invoicing.Billit.Models.Results;

public record DefaultApiResult : ISuccessApiResult, IFailedApiResult
{
    public virtual bool IsSuccess { get; set; }
    public int StatusCode { get; set; }
    public Exception? Error { get; set; }
    public string? ErrorResponse { get; set; }
}