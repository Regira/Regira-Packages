# Regira.Office.PDF.Gotenberg — index card

> HTML to PDF through the Chromium route of a Gotenberg server (`gotenberg/gotenberg:8`): no browser in the
> application and nothing downloaded at run time. The guide lives on the hub `Regira.Office`:
> `get_package(id: "Regira.Office", section: "office.pdf.instructions", heading: "Backend Comparison")`, under
> *Chromium behaviour* and *Gotenberg behaviour*.

- **`services.AddGotenbergPdf(o => o.BaseUrl = …)`** (`Regira.Office.PDF.Gotenberg.DependencyInjection`) registers
  `PdfService` as **`IHtmlToPdfService`** (`Regira.Office.PDF.Abstractions`) on an `HttpClient` of its own. An empty
  `BaseUrl` throws `ArgumentException` at registration.
- **`GotenbergPdfConfig`** (`Regira.Office.PDF.Gotenberg`): `BaseUrl` (with the server's `--api-root-path` when it is
  not `/`), `Timeout` (client side; raise the server's `--api-timeout`, 30 s by default, with it), `Username` and
  `Password` for a server started with `--api-enable-basic-auth`.
- **`Create(HtmlInput)`** (`Regira.Office.PDF.Models`) returns an `IMemoryFile`; read it with `GetBytes()`
  (`Regira.IO.Extensions`). The page, its header band and its footer band are set on the same `HtmlInput`.
- **HTML built from user input:** HTML-encode every value, and read the deny-list paragraph of *Gotenberg behaviour*
  before deploying. Chromium in the container fetches every URL the HTML names, internal addresses included; a
  `--chromium-deny-list` of your own replaces Gotenberg's default `file:` rule, so the pattern must keep it.
- **A refused request throws `HttpRequestException`** with Gotenberg's status and message: `400` for settings Chromium
  refuses, `403` for a refused page, `503` past `--api-timeout`.
- Text renders in the fonts installed in the Gotenberg image, not the host's. Every using:
  `get_package(id: "Regira.Office", section: "office.namespaces", heading: "PDF")`.
