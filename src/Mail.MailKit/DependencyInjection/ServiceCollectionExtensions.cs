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
            .AddTransient(p => p.GetRequiredService<IOptionsSnapshot<MailKitConfig>>().Value)
            .AddTransient<IMailService, MailKitMailer>();

        return services;
    }
}
