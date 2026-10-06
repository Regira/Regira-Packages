# A Word backend on the Open XML SDK
<!-- {% raw %} -->

As of 2026-10-05. Sources: the `Regira-Packages` repository, branch `wip` at `e8b54a0`; the nuget.org pages of DocumentFormat.OpenXml, HtmlToOpenXml.dll and Clippit, read on 2026-10-05; and a throwaway spike against DocumentFormat.OpenXml 3.5.1 (see *The importer*). Vendor behaviour that the spike did not touch is taken from the vendors' own pages and was not measured here. Nothing on this page has been built.

**Status: proposal, not scheduled. Five decisions are open (see *Decisions*). The design assumes [word-loop-blocks.md](word-loop-blocks.md) lands first.**

## Recommendation

Add `Regira.Office.Word.OpenXml`, a Word backend built on Microsoft's Open XML SDK (`DocumentFormat.OpenXml`, MIT). Its `WordService` implements `IWordCreator`, `IWordMerger`, `IWordTextExtractor` and `IWordImageExtractor`, and follows Word.Spire's template syntax: `{{ key }}` tags, bookmarks, titled collection tables, pictures found by Alt Text, `html_` parameters, `<{ key }>` nested documents, headers, footers, conditional blocks and `InputOptions`.

It does not implement `IWordConverter` or `IWordToImagesService`. The SDK edits the package's XML and has no layout engine, so it can neither count pages nor write PDF. Word.Gotenberg already does both, and already takes an `IWordCreator` from the container to render a template before converting it.

The gap this closes: today no backend without a vendor licence can fill `Headers`, `Footers`, `DocumentParameters` or non-default `InputOptions`. Word.Mini refuses them, and the guide's Gotenberg note sends that input to Word.Spire, Word.Syncfusion or Word.Aspose. Word.Mini also reads collection tables and images in MiniWord's own syntax, so a template written for Word.Spire does not fill there. Word.OpenXml with Word.Gotenberg would be the first licence-free stack that fills a Word.Spire template completely, with no size cap, and converts it to PDF.

The steps, in order:

1. The package and the read path: input checks, `GetText`, `GetImages`.
2. Filling a template without importing content: blocks, tags, bookmarks, collection tables, images, `html_`.
3. The importer, and on it nested documents, headers and footers.
4. `Merge` and `InputOptions`.
5. A Gotenberg fixture with Word.OpenXml as its creator.
6. Guides, docs, routing tables and the changelog.

## What exists today

| What | Where | Effect |
|---|---|---|
| `IWordService` composes six interfaces, and a backend may implement a subset | `src/Common.Office/Word/Abstractions` | Word.Mini implements three and Word.Gotenberg two; the guide tells consumers to resolve the narrowest interface they need |
| The template syntax a full backend follows | Word.Spire's `CreateDocument` pipeline: `ResolveConditions`, `InsertDocuments`, `ReplaceCollections`, `ReplaceImages`, `ReplaceGlobalParameters`, `AddHeader` / `AddFooter`, `ProcessInputOptions`. Word.Syncfusion and Word.Aspose match it | Word.OpenXml has a reference implementation for every feature it fills |
| Conditional blocks are shared, internal code | `ConditionalBlocks` and `VisibleText` in `Regira.Office`, reached through `InternalsVisibleTo` in `Common.Office.csproj` | A new backend needs its own entry there, and ships on the Word family's aligned number (`ai/learnings.md`, 2026-09-30) |
| An Open XML walk for blocks, tags and extraction already exists | `ResolveConditions`, `Segments`, `Remove`, `EndWithParagraph`, `TrimTags`, `GetText`, `GetImages` and `ContentRoots` in `src/Word.Mini/WordService.cs` | The block resolution, tag matching across runs, and text and image extraction that Word.OpenXml needs are written and tested once already. The `VisibleText` XML doc already names "the Open XML backends" as reading only `w:t` |
| Open XML page setup and an input guard already exist | `OpenXmlPageSetup`, `PageSizes` and `ConditionalMarkers` in `src/Word.Gotenberg/Internal` | Page size, margins and scaling are written into `w:sectPr`; a package over 32 MiB uncompressed or 1,000 parts is not opened |
| No licence-free creator has a document model | `EnsureSupported` in Word.Mini throws `NotSupportedException` for `DocumentParameters`, `Headers`, `Footers` and non-default `InputOptions`. *Word.Gotenberg limits* in `office.word.instructions.md` sends that input to Word.Spire, Word.Syncfusion or Word.Aspose | A deployment without a vendor licence cannot use those features at all |
| The shared test suite probes capabilities | `WordTestsBase` ignores a scenario whose interface the backend lacks; a fixture re-declares the scenarios it runs | Word.OpenXml is one more fixture. `GotenbergTests` composes Gotenberg with Word.Mini as its creator, which is the shape step 5 copies |
| Some shared scenarios read binary formats | `From_File` runs `template.dot`, `template.doc` and `template.odt`; `Bookmarks` reads `bookmarks.dot` | An OOXML-only backend runs `From_File` on `multipage.docx` alone, as Word.Mini does, and `Bookmarks` needs a `.docx` asset |
| DocumentFormat.OpenXml is already pinned | 3.5.1 in Word.Mini and Word.Gotenberg | 3.5.1 is the latest release; no new pin |

