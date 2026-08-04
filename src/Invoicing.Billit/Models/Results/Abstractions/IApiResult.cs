namespace Regira.Invoicing.Billit.Models.Results.Abstractions;

public interface IApiResult
{
    bool IsSuccess { get; }
    int StatusCode { get; set; }
}