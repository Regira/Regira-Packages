using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Exceptions;
using Regira.Utilities;

namespace Regira.Office.Mail.Models;

public record MailAddress : IMailAddress
{
    private string _email = null!;

    public string? DisplayName { get; set; }
    public string Email
    {
        get => _email;
        set
        {
            if (!RegexUtility.IsValidEmail(value))
            {
                throw new EmailFormatException(value);
            }
            _email = value;
        }
    }


    public static implicit operator string(MailAddress mailAddress)
    {
        return mailAddress.Email;
    }
    public static implicit operator MailAddress(string email)
    {
        return new() { Email = email };
    }

    public override string ToString()
    {
        if (!string.IsNullOrEmpty(DisplayName))
        {
            return $"{DisplayName} <{Email}>";
        }
        return Email;
    }
}