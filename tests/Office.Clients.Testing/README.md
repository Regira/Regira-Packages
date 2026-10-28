# Office.Clients.Testing

Tests for [Common.Office.Clients](../../src/Common.Office.Clients/README.md), the HTTP clients for a remote Regira
Office API (barcodes, QR codes, CSV, Excel, mail parsing, OCR, PDF, vCards, Word), licensed through
[Common.Licensing](../../src/Common.Licensing/README.md). NUnit.

## Running

```bash
dotnet test tests/Office.Clients.Testing
```

`OfficeClientRegistrationTests`, `PdfClientRequestTests` and `VCardClientRequestTests` call no API: they read the
registrations, or answer from canned handlers. Every other fixture is in the `Network` category. All of those but
`LicenseStatusClientTests` and `WordClientRequestTests`, which answer from canned handlers too, call a running Office
API and read two user secrets:

- `Regira:LicenseKey`: without it those fixtures are skipped.
- `ApiServices:BaseUrl`: the API's address. With the key set but no address, the fixtures fail.

Input files come from the sibling projects' `Assets` folders (`Office.Csv.Testing`, `Office.Excel.Testing`,
`Office.Mail.Testing`, `Office.OCR.Testing`, `Office.PDF.Testing`, `Office.Word.testing`), so run it from a full
clone. Skip the `Network` fixtures with `--filter "TestCategory!=Network"`.
