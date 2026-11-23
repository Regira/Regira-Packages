# A Word backend on the Open XML SDK
<!-- {% raw %} -->

As of 2026-11-23. Sources: the `Regira-Packages` repository, branch `wip` at `dce524e`; the nuget.org nuspecs of DocumentFormat.OpenXml 3.5.1, HtmlToOpenXml.dll 3.5.0 and Clippit 3.2.0 to 3.10.1, read on 2026-11-23; and a throwaway spike against DocumentFormat.OpenXml 3.5.1 run on 2026-10-05 (see *The importer*), which was not repeated. Vendor behaviour that the spike did not touch is taken from the vendors' own pages and was not measured here. Nothing on this page has been built.

**Status: proposal, not scheduled. Five decisions are open (see *Decisions*).**

## Recommendation

Add `Regira.Office.Word.OpenXml`, a Word backend built on Microsoft's Open XML SDK (`DocumentFormat.OpenXml`, MIT). Its `WordService` implements `IWordCreator`, `IWordMerger`, `IWordTextExtractor` and `IWordImageExtractor`, and follows Word.Spire's template syntax: `{{ key }}` tags, bookmarks, titled collection tables, pictures found by Alt Text, `html_` parameters, `<{ key }>` nested documents, headers, footers, conditional blocks, loop blocks with their marker rows, and `InputOptions`.

It does not implement `IWordConverter` or `IWordToImagesService`. The SDK edits the package's XML and has no layout engine, so it can neither count pages nor write PDF. Word.Gotenberg already does both, and already takes an `IWordCreator` from the container to render a template before converting it.

The gap this closes: the only creator with a document model that needs no vendor key is Word.Spire on FreeSpire.Doc, which stops at 500 paragraphs or 25 tables per document and at three pages of PDF. Word.Mini has no cap, but it refuses `Headers`, `Footers`, `DocumentParameters` and non-default `InputOptions`, and it reads collection tables and images in MiniWord's own syntax, so a template written for Word.Spire does not fill there. The guide's Gotenberg note sends that input to Word.Spire, Word.Syncfusion or Word.Aspose. Word.OpenXml with Word.Gotenberg would be the first stack that fills a Word.Spire template completely, with no size cap and no vendor licence, and converts it to PDF.

