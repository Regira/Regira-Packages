namespace Regira.Office.Mail.Abstractions;

public interface IMailAddress
{
    string? DisplayName { get; set; }
    string Email { get; set; }
}