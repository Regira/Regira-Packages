using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Regira.Office.PDF.Abstractions;

namespace Regira.Office.PDF.Gotenberg.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The name of the <see cref="HttpClient"/> <see cref="AddGotenbergPdf"/> registers — for adding handlers
    /// or resilience to it with <c>services.AddHttpClient(HttpClientName)</c>.
    /// </summary>
    public const string HttpClientName = "Regira.Office.PDF.Gotenberg";

    /// <summary>
    /// Registers <see cref="PdfService"/> as <see cref="IHtmlToPdfService"/>, on an <see cref="HttpClient"/> pointed
    /// at the Gotenberg server.
    /// </summary>
    /// <remarks>
    /// The client is named <see cref="HttpClientName"/> rather than after the interface, so another package's
    /// client — Word.Gotenberg's, for one, even when it points at the same server — keeps its own base address and
    /// credentials. Which implementation <see cref="IHtmlToPdfService"/> resolves to is still decided by
    /// registration order: the last one wins.
    /// </remarks>
    public static IServiceCollection AddGotenbergPdf(this IServiceCollection services, Action<GotenbergPdfConfig> configure)
    {
        var config = new GotenbergPdfConfig();
        configure(config);
        if (string.IsNullOrWhiteSpace(config.BaseUrl))
        {
            throw new ArgumentException($"{nameof(GotenbergPdfConfig)}.{nameof(GotenbergPdfConfig.BaseUrl)} is required.", nameof(configure));
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
            .AddTypedClient<IHtmlToPdfService, PdfService>();

        return services;
    }
}