Much of the groundwork is in the tree. The block syntax and the order of the walk are shared in `Regira.Office`, an Open XML binding of that walk exists in Word.Mini, and the shared test suite gives a fixture exactly the scenarios whose features it declares. What is new is the document model's work: tags, titled tables, pictures, and above all the importer.

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
| The template syntax a full backend follows | Word.Spire's `CreateDocument` pipeline: `ResolveBlocks`, `InsertDocuments`, `ReplaceCollections`, `ReplaceImages`, `ReplaceGlobalParameters`, `AddHeader` / `AddFooter` with `FillSwitchedOnStories`, `ProcessInputOptions`. Word.Syncfusion and Word.Aspose match it | Word.OpenXml has a reference implementation for every feature it fills |
| Template blocks are shared, internal code | `TemplateBlocks` (conditions, loops, marker rows), `TemplateScope`, `TemplateWalk<TNode>`, `VisibleText` and `NestedDocumentGuard` in `Regira.Office` (`Word/Templating`), reached through `InternalsVisibleTo` in `Common.Office.csproj`. `ConditionalBlocks` remains only for the backends published at 6.5.0 | The syntax, the decision and the order of the work are written once. A backend supplies its document model's operations: a paragraph's text, a container's children per section, clone, insert, remove, fill. A new backend needs its own `InternalsVisibleTo` entry, and ships on the Word family's aligned number (`ai/learnings.md`, 2026-09-30) |
| An Open XML binding of the walk already exists | `MiniTemplateWalk` in `src/Word.Mini/Internal` (263 lines): sections cut at `w:sectPr`, copies scrubbed of bookmarks and given `wp:docPr` and content-control ids of their own, containers left ending in a paragraph, a field filled in the `w:t` that holds its tag | The block work Word.OpenXml needs is written and tested. It carries one MiniWord detail: `Fill` neutralises each value (`TagLikeText`) so MiniWord leaves it whole |
| Tag joining and extraction over Open XML already exist | `TrimTags`, `GetText`, `GetImages` and `ContentRoots` in `src/Word.Mini/WordService.cs` | Matching a tag Word split over runs, and reading text and pictures from the body, headers and footers, are written once already. The `VisibleText` XML doc names "the Open XML backends" as reading only `w:t` |
| Package helpers that need no SDK | `VmlShapeIds` and `WordPackageStories` in `Regira.Office` (`Word/Packaging`), on `System.IO.Compression` and `System.Xml.Linq` | `VmlShapeIds.MakeUnique` renumbers the VML ids a copy repeats, on the written package. The importer's copies need it as a loop's do |
| Open XML page setup and an input guard already exist | `OpenXmlPageSetup` and `TemplateMarkers` in `src/Word.Gotenberg/Internal`; `WordPageSizes` in `Regira.Office` (`Word/Layout`) | Page size, margins and scaling are written into `w:sectPr`; a package over 32 MiB uncompressed (`MaxBytes`) or 1,000 parts (`MaxParts`) is not opened |
| The merge joint is shared | `MergeJoints.StartsOnOddPage` in `Regira.Office` (`Word/Layout`), called by the three merging backends | An input with `EnforceEvenAmountOfPages` starts on an odd page, and so does the one after it. Word.OpenXml's `Merge` asks the same question |
| No licence-free creator has a document model | `EnsureSupported` in Word.Mini throws `NotSupportedException` for `DocumentParameters`, `Headers`, `Footers` and non-default `InputOptions`. *Word.Gotenberg limits* in `office.word.instructions.md` sends that input to Word.Spire, Word.Syncfusion or Word.Aspose | Without a vendor licence those features exist only under FreeSpire.Doc's caps |
| The shared test suite gives a fixture what its backend has | A scenario in `WordTestsBase` declares the features it needs (`[Needs(WordFeature.…)]`); a fixture is a `[WordFixture]` that names what it lacks and why (`[LeavesOut(…, reason)]`) and does not get those scenarios | Word.OpenXml is one more fixture, whose `LeavesOut` list shrinks step by step. `GotenbergTests` composes Gotenberg with Word.Mini as its creator, and leaves out seven features because "its creator here is Word.Mini, which has none of them": that list is what step 5 removes |
| Some shared scenarios read binary formats | `From_File` runs the fixture's own `SourceFiles`; `Bookmarks` reads `bookmarks.dot`, and `WordFeature.Bookmarks` is defined as a bookmark "in a binary `.dot` template" | An OOXML-only backend declares `multipage.docx` alone, as `MiniTests` does, and `Bookmarks` needs a `.docx` asset |
| Created documents are checked with the SDK's validator | `ValidationErrors` in `tests/Office.Word.testing/Abstractions/Docx.cs`, on `OpenXmlValidator` for Office 2019 | The importer's output is held to the same check |
| DocumentFormat.OpenXml is already pinned | 3.5.1 in Word.Mini and Word.Gotenberg | 3.5.1 is the latest release; no new pin |

## What the SDK can do

| Feature | How Word.Spire does it | Open XML approach | Effort |
|---|---|---|---|
| `{{ key }}` parameters | `doc.Replace` with `{{ *key *}}`, ignoring case, line endings as `\v` | A paragraph's `w:t` texts read as one string (Word.Mini's `TrimTags`); the value goes into the run where the tag starts; line endings become `w:br` | Low |
| Bookmarks | The text run after the bookmark named as the key, then the bookmark is removed | The run after `w:bookmarkStart` with that `w:name` | Low |
| Collection tables | The first table whose Alt Text title is the key; its second row is the template row; `{{ row_number }}`; only a cell's first paragraph is filled | The first `w:tbl` whose `w:tblCaption` is the key; clone the second `w:tr` per row | Low |
| Images | Every picture whose Alt Text title or description is the name; the size is kept | Every `wp:docPr` whose `title` or `descr` is the name: a new `ImagePart` in the part that owns the drawing, the `a:blip` re-pointed, `wp:extent` kept | Low |
| `html_` parameters | The paragraph holding the first tag is emptied and the converted HTML appended to it | HtmlToOpenXml converts the HTML to paragraphs (see *Dependencies*) | Low |
| Conditional blocks, loop blocks, marker rows | `ResolveBlocks`, through `SpireTemplateWalk` | `TemplateWalk<TNode>` over Open XML, which `MiniTemplateWalk` is (decision 5) | Low |
| `DocumentParameters` | The nested document's body replaces the `<{ key }>` paragraph | The importer | **High** |
| `Headers` / `Footers` | The template's story of that type, else its default story, else its body | The importer, into a new `HeaderPart` / `FooterPart` | Medium, on top of the importer |
| `Merge` | `InsertTextFromStream` per input, the joint's section start from `MergeJoints` | The importer, section by section | Medium, on top of the importer |
| `InheritFont` | Runs in Normal-styled paragraphs take the reference document's Normal font | Resolve that font through style, `w:docDefaults` and theme, then write it on the runs | Medium |
| `HorizontalAlignment`, `RemoveEmptyParagraphs` | Paragraph format; body paragraphs without children are removed | `w:jc`; remove body paragraphs with no content | Low |
| `EnforceEvenAmountOfPages` | Counts pages and adds a page break when odd; merged, the input and the next one start on an odd page | No page count (decision 2) | — |
| `GetText`, `GetImages` | Read the created document | Word.Mini's (see *Text and image extraction*) | Low |
| `Convert` | Every format Spire writes | Only between `.docx`, `.dotx`, `.docm` and `.dotm` (`ChangeDocumentType`, decision 3) | — |
| `ToImages` | Spire renders pages | No layout engine: Word.Gotenberg with `IPdfToImageService` | — |
| Input formats | `.docx`, `.doc`, `.dot`, `.odt`, `.rtf`, HTML | `.docx`, `.dotx`, `.docm`, `.dotm` only | — |

