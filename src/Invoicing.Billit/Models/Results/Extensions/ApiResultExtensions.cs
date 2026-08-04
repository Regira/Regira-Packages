using Regira.Invoicing.Billit.Models.Results.Abstractions;

namespace Regira.Invoicing.Billit.Models.Results.Extensions;

public static class ApiExceptionExtensions
{
    extension(HttpResponseMessage response)
    {
        public T ToApiResult<T>()
            where T : IApiResult, new()
            => new() { StatusCode = (int)response.StatusCode };
        public DefaultApiResult ToApiResult() => response.ToApiResult<DefaultApiResult>();

        public async Task<T> ToFailedApiResult<T>(Exception ex)
            where T : IFailedApiResult, new()
            => new() { StatusCode = (int)response.StatusCode, Error = ex, ErrorResponse = await response.Content.ReadAsStringAsync() };
        public async Task<FailedApiResult> ToFailedApiResult(Exception ex) => await response.ToFailedApiResult<FailedApiResult>(ex);
    }

    public static async Task ThrowWhenError(this HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync();

            throw new HttpRequestException(HttpRequestError.Unknown, content, null, response.StatusCode);
        }
    }
}