## What the SDK can do

| Feature | How Word.Spire does it | Open XML approach | Effort |
|---|---|---|---|
| `{{ key }}` parameters | `doc.Replace` with `{{ *key *}}`, ignoring case, line endings as `\v` | A paragraph's `w:t` texts read as one string (Word.Mini's `TrimTags`); the value goes into the run where the tag starts; line endings become `w:br` | Low |
| Bookmarks | The text run after the bookmark named as the key, then the bookmark is removed | The run after `w:bookmarkStart` with that `w:name` | Low |
| Collection tables | The first table whose Alt Text title is the key; its second row is the template row; `{{ row_number }}`; only a cell's first paragraph is filled | The first `w:tbl` whose `w:tblCaption` is the key; clone the second `w:tr` per row | Low |
| Images | Every picture whose Alt Text title or description is the name; the size is kept | Every `wp:docPr` whose `title` or `descr` is the name: a new `ImagePart` in the part that owns the drawing, the `a:blip` re-pointed, `wp:extent` kept | Low |
| `html_` parameters | The paragraph holding the first tag is emptied and the converted HTML appended to it | HtmlToOpenXml converts the HTML to paragraphs (see *Dependencies*) | Low |
| Conditional blocks | `ResolveConditions` | Port the Open XML walk (decision 5) | Low |
| `DocumentParameters` | The nested document's body replaces the `<{ key }>` paragraph | The importer | **High** |
| `Headers` / `Footers` | The template's story of that type, else its default story, else its body | The importer, into a new `HeaderPart` / `FooterPart` | Medium, on top of the importer |
| `Merge` | `InsertTextFromStream` per input | The importer, section by section | Medium, on top of the importer |
| `InheritFont` | Runs in Normal-styled paragraphs take the reference document's Normal font | Resolve that font through style, `w:docDefaults` and theme, then write it on the runs | Medium |
| `HorizontalAlignment`, `RemoveEmptyParagraphs` | Paragraph format; body paragraphs without children are removed | `w:jc`; remove body paragraphs with no content | Low |
| `EnforceEvenAmountOfPages` | Counts pages, adds a page break when odd | No page count (decision 2) | — |
| `GetText`, `GetImages` | Read the created document | Port Word.Mini's | Low |
| `Convert` | Every format Spire writes | Only between `.docx`, `.dotx`, `.docm` and `.dotm` (`ChangeDocumentType`, decision 3) | — |
| `ToImages` | Spire renders pages | No layout engine: Word.Gotenberg with `IPdfToImageService` | — |
| Input formats | `.docx`, `.doc`, `.dot`, `.odt`, `.rtf`, HTML | `.docx`, `.dotx`, `.docm`, `.dotm` only | — |

