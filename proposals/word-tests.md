# Measurable Word tests
<!-- {% raw %} -->

As of 2026-10-05. Sources: the `Regira-Packages` repository, branch `wip` at `e8b54a0`; a baseline run of `tests/Office.Word.testing` without the `Containers` category (358 passed, 7 skipped, 31 s wall clock, no vendor licence); and throwaway probes that read the files that run wrote and drove Word.Spire, Word.Aspose, Word.Syncfusion and Word.Mini directly. The Gotenberg container suite was not run; the files in `Assets/Output/Gotenberg` date from 2026-10-04. Nothing on this page has been built.

**Status: built on 2026-10-05, uncommitted; the four decisions are answered (see *Decisions*) and *Outcome* records what the build found. Findings 1, 2 and 5 were fixed in separate changes, 3 and 4 with the build.**

## Recommendation

Make every test that writes a document assert on what the document holds, read by something other than the backend under test. Today 47 of the 358 executed cases check only that a file exists and is not empty, or that a list is not empty. Most of the rest read their result back through the backend's own `GetText`, which renders its input as a template again. Read independently, the files show five defects that the suite passes over.

Three changes, in order:

1. **One independent reader.** `DocxFacts`, `PdfFacts` and `ImageFacts` read a produced file with the Open XML SDK, Docnet and SkiaSharp. The test project already references all three. Every scenario asserts on these facts.
2. **Attributes on the shared scenarios.** `[Test]` moves onto `WordTestsBase`, and a backend leaves out, with a reason, the features it lacks (decision 1). The 145 forwarding overrides go, and a new shared scenario runs on every backend by default.
3. **In-code inputs where an asset adds nothing.** Two nesting tests take 40–58 % of each full backend's fixture time, and in-code templates run them in a fraction of it.

These are test-only changes. The test project is not packable, so no `<Version>` or `CHANGELOG.md` entry moves.

## What exists today

| What | Where | Effect |
|---|---|---|
| Shared scenarios carry no attributes | `WordTestsBase` has 40 virtual scenario bodies. Each fixture re-declares the ones it runs: Spire 40, Aspose 40, Syncfusion 40, Mini 21, Gotenberg 4 | A scenario a fixture leaves out is not run, and nothing reports it. Six that Word.Mini could reach are not declared, and no comment says why. Run against Mini, three fail by design (a binary `.dot`, titled collection tables), one passes, one passes vacuously (`Replace_Image`) and one crashes it (finding 2) |
| Existence-only assertions | `AssertSaved` (the file exists, length > 0) is the only check in `From_File`, `Replace_Image`, `Convert_To`, `From_A3_To_Pdf` and `From_A4_To_Pdf_A3`, and in Spire's `Extern_Document_Inherit_Font`, `From_JsonFile` and `Invoice_Advanced`: 40 cases. `To_Images` and `GetImages` check `count > 0`: 7 more | Word.Mini's `Replace_Image` output still holds the template's placeholder picture, byte for byte, and passes. `From_A4_To_Pdf_A3` never reads a page size |
| Results read back through the backend | `HasContent` and `ReadText` call the backend's `GetText` | `GetText` renders its input as a template first. On all four backends, a file that still holds `{{#if X}}` … `{{/if}}` reads back without the markers and without what they enclose. So the "no `{{` left" assertions read this way cannot fail, and Spire's invoice test missed raw tags (finding 1) |
| `InputOptions` unasserted | `InheritFont`, `HorizontalAlignment`, `EnforceEvenAmountOfPages`, `RemoveEmptyParagraphs` | No test asserts any of the four. `Merge` sets two of them and checks neither. Measured: `InheritFont`, `EnforceEvenAmountOfPages` and `RemoveEmptyParagraphs` take effect on Spire, Aspose and Syncfusion; `HorizontalAlignment` was not measured |
| Four template builders | `Docx` (Abstractions) builds the block scenarios. `WordTestsBase.Document`, `AsposeTests.DocxWithParagraphs` and `MiniTests.SpacedTagsTemplate` do the same job | `Docx.TextBox` writes DrawingML only. Word writes a text box as `mc:AlternateContent` with a VML `mc:Fallback` copy, and no scenario has one |
| Per-backend copies | `Page_Settings_Set_The_Rendered_Page_Size`, `Convert_To_Html_Is_Self_Contained`, `Convert_To_Image_Points_At_ToImages`, `ToImages_Returns_One_Image_Per_Page` and `DocumentBuilder_Builds_Paragraphs_And_Headers` are copied between Aspose, Syncfusion and Gotenberg. `FirstPageSize` exists twice, and Syncfusion inlines it a third time | Spire runs none of them. Its nearest are `DocumentBuilderTests`, with weaker assertions, and `Free_Edition_Pdf_Stops_After_Three_Pages_While_Images_Cover_Every_Page` |
| One realistic template, on one backend | `SpireTests.Factuur` and `Invoice_Advanced` | The invoice data is written out twice. Each test copies the template into `%TEMP%` and never deletes the copy. The only template with text boxes, nested `.dotx` parts and headers runs on Spire alone |
| Output files | `Assets/Output/{Backend}/{hand-picked name}`. `OutputPath` deletes only the file it is about to write. `DocumentBuilderTests` writes into Spire's folder | A renamed test leaves its old file behind, and nothing links a file to the test that wrote it |
| Unused assets | `add_body_footer.docx`, `image_to_insert.docx`, `table_to_insert.docx`, `lorem-10-pages.docx`, `template.pdf`, `Factuur/header-inline.dotx` | No test reads them |
| Untested code | `WordImageCreator` (page selection), `DtoExtensions` (the wire format `WordClient` sends) | See finding 5 |

