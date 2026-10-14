# Applying the 6.6.0 test-run findings
<!-- {% raw %} -->

As of 2026-10-14. Sources: the report *Regira 6.6.0 pre-release test run* (six apps generated from the release candidate on 2026-10-13; kept outside the repositories), Regira-Packages `wip` at `e0c3535`, Regira-Modules at `b5ddae2` and Regira-Tools at `cbe6801`. Every finding was re-read in the code by six read-only checks, one per area. Probes, all in a scratch folder:

- MiniWord 0.9.2 on a template with a tag-like value.
- Builds of a scratch consumer against the hubs' `.targets`.
- SQLite decimal aggregates on EF Core 9.0.20 and 10.0.12.
- A replica of the `how_to` and `recommend_packages` ranking over the real knowledge base.
- `gotenberg/gotenberg:8` (8.37.0) run with five deny lists.

**Status: built 2026-10-14, uncommitted in all three repos (see *Outcome*); the knowledge base rebuilt locally, not yet deployed.**

## Recommendation

The report holds up as a list of problems: 20 findings are confirmed, 13 are partly right, and one (S3) does not reproduce. Its *Recommended fixes* table is not safe to apply as written:

- About twenty of its exact changes carry an error.
- Seven would ship wrong or unsafe guidance (*Corrections*).

Every step below uses the corrected change.

Three findings weigh differently from the report:

- **P3 is critical.** MiniWord splits tag-like text in values, loop rows included, and can drop text. That is corrupted content.
- **P2 is wider.** Ten hubs fall back to the project folder, not eight. Because the property is shared, Entities, Office and Setup follow them into the project. Deleting the fallback line alone still extracts into the project.
- **G3 is defence in depth on today's image.** Gotenberg 8.37.0 refuses `file:///etc/passwd` through a second check of its own ("not within any allowed file prefix") even under `--chromium-deny-list=^https?://.*`. The advice still drops a default security rule, older 8.x images may lack the second check, and the same paragraph wrongly says a refused URL answers `403` (F1).

If only five things are done, do these:

1. *1a*: the Gotenberg deny list and SSRF advice.
2. *1b*: MiniWord's tag-like values.
3. *1c*: nested field errors in `toFeedbackError`.
4. *1d*: the reset-mail subject.
5. The four corrected guide changes in *2a* and *2b* that the report would have shipped wrong: G15, G18, G19 and G20.

## Rules

1. **Critical is always fixed.** That means security, crashes, wrong results, and lost or corrupted content. Otherwise: fix small, document hard.
2. **A code fix starts with a test that fails on `wip`.** A finding whose test passes is dropped.
3. **Ship the shape, not the reporting app** (`AGENTS.md`). Guide prose names the mechanism. A catalogue, an invoice or an `employeeId` appears at most once, as the example.
4. **One home per explanation.** A gap whose rule already has a home gets a pointer, not a second copy (G4, G11, G14, M7).
5. **Guidance, not new defaults.** An advanced feature (concurrency) stays an opt-in line.
6. **Docs in final state**, as `AGENTS.md` asks.
7. **Versions stay your call.** Every package touched here sits in the unreleased 6.6.0 cycle, so the changes add changelog bullets only.

## Verification at a glance

