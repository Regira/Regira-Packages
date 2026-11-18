namespace Regira.Security.Authentication.Web.Constants;

/// <summary>
/// The size limits of what the account controllers read. Several of their actions are anonymous, so without a limit
/// any caller decides how much the server parses, hashes and looks up.
/// </summary>
internal static class AccountInputLimits
{
    /// <summary>The length of Identity's user-name and email columns.</summary>
    public const int UserName = 256;
    /// <summary>Generous on purpose: a limit below a password in use would lock its user out.</summary>
    public const int Password = 1024;
    public const int Url = 2048;
    /// <summary>
    /// A reset or confirmation token is the Base64 of Identity's token and the user name: about 2,400 characters for
    /// a user name of <see cref="UserName"/> characters that are all escaped in JSON, about 750 for an ASCII one. A
    /// refresh token is 43.
    /// </summary>
    public const int Token = 4096;
    /// <summary>The request body of an action that takes one, refused before it is read when it is larger.</summary>
    public const int RequestBody = 16 * 1024;
}
