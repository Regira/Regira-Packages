# Changelog

Release summary for the Regira NuGet packages — newest first. Every change that ships in a package
adds one bullet under **Unreleased** in the same change (format: `` `PackageId` x.y.z — summary ``),
and leaves that package's `<Version>` higher than its last published version. At publish time the
Unreleased block becomes a dated release heading.

## Unreleased

- All packages — initial public release on nuget.org (repository split from the private Regira-Codebase).
- Most packages — documentation accuracy pass: package READMEs, the `Common.Office` topic docs and the shipped `ai/` guides were re-verified against source; broken and misleading code samples corrected (DAL.MySQL/MongoDB/PostgreSQL, IO.Storage, Office Mail quick start), non-existent APIs removed from docs (`DrawImageLayerDto`, `LengthUnit.Pixels`/`Centimeters`, `Regira.Entities.Web.FastEndpoints`, `PaymentMeansCode` 31/58), and behavior claims aligned with the code (PBKDF2 iteration count, Excel `headers`/`DateFormat` per backend, OCR empty-string results, Puppeteer Letter default, Spire PDF capabilities).
- `Regira.DAL.PostgreSQL` — `PgRestoreService` now injects `ILogger<PgRestoreService>` instead of `ILogger<PgBackupService>`, fixing DI logger-category resolution.
- Most packages — README code samples made self-contained where practical and are now compile-verified: `tools/GuideVerifier` grew from the 4 Entities `ai/` folders to 14 snippet groups covering every package README and the Office topic docs (124 snippets compiling).
- `Regira.Office.OCR.PaddleOCR` — upgraded to the PP-OCRv5 models (`Sdcb.PaddleOCR.Models.LocalV5`); the `lang` parameter now selects the model for the language's script (Latin/Chinese/Korean/East Slavic/Greek/Thai).
