using Regira.Office.Mail.Abstractions;

namespace Regira.Office.Mail.Models;

public record MailRecipient : MailAddress, IMailRecipient
{
    public RecipientTypes RecipientType { get; set; } = RecipientTypes.To;


    public static implicit operator string(MailRecipient mailAddress)
    {
        return mailAddress.Email;
    }
    public static implicit operator MailRecipient(string email)
    {
        return new() { Email = email };
    }

    // the address format, where a record would synthesize one that lists its members
    public override string ToString() => base.ToString();
}