Time per fixture in the baseline run. Fixtures run in parallel, so the wall clock is the slowest one:

| Fixture | Cases | Time | `Nested_Documents_Do_Not_Wear_Out_The_Service` + `A_Template_That_Includes_Itself_Fails` |
|---|---|---|---|
| `SyncfusionTests` | 64 | 30.2 s | 17.6 s |
| `AsposeTests` | 72 | 26.4 s | 13.8 s |
| `SpireTests` | 63 | 21.7 s | 8.5 s |
| `DocumentBuilderTests` | 4 | 12.7 s | — |
| `AsposeEvaluationTests` | 4 | 6.1 s | — |
| `GotenbergUnitTests` | 64 | 4.3 s | — |
| `MiniTests` | 38 | 3.2 s | — |
| `ConditionalBlocksTests`, `AsposeLicenseTests` | 56 | 0.8 s | — |

## What measuring found

Each row was read from a file the suite or a probe wrote, using the Open XML SDK, Docnet or SkiaSharp rather than the backend that wrote it.

| # | Finding | Measured | Why the suite passes |
|---|---|---|---|
| 1 | **Word.Spire leaves raw tags in a text box's VML fallback.** | `Factuur/factuur.docx` filled by Spire: two paragraphs in `mc:Fallback` still read `{{ customer.title }}` and `{{ customer.street }}{{ customer.address }}`, while the `mc:Choice` copy is filled. Aspose and Syncfusion leave none | `Factuur` reads back through Spire's `GetText`, which reads the DrawingML copy. Word 2010 and later show that copy, but any Open XML text extraction, and any reader of the fallback, sees the tags |
| 2 | **Word.Mini throws `RegexParseException` for a parameter key holding `[`.** | `A_Parameter_Key_Is_Matched_Literally` against Mini: `Invalid pattern '.*{{(x[1(\.\w+)+)}}.*'`. MiniWord builds a regex from the key, and Word.Mini passes keys through unchecked | `MiniTests` does not declare the scenario |
| 3 | **Word.Aspose's `DocumentBuilder` writes US Letter pages.** | `document_builder.docx` has `w:pgSz` 12240 × 15840 twips. The Spire and Syncfusion builders write A4 (11906 × 16838) | The builder tests count pages, not their size |
| 4 | **`Merge` lays documents out differently per backend.** | Three one-paragraph documents merged, then converted to PDF: Spire 3 pages, Aspose 3, Syncfusion 1. `doc-1.docx` + `doc-2.docx`: Spire 2, Aspose 3, Syncfusion 2. Word.Aspose sets `SectionStart.Continuous` with the comment "without this every appended document starts on a new page", and each document still starts a new page | `Merge` only checks that text from both inputs is present |
| 5 | **`WordClient` drops `InputOptions`, picture size and alignment.** | `WordDocumentInputDto` has no `Options`, and `DtoExtensions` copies only a picture's name and bytes. `WordClient.Convert` sends `PageSize` but neither orientation nor margins | Untested; found while checking `DtoExtensions` coverage |