| ID | Verdict | What the check found | Step |
|---|---|---|---|
| P1 | Confirmed | `Capitalize()` lower-cases then title-cases in the current culture: "AssetHub" gives "Assethub", "IBM" gives "Ibm". The test passes only because "Example" is one capitalised word. No option sets the subject | *1d* |
| P2 | Confirmed, wider | Ten `.targets` files fall back to `$(ProjectDir)`. Because `_RegiraBaseDir` is shared, Entities, Office and Setup extract into the project too once one of them is in the graph | *3a* |
| P3 | Confirmed, critical | MiniWord splits each `w:t` on `<[a-zA-Z/].*?>` and puts every piece on its own line. Loop rows, written before MiniWord runs, are hit too, so the report's `GetMiniValue`-only fix misses them | *1b* |
| P4 | Partly | `HtmlInput` and `UseAttachmentUris` docs: confirmed. `AllowAnonymous` already says "opens these endpoints" and only uses Download as an example | *2b* |
| G1 | Confirmed | Interceptor order is primers, normalizers, auto-truncate, then reactors. The table skips normalizers. The report's renumbering misses the "stages 5 and 6" sentence. Its "flags in run order" line is false | *2a* |
| G2 | Partly | Only Gotenberg wraps the band in a document. Playwright and Puppeteer place it in a `<div>`, and SelectPdf renders it as a page of its own | *2b* |
| G3 | Partly | See *Recommendation*. Recent images also offer `--chromium-deny-private-ips` | *1a* |
| G4 | Confirmed | `WithAttachments` takes a factory, not options, so the report's `FileSystemOptions.RootFolder` wording is wrong. Applies to file-system storage only | *2a* |
| G5 | Partly | The examples as written do not raise CS9107. It is a warning, both snippets are `no-compile`, and GuideVerifier suppresses warnings | *2a* |
| G6 | Partly | Rows are `Dictionary<string, object?>` on all four backends. EPPlus reads a date as `double` unless its format holds `yyyy` | *2b* |
| G7 | Confirmed | The list has five bullets, not four. Bullets 2 and 3 contradict the reactor | *2a* |
| G8 | Confirmed | The report's paging loop is wrong. A direct `IEntityService` call is never capped, and a mediator `ListQuery` is | *2a* |
| G9 | Partly | No "no Word" tip exists. A marker-row loop example exists already ("A schedule per site"). The SDK route beats hand-zipping XML | *2b* |
| G10 | Confirmed | The "no browser-less path" premise is wrong: MiniPdf and PdfPig `ImagesToPdf` exist | *2b* |
| G11 | Partly | `configure` is documented. What's missing: it receives the link entity's builder | *2a* |
| G12 | Partly | The `IImageService` DI line exists (:14). Missing: the namespace row, the pointer to `office.namespaces.md`, and the PdfPig `IPdfService` line | *2b* |
| G13 | Confirmed | An empty patch is a full UPDATE: primers, normalizers and Modified reactors run. The join check must cover `Transaction.Current` too | *2a* |
| G14 | Confirmed | The rule has a home (`instructions:474,802,1996`); the box only needs the fifth cause | *2a* |
| G15 | Partly, unsafe fix | See *Corrections* | *2a* |
| G16 | Confirmed | The four interfaces are referenced nowhere but their definitions | *2a* |
| G17 | Partly | The report's wording contradicts `instructions:522`. Separately, an interface-scoped class prepper registered twice probably runs twice (F7) | *2a*, *3b* |
| G18 | Partly, wrong fix | See *Corrections* | *2a* |
| G19 | Partly, wrong fix | See *Corrections* | *2a* |
| G20 | Confirmed, wrong fix | See *Corrections*. The roles recipe seeds "Manager", the SPA checks "Administrator" | *2b* |
| M1 | Confirmed | `fieldKey` lower-cases only the first character. The docs say "starts every key lower-case", which the code does, but nested keys miss the model's field names | *1c* |
| M2 | Confirmed | The proposed `v-if` is right. The template is generated from `entities.examples.md` §16 | *2c* |
| M3 | Confirmed | One fixed anchor is simpler than marker comments | *2c* |
| M4 | Confirmed | The API ignores `siteName` (`[Obsolete]`, pinned by a test). Removing it leaves four unused lines, and the auth docs still send it | *2c* |
| M5 | Confirmed, narrower | The defaults are in the guides already; only `get_type` lacks them. JSDoc reaches `.d.ts` | *2c* |
| M6 | Confirmed | The proposed "fetch public data in `onMounted`" contradicts the auth guide. A sign-up recipe as worded would log the password (see *Corrections*) | *3c* |
| M7 | Partly | The owned-rows recipe and `sortBy` are missing. `saveAs`, the `$auth` union and small temp ids are documented elsewhere and need pointers only | *2c* |
| S1 | Confirmed, wrong fix | See *Corrections*. The query is not in the run's logs | *2d* |
| S2 | Confirmed | Four providers have no card; RazorComponents costs two hops. The DI fact is in the guide (`office.pdf.instructions.md:108`) | *2d* |
| S3 | Not reproduced | EventPlanner never called `recommend_packages`, and the three steering lines return RazorComponents, Gotenberg and MailKit. On a full prompt Gotenberg and MailKit drop: `Score` counts a repeated word each time, and counting it once still leaves MailKit out | *2d* |

## Corrections

The report's exact changes that must not ship as written:

| ID | Report's change | Problem | Use instead |
|---|---|---|---|
| G15 | "Drop `RequireAuthenticatedUser()` from the policy" for a public catalogue | The handler succeeds every read, so every mapped entity's reads become anonymous | Per entity only: `e.Endpoints(o => o.AllowAnonymous(EntityEndpoint.List, EntityEndpoint.Search, EntityEndpoint.Details))` |
| G18 | Decimal `Sum`, `Average`, `Min`, `Max`, `GroupBy` + `Sum` and `OrderBy` translate on SQLite (EF Core 10) | On EF Core 9 (`net8.0`/`net9.0`) `Min`, `Max` and `OrderBy` on a decimal throw `NotSupportedException`: a runtime 500 | Split by target (*2a*) |
| G19 | Set-based `ExecuteUpdate … Where Stock >= qty` against lost decrements | It runs at once, outside the order's `SaveChanges`, so a refused or failed order still decrements stock; it also skips primers | One ⚠️ line pointing to `IHasConcurrencyToken`: the losing save answers 409 |
| G20 | A `UserClaimsPrincipalFactory` subclass adds the claim | With roles on, the plain factory drops the roles from the token; `AddRoles` also replaces an earlier factory registration | Derive from `UserClaimsPrincipalFactory<TUser, IdentityRole>` and register it after `AddRoles` (*2b*) |
| M6 | "No sign-up form ships: `POST users` then `login()`" | `users` is not a credential URL, so a failed sign-up logs the password. The subclass needs `[AllowAnonymous]`. A taken name answers 200 (F6) | Recipe with `credentialUrls: ["users"]` (*3c*) |
| S1 | `.Where(x => x.rank.Strong > 0)` | The wrong recipe has Strong 1: its alias `render-tree` gives "render" a whole-word hit | `Covered >= 2 \|\| terms.Count <= 2` (*2d*) |
| P2 | Delete the fallback line | With `_RegiraBaseDir` empty, the unconditioned target still creates `.regira\instructions` relative to the project | Delete the line and condition the target (*3a*) |

Smaller errors, corrected in the steps:

- **G1:** the reactors sentence and the "run order" line.
- **G2:** "the package supplies `<html>`" is Gotenberg only, and "inline styles" contradicts the SelectPdf advice.
- **G4:** the options type.
- **G6:** EPPlus dates.
- **G7:** five bullets.
- **G8:** the paging loop.
- **G9:** "one run per paragraph" and the five hand-zipped parts.
- **G10:** the browser-less premise.
- **G12:** the table to put the row in.
- **G13:** `Transaction.Current`.
- **G17:** "re-stamp" and "global".
- **M3:** marker comments.
- **M4:** the unused imports.
- **M7:** where `sortBy` goes.
- **S3:** the dilution theory.

## Found while verifying

