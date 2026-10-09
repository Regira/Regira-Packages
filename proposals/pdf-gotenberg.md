# HTML to PDF through Gotenberg's Chromium route

As of 2026-10-08. Sources: the `Regira-Packages` repository, branch `wip` at `ad2eed6`, where PDF.MsPlaywright,
PDF.Puppeteer and PDF.SelectPdf apply every `HtmlInput` setting through the shared `ChromiumPdfLayout`; Gotenberg's
documentation of the route, [Convert HTML to PDF](https://gotenberg.dev/docs/convert-with-chromium/convert-html-to-pdf),
read on 2026-10-08; and a throwaway spike against a local `gotenberg/gotenberg:8` container, which reported version
8.37.0, read back with the PdfPig library (see *What the spike showed*).

**Status: built on 2026-10-09 for 6.5.1, uncommitted. All four decisions were built as recommended; *Outcome* records where the build differs from the design.**

## Recommendation

Add `Regira.Office.PDF.Gotenberg`, a fourth HTML→PDF backend. Its `PdfService` implements `IHtmlToPdfService` by
posting the HTML to the Chromium route of a Gotenberg server, `forms/chromium/convert/html`, the way Word.Gotenberg
posts documents to the LibreOffice route.

This gives HTML→PDF without a browser inside the application. PDF.MsPlaywright and PDF.Puppeteer download Chromium on
first use and start a browser for every PDF. That needs network access and a writable disk at run time, and adds one to
three seconds per conversion. Gotenberg keeps Chromium running in a container of its own. In the spike, a warm
conversion took 0.11 s. An application that already runs Gotenberg for Word.Gotenberg gets HTML→PDF from the same
server.

Playwright stays the recommended default. Gotenberg is for container setups: an application image that must stay slim
or may not download at run time, or a deployment that already runs Gotenberg.

The work is small, because the page settings are already solved. `ChromiumPdfLayout` turns `HtmlInput` into
Chromium's page size, margins and header and footer bands, and the route takes the same settings as form fields. The
new code is the request, the registration and a test fixture.

Steps, in order:

1. The package: config, `PdfService`, `AddGotenbergPdf`, and `InternalsVisibleTo` for the shared layout.
2. A `GotenbergTests` fixture on the shared `HtmlToPdfTestsBase`, against a Gotenberg container.
3. Guides, docs, routing tables, the solution, GuideVerifier and the changelog.

## What exists today

| What | Where | Effect |
|---|---|---|
| The HTML backends share one layout | `src/Common.Office/PDF/Internal/ChromiumPdfLayout.cs`, internal, visible to PDF.MsPlaywright and PDF.Puppeteer | Page size from `Format` and `Orientation`, margins in units of `DPI`, header and footer as positioned bands. The new package is a third `InternalsVisibleTo` |
| Word.Gotenberg talks to the LibreOffice route | `WordService`, `GotenbergWordConfig`, `AddGotenbergWord` | A named `HttpClient` (`BaseUrl`, `Timeout`, basic auth) and a non-success response thrown as `HttpRequestException`, with a hint on 503. About forty lines of connection code that the new package needs too (decision 2) |
| `AddGotenbergWord` is named for its route | Its remarks: *"rather than `AddGotenberg` because Gotenberg also converts HTML"* | Leaves room for `AddGotenbergPdf` |
| The HTML settings scenarios are shared | `tests/Office.PDF.Testing/Abstractions/HtmlToPdfTestsBase.cs`: page size, margins, header and footer placement, read back with PdfPig | The new fixture inherits them, and passes them or fails them on the same terms as the other three |
| Gotenberg container tests have a pattern | `tests/Office.Word.testing/GotenbergTests.cs`: `gotenberg/gotenberg:8` through Testcontainers 4.15.0, category `Containers`, `GOTENBERG_URL` for a running server, `REGIRA_PROVIDER_TESTS=containers` to start one | Copied into the PDF test project, which has no Testcontainers reference yet |
| PDF packages ship at 6.5.1 | `Common.Office`, the PDF backends | The new package starts on the family's number |

## What the spike showed

One request per row against Gotenberg 8.37.0, read back with PdfPig. The HTML resets the body margin and puts one
20px word at the top-left corner of the content box.

| Request | Result |
|---|---|
| `paperWidth=210mm`, `paperHeight=297mm` | 595.9 × 841.9 pt, A4. Gotenberg parses `mm`, though its documentation lists only `in`, `pt` and `cm` (decision 3) |
| `pt` margins of 70.866 (25 mm) | The word starts at 69.7 pt from the left and 72.6 pt from the top, as on Playwright |
| `7.4cm` × `10.5cm`, margins `0` | 210.0 × 298.1 pt, A7, and the word at 0,0 |
| Margins and paper left out | 612 × 792 pt, US Letter, with 0.39 in margins: Gotenberg's defaults. The new backend always sends both, so they never apply |
| `header.html` and `footer.html`: full HTML documents, each holding the band `ChromiumPdfLayout` builds today | Header, body and footer land where they land on Playwright, on all three pages. The body starts at 78.1 pt, the same as Playwright and Puppeteer. `pageNumber` and `totalPages` read 1/3, 2/3, 3/3 |
| `header.html` alone | Header and body, and no footer: Gotenberg adds no default footer, unlike Chromium's own default |
| `paperWidth=abc` | `400`, plain text: *Invalid form data: form field 'paperWidth' is invalid (got 'abc', …)* |
| Five warm conversions, one page each | 0.112–0.116 s each, request to response, on the same machine. The first request after the container started took 0.95 s |

## Design

### Package

`src/PDF.Gotenberg/`, package and assembly `Regira.Office.PDF.Gotenberg`, namespace `Regira.Office.PDF.Gotenberg`. It
references `Common.Office` and `Microsoft.Extensions.Http`, as Word.Gotenberg does, and nothing else: no browser, no
OpenXml.

### Config and DI

<!-- no-compile -->
```csharp
public class GotenbergPdfConfig
{
    public string BaseUrl { get; set; } = null!;   // e.g. http://gotenberg:3000, with the server's --api-root-path
    public TimeSpan? Timeout { get; set; }         // client side; Gotenberg's own --api-timeout answers 503
    public string? Username { get; set; }          // --api-enable-basic-auth
    public string? Password { get; set; }
}

services.AddGotenbergPdf(o => o.BaseUrl = "http://gotenberg:3000");   // IHtmlToPdfService → PdfService
```

The members are `GotenbergWordConfig`'s connection members, without `ImageOptions`. The named client is
`Regira.Office.PDF.Gotenberg`, kept apart from Word.Gotenberg's for the reason its remarks give: another package's
configuration never reaches it.

### The request

`PdfService(HttpClient client)` builds a `MultipartFormDataContent`:

| Part | From |
|---|---|
| `files`: `index.html` | `HtmlInput.HtmlContent` |
| `files`: `header.html`, `footer.html` | `ChromiumPdfLayout.HeaderTemplate` and `FooterTemplate`, each wrapped in `<!DOCTYPE html><html><head><meta charset="utf-8"></head><body>…</body></html>`, since Gotenberg requires a whole document. Sent only when the input has that header or footer |
| `paperWidth`, `paperHeight` | `ChromiumPdfLayout.Width`, `Height` |
| `marginTop`, `marginRight`, `marginBottom`, `marginLeft` | `ChromiumPdfLayout.Margin*` |

Nothing else is sent, so the other route fields keep Gotenberg's defaults. `printBackground` stays `false`, as on
Playwright and Puppeteer. `emulatedMediaType` stays `print`. `preferCssPageSize` stays `false`, so `Format` wins over
CSS `@page`.

### Errors

As in Word.Gotenberg: a non-success response throws `HttpRequestException` with the status code, the URL and the
response body, and the 503 hint about `--api-timeout`. The route's documented codes all surface this way: `400` for
an invalid field or an unreachable main page, `409` for an HTTP status or console exception it was told to fail on (it
is told nothing, so only main-page statuses of 499–599), `403` for a URL that outbound filtering forbids, and `503`
for a timeout.

## Tests

`GotenbergTests : HtmlToPdfTestsBase` in `Office.PDF.Testing`, gated as Word's `GotenbergTests` is: category
`Containers`, `GOTENBERG_URL` for a running server, otherwise a `gotenberg/gotenberg:8` container when
`REGIRA_PROVIDER_TESTS=containers`, skipped without Docker. It inherits every settings scenario: page size and
orientation, margins of 0, 10 and 25 mm, header and footer on every page, and a header alone. The spike suggests all
pass unchanged. One more case belongs to this fixture: a `400` surfaces as `HttpRequestException` carrying the status code and
Gotenberg's message.

The test project gains `Testcontainers` 4.15.0, the version the Word and PostgreSQL tests pin.

## Files to change

| File | Change |
|---|---|
| `src/PDF.Gotenberg/` | New: `.csproj`, `GotenbergPdfConfig`, `PdfService`, `DependencyInjection/ServiceCollectionExtensions.cs`, `README.md` (copy Word.Gotenberg's shape) |
| `src/Common.Office/Common.Office.csproj` | `InternalsVisibleTo` `Regira.Office.PDF.Gotenberg`, in the headless-Chromium group |
| `src/Common.Office/PDF/Internal/ChromiumPdfLayout.cs` | Only if decision 3 picks centimetres |
| `src/Common.Office/ai/office.pdf.instructions.md`, `office.namespaces.md` | Installation, the backend table, the recommendation (Gotenberg for container setups), the Chromium behaviour block naming three backends, registration, the namespace rows |
| `src/Common.Office/docs/pdf/README.md` | Projects table, installation, an implementation note beside *Playwright / Puppeteer* |
| `src/Common.Office/ai/office.word.instructions.md`, `docs/word/README.md` | One line each where Word.Gotenberg is set up: the same server converts HTML through PDF.Gotenberg |
| `ai/AGENTS.md`, `src/Common.Setup/ai/copilot-instructions.md` | The `Office.PDF` routing rows: Gotenberg for HTML to PDF in container setups |
| `Regira-Packages.slnx` | The new project beside the other PDF backends |
| `tools/GuideVerifier/projects.json` | `src/PDF.Gotenberg/PDF.Gotenberg.csproj` in the `office-pdf` group |
| `tests/Office.PDF.Testing/` | Project reference, `Testcontainers`, `GotenbergTests.cs`, `README.md` (the `Containers` category and its variables) |
| `CHANGELOG.md` | `` `Regira.Office.PDF.Gotenberg` 6.5.1 — HTML to PDF through Gotenberg's Chromium route ``; `Regira.Office` and `Regira.Setup` guide lines |

Regira-Website's `packages.json` picks the package up from `src/` the next time `npm run packages` runs there.

## Decisions

1. **Names and home.** Recommended: a new package `Regira.Office.PDF.Gotenberg` with `PdfService`,
   `GotenbergPdfConfig` and `AddGotenbergPdf`. It is a PDF backend, and `PdfService` matches PDF.PdfPig and
   PDF.MiniPdf. The alternative is to add HTML conversion to Word.Gotenberg, which would then live in the Word family
   and pull OpenXml into an HTML-only application.
2. **The connection code.** Recommended: copy Word.Gotenberg's four config members, client set-up and error message
   into the new package, about forty lines. The alternative is a shared `Regira.Office.Gotenberg` package that both
   reference. That avoids the copy, but adds a third package and a third version line for code that has not changed
   since Word.Gotenberg was written. One `AddGotenberg` that registers both backends on one client is the variant
   that alternative would make possible.
3. **Units.** `ChromiumPdfLayout` writes millimetres. Gotenberg 8.37.0 parses them, but documents only `in`, `pt` and
   `cm`, so a later Gotenberg could drop them without notice. Recommended: the layout writes centimetres, which
   Playwright, Puppeteer and Gotenberg all document, and the settings tests run again on all three. The alternatives
   are to keep millimetres and rely on the undocumented parsing, or to have the Gotenberg backend convert the
   layout's strings.
4. **The container fixture.** Recommended: yes. It is the only way to test the backend, it reuses the settings
   scenarios unchanged, and it runs only on machines that opt in. Without it, the backend ships untested.

## Not in scope

- **Gotenberg's own options:** `printBackground`, `waitDelay`, `waitForExpression`, `pdfa`, `pdfua`, `metadata`,
  watermarks, encryption, splitting and the rest. `HtmlInput` has none of them, and the other three HTML backends do
  not offer them either. They can come later as an opt-in options member on the config.
- **Assets.** The route takes images, fonts and stylesheets as extra files that `index.html` names by filename.
  `HtmlInput` has no such member, so the HTML embeds them as `data:` URIs or names them by absolute URL, as it must for
  Playwright too.
- **Office documents and URLs through Chromium.** The route family also converts a URL or Markdown. Office documents
  already go through MiniPdf, or Word.Gotenberg's LibreOffice route.

## Risks

- **Fonts.** Text renders in the fonts installed in the Gotenberg image, not the host's. A page that looked right
  through Playwright on a Windows machine can lay out differently. Web fonts by URL or `data:` URI behave alike on
  every backend.
- **URLs resolve from the container.** An absolute URL in the HTML is fetched by Gotenberg on its own network, where
  `localhost` is the container itself. HTML built from user input can make the server request internal addresses:
  Gotenberg's outbound URL filtering (`403`) is the guard, and the guide should say so.
- **Request size and time.** The whole HTML, including `data:` images, goes over HTTP on every call. A large document
  meets Gotenberg's `--api-timeout` (30 s by default), which the 503 hint names.

## Outcome

Built as designed, with these differences:

- **Centimetres everywhere in the layout.** `ChromiumPdfLayout` writes its page size, its margins and the CSS
  lengths inside the header and footer bands in centimetres, to four decimals, which keeps the millimetres'
  thousandths. Playwright and Puppeteer pass the settings tests unchanged.
- **The header and footer go as Chromium gives them.** `ChromiumPdfLayout` fills the missing one with an empty
  template when only one is set, so both `header.html` and `footer.html` are sent whenever either is. The empty
  footer adds nothing to the page.
- **The shared header scenario sets `line-height:1`.** Gotenberg rendered `Header_And_Footer_Sit_On_Every_Page…`
  2 pt lower than Playwright, failing by 0.14 pt. The image has no Arial, so a 12px span in the 16px band took Noto
  Sans's taller `normal` line height. The scenario's header and footer now set `line-height:1`, as its body probe
  already did, so the test measures band placement and not fonts. This is the *Fonts* risk, documented in the guides.
- **Two fixture cases, not one.** The `400` case asks for margins that leave no room for content, which Chromium
  refuses (*Chromium does not handle the provided settings*). A second case checks that HTML without a charset
  declaration, which Gotenberg reads from a file, still reads as UTF-8.
- **`Regira.runsettings`** names the Gotenberg fixtures among the container suites it opts in.

Tests: all 146 in `Office.PDF.Testing` pass, `GotenbergTests` (12) included, against a server through `GOTENBERG_URL`
and against a container the fixture started. GuideVerifier compiles the `office-pdf` and `office-word` snippets.
