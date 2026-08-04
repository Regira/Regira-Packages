using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Security.Authentication.Cookie.Extensions;

namespace Web.Security.Testing.Infrastructure.Cookie;

/// <summary>API mode: a script-called endpoint must get 401/403, never a redirect to an HTML login page.</summary>
public class CookieStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCookieAuthentication(o =>
        {
            o.IsApi = true;
            o.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        });

        services.AddControllersFor(typeof(CookieAuthController), typeof(TestController));
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseRouting();
        app.UseAuthentication();
        app.UseAuthorization();

        app.UseEndpoints(endpoints => endpoints.MapControllers().RequireAuthorization());
    }
}