| # | Finding | Where | Class | Step |
|---|---|---|---|---|
| F1 | "A refused one answers `403`" is true only for the page itself. A refused resource is left out and the conversion answers 200; the probe logged `value filtered` | `office.pdf.instructions.md:100`, `docs/pdf/README.md:259-260` | Wrong guide | *1a* |
| F2 | "The normalizing primer then truncates": the auto-truncate interceptor truncates | `entities.instructions.md:1482` | Wrong guide | *2a* |
| F3 | The advanced example writes raw concatenated text into `NormalizedContent`, which `instructions:1458` forbids | `entities.advanced.example.md:405` | Wrong guide | *2a* |
| F4 | An Excel export example is missing an `await` | `office.excel.examples.md:65` | Wrong guide | *2b* |
| F5 | `IPdfToImageAsyncService` is documented, but no backend implements it | `office.pdf.instructions.md:195-200` | Misleading guide | *2b* |
| F6 | `UserControllerBase.Create` answers 200 when the username exists, without creating the user or touching its password. An admin believes the account was made | `Security.Authentication.Web/Controllers/UserControllerBase.cs:39-61` | Wrong result | *3d* |
| F7 | Type-based `AddPrepper` registers with `AddTransient`; `AddValidator` uses `TryAddEnumerable` for exactly this case. An interface-scoped prepper added in two `For<>()` blocks probably runs twice per save (read, not yet run) | `Entities.DependencyInjection/Preppers/ServiceCollectionPrepperExtensions.cs:15-23,47-57` | Wrong result, to be tested | *3b* |
| F8 | The scaffold says "Administrator" is the role the back-end guides seed; the roles recipe seeds "Manager" | Modules `_template/scaffold.mjs:1050-1052`, `security.instructions.md:879` | Wrong message | *2b* (G20) |
| F9 | The composables "merge" `sortBy`; `useSearchView` sends the search object and paging only | Modules `entities.signatures.md:174`, `search-view.ts:40` | Wrong guide | *2c* (M7) |
| F10 | `how_to` "configure kubernetes ingress" returns `identity-user-entity`: "net" is a loose substring of "kubernetes" | Tools `HowToTools.cs` ranking | Wrong answer | *2d* (S1) |

## 1. Critical and security

### 1a. Gotenberg's deny list and SSRF advice (G3, F1)

Replace the `--chromium-deny-list` sentences in the Gotenberg bullet of `office.pdf.instructions.md` (:97-103) and the **URLs** bullet of `docs/pdf/README.md` (:256-262) with:

> Gotenberg's default deny list, `^file:(?!//\/tmp/).*`, refuses every `file:` URL but its own working files. A `--chromium-deny-list` replaces that default, so a pattern of your own keeps the `file:` rule. HTML that needs nothing from the network takes `--chromium-deny-list=^(https?://|file:(?!//\/tmp/)).*`. For HTML built from user input, also start the server with `--chromium-deny-private-ips`, which refuses every URL whose host resolves to a loopback, private or link-local address, cloud metadata included. Then restrict the container's outbound network. A refused page answers `403`; a refused image, frame or stylesheet is left out of the PDF. A pattern such as `^(?!data:).*` also refuses the page Gotenberg loads itself, so every conversion answers `403`.

Package: `Regira.Office` (guide), docs only.

### 1b. MiniWord's tag-like values (P3)

**Mechanism.** MiniWord 0.9.2 splits every `w:t` with `(<[a-zA-Z/].*?>|\n|\r\n)` after filling tags, keeping the tag-like pieces. Each piece becomes its own `w:t` with a `<w:br/>` between them. A trailing run can lose its `xml:space="preserve"`, and with it a space. The trigger is `<` followed by a letter or `/`, with a `>` later on the same line.

**Change** (decision 2 (a)):

1. One internal helper in `Word.Mini/Internal` inserts U+2060 (word joiner) after each `<` that starts a tag-like piece (`<(?=[A-Za-z/])`) and reports whether it changed anything.
2. It is applied to every string written as a value: `GetMiniValue`'s globals and collection rows (`WordService.cs:148-203`) and the loop rows that `MiniTemplateWalk.Fill` writes before MiniWord runs (`Internal/MiniTemplateWalk.cs:188-215`).
3. When something was neutralised, `Create` opens its output and, in every `W.Text` of `ContentRoots`, replaces `<` + U+2060 by `<`. The document's text then equals the values as given.
4. Template text is left alone: MiniWord's own `{{if(a < b)}}` uses `<`.

**Tests** in `tests/Office.Word.testing/MiniTests.cs`, written first, each asserting the extracted text and that no `w:br` was added:

- a global value `Casey <b>Customer</b>`;
- a collection row;
- a `{{#each}}` row;
- a value next to a tab, keeping its trailing space.

**Guide.** The *Word.Mini limits* paragraph in `office.word.instructions.md` (:59) gains: "A tag-like text typed in the template itself (`<b>`) is split onto lines of its own; values are written as given." The line at :346-347 ("written as text") then stands.

Packages: `Regira.Office.Word.Mini` (code), `Regira.Office` (guide).

### 1c. Nested field errors in `toFeedbackError` (M1)

**Code.** `src/vue/ui/feedback/feedback.ts:89` becomes:

```ts
// each segment of a key's path starts lower-case whatever the server's naming policy, so it matches the model
const fieldKey = (key: string) => key.replace(/(^|\.)([A-Z])/g, (_, dot: string, letter: string) => dot + letter.toLowerCase())
```

**Docs.** The JSDoc of `toFeedbackError` and the three guide lines (`ui.signatures.md:89`, `ui.instructions.md:102`, `entities.patterns.md:1064`) say "each segment of a key's path starts lower-case (`CategoryId` → `categoryId`, `Lines[0].Quantity` → `lines[0].quantity`)".

**Test.** A nested case in `__tests__/vue/entities/form-failures.test.js`, from both `errorDetails` and `errors`, read back with `fieldMessages(error, "lines[0].quantity")`.