## Design

### Package and registration

- `src/Word.OpenXml/`, package and assembly `Regira.Office.Word.OpenXml`, namespace `Regira.Office.Word.OpenXml`, `net8.0;net10.0`.
- Apache-2.0 like Word.Mini, with no licence check and no size cap.
- `WordService` has a parameterless constructor and no DI extension. The Office backends take their configuration through the constructor and ship no extension (`ai/learnings.md`, 2026-09-16).
- An `[Obsolete]` alias is not needed: the package is new.

The licence-free pairing registers it as the creator Word.Gotenberg picks up:

<!-- no-compile -->
```csharp
services.AddSingleton<IPdfToImageService, Regira.Office.PDF.DocNET.PdfManager>();
services.AddSingleton<IWordCreator, Regira.Office.Word.OpenXml.WordService>();
services.AddSingleton<IWordMerger, Regira.Office.Word.OpenXml.WordService>();
services.AddGotenbergWord(o => o.BaseUrl = builder.Configuration["Gotenberg:BaseUrl"]!);
```

### The create pipeline

`Create` runs Word.Spire's order, so a template reads the same on both:

1. Enter `NestedDocumentGuard`, the per-flow depth limit each backend copies.
2. Open the template as an editable package in memory, after the input checks (see *Inputs*).
3. Resolve conditional blocks, so a dropped branch's tags, pictures and nested documents are never processed.
4. Insert nested documents.
5. Fill collection tables, before the global pass, so a global does not fill a template row.
6. Replace pictures.
7. Fill global parameters, `html_` parameters and bookmarks.
8. Add headers and footers.
9. Apply `InputOptions`.

A nested document, header or footer is built by the same pipeline into bytes of its own, then imported.

### Tags

- A paragraph's `w:t` elements are read as one string, each character mapped to its element, as `TrimTags` does in Word.Mini. A match is written into the element where it starts, which keeps that run's formatting, and its other characters are removed. Only `w:t` counts, so a tag in a field code (`w:instrText`) or a tracked deletion (`w:delText`) is not filled, as the `VisibleText` contract says.
- The pattern is `{{ *<key> *}}` with the key escaped, ignoring case, built with `RegexOptions.NonBacktracking` (`ai/learnings.md`, 2026-09-30). `A_Parameter_Key_Is_Matched_Literally` pins this.
- A value is written with `ToString()`, `null` as empty text. Each line ending becomes a `w:br`, and a run whose text starts or ends with a space gets `xml:space="preserve"`.
- Tags are filled in the body, headers and footers, and in the text boxes inside them. Whether Word.Spire also fills footnotes and endnotes is not measured here; step 2 measures it and matches it.
- Word writes a text box twice, inside `mc:AlternateContent`: a DrawingML choice and a VML fallback, each with its own `w:txbxContent`. The SDK keeps both by default. Tags and blocks are applied to both, so the two stay alike. Extraction reads only the choice, so text-box text is not reported twice.

### Collection tables, pictures, bookmarks and HTML

- **Collection tables.** The first `w:tbl` whose `w:tblPr/w:tblCaption` equals the key, case included, as Word.Spire's `FindTable` matches `Title`. Its second row is cloned per collection row, and each cell's first paragraph is filled with the row's fields, `{{ row_number }}` included. Every quirk *Collection Tables* in `office.word.instructions.md` documents is kept as it is.
- **Pictures.** A picture matches on `wp:docPr/@title` or `@descr`. The spike's `template_image.docx` holds its name in `title` with an empty `descr`; Word's current Alt Text pane writes `descr`. The new image is added to the part that owns the drawing, which is the header's part for a picture in a header. Its relationship id replaces `a:blip/@r:embed`, and `wp:extent` and `a:ext` stay, so the size is kept as on Word.Spire.
- **Bookmarks.** A global parameter whose key names a bookmark replaces the text of the run after its `w:bookmarkStart`, and the bookmark is removed, as on Word.Spire.
- **HTML.** Word.Spire's `InjectHtml` empties the paragraph holding an `html_` key's first tag and appends the HTML to it. HtmlToOpenXml produces block-level paragraphs, so here they take that paragraph's place. A key without a tag stays unused, as on Word.Spire.

