# Changelog

Release summary for the Regira NuGet packages — newest first. Every change that ships in a package
adds one bullet under **Unreleased** in the same change (format: `` `PackageId` x.y.z — summary ``),
and leaves that package's `<Version>` higher than its last published version. At publish time the
Unreleased block becomes a dated release heading.

## Unreleased

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