**Changelog.** One `@regira/modules` bullet.

### 1d. The reset-mail subject (P1)

**Code.** `PasswordControllerBase.cs:64` becomes `var subject = $"{options.SiteName} Password Reset".Trim();`. `using Regira.Utilities` stays, because `Base64Encode` needs it.

**Test.** `IdentityControllerTests` gains `WithAccountEmails(o => o.SiteName = "ExampleHub")`, expecting "ExampleHub Password Reset". It fails on `wip`.

**Docs.** Already say "Example Password Reset" (`AccountEmailOptions.cs:23`, `security.instructions.md:1059`, `docs/controllers.md:134`).

Package: `Regira.Security.Authentication.Web`.

## 2. The easy fixes, one batch per area

### 2a. Entities guides (`src/Common.Entities/ai`)

- **G15** (`patterns:1559`):
  - Add "A public catalogue opens its reads per entity: `e.Endpoints(o => o.AllowAnonymous(EntityEndpoint.List, EntityEndpoint.Search, EntityEndpoint.Details))`, plus `Download` for its images; its writes stay under the policy."
  - `patterns:1263` and `instructions:1807` say it "opens chosen endpoints (a catalogue's reads, the downloads an `<img>` loads)".
  - `signatures:1935` needs nothing.
- **G18** (`patterns:590`, the "These translate on SQLite" list), and a cause in the troubleshooting row at `instructions:2018`. Add:
  > Decimal `Sum` and `Average`, grouped too, translate on every target. Decimal `Min`, `Max` and `OrderBy` (ordering by a grouped sum included) translate on `net10.0` only; EF Core 9 throws `NotSupportedException`, so order the grouped rows after `ToListAsync()`.
- **G19:**
  - After `patterns:1614`, add a ⚠️ bullet: "Two concurrent orders can read the same stock and lose a decrement; where that matters, give `Product` `IHasConcurrencyToken` so the losing save answers 409 (§Optimistic concurrency)."
  - After the aggregate example (~:555), add one sentence: a mapped-only app writes the same query as `app.MapGroup("api/dashboard").RequireAuthorization().MapGet("spend-by-month", …)` with its prefix spelled out, linking `setup:413-419` on dropping `AddControllers()`.
- **G1:**
  - In the write-pipeline table (`instructions:511-520`), rows 6 *Normalizers* and 7 *Auto-truncate* go in, *Reactors* becomes 8, and the sentence at :522 reads "stages 5 to 8".
  - The short pipeline line at :92-95 names normalizers.
  - In patterns after :1935: "A `[Normalized]` property or `e.AddNormalizer<>()` sees a value a primer set: the normalizer interceptor runs after the primer interceptor in the same `SaveChanges`."
  - At `patterns:2150`, replace "the flags are listed in run order" with "the three interceptor rows run in table order inside `SaveChanges`; reactors run after the commit; the conventions shape the model and run at no save".
  - A `how_to` marker `write-pipeline-order` goes in `instructions.md` only, under a `####` heading of its own above the table.
  - F2: :1482 names the auto-truncate interceptor.
- **G4** (`setup:504-509`): inside the same `if` block, after `EnsureDeleted()`, "with file-system storage, also delete the uploads folder the attachments' `BinaryFileService` writes to", with a pointer to *Files follow the rows* (`instructions:1621`) for why.
- **G5:**
  - Both examples (`examples:512`, `:1000`) become `(INormalizer? normalizer = null) : EntityNormalizerBase<X>(normalizer)` and use the inherited `DefaultPropertyNormalizer`.
  - *Custom Normalizer* (`instructions:1497-1501`) gains: "Pass the `INormalizer` to the base; capturing it as well raises warning CS9107. The interceptor calls `HandleNormalizeMany` once per entity type per save, and the base loops `HandleNormalize`: override it to batch a lookup."
  - F3: `entities.advanced.example.md:405` normalizes before writing `NormalizedContent`.
- **G7** (`patterns:1838-1871`): keep "Three consequences" and the three real ones. Bullets 2 and 3 open with "Without the reactor below,". The reactor moves into a paragraph after the list, and the query-filter ⚠️ into its own callout.
- **G8:** a subsection after `patterns:570`, *Export what the overview shows*:
  - Bind the overview's search object from the query and call `List(IList<TSearchObject?>, IList<TSortBy>, TIncludes?, pagingInfo: null)` on `IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>`. The keyword `Q` filter and row security run there.
  - A direct service call is never capped, so one call, no loop. A mediator `ListQuery` is clamped to `MaxPageSize`.
  - The snippet is compiled.
  - The routing line at `instructions:870` sends export here and aggregates to the DbContext section.
- **G11:** one home in the `instructions:1629` callout, with pointers from :626 and :1969. Add:
  > `configure` receives the link entity's builder, so a link validator registers there: `e.HasAttachments(x => x.Attachments, a => a.AddValidator<OrderAttachmentValidator>())`, or anywhere with `options.AddValidator<T>()`. `e.AddValidator<T>()` on the owner's builder takes only the owner's validators (CS0311).

  The snippet is self-contained or `no-compile`: the `entities` group has no shared types.
- **G13:** at the end of *Several sends in one transaction* (~:1097), outside the `entity-operations` recipe body (:854-909), so the Regira-Tools fallback copy is untouched. Add:
  > An empty patch (`JsonDocument.Parse("{}").RootElement`) is a full update: every property is marked modified, primers stamp `LastModified` and the token, normalizers, preppers and validators run, and Modified reactors fire. It is how to recompute a derived value through the mediator. A handler override opens no transaction; one that must span two saves joins any open transaction (`Database.CurrentTransaction` or `Transaction.Current`) and begins its own only when none is open.

  Add a six-line snippet.