Measured without a defect: the replaced picture is `sample1.jpg` byte for byte on Spire, Aspose and Syncfusion. Every A3 PDF measures 841–842 × 1190 pt. `To_Images` renders two A4-proportioned pages (height/width 1.414), with 27–32 % ink. Every `Convert_To` output's magic bytes match its format. `RemoveEmptyParagraphs` removes 3 of 3 empty paragraphs. With `InheritFont`, the nested document's runs take the parent's Normal size: 22 half-points where the source had 18 and 20, a few runs excepted. With `EnforceEvenAmountOfPages` on every input, the three-document merge comes out at 4 pages on all three backends (3, 3 and 1 without it).

One divergence was not investigated: the 2-page A4 template converted to A3 is 2 pages on Aspose and Gotenberg, and 4 on Syncfusion and on Spire, where the fourth page is the free edition's notice.

## Design

### The independent reader

`Abstractions/Facts.cs` reads a produced file once and returns plain values to assert on:

<!-- no-compile -->
```csharp
internal sealed record DocxFacts(
    string BodyText,                          // visible text, a paragraph a line, text boxes included
    IReadOnlyList<string> Leftovers,          // paragraphs still holding {{ }}, {{#if}} or <{ }>: every story, mc:Fallback included
    IReadOnlyList<int> TableRows,             // rows of each top-level body table
    IReadOnlyList<PictureFact> BodyPictures,  // width, height and SHA-1 of each picture the body shows
    IReadOnlyList<SectionFact> Sections,      // page size in twips, title page, header and footer story types
    IReadOnlyList<StoryFact> Headers,         // text per story type: default, first, even
    IReadOnlyList<StoryFact> Footers)
{
    public static DocxFacts Read(IMemoryFile file);
}

internal sealed record PdfFacts(int Pages, IReadOnlyList<(int Width, int Height)> PageSizes, IReadOnlyList<string> PageTexts);
internal sealed record ImageFacts(int Width, int Height, double Ink);   // Ink: share of sampled pixels darker than paper
internal static class Formats { public static FileFormat Sniff(byte[] bytes); }  // %PDF-, {\rtf, OLE, OPC, zip mimetype, <html
```

Body-scoped facts read the same with or without a licence. Aspose's evaluation watermark is a header picture plus header and footer text, and Syncfusion's trial banner is a header paragraph, so the body is untouched in both. `Docx`'s readers (`HeaderText`, `BodyText`, `VisibleText`, `Count`, `TableCount`) fold into `DocxFacts`.

Every scenario that creates a document ends with `AssertNoLeftovers(output)`. A test whose template keeps a placeholder on purpose names it: `nested_templates.docx` holds `<{doc3}>`, which no test supplies. `GetText` stays tested by its own scenario and stops being the way other scenarios read their results.

| Scenario | Asserts today | Asserts on facts |
|---|---|---|
| `Replace_Image` | the file exists | a body picture has `sample1.jpg`'s SHA-1 and none has `placeholder.png`'s |
| `Template_Row` | `GetText` holds "Item #12" | the collection table has 4 rows, with #10, #12 and #31 in that order |
| `From_File` | the file exists | the template's 4-row table and 3 pictures survive, and the body text matches the `.docx` source |
| `Convert_To` and `Convert_Tags_The_Actual_Output_Format` | the file exists; the content type, in a second conversion | one conversion: the sniffed format and the content type both match |
| `From_A3_To_Pdf`, `From_A4_To_Pdf_A3` | the file exists | every page is 842 × 1191 pt ± 2 |
| `To_Images` | count > 0 | one image per PDF page, height/width 1.414 ± 0.01, ink > 5 % |
| `GetImages` | count > 0 | the template's three pictures, by size |
| `Add_FirstPage_Header_And_Footer` | the same text checks as the default case | the title page is set and the header text sits in the `first` story |
| `Merge` | text from both inputs | page count after `Convert` (even with `EnforceEvenAmountOfPages`), and the second input's Normal runs at the first's font size |
| `Extern_Document_Inherit_Font` | the file exists | the nested paragraphs are justified, at the parent's Normal size |
| `Factuur`, on every full backend | Spire's `GetText` | no leftovers in any story, fallback included |

### Shared scenarios carry their attributes

<!-- no-compile -->
```csharp
public abstract class WordTestsBase
{
    /// <summary>The formats this backend converts to; a case outside it skips.</summary>
    protected virtual IReadOnlySet<FileFormat> ConvertsTo => new HashSet<FileFormat>();

    [Test]
    public async Task Merge()
    {
        var merger = RequireMerger();   // Assert.Ignore("Word.Mini: no IWordMerger") when absent
        ...
    }

    [TestCaseSource(nameof(OutputFormats))]   // a TestCaseSource must be static: the superset, filtered per fixture
    public async Task Convert_To(FileFormat format)
    {
        RequireFormat(ConvertsTo, format);
        ...
    }
}
```

