namespace Regira.Invoicing.Billit.Models.Results.Abstractions;

public interface ISuccessApiResult : IApiResult;

public interface ISuccessApiResponse<T> : ISuccessApiResult
{
    T Result { get; set; }
}