### The importer

Nested documents, headers, footers and `Merge` all copy content from one package into another. Word.Spire clones child objects (`InsertDocumentContent`), and its library carries the parts along. In Open XML the importer carries them:

- **Relationships.** Every attribute in the relationships namespace on a copied element is rewritten: `a:blip/@r:embed` and `@r:link`, `w:hyperlink/@r:id`, `c:chart/@r:id`, `o:OLEObject/@r:id`, `v:imagedata/@r:id`, and SmartArt's `dgm:relIds`.
  - An internal target is copied with `AddPart(sourcePart)` on the destination part.
  - An external target is re-added with `AddHyperlinkRelationship` or `AddExternalRelationship`.
  - The destination is the part that will own the content: the main part, or the new header or footer part.
- **Styles.** A style the copied content uses and the destination lacks is copied with its `basedOn`, `next` and `link` chain. When both packages define the same style id, the destination's definition wins, as Word's paste does by default.
- **Lists.** Each `w:num` the content uses is copied with its `w:abstractNum`, under new ids and a new `w:nsid`. Otherwise an imported list would continue the host document's numbering.
- **Notes.** Footnotes and endnotes the content references are copied under new ids. Comments are dropped, with their ranges and references.
- **Ids.** `wp:docPr/@id` and bookmark ids are renumbered, and a bookmark whose name the destination already has is renamed. `w14:paraId` and `w14:textId` are removed; Word regenerates them.
- **Sections.** A nested document's `w:sectPr` elements are dropped, so its sections flatten into the host's, as on Word.Spire. In a merge, each input keeps its sections (see *Merge*).

The spike, against DocumentFormat.OpenXml 3.5.1:

- `AddPart` given a part from another package deep-copies it. Three image parts were copied, and so was a chart part together with its embedded workbook, each under a new relationship id. The importer only has to rewrite the ids it gets back.
- `ChangeDocumentType` switches a package from `.docx` to `.dotx`.
- The test templates address their table by `w:tblCaption` (`Template_Table` in `template_row.docx`) and their picture by `wp:docPr/@title` (`placeholder` in `template_image.docx`).

`w:altChunk` is not a way around the importer. Word expands an alternative-format chunk only when it opens the file, so `GetText` and `GetImages` on the output would not see the content. LibreOffice bug 151080 reports a document whose `altChunk` holds an MHT sub-document opening blank in Writer, which is what Word.Gotenberg converts with.

### Headers and footers

- The source story is chosen as Word.Spire chooses it: the input template's header of that type, else its default header, else its body.
- The story is imported into a new `HeaderPart` or `FooterPart`. The first section's `w:sectPr` gets a `w:headerReference` or `w:footerReference` of type `default`, `first` or `even`.
- `FirstPage` sets `w:titlePg` on that section. `Even` sets `w:evenAndOddHeaders` in the settings part, which applies to the whole document. In Word.Spire the switch belongs to a section.
- `Odd` maps to `default`: once even and odd pages differ, the default story serves the odd pages, and OOXML has no separate odd story.
- When a first-page or even-page story is switched on and the other kind has no story of that type, it gets a copy of its default story, as `FillSwitchedOnStories` does on Word.Spire. `A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them` pins this, through step 5.
- A nested document whose body is empty, placed by a `<{ key }>` in a header or footer, replaces that default story with its own, as on Word.Spire.

### Merge

