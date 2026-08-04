using Regira.Invoicing.Billit.Models.Results.Abstractions;

namespace Regira.Invoicing.Billit.Models.Results;

public record FailedApiResult : IFailedApiResult
{
    public bool IsSuccess => false;
    public int StatusCode { get; set; }
    public Exception? Error { get; set; }
    public string? ErrorResponse { get; set; }
}