- **G14** (`instructions:1677`): the box says "five causes" and adds "or the owner is nested inside another entity's DTO: after-mappers run for the read root only (§Step 10)", closing with the box's existing fallback.
- **G16:** four rows (`IHasUserId`, `IHasSlug`, `IHasUri`, `IHasDefault<>`) reading *(contract only)* in the table at `instructions:1897-1918`, like `IHasStartDate`. Under the table, add: "a contract only: scope an owner filter, prepper or validator to it yourself."
- **G17:**
  - At `instructions:524`: "`prepareFunc` runs after the sync; to change which rows the collection holds (add, remove, de-dupe), use an `e.Prepare(...)` registered before `Related()`."
  - At :620, a bullet beside the validator one: "A prepper's `TEntity` may be an interface or base class; it runs for every service whose entity implements it, wherever it is registered."
  - Until *3b* lands, the bullet adds "register it once".

Package: `Regira.Entities` (guides), docs only. Run the `entities` GuideVerifier group and Regira-Tools' `McpServer.Testing`.

### 2b. Office and Security guides, XML docs

- **G20** (`security.instructions.md`, Roles end-to-end):
  - Step 1 seeds `"Administrator"`, "the role the scaffolded SPA's `$isAdmin` checks, exact and case-sensitive". That makes the scaffold message (F8) true.
  - Add a short *A claim of your own in the token* recipe:
    - subclass `UserClaimsPrincipalFactory<TUser, IdentityRole>` when roles are on;
    - register it with `.AddClaimsPrincipalFactory<T>()` after `.AddRoles<IdentityRole>()`;
    - the SPA reads it with `useAuthStore().getClaimValue("…")` (`string | string[] | undefined`).
  - Prose names "a claim of your own", and the example uses one name.
- **G2:**
  - The `HeaderHtmlContent`/`FooterHtmlContent` table rows (`office.pdf.instructions.md:239-240`, `docs/pdf/README.md:169-170`) read "repeating page header (or footer): the band's markup, a fragment such as a `<div>`, not a document".
  - The Chromium bullet (:83, README :232) adds: "the Chromium backends place it inside a positioned `<div>`, so `<!DOCTYPE>`, `<html>`, `<head>` and `<body>` do not belong in it; a fragment works on every backend."
- **G6:**
  - The Read bullet (`office.excel.instructions.md:79`) reads: "each row is a `Dictionary<string, object?>`: an empty cell is `null`, text a `string`, a number a `double`, a boolean a `bool`, a date cell a `DateTime`, except on EPPlus, which reads a date as `double` unless its number format holds `yyyy`."
  - `:93` and `:111` say `object?`.
  - F4: the missing `await` at `office.excel.examples.md:65`.