- Each input goes through `Create` first.
- The first input's package is the base, so its styles, settings and theme win.
- Each later input's content is imported section by section. Its last `w:sectPr` becomes the section break that ends it, keeping its page setup and its header and footer references, whose parts are imported. Each input therefore starts on a new page, as on Word.Spire.
- `InheritFont` on a later input reads the first input's Normal font, as Word.Spire's `firstDoc` reference does.

### Input options

- **`InheritFont`.** It applies where Word.Spire applies it, when the document is not its own reference: nested documents, headers and footers, and merged inputs after the first.
  - The reference font is resolved from the `Normal` style's `w:rFonts` and `w:sz`, then its `basedOn` chain, then `w:docDefaults`.
  - A theme reference such as `minorHAnsi` is read from the theme part.
  - The font is written on the runs of each paragraph whose style name starts with `Normal`, or that has no style.
- **`HorizontalAlignment`.** `w:jc` on the same paragraphs. `Justify` is written as `both`.
- **`RemoveEmptyParagraphs`.** Removes the body paragraphs with no content, as Word.Spire removes body paragraphs without children. A paragraph carrying `w:sectPr` stays, because it is a section break.
- **`EnforceEvenAmountOfPages`.** See decision 2.

### Text and image extraction

`GetText` and `GetImages` run against the created document, as on every creating backend, and are ported from Word.Mini. A picture's name is its Alt Text title, then its description, then its object name, then its part name.

### Inputs

- `.docx`, `.dotx`, `.docm` and `.dotm` are read. The format is judged from the content, since the SDK reads nothing but a WordprocessingML package whatever the file is called. Anything else throws `NotSupportedException` naming what was found: an OLE container (`.doc`, `.dot`) or a zip that is not a word-processing package (`.odt`). `SourceFormat.Sniff` in Word.Gotenberg recognises the same signatures, though Word.Gotenberg lets a file name decide first. The guide sends `.doc` and `.odt` templates to Word.Spire or Word.Aspose.
- Templates often come from users. The package is opened only within the limits Word.Gotenberg's scan uses, 32 MiB uncompressed and 1,000 parts, judged from the zip directory before anything is read. Over them, `Create` throws. Word.Gotenberg's scan sends the package on unread instead, but a creator has nothing to fall back to.

### Out of scope

- **`Convert` and `ToImages`.** These belong to Word.Gotenberg (decision 3).
- **`DocumentBuilder`.** Word.Spire, Word.Syncfusion and Word.Aspose each ship one (decision 4).
- **Comments** in imported content are dropped.
- **Retiring Word.Mini.** Word.OpenXml covers everything Word.Mini does except MiniWord's own syntax. Whether Word.Mini stays is a separate decision.
- **Loop blocks.** They arrive with the Open XML walk this backend ports (see *Risks*).

## Dependencies

- **DocumentFormat.OpenXml** 3.5.1. MIT, maintained by Microsoft, the latest release, and already pinned by Word.Mini and Word.Gotenberg.
- **HtmlToOpenXml.dll** 3.5.0 (2026-07-20). MIT; targets `net8.0`, `net10.0`, `netstandard2.0` and `net462`. It depends on DocumentFormat.OpenXml ≥ 3.5.1, AngleSharp ≥ 1.5.0 and Microsoft.Extensions.Logging.Abstractions ≥ 6.0.0. It is used only for `html_` parameters.
- **Clippit**, evaluated and not taken (decision 1). It is an MIT fork of Open-XML-PowerTools, maintained by Sergey Tihon. Its `DocumentBuilder` merges documents with the style, numbering and relationship fixes the importer needs.
  - The latest release, 3.9.1 (2026-09-21), targets `net10.0` only. The last release seen with both `net8.0` and `net10.0` is 3.3.1 (2026-03-24); Regira's packages target both.
  - It depends on SixLabors.ImageSharp.Drawing. Under the Six Labors Split Licence, a package installed through an unrelated third party's dependency is Apache-2.0. So the licence holds, but the dependency is heavy for a merger.
  - It edits parts as LINQ to XML documents cached on the part, beside the SDK's typed DOM. Mixing the two in one package needs a save between them.
  - .NET 8 support ends on 2026-11-10. If Regira drops `net8.0` then, the framework objection goes away.

