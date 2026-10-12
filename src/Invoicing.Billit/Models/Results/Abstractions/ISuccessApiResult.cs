namespace Regira.Invoicing.Billit.Models.Results.Abstractions;

[Obsolete("Nothing in Regira.Invoicing.Billit reads or writes it. Will be removed in v7.")]
public interface ISuccessApiResult : IApiResult;

[Obsolete("Nothing in Regira.Invoicing.Billit reads or writes it. Will be removed in v7.")]
public interface ISuccessApiResponse<T> : ISuccessApiResult
{
    T Result { get; set; }
}