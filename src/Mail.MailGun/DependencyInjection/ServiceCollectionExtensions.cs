using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Office.Mail.Abstractions;

namespace Regira.Office.Mail.MailGun.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMailGun(this IServiceCollection services, Action<MailgunConfig> configure)
    {
        services
            .Configure<MailgunConfig>(configure.Invoke)
            .AddTransient(p => p.GetRequiredService<IOptionsSnapshot<MailgunConfig>>().Value)
            .AddTransient<IMailService, MailGunMailer>();

        return services;
    }
}