## Decisions

1. **The importer: written here or Clippit?**
   - Written here: an importer of the shape above, covering what Word.Spire's templates need. The spike shows the hardest part, copying parts across packages, is the SDK's own.
   - Clippit: a tested merger, but `net10.0` only on current releases, and a second object model.

   Recommendation: write it here, and look at Clippit again if the importer's long tail outgrows it once `net8.0` is dropped.
2. **`EnforceEvenAmountOfPages` without a page count.** A section's `w:type` of `oddPage` makes Word and LibreOffice start it on an odd page, inserting a blank page when needed, at layout time.
   - In `Merge`, an input carrying the option makes the next input start on an odd page. That is Word.Spire's output except for the blank page Word.Spire adds after the last document.
   - For `Create` on its own, no section type helps: a trailing section always occupies a page of its own.
   - Option 1: odd-page starts in `Merge`, `NotSupportedException` in `Create`.
   - Option 2: `NotSupportedException` in both, as Word.Mini throws for any non-default `InputOptions`.

   Recommendation: option 1, with the difference in the guide's limits note.
3. **A converter, or none?** A converter could write `.docx`, `.dotx`, `.docm` and `.dotm` with `ChangeDocumentType`, apply `ConversionOptions.Settings` with Word.Gotenberg's page setup, and throw for every other format. It would also join the last-wins `IWordConverter` contest with Word.Gotenberg, whose `Convert` is the one the pairing needs. Recommendation: none.
4. **`DocumentBuilder`: now or later?** The builder's `Paragraph.Style` names Word's built-in styles. A `w:pStyle` naming a style the styles part does not define renders as Normal, so the builder has to write the definitions for the ones it uses. Recommendation: later, as its own step, if a consumer asks.
5. **Where the Open XML walk lives.** Block resolution, tag matching and extraction exist in Word.Mini.
   - Option 1: copy them into Word.OpenXml, as `NestedDocumentGuard` and `PageSizes` are copied today.
   - Option 2: move them into Word.OpenXml, and have Word.Mini call them through `InternalsVisibleTo`. Word.Mini then depends on Word.OpenXml and becomes a thin MiniWord adapter.

   Every later template feature, such as inline conditions or the loop's phase 2, is written once per walk. Recommendation: option 2.

## Steps

1. **The package and the read path.**
   - `src/Word.OpenXml/` with its `.csproj` and `README.md`.
   - The `InternalsVisibleTo` entry in `Common.Office.csproj`, and the solution entry in `Regira-Packages.slnx`.
   - The input checks, `GetText` and `GetImages`.
   - `OpenXmlTests : WordTestsBase` in `tests/Office.Word.testing`, with output folder `OpenXml` and a project reference. It runs `GetText`, `GetImages` and `From_File("multipage.docx")`.
2. **Filling without import.**
   - Blocks (as decision 5 says), tags, bookmarks, collection tables, pictures and `html_`.
   - Measure whether Word.Spire fills tags in footnotes and endnotes, and match it.
   - Add `bookmarks.docx` to `Assets/Input` as the `.docx` twin of `bookmarks.dot`, and an `OpenXmlTests` override of `Bookmarks` that reads it.
   - Run `Replace_Parameters`, `Replace_Image`, `Template_Row`, `A_Missing_Collection_Table_Leaves_The_Others`, `A_Parameter_Key_Is_Matched_Literally`, `A_Null_Or_Unused_Parameter_Is_Harmless` and every conditional-block scenario.