NUnit discovers inherited `[Test]` methods on a concrete fixture. A fixture overrides a scenario only where its backend behaves differently, like Aspose's licence gate on `A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them`. `[TestCase]` is not inherited, so an override restates its cases. Every skip names a backend and a capability, so the TRX reads as the capability matrix. The per-backend copies listed above become shared scenarios. Three more join them: `InputOptions`, the `DocumentBuilder` (each backend supplies its builder through a factory), and the invoice.

### Inputs

`Nested_Documents_Do_Not_Wear_Out_The_Service` guards against a per-instance counter summed over calls. Fifty-one calls with two nested documents each pass `MaxDepth` (100), and one-paragraph in-code documents are enough. `A_Template_That_Includes_Itself_Fails` reaches the limit through 100 levels of `lorem_ipsum.docx` as its own header; a one-paragraph document reaches it the same way. Measured in isolation (Release build, warm, one backend at a time):

| Backend | Wear-out: 101 calls, assets | 51 calls, in code | Self-inclusion: asset | In code |
|---|---|---|---|---|
| Spire | 5.0 s | 0.9 s | 0.9 s | 0.4 s |
| Aspose | 7.1 s | 1.0 s | 0.5 s | 0.2 s |
| Syncfusion | 2.1 s | 0.1 s | 0.4 s | < 0.1 s |

In the suite these tests take several times longer, because fixtures share the CPU. Merging `Convert_To` with `Convert_Tags_The_Actual_Output_Format` halves the conversions as well.

`Docx` becomes the only builder. It gains a page-break paragraph, and `TextBox` writes what Word writes: `mc:AlternateContent` with the DrawingML box as `mc:Choice` and a VML `v:textbox` as `mc:Fallback`. Every block and parameter scenario with a text box then covers both copies.

### Output files

- One `[SetUpFixture]` empties `Assets/Output` once per run, before any fixture starts, so no fixture races another's cleanup.
- A file is named after its test, `{Backend}/{TestName}.{ext}` from `TestContext.CurrentContext.Test.Name`, and attached with `TestContext.AddTestAttachment` so the TRX links to it.
- `{TestName}.facts.json` beside it holds the facts the test read. Diffing two runs then shows what a vendor upgrade or a code change moved, without making those values a gate (decision 2).

## Decisions

1. **Unsupported capabilities: skip or absence.** Answered: absence. A fixture marked `[WordFixture]` declares `[LeavesOut(feature, reason)]`, and a scenario that `[Needs]` a left-out feature is not built for it, so the results list only what the backend does. A scenario that reaches for a capability its fixture did not leave out fails, naming the feature, so nothing is left out unnoticed.
2. **The facts JSON: record or gate.** Answered: record. Every output has its `.facts.json` beside it; no run compares them to approved files.
3. **`Merge` layout (finding 4).** Answered: each input on a new page, as Word.Spire and Word.Aspose published it, so the release breaks no merge, with an option to run them on. `IWordMerger.Merge(inputs, MergeOptions? options, cancellationToken)` is a default interface method beside the original overload; `MergeOptions.FollowOn` runs each input on from the previous one's last page. The options parameter takes no default value: with one, `merger.Merge(inputs)` through the interface would match both overloads and fail to compile (CS0121).
4. **The Aspose builder's page size (finding 3).** Answered: A4. Every blank document Word.Aspose starts is A4 portrait.

## Steps

