namespace Regira.Office.Mail.Exceptions;

public class EmailFormatException(string? input, string? message = null, Exception? inner = null)
    : FormatException(message, inner)
{
    public string? EmailInput { get; set; } = input;
}