- **G9** (after *1b*):
  - *No Word at hand*: build the template in code with the Open XML SDK (`WordprocessingDocument.Create`, which arrives with Word.Mini's `DocumentFormat.OpenXml` dependency), each marker in a paragraph or table row of its own, as a compiled snippet.
  - *Without a vendor licence* (:115-131) gains one line: loops and marker rows work on Word.Mini too, as in *A schedule per site* (`office.word.examples.md:65-97`).
  - No second invoice example.
- **G10** (`office.pdf.examples.md`):
  - *Preview the first page*: `Split(pdf, [new PdfSplitRange { Start = 1, End = 1 }])`, then `ToImages(...).First()`; `ToImages` renders every page it is given.
  - *Sample PDFs without a browser*: PdfPig's `PdfDocumentBuilder` (`UglyToad.PdfPig.Writer`, which arrives with `Regira.Office.PDF.PdfPig`) for text, MiniPdf for `.docx`/`.xlsx`, `ImagesToPdf` for images.
  - Both compiled in the `office-pdf` group.
  - F5: one line at :195-200: "no backend implements `IPdfToImageAsyncService` yet; use `IPdfToImageService`."
- **G12:**
  - A row `Regira.Media.Drawing.Services.Abstractions` / `IImageService` (PdfPig, DocNET) in the **PDF** table of `office.namespaces.md`.
  - "Exact usings: `office.namespaces.md`" in the PDF guide's module context.
  - The `IPdfService` → `Regira.Office.PDF.PdfPig.PdfService` registration and its `using` beside the existing `IImageService` line in the :322-327 sample.
- **P4:**
  - `HtmlInput.HtmlContent`, `HeaderHtmlContent` and `FooterHtmlContent` get summaries: "HTML fragment repeated on every page in a band of `HeaderHeight`; inline styles only, since the page's stylesheets do not reach it; page-number spans are filled where the backend supports them."
  - `UseAttachmentUris` (`AspNetAttachmentUriResolverRegistrar.cs:33-53`) names the downloads `MapEntityEndpoints()` maps beside the controllers. Its null case reads "neither mapped endpoints nor a controller serve the owner's downloads".
  - `EntityEndpointOptions.AllowAnonymous` adds "any `EntityEndpoint`; attachment endpoints follow the owner's registration", and `Excluded`/`Anonymous` get summaries.

Packages: `Regira.Office` and `Regira.Security` (guides), `Regira.Office` and `Regira.Entities.Web` (XML docs). GuideVerifier groups: `office-pdf`, `office-word`, `office-excel`, `security`.

### 2c. Regira-Modules

The `_template/entity-slice`, `app-shell` and `entity-attachments` folders are generated from the guides by `npm run build`, so their fixes go into the guide. `scaffold.mjs` is edited directly. Files stay LF.

- **M2:**
  - `entities.examples.md` §16 (:1054) gets `v-if="canEdit && (!readonly || item?.id)"`.
  - `entities.patterns.md:1292` reads "readonly keeps that button while a row is selected and opens it read-only; an empty read-only control shows none".
  - A case in `__tests__/template/input-selector.test.js`; its `FormModalButton` stub needs a marker render.
- **M3:**
  - The three injections (`scaffold.mjs:525,579,626`) anchor on `id: number = 0` through `insertAfterOrFail`, so the fields sit above the placeholder block.
  - The comment at :716 follows.
  - `__tests__/template/scaffold.test.js` asserts the fields precede `title = ""`.
- **M4:**
  - `ForgotPasswordForm.vue` in `entities.shell.template.md` (:869, :887-888) drops `siteName`, `title`, `useConfig()` and the then-unused imports, and passes `{ siteUrl }`.
  - `auth.examples.md:118` and `auth.instructions.md:140,283` stop sending it.
  - `siteName` is marked `@deprecated` in `auth-service.ts:10` and `useForgotPasswordForm.ts:26`, matching the API's `[Obsolete]`.
- **M5:**
  - JSDoc on `formatNumber`, `formatCurrency` and `formatCurrencyCompact`: "`minDigits` defaults to 2 and `maxDigits` to `minDigits`; a count is `formatNumber(n, culture, 0)`; `currency` defaults to EUR".
  - Write it as prose before any `@tag`: the scanner keeps prose only up to the first tag.
  - It reaches `get_type` after the next knowledge rebuild.
- **M7:**
  - *Owned rows with a relation and scalars*, about 25 lines after `entities.patterns.md:768`: an `InputSelector` with `:filter-defaults`; `@select` sets the foreign key, the object and a default cost on `newItem`; `pickerKey++`. Re-point `scaffold.mjs:958-960` and `entities.instructions.md:509` to it.
  - `http.instructions.md:36`: "`getFile` returns the Blob; `saveAs(blob)` from `@regira/modules/utilities/file-utility` downloads it."
  - F9: `entities.signatures.md:174` says the composables add paging only. Declare `sortBy?: Array<string>` on the SearchObject for `useSearchView` and `useListView`.
  - `entities.setup.md:933-934` points to the `$auth` union at `auth.instructions.md:298`.
  - `entities.instructions.md:150`: a temp id is a small negative integer, because `-Date.now()` overflows the server's `int` and answers 400.

One changelog bullet per change in Modules' `CHANGELOG.md`. Run `npm test` and `npm run build`, then check `git ls-files --eol`.

### 2d. The MCP server and package cards

- **S1:**
  - The filter at `mcp-server/Tools/HowToTools.cs:69` becomes `.Where(x => x.rank.Score > 0 && (x.rank.Covered >= 2 || terms.Count <= 2))`.
  - Replayed against every existing test query, every example in the tool description and the run's three real calls, it keeps their answers. It returns "No recipe matched" for the S1 query and for F10.
  - Tests in `HowToRankingTests`: a fixture recipe with alias `render-tree`, and two real-KB cases expecting no match.
- **S2:** four hand cards in the Packages repo, each in a new provider `ai/` folder (`AGENTS.md` allows one for a card) and packed by one csproj line, as `Security.Authentication.csproj:39` does (`<None Include="ai\….card.md" Pack="true" PackagePath="ai\" />`):

  | Provider | Card |
  |---|---|
  | `src/Mail.MailKit` | `mail.mailkit.card.md` |
  | `src/PDF.Gotenberg` | `pdf.gotenberg.card.md` |
  | `src/PDF.PdfPig` | `pdf.pdfpig.card.md` |
  | `src/Web.HTML.RazorComponents` | `web.html.razorcomponents.card.md` |

  Each card follows the existing shape:
  - A `# <PackageId> — index card` title.
  - A `>` line naming the hub guide with its exact `get_package(...)` call. A card switches off the parent-prose hint, so it must carry the pointer itself.
  - Bullets for the registration call, the interface it registers (`AddGotenbergPdf` → `IHtmlToPdfService`) and the namespace. PdfPig's card adds the must-knows: it needs an `IImageService`, and `GetBytes()`.

  No `office.card.md`: provider cards never link to the hub card, so it saves no hop.
- **S3:** a real-KB test checks that the EventPlanner steering lines return RazorComponents, Gotenberg and MailKit.

Run `dotnet test tests/McpServer.Testing` with an in-repo `--artifacts-path`, without stopping a running test-run server. Build the four provider csprojs to confirm the cards pack.

## 3. Medium, after the decisions

### 3a. One `.regira` folder (P2)

Decision 1 (a), in all ten files:

- `Common.IO.Storage`, `Common.Invoicing`, `Common.Media`, `Common.Payments`, `Common.Security`, `Common.System` and `Common.Web`;
- `TreeList`, `Web.Analytics` and `Web.Analytics.GeoIP2`.

In each `build/Regira.*.targets`:

1. Delete `<_RegiraBaseDir Condition="'$(_RegiraBaseDir)' == ''">$(ProjectDir)</_RegiraBaseDir>`.
2. Add `Condition="'$(_RegiraBaseDir)' != ''"` to the `…ExtractAiFiles` target, with the comment block from `Regira.Entities.targets`.

`ai/AGENTS.md:86` and `:95` describe one rule: "copied into `.regira/instructions/` at the solution root during a solution build; a build of a single project extracts nothing".

**Check by hand:** pack the hubs, and build a scratch consumer once through its solution and once as a bare csproj. Expect one folder at the solution root and none in the project. No test covers this today.

**Changelog:** one bullet per package, with **Behaviour change:** a project built without a solution no longer receives the guides.

### 3b. A prepper registered twice (F7, G17)

1. A test in the Entities tests: one interface-scoped class prepper added in two `For<>()` blocks, counting calls per save. If it runs once, F7 is dropped and *2a*'s "register it once" goes.
2. If it runs twice (decision 3 (a)), the type-based `AddPrepper` overloads (`ServiceCollectionPrepperExtensions.cs:15-23,47-57`) use `TryAddEnumerable`, as `AddValidator` does. The factory and lambda overloads cannot be compared and stay as they are.
3. **Behaviour change:** a prepper class registered twice runs once.

Package: `Regira.Entities.DependencyInjection`.

### 3c. Auth timing in the front-end guides (M6)

**Where:**
- `frontend.bootstrap.md` step 8;
- `auth.instructions.md` after :106, *Route guard*, *Show the login form on time* (~:268) and *Account UI* (~:255);
- the `onAuthenticated.ts` JSDoc;
- *Auth reload hooks* in `entities.patterns.md` (~:1360).

**What they say:**
- A cold load of a protected route while signed out issues one anonymous mount fetch per view: a 401 and an `axios error` line. Judge the console after sign-in.
- On a reload or deep link to a role-gated route, the view mounts and fetches, its `onAuthenticated` runs once on the token, and both answer 403 before the guard redirects. In-app navigation is redirected before mount. Check `hasRole` or `routeAllows` before fetching.
- `onAuthenticated` fires only while a token is held. On an `allowAnonymous` route, public data loads in `onMounted` and re-loads after sign-in through `onAuthenticated({ immediate: false })`. The rule at `auth.instructions.md:97` stands for everything else.
- A login-on-demand variant: `showLogin = (isRequired || loginRequested) && !isAuthenticated`.
- *Sign-up* (decision 5 (a)), about 15 lines:
  - `POST users`, then `login()`;
  - `credentialUrls: ["users"]` on the auth plugin, so a failed sign-up never logs the password;
  - `[AllowAnonymous]` on the app's `UserControllerBase` subclass;
  - the taken-name answer from *3d*.
- A `how_to` marker `public-pages-in-auth-app`.

Docs only. Re-run `McpServer.Testing` after the knowledge rebuild.

### 3d. Creating a user whose name is taken (F6)

Decision 4 (a):

- **Code:** drop the `FindByNameAsync` pre-check in `UserControllerBase.Create`, so `CreateAsync` answers its own `DuplicateUserName` error as a 400 through `AddIdentityErrors`.
- **Test:** a test in `IdentityControllerTests`, which fails first.
- **Changelog:** **Behaviour change:** a taken name answers 400, not 200.

Package: `Regira.Security.Authentication.Web`.

## 4. The test harness (Regira-Tools)

1. **Retro read early.** `TESTRUN.md` §4 and §5 (:142, :151, :184, :195) write each app's prompt and retro under `C:\Projects\AI\_local\prompts\<App>.txt` and `_local\retro\<App>.txt`, passed with `$(cat …)`. They are never placed in the app folder, where the build session read them.
2. **Login probe.** The §5 probe (:178) drops `head -1` and requires `"is_error":false` on the `"type":"result"` line. "Connected" showed beside `OAuth session expired`.
3. **Cost line.** It (:10-11) reads about $60 for three basic apps and $90 for three advanced ones. A recovery note covers the weekly limit: restart the server (§2), then `claude -c -p "continue and finish …"` in the app folder, logged to `<App>-resume.jsonl`.
4. **Health check.** The `/health` check (:60-62) runs before every launch, resume and retro, not only the first.
5. **App folder wording.** All 18 prompts in `prompt-create-apps.txt` and `prompt-create-apps-adv.txt` drop the folder from "Create an app in X/", for example "Create the Fleet app (back-end and front-end), which …".
6. **Unmeasurable asks.** "Include total cost if possible" goes from line 10 of both prompt files. §4 step 4 of the steering says whether a Word template may be generated in code. The prompts name the primary entity, who "Administrators" and "Employees" are, and the mandatory-reserve rule.
7. **Leftover guide folders.** Covered by *3a* once published. Until then, the prompts add `.regira/` to each app's `.gitignore`.

## Not proposed

- **D1** (scaffold from a field list, `--owns` with `--rel`): medium to large, and M3 and M7 remove the trap the run hit.
- **D2** (`useRegisterForm`, `requestLogin()`): decision 5 (b). Its best argument is registering `users` as a credential URL by construction. `requestLogin()` needs a new flag, because `authRequired` is never cleared.
- **D3** (skip the mount fetch on a disallowed route): the entities composables do not import auth, so it means coupling or a new fetch predicate. With the token-arrival re-fetch still there, it removes at most one 403. *3c* first.
- **D4** (files of a creating request stored under owner `0`): large; documented already.
- **D5** (a core-headings read mode for `entities.instructions.md`): a knowledge-server design question of its own.
- **D6** (minimal-API account endpoints): large; the controllers stay the account surface.
- **D7** (small helpers: `Touch`, `RunInTransaction`, `e.SequentialCode`, `ToImage(pdf, page)`, `MmToPt(int, int)`, multi-name `get_type`): each adds API without a defect behind it. *2a*'s G13 shows the empty patch, and *2b*'s G10 the first-page preview.
- **D8** (eager `defineStore(Entity.name)` and barrel cycles): needs its own evaluation. The guide's deep-import cure stands.
- **A backend for `IPdfToImageAsyncService`:** F5 is documented instead.
- **Literal tags in Word.Mini template text:** documented in *1b*, not neutralised, because MiniWord's own `if` syntax uses `<`.

## Outcome

Built on Packages `wip` at `e0c3535`, Modules at `b5ddae2` and Tools at `cbe6801`, uncommitted. Every decision took its recommended option: 1 (a), 2 (a), 3 (a), 4 (a) and 5 (a). The order of importance: security, keep it simple, best practices. Each code fix started with a test that failed on the old code.

**Built**

- **Step 1.** *1a* adds one sentence: a `--chromium-allow-list` match bypasses `--chromium-deny-private-ips`, as Gotenberg's `--help` states. *1b* is the internal `TagLikeText` helper: globals, collection rows and `{{#each}}` rows are neutralised, and the restore rewrites a document only when a joiner is present. *1c* and *1d* as written.
- **Step 2.** *2a*–*2d* as written, apart from the deviations below. F7 (*3b*) ran twice, so G17 carries no "register it once".
- **Step 3.** *3a* in all ten targets; a scratch project confirmed a bare build writes no `.regira` and a solution build one folder at the root. *3b* `TryAddEnumerable` for the type-based overloads. *3c* in the Modules auth guides. *3d* drops the pre-check.
- **Step 4.** Items 1–7. The prompts' role and reserve wording was reviewed in round 38. RoomPlanner names an Administrator as the approver. The advanced Blog and RoomBooking prompts became `BlogAdv` and `RoomPlannerAdv`.

**Deviations**

- **S3 not applied.** `Score` has no `.Distinct()`; adding one brings Gotenberg back on a full prompt but still pushes MailKit out (6th to 7th). The test pins the steering-line result instead.
- **S1 widened.** The `Covered >= 2 || terms <= 2` gate turned away questions ("how do I seed the database"), so `how_to` also drops the question words `how`, `use`, `using`, `can` and `does`, and counts `Covered` on whole words.
- **`HtmlInput`** says a band "carries its own styles", not "inline styles only": SelectPdf's `body{margin:0}` needs a style block.
- **The PdfPig card** registers `IPdfService` as a singleton, as the guide does; the service is stateless.
- **The self-registration example** in `security.instructions` no longer checks whether the account existed: the base answers `200` only for an account it created. The guide says a public sign-up's `400` tells whether a name is taken.

**Tests**

- Packages: Office.Word 621 (+7 skipped), Web.Security 211 on net8.0 and 222 on net10.0, Entities 582 on net8.0 and 585 on net10.0, Entities.DependencyInjection 140, Entities.Web 276, Entities.Mediator 26, Office.PDF 148 (+15 skipped), Office.Excel 77. GuideVerifier: every group compiles.
- Modules: Vitest 48 files, 573 tests; `vue-tsc --noEmit` clean.
- Tools: McpServer.Testing 479, against the rebuilt knowledge base.

**Pending:** deploy the rebuilt knowledge base. Its checks passed: `get_package_card` answers from the four provider cards, `get_type formatNumber` shows the defaults, and `how_to` answers no recipe for the S1 query.

**Kept:** the restore after MiniWord reads the written document, not the input: loop rows can nest and hold objects or JSON written through `ToString()`, so a check of the input's strings would miss a neutralised value and leave a joiner behind. A test pins a nested loop row.

## Decisions

1. **Guide extraction (*3a*).**
   - (a) Every hub extracts only in a solution build, as Entities, Office and Setup already do. *Recommended:* one folder, one rule, and the MCP server is the primary channel (`ai/AGENTS.md:78`).
   - (b) Also look for a solution file in the project's folder and its parent, so `dotnet run --project` in the usual layouts still extracts once. More MSBuild in ten files.
   - (c) Leave it and document the second copy.
2. **MiniWord values (*1b*).**
   - (a) Neutralise, then restore the text after MiniWord. *Recommended:* the document's text equals the values.
   - (b) Neutralise only. Simpler, but an invisible U+2060 stays after each such `<` and rides into `GetText`, search and copy-paste.
3. **Prepper registration (*3b*).**
   - (a) Test, then `TryAddEnumerable` for the type-based overloads. *Recommended:* the validators' rule.
   - (b) Document "register it once".
4. **A taken username (*3d*).**
   - (a) 400 with Identity's `DuplicateUserName`. *Recommended:* the admin sees the refusal. A public sign-up then reveals taken names, as Identity's own error does.
   - (b) Keep 200 and document it. The sign-up recipe then cannot tell a taken name from success.
5. **Sign-up (*3c*).**
   - (a) A guide recipe now; D2 later if apps keep asking. *Recommended.*
   - (b) Build `useRegisterForm` (D2) instead.

## How every step is tested

- **Baseline first.** Run each step's test projects on `net8.0` and `net10.0` before touching code, and note anything already red. Mail tests reach live Mailgun, so run offline with `--filter "TestCategory!=Network"`.
- **Code fixes: a failing test first.** That covers *1b* (`MiniTests`), *1c* (`form-failures.test.js`), *1d* and *3d* (`IdentityControllerTests`), *3b* (Entities), and S1 and S3 (`McpServer.Testing`).
- **Gotenberg.** Re-run the five-configuration probe (an iframe on `file:///etc/passwd`, and plain HTML) with the pattern from *1a*. Add a `--chromium-deny-private-ips` case on a private address, and check Gotenberg's log for `denied list` and `value filtered`.
- **Docs compile.** Run `dotnet run --project tools/GuideVerifier -- . --group <group>` for every group a step edits: `entities`, `office-pdf`, `office-word`, `office-excel`, `security`.
- **Regira-Tools after every Packages guide edit.** `HowToTools.cs` keeps word-for-word fallbacks of some `entities.patterns` recipes. Run `dotnet test tests/McpServer.Testing` with an in-repo `--artifacts-path`, never stopping a running test-run server.
- **Regira-Modules.** `npm test`, then `npm run build` to regenerate the templates, then `git ls-files --eol` for LF.
- **Knowledge rebuild last.**
  - `get_package_card` answers from the four new cards.
  - `get_type formatNumber` shows the defaults.
  - `how_to "render an HTML badge PDF with a footer through Gotenberg"` answers no recipe.
- **Changelog.** Each step adds its packages' bullets under *Unreleased*. **Behaviour change:** clauses go on *3a*, *3b* and *3d*.

<!-- {% endraw %} -->