## Design

### Package and registration

- `src/Word.OpenXml/`, package and assembly `Regira.Office.Word.OpenXml`, namespace `Regira.Office.Word.OpenXml`. Target frameworks as the rest of the family: `net8.0;net10.0` today.
- Apache-2.0 like Word.Mini, with no licence check and no size cap.
- `WordService` has a parameterless constructor and no DI extension. The Office backends take their configuration through the constructor and ship no extension (`ai/learnings.md`, 2026-09-16).
- An `[Obsolete]` alias is not needed: the package is new.

The licence-free pairing registers it as the creator Word.Gotenberg picks up:

<!-- no-compile -->
```csharp
services.AddSingleton<IImageService, Regira.Drawing.SkiaSharp.Services.ImageService>();
services.AddSingleton<IPdfToImageService, Regira.Office.PDF.PdfPig.PdfService>();
services.AddSingleton<IWordCreator, Regira.Office.Word.OpenXml.WordService>();
services.AddSingleton<IWordMerger, Regira.Office.Word.OpenXml.WordService>();
services.AddGotenbergWord(o => o.BaseUrl = builder.Configuration["Gotenberg:BaseUrl"]!);
```

Where a server is not wanted, the created `.docx` goes to `Regira.Office.PDF.MiniPdf` (`IDocumentToPdfService`) instead, in process and with a less faithful layout, as the guide already says for Word.Mini.

### The create pipeline

`Create` runs Word.Spire's order, so a template reads the same on both:

1. Enter `NestedDocumentGuard`, the per-flow depth limit the creating backends share.
2. Open the template as an editable package in memory, after the input checks (see *Inputs*).
3. Resolve the template's blocks and fill the fields of its loops' rows, so a dropped branch's tags, pictures and nested documents are never processed.
4. Insert nested documents.
5. Fill collection tables, before the global pass, so a global does not fill a template row.
6. Replace pictures.
7. Fill global parameters, `html_` parameters and bookmarks.
8. Add headers and footers.
9. Apply `InputOptions`.
10. Make the VML shape ids unique in the written package (`VmlShapeIds.MakeUnique`), as Word.Mini does after its blocks.

A nested document, header or footer is built by the same pipeline into bytes of its own, then imported.

### Tags

- A paragraph's `w:t` elements are read as one string, each character mapped to its element, as `TrimTags` does in Word.Mini. A match is written into the element where it starts, which keeps that run's formatting, and its other characters are removed. Only `w:t` counts, so a tag in a field code (`w:instrText`) or a tracked deletion (`w:delText`) is not filled, as the `VisibleText` contract says.
- The pattern is `{{ *<key> *}}` with the key escaped, ignoring case, built with `RegexOptions.NonBacktracking` (`ai/learnings.md`, 2026-09-30). `A_Parameter_Key_Is_Matched_Literally` pins this.
- A value is written with `ToString()`, `null` as empty text. Each line ending becomes a `w:br`, and a run whose text starts or ends with a space gets `xml:space="preserve"`. `A_Parameter_Value_Is_Inserted_Verbatim` and `A_Multiline_Parameter_Becomes_A_Line_Break` pin both.
- Tags are filled in the body, headers and footers, and in the text boxes inside them. Whether Word.Spire also fills footnotes and endnotes is not measured here; step 2 measures it and matches it.
- Word writes a text box twice, inside `mc:AlternateContent`: a DrawingML choice and a VML fallback, each with its own `w:txbxContent`. The SDK keeps both by default. Tags and blocks are applied to both, so the two stay alike; `MiniTemplateWalk` already walks both, and `A_Text_Box_Is_Filled_In_Both_Of_Its_Copies` pins it.

