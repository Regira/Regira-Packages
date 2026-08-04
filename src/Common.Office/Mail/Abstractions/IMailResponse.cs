namespace Regira.Office.Mail.Abstractions;

public interface IMailResponse
{
    bool Success { get; }
    string? Status { get; set; }
    string? Content { get; set; }
    Exception? Exception { get; set; }
}