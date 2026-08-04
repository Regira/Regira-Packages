using Regira.Office.Mail.Models;

namespace Regira.Office.Mail.Abstractions;

public interface IMailRecipient : IMailAddress
{
    RecipientTypes RecipientType { get; set; }
}