### Collection tables, pictures, bookmarks and HTML

- **Collection tables.** The first `w:tbl` whose `w:tblPr/w:tblCaption` equals the key, case included, as Word.Spire's `FindTable` matches `Title`. Its second row is cloned per collection row, and each cell's first paragraph is filled with the row's fields, `{{ row_number }}` included. Every quirk *Collection Tables* in `office.word.instructions.md` documents is kept as it is. A loop and a titled table may read the same collection (`A_Loop_And_A_Titled_Table_Read_The_Same_Collection`): the blocks are resolved first, the table is filled after.
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
- **Ids.** Imported content is a copy, and a copy keeps ids Word needs unique (`ai/learnings.md`, 2026-10-07). `wp:docPr/@id`, content-control `w:id` and bookmark ids are renumbered, as `MiniTemplateWalk.Scrub` renumbers a loop's copies; a bookmark whose name the destination already has is renamed; VML shape ids go through `VmlShapeIds.MakeUnique` on the written package. `w14:paraId` and `w14:textId` are removed; Word regenerates them.
- **Sections.** A nested document's `w:sectPr` elements are dropped, so its sections flatten into the host's, as on Word.Spire. In a merge, each input keeps its sections (see *Merge*).
- **Adjacent tables.** Two tables with no paragraph between them are one table to Word (`ai/learnings.md`, 2026-10-07). Content that ends in a table, imported before a table, keeps a paragraph between the two.
- **Text boxes.** A `<{ key }>` in a text box is replaced in both of its copies (`A_Nested_Document_In_A_Text_Box_Is_Inserted_In_Both_Of_Its_Copies`).

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

- Each input goes through `Create` first, once, with the first input as its font reference where it asks for `InheritFont` (`ai/learnings.md`, 2026-10-05).
- The first input's package is the base, so its styles, settings and theme win.
- Each later input's content is imported section by section. Its last `w:sectPr` becomes the section break that ends it, keeping its page setup and its header and footer references, whose parts are imported.
- Only the joint between two inputs is set: the first section of a later input starts on a new page, or on an odd page when `MergeJoints.StartsOnOddPage` says so (decision 2). An input keeps its own section breaks. `Merged_Documents_Start_On_A_New_Page` pins the first.

### Input options

- **`InheritFont`.** It applies where Word.Spire applies it, when the document is not its own reference: nested documents, headers and footers, and merged inputs after the first.
  - The reference font is resolved from the `Normal` style's `w:rFonts` and `w:sz`, then its `basedOn` chain, then `w:docDefaults`.
  - A theme reference such as `minorHAnsi` is read from the theme part.
  - The font is written on the runs of each paragraph whose style name starts with `Normal`, or that has no style.
- **`HorizontalAlignment`.** `w:jc` on the same paragraphs. `Justify` is written as `both`.
- **`RemoveEmptyParagraphs`.** Removes the body paragraphs with no content, as Word.Spire removes body paragraphs without children. A paragraph carrying `w:sectPr` stays, because it is a section break.
- **`EnforceEvenAmountOfPages`.** See decision 2.

### Text and image extraction

`GetText` and `GetImages` run against the created document, as on every creating backend, and start from Word.Mini's. A picture's name is its Alt Text title, then its description, then its object name, then its part name.

Word.Mini's `AppendText` appends every paragraph under the body, headers and footers. Read from the code, not run: a text box's paragraphs are reached once per copy, and again through the paragraph that anchors the box, so its text would be reported several times. Step 1 measures what Word.Spire's `GetText` returns for a template with a text box, and Word.OpenXml reports it that way: each text box once, from its DrawingML copy.

### Inputs

