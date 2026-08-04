using System.ComponentModel.DataAnnotations;
using Regira.Office.Mail.Models;

namespace Regira.Office.Mail.Web;

public class MailInput
{
    public class Attachment
    {
        [Required]
        public string? FileName { get; set; }
        public byte[]? Bytes { get; set; }
        public string? ContentType { get; set; }
    }
    public class Address
    {
        public string? DisplayName { get; set; }
        [EmailAddress]
        [Required]
        public string Email { get; set; } = null!;

        public static implicit operator Address(string email)
        {
            return new() { Email = email };
        }
    }
    public class Recipient : Address
    {
        public RecipientTypes RecipientType { get; set; } = RecipientTypes.To;

        public static implicit operator Recipient(string email)
        {
            return new() { Email = email };
        }
    }

    public Address? From { get; set; }
    [Required]
    public ICollection<Recipient>? To { get; set; }
    public Address? ReplyTo { get; set; }
    [Required]
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public bool IsHtml { get; set; } = true;
    public ICollection<Attachment>? Attachments { get; set; }
}