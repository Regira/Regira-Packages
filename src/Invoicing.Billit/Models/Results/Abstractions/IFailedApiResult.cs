namespace Regira.Invoicing.Billit.Models.Results.Abstractions;

[Obsolete("Nothing in Regira.Invoicing.Billit reads or writes it. Will be removed in v7.")]
public interface IFailedApiResult : IApiResult
{
    Exception? Error { get; set; }
    public string? ErrorResponse { get; set; }
}