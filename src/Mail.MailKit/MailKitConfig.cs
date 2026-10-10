using MailKit.Security;

namespace Regira.Office.Mail.MailKit;

public class MailKitConfig
{
    /// <summary>Host name of the SMTP server.</summary>
    public string Host { get; set; } = null!;
    /// <summary>
    /// 587 by default: message submission with STARTTLS. Implicit TLS uses 465 with
    /// <see cref="SecureSocketOptions.SslOnConnect"/>.
    /// </summary>
    public int Port { get; set; } = 587;
    /// <summary>
    /// <see cref="SecureSocketOptions.StartTls"/> by default, which fails the send when the server offers no TLS
    /// instead of authenticating in clear text. <see cref="SecureSocketOptions.Auto"/> falls back to an
    /// unencrypted connection on such a server. A local test server without TLS takes
    /// <see cref="SecureSocketOptions.None"/>.
    /// </summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;
    /// <summary>Leave empty for a server that accepts mail without SMTP AUTH, such as an internal relay or a local test server.</summary>
    public string? UserName { get; set; }
    public string? Password { get; set; }
    /// <summary>Timeout of each SMTP command, in milliseconds. 2 minutes by default.</summary>
    public int Timeout { get; set; } = 120_000;
}
