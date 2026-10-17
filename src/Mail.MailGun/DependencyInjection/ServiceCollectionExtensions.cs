using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Office.Mail.Abstractions;

namespace Regira.Office.Mail.MailGun.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="MailGunMailer"/> as <see cref="IMailService"/>.
    /// </summary>
    public static IServiceCollection AddMailGun(this IServiceCollection services, Action<MailgunConfig> configure)
    {
        services
            .Configure<MailgunConfig>(configure.Invoke)
            // built from configure on every resolve, so a mailer resolves from the root provider too, in a singleton say
            .AddTransient(p => p.GetRequiredService<IOptionsFactory<MailgunConfig>>().Create(Options.DefaultName))
            .AddTransient<IMailService, MailGunMailer>();

        return services;
    }
}
