using Regira.Office.Mail.Abstractions;

namespace Regira.Office.Mail.Models;

public class MailResponse: IMailResponse
{
    public bool Success { get; set; }
    public string? Status { get; set; }
    public string? Content { get; set; }
    public Exception? Exception { get; set; }
}