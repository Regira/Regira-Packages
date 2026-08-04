namespace Regira.Invoicing.Billit.Models.Results.Abstractions;

public interface IFailedApiResult : IApiResult
{
    Exception? Error { get; set; }
    public string? ErrorResponse { get; set; }
}