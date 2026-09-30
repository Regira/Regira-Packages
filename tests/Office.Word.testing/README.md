# Office.Word.testing

Tests for the Word backends [Word.Aspose](../../src/Word.Aspose/README.md),
[Word.Gotenberg](../../src/Word.Gotenberg/README.md), [Word.Mini](../../src/Word.Mini/README.md),
[Word.Spire](../../src/Word.Spire/README.md) and [Word.Syncfusion](../../src/Word.Syncfusion/README.md). The shared
scenarios in `WordTestsBase` (templating, conditional blocks, headers and footers, merging, conversion, page images,
text and image extraction) run against every backend that supports them; the rest are skipped per backend. NUnit.

## Running

```bash
dotnet test tests/Office.Word.testing
```

- `Containers`: `GotenbergTests` runs against a real Gotenberg server. `GOTENBERG_URL` points it at a running one;
  otherwise it starts a `gotenberg/gotenberg:8` container in Docker when `REGIRA_PROVIDER_TESTS=containers` (set by
  `Regira.runsettings`), and skips when neither applies or Docker is unavailable. `REGIRA_CONTAINER_REUSE=1` keeps
  the container between runs (see [CONTRIBUTING.md](../../CONTRIBUTING.md)). `GotenbergUnitTests` needs no server.
- `License`: `AsposeTests`, `AsposeEvaluationTests` and `SyncfusionTests` need no licence; unlicensed, they run on
  evaluation or trial output. The tests proving a licence lifts those limits skip unless one is configured:
  `ASPOSE_WORDS_LICENSE` (the licence file, Base64) or `ASPOSE_WORDS_LICENSE_PATH` for Aspose, the
  `SyncFusion:LicenseKey` user secret or `SYNCFUSION_LICENSE_KEY` for Syncfusion. `AsposeEvaluationTests` skips
  when an Aspose licence is configured.

Skip the container suite with `--filter "TestCategory!=Containers"`. Output goes to `Assets/Output/{Backend}`.
