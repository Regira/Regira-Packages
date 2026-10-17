namespace Regira.Invoicing.Billit.Models.Results.Abstractions;

[Obsolete("Nothing in Regira.Invoicing.Billit reads or writes it. Will be removed in v7.")]
public interface IApiResult
{
    bool IsSuccess { get; }
    int StatusCode { get; set; }
}