- `.docx`, `.dotx`, `.docm` and `.dotm` are read. The format is judged from the content, since the SDK reads nothing but a WordprocessingML package whatever the file is called. Anything else throws `NotSupportedException` naming what was found: an OLE container (`.doc`, `.dot`) or a zip that is not a word-processing package (`.odt`). `SourceFormat.Sniff` in Word.Gotenberg recognises the same signatures, though Word.Gotenberg lets a file name decide first. The guide sends `.doc` and `.odt` templates to Word.Spire or Word.Aspose.
- Templates often come from users. The package is opened only within the limits Word.Gotenberg's scan uses, `TemplateMarkers.MaxBytes` (32 MiB uncompressed) and `MaxParts` (1,000), judged from the zip directory before anything is read. Over them, `Create` throws. Word.Gotenberg's scan sends the package on unread instead, but a creator has nothing to fall back to.

### Out of scope

- **`Convert` and `ToImages`.** These belong to Word.Gotenberg (decision 3).
- **`DocumentBuilder`.** Word.Spire, Word.Syncfusion and Word.Aspose each ship one (decision 4).
- **Comments** in imported content are dropped.
- **Retiring Word.Mini.** Word.OpenXml covers everything Word.Mini does except MiniWord's own syntax. Whether Word.Mini stays is a separate decision.

## Dependencies

- **DocumentFormat.OpenXml** 3.5.1. MIT, maintained by Microsoft, the latest release, and already pinned by Word.Mini and Word.Gotenberg. It ships `net8.0` and `net10.0` assets.
- **HtmlToOpenXml.dll** 3.5.0, the latest release. MIT; targets `net8.0`, `net10.0`, `netstandard2.0` and `net462`. It depends on DocumentFormat.OpenXml ≥ 3.5.1, AngleSharp ≥ 1.5.0 and Microsoft.Extensions.Logging.Abstractions ≥ 6.0.0. It is used only for `html_` parameters.
- **Clippit**, evaluated and not taken (decision 1). It is an MIT fork of Open-XML-PowerTools, maintained by Sergey Tihon. Its `DocumentBuilder` merges documents with the style, numbering and relationship fixes the importer needs.
  - The latest release, 3.10.1 (2026-11-22), targets `net10.0` and `net11.0`. The last release with a `net8.0` asset is 3.4.4; Regira's packages target `net8.0` and `net10.0` today.
  - Since 3.5.0 it depends on SkiaSharp; 3.10.1 asks for SkiaSharp 4.153.1 and `SkiaSharp.NativeAssets.Linux.NoDependencies`, a native library for a package that merges XML. The family already carries SkiaSharp 4.156.0 in Drawing.SkiaSharp and PDF.PdfPig, so the version resolves, but a Word.OpenXml consumer would get it without asking. Up to 3.4.7 the dependency was SixLabors.ImageSharp.Drawing.
  - It edits parts as LINQ to XML documents cached on the part, beside the SDK's typed DOM. Mixing the two in one package needs a save between them.
  - .NET 8 support ended on 2026-11-10, and `proposals/dotnet-11.md` recommends `net10.0;net11.0` for 7.0.0. Once that is decided, the framework objection goes away and the dependency one stays.

## Decisions

1. **The importer: written here or Clippit?**
   - Written here: an importer of the shape above, covering what Word.Spire's templates need. The spike shows the hardest part, copying parts across packages, is the SDK's own.
   - Clippit: a tested merger, but with no `net8.0` asset on current releases, a native graphics dependency, and a second object model.

   Recommendation: write it here, and look at Clippit again if the importer's long tail outgrows it once `net8.0` is dropped.
2. **`EnforceEvenAmountOfPages` without a page count.** A section's `w:type` of `oddPage` makes Word and LibreOffice start it on an odd page, inserting a blank page when needed, at layout time.
   - In `Merge`, the joints follow `MergeJoints.StartsOnOddPage`, as on the other backends: an input carrying the option starts on an odd page, and so does the one after it. That is Word.Spire's pagination except for the blank page Word.Spire adds after a last, odd document.
   - For `Create` on its own, no section type helps: a trailing section always occupies a page of its own.
   - Option 1: odd-page joints in `Merge`, `NotSupportedException` in `Create`, and in `Merge` for the last input, whose padding cannot be written.
   - Option 2: `NotSupportedException` wherever the option is set, as Word.Mini throws for any non-default `InputOptions`.

   Recommendation: option 1, with the difference in the guide's limits note. A pairing with Word.Gotenberg could count the pages of the PDF and pad the document, but that is the pairing's feature, not the creator's.
