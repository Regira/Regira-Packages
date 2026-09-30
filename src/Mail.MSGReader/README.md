# Regira.Office.Mail.MSGReader

Reads existing email files into [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) mail models, built on [MsgReader](https://www.nuget.org/packages/MsgReader). `MsgParser` parses Outlook `.msg` files and `EmlParser` parses `.eml` files; both implement `IMessageParser` and return an `IMessageObject` with the sender, recipients, subject, body and attachments. It reads messages and sends none.

## Installation

```xml
<PackageReference Include="Regira.Office.Mail.MSGReader" Version="6.*" />
```

## Documentation

- [Mail](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail/) — the `IMessageObject`, address and recipient models a parsed file is returned as, and the mail backends that send them

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
