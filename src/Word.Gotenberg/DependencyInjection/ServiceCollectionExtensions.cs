using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Regira.Office.Word.Abstractions;

namespace Regira.Office.Word.Gotenberg.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The name of the <see cref="HttpClient"/> <see cref="AddGotenbergWord"/> registers — for adding handlers
    /// or resilience to it with <c>services.AddHttpClient(HttpClientName)</c>.
    /// </summary>
    public const string HttpClientName = "Regira.Office.Word.Gotenberg";

    /// <summary>
    /// Registers <see cref="WordService"/> as <see cref="IWordConverter"/> and
    /// <see cref="IWordToImagesService"/>, both on one <see cref="HttpClient"/> pointed at the Gotenberg
    /// server.
    /// </summary>
    /// <remarks>
    /// <see cref="WordService"/> takes two optional collaborators from the container when they are
    /// registered: an <c>IPdfToImageService</c>, which <see cref="IWordToImagesService.ToImages"/> needs,
    /// and an <see cref="IWordCreator"/>, which renders template substitutions before conversion.
    /// <para>
    /// The client is named <see cref="HttpClientName"/> rather than after an interface, so another package's
    /// client for the same interface keeps its own base address and credentials. Which implementation
    /// <see cref="IWordConverter"/> resolves to is still decided by registration order: the last one wins.
    /// </para>
    /// <para>
    /// Named <c>AddGotenbergWord</c> rather than <c>AddGotenberg</c> because Gotenberg also converts HTML,
    /// spreadsheets and presentations.
    /// </para>
    /// </remarks>
    public static IServiceCollection AddGotenbergWord(this IServiceCollection services, Action<GotenbergWordConfig> configure)
    {
        var config = new GotenbergWordConfig();
        configure(config);
        if (string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            throw new ArgumentException($"{nameof(GotenbergWordConfig)}.{nameof(GotenbergWordConfig.BaseUrl)} is required.", nameof(configure));
        }

        void ConfigureClient(HttpClient client)
        {
            client.BaseAddress = new Uri(config.BaseUrl.TrimEnd('/') + "/");
            if (config.Timeout is { } timeout)
            {
                client.Timeout = timeout;
            }
            if (!string.IsNullOrEmpty(config.Username))
            {
                var credentials = System.Convert.ToBase64String(Encoding.UTF8.GetBytes($"{config.Username}:{config.Password}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
            }
        }

        services.AddSingleton(config);
        // AddHttpClient<TClient, TImplementation> would name the client after the interface, and share it with
        // every other package registering a client for that interface — both configure actions then run on it
        services.AddHttpClient(HttpClientName, ConfigureClient)
            .AddTypedClient<IWordConverter, WordService>()
            .AddTypedClient<IWordToImagesService, WordService>();

        return services;
    }
}
