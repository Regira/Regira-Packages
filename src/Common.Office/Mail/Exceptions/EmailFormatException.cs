namespace Regira.Office.Mail.Exceptions;

public class EmailFormatException(string? input, string? message = null, Exception? inner = null)
    : FormatException(message ?? $"'{input}' is not a valid e-mail address; a display name goes in MailAddress.DisplayName.", inner)
{
    public string? EmailInput { get; set; } = input;
}