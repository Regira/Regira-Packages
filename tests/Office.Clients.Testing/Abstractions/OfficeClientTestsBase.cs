using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Regira.Licensing.DependencyInjection;
using Regira.Office.Clients.DependencyInjection;

namespace Office.Clients.Testing.Abstractions;

public abstract class OfficeClientTestsBase
{
    protected IServiceProvider Services = null!;

    protected OfficeClientTestsBase()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets(GetType().Assembly)
            .Build();

        var licenseKey = config["Regira:LicenseKey"];
        if (string.IsNullOrEmpty(licenseKey))
            Assert.Ignore("Regira:LicenseKey is not configured");

        var services = new ServiceCollection();
        if (!string.IsNullOrEmpty(licenseKey))
            services.UseRegira(licenseKey);
        services.AddOfficeClients(o =>
        {
            o.BaseUrl = config["ApiServices:BaseUrl"]!;
        });

        Services = services.BuildServiceProvider(validateScopes: true);
    }

    [OneTimeTearDown]
    public void BaseTearDown()
    {
        (Services as IDisposable)?.Dispose();
    }
}
