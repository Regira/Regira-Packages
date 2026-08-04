namespace Office.Mail.Testing;

public static class MailConstants
{
    public const string LOCALHOST_REFERRER = "https://localhost";
    public const string EMPTY_INPUT = @"{}";
    public const string SIMPLE_INPUT = @"
{
    ""from"":""sender@domain.com"",
    ""to"":[""recipient@domain.com""],
    ""subject"":""Subject placeholder"",
    ""body"":""Testing...""
}";
    public const string INPUT_REPLYTO = @"
{
    ""from"":""sender@domain.com"",
    ""replyTo"":""reply_to_me@domain.com"",
    ""to"":[""recipient@domain.com""],
    ""subject"":""Subject placeholder"",
    ""body"":""Testing...""
}";
    public const string EXTENDED_INPUT = @"
{
    ""from"":{""displayName"": ""My Name"", ""email"": ""sender@domain.com""},
    ""replyTo"":{""displayName"": ""Reply To"", ""email"": ""reply_to_me@domain.com""},
    ""to"":[
        {""displayName"": ""First Recipient"", ""email"": ""recipient1@domain.com""},
        {""displayName"": ""Second Recipient"", ""email"": ""recipient2@domain.com""},
        ""recipient3@domain.com""
    ],
    ""subject"":""Subject placeholder"",
    ""body"":""Testing..."",
    ""isHtml"":true
}";
    public const string INPUT_NO_HMTL = @"
{
    ""from"":""sender@domain.com"",
    ""to"":[""recipient@domain.com""],
    ""subject"":""Subject placeholder"",
    ""body"":""Testing..."",
    ""isHtml"":false
}";
    public const string INPUT_NO_SENDER = @"
{
    ""to"":[""recipient@domain.com""],
    ""subject"":""Subject placeholder"",
    ""body"":""Testing...""
}";
    public const string INPUT_NO_RECIPIENTS = @"
{
    ""from"":""sender@domain.com"",
    ""subject"":""Subject placeholder"",
    ""body"":""Testing...""
}";
    public const string INPUT_NO_SUBJECT = @"
{
    ""from"":""sender@domain.com"",
    ""to"":[""recipient@domain.com""],
    ""body"":""Testing...""
}";
    public const string INPUT_NO_BODY = @"
{
    ""from"":""sender@domain.com"",
    ""to"":[""recipient@domain.com""],
    ""subject"":""Subject placeholder""
}";
    public const string INPUT_INVALID_SENDER = @"
{
    ""from"":""bad_email_format"",
    ""to"":[""recipient@domain.com""],
    ""subject"":""Subject placeholder"",
    ""body"":""Testing...""
}";
    public const string INPUT_INVALID_RECIPIENT = @"
{
    ""from"":""sender@domain.com"",
    ""to"":[""bad_email_format"",""recipient@domain.com""],
    ""subject"":""Subject placeholder"",
    ""body"":""Testing...""
}";
}