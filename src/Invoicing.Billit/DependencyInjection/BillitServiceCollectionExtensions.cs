using Microsoft.Extensions.DependencyInjection;
using Regira.Invoicing.Billit.Config;
using Regira.Invoicing.Billit.Services;

namespace Regira.Invoicing.Billit.DependencyInjection;

public static class BillitServiceCollectionExtensions
{
    public static IServiceCollection AddBillit(this IServiceCollection services, Func<IServiceProvider, BillitConfig> configFactory)
    {
        services.AddHttpClient("billit", (p, client) =>
        {
            var config = configFactory(p);
            client.ConfigureClient(config);
        });

        services
            .AddTransient<IFileManager, FileManager>()
            .AddTransient<IInvoiceManager, InvoiceManager>()
            .AddTransient<IPartyManager, PartyManager>()
            .AddTransient<IPeppolManager, PeppolManager>();

        return services;
    }
    public static HttpClient ConfigureClient(this HttpClient client, BillitConfig config)
    {
        client.BaseAddress = new Uri(config.Api?.BaseUrl ?? throw new NullReferenceException("Config key Billit.Api.BaseUrl missing"));
        client.DefaultRequestHeaders.Add(BillitConstants.HeaderApiKeyName, config.Api.Key);
        client.DefaultRequestHeaders.Add(BillitConstants.HeaderPartyIdName, config.PartyId);

        return client;
    }
}