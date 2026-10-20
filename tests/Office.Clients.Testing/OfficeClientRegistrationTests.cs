using Microsoft.Extensions.DependencyInjection;
using Regira.Office.Clients.DependencyInjection;
using Regira.Office.Word.Abstractions;

namespace Office.Clients.Testing;

/// <summary>
/// What <see cref="OfficeClientServiceCollectionExtensions.AddOfficeClients"/> sets on its clients. No API is called.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class OfficeClientRegistrationTests
{
    [Test]
    public void Each_Client_Is_Named_After_Its_Interface()
    {
        var services = new ServiceCollection();
        services.AddOfficeClients(o => o.BaseUrl = "https://office.example.test/api");
        services.AddHttpClient(nameof(IWordConverter), c => c.Timeout = TimeSpan.FromMinutes(5));
        using var sp = services.BuildServiceProvider();

        var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IWordConverter));

        Assert.That(client.BaseAddress, Is.EqualTo(new Uri("https://office.example.test/api/")));
        Assert.That(client.Timeout, Is.EqualTo(TimeSpan.FromMinutes(5)), "an app configures the client by that name");
    }

    [Test]
    public void A_Missing_BaseUrl_Is_Named_When_A_Client_Resolves()
    {
        var services = new ServiceCollection();
        services.AddOfficeClients(_ => { });
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() => sp.GetRequiredService<IWordConverter>());

        Assert.That(ex!.Message, Does.Contain("OfficeClientOptions.BaseUrl"));
    }
}