1. `DocxFacts`, `PdfFacts`, `ImageFacts`, `Formats.Sniff` and `AssertNoLeftovers`; `Docx` as the only builder, Word-shaped text boxes included. No scenario changes yet.
2. Rewrite the 47 weak cases on facts, and read every created document through facts instead of `GetText`.
3. Attributes on `WordTestsBase`; delete the forwarding overrides; promote the per-backend copies; add the `InputOptions`, `DocumentBuilder` and invoice scenarios.
4. In-code inputs for the two nesting tests; merge the two conversion scenarios; output naming, attachments and facts JSON.
5. Hygiene: `ClassicAssert` to `Assert.That`, the unused assets, the invoice tests' `%TEMP%` copies and commented-out code, `DocumentBuilderTests`' `ParallelScope.All` (the project's convention is fixtures in parallel, tests serial), and the test README.

A scenario that exposes findings 1–4 is added with `[Ignore]` naming the finding, so the fix that lands later flips it on.

## Outcome

Measured on 2026-10-05 without the container suite: 418 cases (411 run, 7 licence-gated skips) in 20 s, where the baseline ran 365 in 31 s. With it, through `Regira.runsettings`: 434 cases (427 run, the same 7 skips) in 44 s, the Gotenberg container's start included; all 16 Gotenberg cases pass. After the five reviews that followed: 517 cases (510 run) in 53 s.

- No forwarding override is left. Spire's fixture is 59 lines (from 455), Aspose's 150 (451), Syncfusion's 100 (305) and Gotenberg's 147 (224); `DocumentBuilderTests` is gone. What each fixture keeps is its case lists, its builder hook and what its backend does differently.
- Shared now, on every backend that has the feature: page settings (five sizes, A2 and A7 included), self-contained HTML, image formats pointing at `ToImages`, one image per page, the document builder, the invoice and its nested-template variant, parameters read from JSON, `html_` parameters, verbatim and multiline values, the four `InputOptions`, both merge layouts, and `AutoScalePictures`.
- The new scenarios exposed six more defects, fixed with the build:
  - Word.Spire set A0–A2 and A7–A10 as A4; its `AutoScalePictures` grew a picture by the square of the text-width ratio and threw past Word's 22-inch shape limit; its `DocumentBuilder` dropped `PageBreakAfter`; and `Convert` to `Png` or `Jpeg` threw `Exception`, not `NotSupportedException`.
  - Word.Syncfusion's `AutoScalePictures` grew pictures by the same square, and its `Merge` ran an input's own section breaks together.
  - On all three full backends, the page that `EnforceEvenAmountOfPages` pads a merged input with was lost: the next input now starts on an odd page.
- Each of the three A4-to-A3 conversions now takes 2 pages, as Aspose's and Gotenberg's did; Spire and Syncfusion took 4.
- A review of the build found five more, each now pinned by a shared scenario or a request test: Word.Spire turned a landscape section into a portrait-shaped page; Word.Aspose threw and Word.Syncfusion wrote past Word's 22-inch shape limit when scaling pictures; Word.Mini left the tags of an empty collection, or of a field no row supplies, when the collection's key is not a word; and `WordClient` silently dropped orientation and margins, and merged a padded input among several without its odd-page start, losing its padding page — both now throw `NotSupportedException`. The merge-joint rule, the picture-scaling limit and the page-size table each live once, in `Regira.Office.Word.Layout`.
- A later review found two more, pinned the same way: on all three full backends, a merged input with `InheritFont` and `EnforceEvenAmountOfPages` was padded in its own font and again in the inherited one, two blank pages where it needed none; and Word.Gotenberg scaled a picture in a text box twice. Two of its claims did not hold under a scenario, which stays: Word.Syncfusion writes a landscape section in the size asked for, through `Convert` and its `DocumentBuilder`, and an inherited font keeps a merged input's alignment.
- The review after that found one more: Word.Mini left a collection's field empty when its tag was spaced around the dot and its key not a word. Checking another claim showed that Word.Spire and Word.Aspose threw for a picture already past the 22-inch limit on page settings that keep the text width, as they set its own size again. The Aspose and Syncfusion merges no longer save and reload each input between, a third of a merge's time; every scenario's facts came out the same without it.
- The last review found two more: a picture held at the 22-inch limit could land a rounding error past it, about one size in twenty, which Word.Spire and Word.Aspose throw for; and Word.Gotenberg let a picture in a text box grow more than the box. Word.Gotenberg's groups of shapes, noticed in passing, now scale as a whole. A0–A2 pages pass Word's own 22-inch page limit, which `PageSize` now says.
- Observed, not changed: `Convert` to `Dotx`, `Docm` or `Dotm` is tagged with the `.docx` content type, because `WordContentTypes` names one type for every Open XML word-processing format.

## Risks

- **Vendor drift.** Page counts and layout move between vendor releases. Assert page counts only where the scenario is about pages.
- **Free and evaluation editions.** Spire's free edition writes three pages of PDF and then a notice page, so a page count on Spire holds only for documents of three pages or fewer. Aspose's evaluation mode truncates long documents; the test assets stay well below that.
- **Skip noise.** About 20 skips on Word.Mini (decision 1).
- **Gotenberg.** Its container suite was not part of the baseline. The facts reader applies to its PDFs and images unchanged.

<!-- {% endraw %} -->