3. **A converter, or none?** A converter could write `.docx`, `.dotx`, `.docm` and `.dotm` with `ChangeDocumentType`, apply `ConversionOptions.Settings` with Word.Gotenberg's page setup, and throw for every other format. It would also join the last-wins `IWordConverter` contest with Word.Gotenberg, whose `Convert` is the one the pairing needs. Recommendation: none.
4. **`DocumentBuilder`: now or later?** The builder's `Paragraph.Style` names Word's built-in styles. A `w:pStyle` naming a style the styles part does not define renders as Normal, so the builder has to write the definitions for the ones it uses. Recommendation: later, as its own step, if a consumer asks.
5. **Where the Open XML binding lives.** The block syntax and the order of the walk are shared in `Regira.Office`. What Word.OpenXml needs beside them is the Open XML binding, which exists in Word.Mini: `MiniTemplateWalk`, `TrimTags`, `ContentRoots`, `GetText` and `GetImages`. It cannot move into `Regira.Office`, which does not reference the SDK; its own package helpers read the zip with `System.Xml.Linq` for that reason.
   - Option 1: copy it into Word.OpenXml. Two Open XML bindings then have to stay alike, and every change to what `TemplateWalk<TNode>` asks of a model is made twice for the same model.
   - Option 2: move it into Word.OpenXml, and have Word.Mini reference Word.OpenXml and reach it through `InternalsVisibleTo`. The walk takes MiniWord's value neutraliser as a hook. Word.Mini becomes a thin MiniWord adapter. It then also carries HtmlToOpenXml.dll and AngleSharp, which it never calls, and binds to Word.OpenXml's internals as both already bind to `Regira.Office`'s.
   - Option 3: a third package holding only the binding, referenced by both. Word.Mini gains no unused dependency, at the price of one more package to publish in step with the family.

   Recommendation: option 2. One binding is worth an unused managed dependency in Word.Mini; take option 3 only if that dependency is unwanted.

## Steps

1. **The package and the read path.**
   - `src/Word.OpenXml/` with its `.csproj` and `README.md`.
   - The `InternalsVisibleTo` entry in `Common.Office.csproj`, and the solution entry in `Regira-Packages.slnx`.
   - The input checks, `GetText` and `GetImages`, with the binding where decision 5 puts it.
   - Tests of their own in `tests/Office.Word.testing`, with a project reference: each refused input format and both limits, and what `GetText` returns for a text box on Word.Spire and here.
   - The shared fixture starts in step 2: every shared scenario assumes a backend that fills `{{ key }}` parameters.
2. **Filling without import.**
   - Blocks, tags, bookmarks, collection tables, pictures and `html_`.
   - Measure whether Word.Spire fills tags in footnotes and endnotes, and match it.
   - `OpenXmlTests : WordTestsBase`, a `[WordFixture]` with output folder `OpenXml` and `SourceFiles` of `multipage.docx`. It leaves out `Converting`, `PageImages`, `OtherFormats` and `DocumentBuilder` for good, and `NestedDocuments`, `HeadersAndFooters`, `Merging` and `InputOptions` until the steps that bring them.
   - Add `bookmarks.docx` to `Assets/Input` as the `.docx` twin of `bookmarks.dot`, and an `OpenXmlTests` override of `Bookmarks` that reads it.
   - That gives the fixture every scenario that needs only `Creating`, the block and loop scenarios among them, plus `TitledTables`, `AltTextPictures`, `HtmlParameters` and the extraction scenarios.
3. **The importer, nested documents, headers and footers.**
   - Take `NestedDocuments` and `HeadersAndFooters` off the fixture's `LeavesOut`.
   - Add importer scenarios to `OpenXmlTests`: a picture in an imported header, an imported list restarting its numbering, a hyperlink, a footnote, a chart, a table imported next to a table, and distinct `wp:docPr`, content-control and bookmark ids.
   - Every created document in these scenarios is free of `Docx.ValidationErrors`.
