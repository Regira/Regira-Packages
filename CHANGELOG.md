# Changelog

Release summary for the Regira NuGet packages — newest first. Every change that ships in a package
adds one bullet under **Unreleased** in the same change (format: `` `PackageId` x.y.z — summary ``),
and leaves that package's `<Version>` higher than its last published version. At publish time the
Unreleased block becomes a dated release heading.

## Unreleased

- `Regira.Common` 6.1.3 — `MemoryFileExtensions.GetStream()` no longer leaves the backing stream at EOF: it restored neither the source position nor the copy's, so a second `GetStream()` on the same file handed a sequential reader zero bytes (an empty attachment download over HTTP, with a correct `Content-Length`).
- `Regira.Entities` 6.1.2 — packed guides: worked *Role-gated write authorization filter* recipe (allow-list keyed on the controller, with the `POST search`/`POST list` read overloads and the separate attachment controller called out); the response-shape section now carries populated JSON for the search/save/400 envelopes and the attachment link DTO, whose file metadata nests under `attachment`; the *Feature recipes* index now lists every recipe in the patterns guide; EF translation hazards extended with method calls in projections and provider-specific `EF.Functions`; one `EntityAttachment` subclass per owner stated explicitly; version guidance changed from a `6.0.0` pin to a major-6 constraint resolved at add time.
- `Regira.Setup` 6.1.2 — packed guides: *Standard hosted API with auth* added to the template-selection tables (`BasicApi` plus `SelfHostingApiWithAuth`'s auth registrations), the shape an authenticated Entities API actually takes.
- `Regira.Web` 6.1.2 — packed guide: `UseCentralRoutePrefix` now names its package and namespace at the call site.

## 6.1.1 — 2026-08-12

- All packages — version aligned at 6.1.1 for a whole-family publish.
- `Regira.Web.Swagger` 6.1.1 — `Microsoft.OpenApi` pinned back to 2.11.0 on net10, matching the floor `Regira.Security.Authentication.Web` sets, so an app referencing both resolves one version instead of a 2.x/3.x conflict.
- `Regira.Entities.DependencyInjection` 6.1.1 — startup validation warns when an attachments owner's registered input DTO cannot carry the `Attachments` collection (adds/removes/reorders through the parent are otherwise dropped silently, 200 OK).
- `Regira.Entities` 6.1.1 — packed guides: mapped attachment owners must declare the collection on the input DTO; role-gated server-owned state pattern; who-flushes-what on wrapping services; `HasAttachment` is never populated; period/slug/uri interfaces documented; `Microsoft.OpenApi` pin 2.11.0; startup-validation catalogue lists all nine checks.
- `Regira.Security` 6.1.1 — packed guides: *Roles end-to-end* recipe (Identity → JWT → SPA — `AddRoles` plus `ClaimsIdentity.RoleClaimType = "role"`, without which Identity tokens carry no gate-satisfying role claims) and a new package index card.
- `Regira.Setup` 6.1.1 / `Regira.Security.Authentication.Web` 6.1.1 — packed guide corrections: template-selection wording, `Microsoft.OpenApi` pin 2.11.*, the version-floor bullet now names 2.11.0 and staying on 2.x.
- `Regira.Entities.EFcore` 6.1.1 — licensing corrected: the package belongs to the commercial Entities product line (6.1.0 shipped it Apache-2.0 by mistake); it now ships the Regira Commercial License and the free-tier description suffix. The other six commercial packages republish at 6.1.1 with the corrected packed license scope; `Regira.Entities` updates the licensing matrices and README wording.

## 6.1.0 — 2026-08-11

- All packages — licensing made explicit per package: the ~68 packages without license validation are now Apache-2.0 (SPDX `PackageLicenseExpression`; repo root `LICENSE` is the Apache-2.0 text, plus a `NOTICE` file); the six license-validating packages (`Regira.Licensing`, `Regira.Entities.DependencyInjection`, `Regira.Entities.Web`, `Regira.Entities.Mapping.Mapster`, `Regira.Entities.Mapping.AutoMapper`, `Regira.Office.Clients`) ship the Regira Commercial License as `REGIRA-COMMERCIAL-LICENSE.md` (source: `legal/`) and say so in their descriptions. The commercial license text now grants redistribution in compiled form, defines simple/complex entity registrations, covers `regira.mcp` (30 req/60 s), states free-tier perpetuity per released version, and aligns tier naming with regira.com/licensing.
- All packages — every package README (committed and generated) now carries an accurate `## License` section; generated stubs state per package whether any key validation exists.
- All packages — SourceLink enabled (`PublishRepositoryUrl` + `EmbedUntrackedSources`); snupkg symbols now resolve sources from the public GitHub repo.
- `Regira.Licensing` 6.1.0 — the embedded license public key is rotated from the dev/test key to the production key.
- NuGet metadata tuning for discoverability: the four Excel packages name spreadsheet import/export (and, for NpoiMapper, mapping) in their descriptions and tags; the barcode packages add `qr-code`/`barcode`/`scanner`/`generator` tags; `Regira.IO.Storage` adds `file-storage`; the hub packages (`Regira.Entities`, `Regira.Office`, `Regira.Setup`) are tagged `mcp`/`ai-agents`; generated package READMEs link the hosted MCP server, and the `Regira.Entities` README documents AI-assisted setup.
- Version aligned at 6.1.0 for a whole-family publish — a minor bump because this release changes license terms and rotates the license-signing key.

## 6.0.2 — 2026-08-08

- All packages — version aligned at 6.0.2 for a whole-family publish.
- `Regira.Entities.EFcore` 6.0.2 — a required relationship the change tracker refuses to sever is now reported as a constraint conflict (409) instead of an unhandled `InvalidOperationException`. EF resolves that case client-side, before the provider is reached, so it never arrived as the `DbUpdateException` the constraint path catches — a hierarchy that eager-loads its own children hit it on every parent delete. Both throw sites are covered (`Remove`, and `SaveChanges` under deferred cascade timing), including the `Attachment` principal removed by `EntityAttachmentWriteService`. Adds `InvalidOperationExceptionExtensions.IsSeveredRequiredRelationship`.
- `Regira.Entities` 6.0.2 — the packed `Order + OrderLine` example no longer rejects a partial update that omits the child collection: `Modify` validates only an explicitly emptied collection, matching the `Related()` contract the same snippet documents. Adds delete-behaviour guidance for a required self-referencing foreign key, and names the literal parameter in every MCP call example.
- `Regira.Setup` 6.0.3 — the packed setup guides name the literal parameter in every MCP call example.
- `Regira.TreeList` 6.0.2 — the packed examples name the literal parameter in every MCP call example.

## 6.0.1 — 2026-08-06

- All packages — version aligned at 6.0.1 for a whole-family publish.
- `Regira.Setup` 6.0.2 — the packed `copilot-instructions.md` no longer routes a "NuGet feed" setup concern to `shared.setup.md`; packages install from nuget.org with no feed configuration.
- `Regira.Office.PDF.SelectPdf` 6.0.1 — ships a `buildTransitive` targets file that copies SelectPdf's native `Select.Html.dep` into the consumer's build and publish output. NuGet does not flow the upstream package's `contentFiles` through a transitive reference, so every consumer built cleanly and then threw `Conversion failure. Could not find 'Select.Html.dep'` on the first conversion. Opt out with `RegiraSelectPdfCopyNativeDep=false`.
- `Regira.Office.OCR.Tesseract` 6.0.1 — ships a `buildTransitive` targets file that copies `tessdata/eng.traineddata` into the consumer's build and publish output, for the same reason. The trained data now packs once at a fixed `tessdata/` path instead of four times via the automatic `contentFiles` layout (7.5 MB → 1.9 MB), and `tessdata/readme.md` is no longer shipped to consumers. Opt out with `RegiraTesseractCopyTessData=false`.
- `Regira.Office` 6.0.1 — packed guides: results are read with `GetBytes()`, never `.Bytes`. `IMemoryFile` carries either bytes or a stream depending on the method (DocNET's `Split` returns bytes, its `Merge(IEnumerable<IMemoryFile>)` a stream), so `.Bytes` was null for half the API and produced a 200 with an empty body; the PDF, Word, mail, OCR and barcode examples used it.
- `Regira.IO.Storage` 6.0.1 — packed guide and README: `GetBytes()`/`GetStream()` are documented under `MemoryFileExtensions` (`Regira.IO.Extensions`, in `Regira.Common`) rather than under a `BinaryFileExtensions` heading, with the empty-body trap stated where `Save` is described.
- `Regira.Media` 6.0.1, `Regira.System` 6.0.1, `Regira.Web` 6.0.1 — packed guides use `GetBytes()` on `IMemoryFile` results, consistent with the rule above.
- `Regira.Entities` 6.0.1 — packed guides: the non-owned child aggregate recipe is a top-level pattern reachable from the relationship decision table; a dependent's archived query filter also hides its rows from the parent's aggregate recompute (`IgnoreQueryFilters()`, scoped by the parent FK); after-mappers run for the read root only, so a field needed on a nested DTO belongs on the entity; an earlier seed wave is re-read detached, not through `Details()`; seed checks cover state ratios, not only `count: 0` invariants; report endpoints project a `GroupBy` into an anonymous type and prefer a join over a correlated `SelectMany`; the shared `Attachment` base costs no licence slot; `OrderOrThenBy` chains within one `SortBy` arm.

## 6.0.0 — 2026-08-05

- All packages — initial public release on nuget.org (repository split from the private Regira-Codebase).
- Most packages — documentation accuracy pass: package READMEs, the `Common.Office` topic docs and the shipped `ai/` guides were re-verified against source; broken and misleading code samples corrected (DAL.MySQL/MongoDB/PostgreSQL, IO.Storage, Office Mail quick start), non-existent APIs removed from docs (`DrawImageLayerDto`, `LengthUnit.Pixels`/`Centimeters`, `Regira.Entities.Web.FastEndpoints`, `PaymentMeansCode` 31/58), and behavior claims aligned with the code (PBKDF2 iteration count, Excel `headers`/`DateFormat` per backend, OCR empty-string results, Puppeteer Letter default, Spire PDF capabilities).
- `Regira.DAL.PostgreSQL` — `PgRestoreService` now injects `ILogger<PgRestoreService>` instead of `ILogger<PgBackupService>`, fixing DI logger-category resolution.
- Most packages — README code samples made self-contained where practical and are now compile-verified: `tools/GuideVerifier` grew from the 4 Entities `ai/` folders to 14 snippet groups covering every package README and the Office topic docs (124 snippets compiling).
- `Regira.Office.OCR.PaddleOCR` — upgraded to the PP-OCRv5 models (`Sdcb.PaddleOCR.Models.LocalV5`); the `lang` parameter now selects the model for the language's script (Latin/Chinese/Korean/East Slavic/Greek/Thai).
