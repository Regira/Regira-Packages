using Microsoft.Extensions.DependencyInjection;
using Regira.Invoicing.Billit.Config;
using Regira.Invoicing.Billit.DependencyInjection;
using Regira.Invoicing.Billit.Services;
using Regira.Invoicing.Invoices.Abstractions;
using Regira.Serializing.Abstractions;
using Regira.Serializing.Newtonsoft.Json;

namespace Invoicing.Testing;

[TestFixture]
public class BillitRegistrationTests
{
    [Test]
    public void AddBillit_Serves_IInvoiceService_With_The_Invoice_Manager()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ISerializer, JsonSerializer>();
        services.AddBillit(_ => new BillitConfig { PartyId = "1", Api = new() { BaseUrl = "https://billit.test/", Key = "key" } });
        using var provider = services.BuildServiceProvider();

        var invoiceService = provider.GetRequiredService<IInvoiceService>();

        Assert.That(invoiceService, Is.InstanceOf<InvoiceManager>());
        // the README and the guide name IInvoiceManager among what AddBillit registers
        Assert.That(provider.GetRequiredService<IInvoiceManager>(), Is.InstanceOf<InvoiceManager>());
    }
}
