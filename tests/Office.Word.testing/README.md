# Office.Word.testing

Tests for the Word backends [Word.Aspose](../../src/Word.Aspose/README.md),
[Word.Gotenberg](../../src/Word.Gotenberg/README.md), [Word.Mini](../../src/Word.Mini/README.md),
[Word.Spire](../../src/Word.Spire/README.md) and [Word.Syncfusion](../../src/Word.Syncfusion/README.md). NUnit.

## How the suite is built

- **Shared scenarios.** `Abstractions/WordTestsBase` holds the scenarios every backend runs: templating, conditional
  blocks, headers and footers, merging, input options, conversion, page images, extraction and the document builder.
  A backend fixture is marked `[WordFixture]` and gets all of them, apart from those that `[Needs]` a feature it
  `[LeavesOut]` — Word.Mini leaves out converting, for one, with the reason beside it. A left-out scenario does not
  appear in the results. A fixture overrides a scenario only where its backend behaves differently, and declares the
  case lists that differ per backend (`SourceFiles`, `OutputFormats`).
- **Measured results.** A scenario reads what it produced with `DocxFacts`, `PdfFacts` or `ImageFacts`
  (`Abstractions/Facts`): the Open XML SDK, Docnet and SkiaSharp, never the backend under test, whose own reader renders
  its input as a template again. It asserts on text, leftover tags (every story, VML fallback copies included), table
  rows, picture hashes and sizes, page and section breaks, page counts and sizes, and how much of a rendered page is
  inked.
- **Templates in code.** `Abstractions/Docx` builds the small templates a scenario needs beside its assertions; the
  binary assets in `Assets/Input` are for what code cannot easily build.
- **Outputs.** Every file a scenario produces is saved as `Assets/Output/{Backend}/{test name}`, with the facts it
  was read for beside it as `{file}.facts.json`, and attached to the test result. `OutputSetUp` empties
  `Assets/Output` at the start of a run. Diff two runs' facts to see what a vendor upgrade or a change moved.

`DtoExtensionsTests` round-trips the `WordTemplateInput` an Office client sends as JSON. `TemplateBlocksTests`
covers the block syntax every backend shares, conditions and loops, and `PictureScalingTests` the rule every
backend scales a picture by.
`TextBoxFallbacksTests` covers Word.Spire's rewrite of a text box's fallback copy.

## Running

```bash
dotnet test tests/Office.Word.testing
```

- `Containers`: `GotenbergTests` runs against a real Gotenberg server. `GOTENBERG_URL` points it at a running one;
  otherwise it starts a `gotenberg/gotenberg:8` container in Docker when `REGIRA_PROVIDER_TESTS=containers` (set by
  `Regira.runsettings`), and skips when neither applies or Docker is unavailable. `REGIRA_CONTAINER_REUSE=1` keeps
  the container between runs (see [CONTRIBUTING.md](../../CONTRIBUTING.md)). `GotenbergUnitTests` needs no server.
- `License`: `AsposeTests`, `AsposeEvaluationTests` and `SyncfusionTests` need no licence; unlicensed, they run on
  evaluation or trial output, which adds a banner paragraph and marks headers and footers; no scenario counts
  paragraphs, so the banners change no outcome. The tests proving a
  licence lifts those limits skip unless one is configured: `ASPOSE_WORDS_LICENSE` (the licence file, Base64) or
  `ASPOSE_WORDS_LICENSE_PATH` for Aspose, the `SyncFusion:LicenseKey` user secret or `SYNCFUSION_LICENSE_KEY` for
  Syncfusion. `AsposeEvaluationTests` skips when an Aspose licence is configured.

Skip the container suite with `--filter "TestCategory!=Containers"`.
