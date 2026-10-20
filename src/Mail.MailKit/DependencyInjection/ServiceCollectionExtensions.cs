using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Office.Mail.Abstractions;

namespace Regira.Office.Mail.MailKit.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMailKit(this IServiceCollection services, Action<MailKitConfig> configure)
    {
        services
            .Configure<MailKitConfig>(configure.Invoke)
            // built from configure on every resolve, so a mailer resolves from the root provider too, in a singleton say
            .AddTransient(p => p.GetRequiredService<IOptionsFactory<MailKitConfig>>().Create(Options.DefaultName))
            .AddTransient<IMailService, MailKitMailer>();

        return services;
    }
}