4. **`Merge` and `InputOptions`.** Take `Merging` and `InputOptions` off the `LeavesOut`, and add the `EnforceEvenAmountOfPages` behaviour decision 2 chooses. The three scenarios that need `Merging` and `Converting` on one backend do not reach this fixture.
5. **The Gotenberg pairing.** Add a second `GotenbergTests`-shaped fixture with Word.OpenXml as the creator, without the `LeavesOut` that names Word.Mini. It runs `A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them`, and converts a template with headers, footers and a nested document to PDF, which Word.Mini's creator refuses. Handing the base a composite of the two services would also bring the three merge-and-convert scenarios, the padding ones among them.
6. **Docs.**
   - `office.word.instructions.md`:
     - the *Installation* block and the *Backend Comparison* row (`Word.OpenXml` | Open XML SDK | ✓ | — | ✓ | text, images | Apache-2.0, no key, no size cap) with its recommendation, which today sends a licence-free deployment to Word.Mini;
     - a *Word.OpenXml limits* note;
     - the *IWordService* paragraph;
     - the backend lists in the *Collection Tables*, *HTML Parameters*, *Conditional Blocks* and *Loop Blocks* headings;
     - the *Word.Gotenberg limits* sentence naming the creators with a document model;
     - *Registration*.
   - `office.word.examples.md` if the licence-free pairing earns an example there, and `office.card.md` and `office.namespaces.md`.
   - `docs/word/README.md`: *Projects*, *Installation*, the backend lists in *Collection tables*, *Conditional blocks* and *Loop blocks*, and a `### Word.OpenXml` section under *Implementation notes*.
   - `docs/word/examples.md`: *Example 8* pairs Word.Mini with Word.Gotenberg, and could pair Word.OpenXml instead.
   - The messages in code that name a creator: Word.Mini's `EnsureSupported` ("Use Regira.Office.Word.Spire for these") and Word.Gotenberg's `GetSource` and `EnsurePdf`.
   - The routing table in `ai/AGENTS.md`.
   - The `office-word` group in `tools/GuideVerifier/projects.json`.
   - CHANGELOG bullets for `Regira.Office.Word.OpenXml`, for `Regira.Office`, whose assembly gains the `InternalsVisibleTo` entry, for Word.Mini with decision 5's option 2, and for Word.Mini and Word.Gotenberg where a message changes.
   - `ai/learnings.md` for whatever the importer teaches.

## Risks

- **The importer's long tail.** OOXML content can reference a lot: SmartArt, OLE objects, content controls bound to custom XML parts, glossary documents, comments. Rewriting relationship attributes generically covers anything that points at a part, and `AddPart` copies the part with its children. What the importer does not carry, it drops on purpose, and the guide names it. The importer scenarios and `Docx.ValidationErrors` in step 3 are the guard. The validator flags repeated `docPr` and VML shape ids, not repeated content-control ids or bookmark names (`ai/learnings.md`, 2026-10-07), so those two have scenarios of their own.
- **Fidelity next to Word.Spire.** The destination's styles and `w:docDefaults` win, so imported text can look different from the same merge on Word.Spire. Word.Spire's own behaviour on clashing styles is not measured here. Comparing the `Merge` and `Nested_Documents` outputs of both backends side by side in step 4 would show the difference. On FreeSpire.Doc that comparison stays under 500 paragraphs and 25 tables.
- **Blank pages in Gotenberg's PDF.** LibreOffice's PDF export has an option to leave out automatically inserted blank pages, the ones an odd-page section start creates. Check that Word.Gotenberg's route keeps them before decision 2's option 1 relies on them.
- **HTML in a header.** HtmlToOpenXml adds a picture's or list's part to the part it is given. Check that an `html_` tag in a header gets its parts on the header's part.
- **A fifth creating backend.** Every template feature that touches the document model is then written for one more model. The block walk is the exception: its order lives once in `TemplateWalk<TNode>`, and with decision 5's option 2 the Open XML model has one binding, shared with Word.Mini.
- **The internal binding.** Word.OpenXml binds to `Regira.Office` internals like the other Word backends, so it publishes on the family's aligned number (`ai/learnings.md`, 2026-09-30). With decision 5's option 2, Word.Mini binds to Word.OpenXml's internals the same way.
- **An output that is a template again.** A value that fills a paragraph with a lone `{{#if X}}` makes the created document use blocks (`ai/learnings.md`, 2026-09-30). In the pairing, Word.Gotenberg sends what its creator returned straight to the server in the same call, so that path is safe, and step 5 pins it with this creator. An application that creates with Word.OpenXml and converts the result in a second call meets it, as with every creator; the guide's existing advice covers it.

<!-- {% endraw %} -->
