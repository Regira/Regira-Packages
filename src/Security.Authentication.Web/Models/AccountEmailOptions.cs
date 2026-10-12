namespace Regira.Security.Authentication.Web.Models;

/// <summary>
/// What the password-reset and email-confirmation mails may carry. Bound from configuration, so the application decides
/// where a mailed token can go and what the mail is called — never the caller who asked for it.
/// <para>
/// Read per request by <c>PasswordControllerBase</c> and <c>UserControllerBase</c> through <c>IOptions</c>, e.g.
/// <c>services.Configure&lt;AccountEmailOptions&gt;(configuration.GetSection("Authentication:AccountEmails"))</c>.
/// </para>
/// </summary>
public class AccountEmailOptions
{
    /// <summary>
    /// The origins (scheme, host and port, e.g. <c>https://app.example.com</c>) the page a mailed link opens may be on.
    /// A request's <c>siteUrl</c> or <c>confirmEmailUrl</c> on another origin answers <c>400</c>.
    /// <para>
    /// With none listed, a request that needs a link throws <see cref="InvalidOperationException"/>: a link the caller
    /// chose would hand a genuine token to whoever chose it.
    /// </para>
    /// </summary>
    public ICollection<string> AllowedOrigins { get; set; } = [];

    /// <summary>The application's name, which starts the password-reset mail's subject ("Example Password Reset").</summary>
    public string? SiteName { get; set; }
}
