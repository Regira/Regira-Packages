using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Regira.Office.Mail.Abstractions;

namespace Regira.Office.Mail.SendGrid.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSendGrid(this IServiceCollection services, Action<SendGridConfig> configure)
    {
        services
            .Configure<SendGridConfig>(configure.Invoke)
            .AddTransient(p => p.GetRequiredService<IOptionsSnapshot<SendGridConfig>>().Value)
            .AddTransient<IMailService, SendGridMailer>();

        return services;
    }
}