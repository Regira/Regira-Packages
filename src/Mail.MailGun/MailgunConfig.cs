namespace Regira.Office.Mail.MailGun;

public class MailgunConfig
{
    public string Api { get; set; } = null!;
    public string Key { get; set; } = null!;
    public string Domain { get; set; } = null!;
    /// <summary>
    /// Sends every message with Mailgun's <c>o:testmode</c> flag. Mailgun validates, accepts and logs the
    /// call exactly as it would a real one — the response is a normal success — but never delivers to the
    /// recipient. Message counts and charges may still apply, so this suppresses delivery, not billing.
    /// <para>
    /// Off by default. Intended for test suites and staging hosts that send to real addresses; it is a
    /// property of the environment rather than of a message, so it is set once on the config.
    /// </para>
    /// </summary>
    public bool TestMode { get; set; }
}