3. **The importer, nested documents, headers and footers.**
   - Run `Nested_Documents`, `Add_Header_And_Footer`, `Add_FirstPage_Header_And_Footer`, `A_Template_That_Includes_Itself_Fails`, `Nested_Documents_Do_Not_Wear_Out_The_Service` and `A_Dropped_Branch_Inserts_No_Nested_Document`.
   - Add importer scenarios to `OpenXmlTests`: a picture in an imported header, an imported list restarting its numbering, a hyperlink, a footnote, a chart, and distinct `wp:docPr` and bookmark ids.
   - Output must be `OpenXmlValidator`-clean, as the loop proposal asks of its clones.
4. **`Merge` and `InputOptions`.** Run `Merge`, and add the `EnforceEvenAmountOfPages` behaviour decision 2 chooses.
5. **The Gotenberg pairing.** Add a second `GotenbergTests`-shaped fixture with Word.OpenXml as the creator. It runs `A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them`, and converts a template with headers, footers and a nested document to PDF, which Word.Mini's creator refuses.
6. **Docs.**
   - `office.word.instructions.md`:
     - the *Installation* block and the *Backend Comparison* row (`Word.OpenXml` | Open XML SDK | ✓ | — | ✓ | ✓ | MIT, no key, no size cap) with its recommendation;
     - a *Word.OpenXml limits* note;
     - the *IWordService* paragraph;
     - the backend lists in the *Collection Tables*, *HTML Parameters* and *Conditional Blocks* headings;
     - the *Word.Gotenberg limits* sentence naming the creators with a document model;
     - *Registration*.
   - `office.word.examples.md` if the licence-free pairing earns an example there.
   - `docs/word/README.md`: *Projects*, *Installation*, the backend lists in *Collection tables* and *Conditional blocks*, and a `### Word.OpenXml` section under *Implementation notes*.
   - `docs/word/examples.md`: *Example 8* pairs Word.Mini with Word.Gotenberg, and could pair Word.OpenXml instead.
   - The routing tables in `ai/AGENTS.md` and `src/Common.Setup/ai/copilot-instructions.md`.
   - The `office-word` group in `tools/GuideVerifier/projects.json`.
   - CHANGELOG bullets for `Regira.Office.Word.OpenXml` and for `Regira.Office`, whose assembly gains the `InternalsVisibleTo` entry, plus Word.Mini if decision 5 picks option 2.
   - `ai/learnings.md` for whatever the importer teaches.

## Risks

- **The importer's long tail.** OOXML content can reference a lot: SmartArt, OLE objects, content controls bound to custom XML parts, glossary documents, comments. Rewriting relationship attributes generically covers anything that points at a part, and `AddPart` copies the part with its children. What the importer does not carry, it drops on purpose, and the guide names it. The importer scenarios and `OpenXmlValidator` in step 3 are the guard.
- **Fidelity next to Word.Spire.** The destination's styles and `w:docDefaults` win, so imported text can look different from the same merge on Word.Spire. Word.Spire's own behaviour on clashing styles is not measured here. Comparing the `Merge` and `Nested_Documents` outputs of both backends side by side in step 4 would show the difference.
- **Blank pages in Gotenberg's PDF.** LibreOffice's PDF export has an option to leave out automatically inserted blank pages, the ones an odd-page section start creates. Check that Word.Gotenberg's route keeps them before decision 2's option 1 relies on them.
- **HTML in a header.** HtmlToOpenXml adds a picture's or list's part to the part it is given. Check that an `html_` tag in a header gets its parts on the header's part.
- **A fifth creating backend.** Every template feature is then written for one more document model. [word-loop-blocks.md](word-loop-blocks.md) changes the block walk on "the four creating backends", and Word.OpenXml would be a fifth. This proposal assumes the loop lands first, and ports the finished top-down walk from Word.Mini, not the current one. Decision 5's option 2 keeps the count of Open XML walks at one.
- **The internal binding.** Word.OpenXml binds to `Regira.Office` internals like the other Word backends, so it publishes on the family's aligned number (`ai/learnings.md`, 2026-09-30).

<!-- {% endraw %} -->
