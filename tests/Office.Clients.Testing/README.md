# Office.Clients.Testing

Tests for [Common.Office.Clients](../../src/Common.Office.Clients/README.md), the HTTP clients for a remote Regira
Office API (barcodes, QR codes, CSV, Excel, mail parsing, OCR, PDF, Word), licensed through
[Common.Licensing](../../src/Common.Licensing/README.md). NUnit.

## Running

```bash
dotnet test tests/Office.Clients.Testing
```

Every fixture is in the `Network` category. All but `LicenseStatusClientTests`, which answers from a canned
handler, call a running Office API and read two user secrets:

- `Regira:LicenseKey`: without it those fixtures are skipped.
- `ApiServices:BaseUrl`: the API's address. With the key set but no address, the fixtures fail.

Input files come from the sibling projects' `Assets` folders (`Office.Csv.Testing`, `Office.Excel.Testing`,
`Office.Mail.Testing`, `Office.OCR.Testing`, `Office.PDF.Testing`, `Office.Word.testing`), so run it from a full
clone. Skip the suite with `--filter "TestCategory!=Network"`.
