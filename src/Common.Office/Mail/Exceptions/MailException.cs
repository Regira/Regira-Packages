using Regira.Office.Mail.Abstractions;

namespace Regira.Office.Mail.Exceptions;

public class MailException(string message, Exception? innerException = null) : Exception(message, innerException)
{
    public IMessageObject? MessageObject { get; set; }
    public string? ResponseContent { get; set; }
}