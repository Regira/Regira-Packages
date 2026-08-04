using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Regira.Security.Authentication.Cookie.Extensions;
using Regira.Security.Authentication.Core.Extensions;
using Regira.Security.Authentication.Jwt.Extensions;

namespace Web.Security.Testing.Infrastructure.Cookie;

/// <summary>
/// A cookie scheme under a <b>non-default name</b>, alongside the scheme selector.
/// <para>
/// Both details matter and the default fixture has neither: a scheme called <c>"Cookies"</c> hides a resolver that
/// falls back to that constant, and without the selector nothing sets a default scheme that cannot sign anyone in.
/// </para>
/// </summary>
public class CustomCookieSchemeStartup
{
    public const string CookieScheme = "MyCookie";
    public const string CookieName = ".MyApp.Session";

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddCookieAuthentication(o =>
        {
            o.AuthenticationScheme = CookieScheme;
            o.CookieName = CookieName;
            o.IsApi = true;
        });

        services
            .AddJwtAuthentication(o =>
                o.Secret = string.Join(":", Enumerable.Range(0, 3).Select(_ => Guid.NewGuid().ToString("N"))))
            .AddSchemeSelector();

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
