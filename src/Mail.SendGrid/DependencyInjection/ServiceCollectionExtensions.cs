using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Office.Mail.Abstractions;

namespace Regira.Office.Mail.SendGrid.DependencyInjection;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SendGridMailer"/> as <see cref="IMailService"/>.
    /// </summary>
    public static IServiceCollection AddSendGrid(this IServiceCollection services, Action<SendGridConfig> configure)
    {
        services
            .Configure<SendGridConfig>(configure.Invoke)
            // built from configure on every resolve, so a mailer resolves from the root provider too, in a singleton say
            .AddTransient(p => p.GetRequiredService<IOptionsFactory<SendGridConfig>>().Create(Options.DefaultName))
            .AddTransient<IMailService, SendGridMailer>();

        return services;
    }
}
