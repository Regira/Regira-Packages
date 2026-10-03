# Loop blocks in Word templates
<!-- {% raw %} -->

As of 2026-10-01. Sources: the `Regira-Packages` repository, branch `wip` at `5219481`. Vendor loop features are named from their own documentation and were not measured here. Nothing on this page has been built.

**Status: proposal, targeted at 6.5.1. Six decisions are open (see *Decisions*).**

## Recommendation

Add `{{#each Key}}` … `{{/each}}` as a second kind of template block beside `{{#if}}`, with the same rules as `{{#if}}`:

- each marker stands alone in its own paragraph;
- the syntax and the decision live once in `Regira.Office`;
- Word.Spire, Word.Syncfusion, Word.Aspose and Word.Mini all read it the same way.

Everything between the markers (paragraphs, tables, whole pages) is written once per row of `CollectionParameters[Key]`, and inside the block `{{Field}}` reads that row.

```text
{{#each Orders}}
Order {{Number}} of {{Date}}
{{#if Note}}
{{Note}}
{{/if}}
{{else}}
There are no orders this month.
{{/each}}
```

The titled-table construction stays as it is. It keeps the same lookup, the same template row and the same output, and one `CollectionParameters` key can feed both a titled table and a loop.

The table already exists, so a loop is only worth adding for what the table cannot do:

1. **Content other than a table row.** Paragraphs, list items, a heading with a table per item, a page per item.
2. **Nesting.** A loop can run over a collection held by the current row, such as an order's lines.
3. **Per-row conditions.** An `{{#if}}` inside a loop reads the row. An `{{#if}}` inside a titled table's template row is decided once for every row (*Conditional Blocks* in `office.word.instructions.md`).
4. **Several rows per item.** Marker rows (step 3) give this. The titled table has exactly one template row.
5. **A visible loop.** A titled table is marked only by its Alt Text title, which a reader of the template never sees.

The steps, in order:

1. `Regira.Office`: `ConditionalBlocks` becomes `TemplateBlocks`. It handles two kinds of block and a chain of scopes, and its `Resolve` returns placements instead of removals.
2. Word.Spire, Word.Syncfusion, Word.Aspose and Word.Mini: the block walk runs top-down. It clones a block's content once per row and fills that row's fields.
3. Marker rows in a table. This step can ship separately (decision 1).
4. Word.Gotenberg: the exception message changes, and a document with a lone `{{#each}}` paragraph needs a creator to convert.
5. Guides, docs and the changelog.

## What exists today

| What | Where | Effect |
|---|---|---|
| A collection fills the table whose Alt Text title is its key | `FindTable` in each backend's `Extensions/WordDocumentExtensions.cs` | Nothing visible in the template says which table repeats. Only the first table with that title is filled |
| The table's second row is the template row. Rows before and after it stay | `table.Rows[1]` in the Spire, Syncfusion and Aspose `WordService` | One row per item, and one template row per table |
| Only a cell's first paragraph is filled | `FirstParagraph` (Spire, Aspose), `Paragraphs[0]` (Syncfusion) | A second paragraph in the cell keeps its `{{Field}}` text |
| Every `{{tag}}` in the row reads the item, regardless of case, and a tag the item lacks becomes empty | `itemDic.TryGetValue` over `DictionaryUtility.ToDictionary`, which ignores case by default | A global `{{Currency}}` in the row is blanked before the global pass reaches it |
| The row reads only tags matching `ParamRegex` (`{{ *[a-zA-Z0-9._]+ *}}`), and writes values with `ToString()` | The template-row fill in each `WordService` | A value is written in the current culture, and a line ending in it is not turned into a line break, as the global pass does |
| `ToDictionary` reads dictionaries and objects | `src/Common/Utilities/DictionaryUtility.cs` | It has no `JsonElement` case: an element falls through to reflection and yields its `ValueKind`. Two keys that differ only in case throw `ArgumentException` |
| `{{#if}}` blocks are resolved first, against the input | `ResolveConditions` opens each `CreateDocument` pipeline | A block in a titled table's template row is decided once for all rows |
| A block opens and closes in one container | `ConditionalBlocks.Resolve`, per body, cell, text box, content control, header or footer, within one section | A block across a section break throws `FormatException`. The `acrossSections` case in `WordTestsBase` pins it |
| Word.Mini loops through MiniWord | `GetMiniValue` hands the collection to MiniWord, whose table row holds `{{Items.Name}}` tags | Its template syntax differs from the other three backends', and it has no `{{row_number}}` |
| The titled table is documented as it works | *Collection Tables* in `office.word.instructions.md` and `docs/word/README.md`, added in 6.5.0 | The loop's docs sit beside it |
| 6.5.0 keeps a lone `{{#each}}` line as text | The XML docs of `ConditionalBlocks` and `OpensBlock` say `{{#each}}` does not open a block, since other template languages write it on a line of its own. `ConditionalBlocksTests` asserts `OpensBlock("{{#each items}}")` is false. The shared test `Marker_Text_In_A_Document_Without_Blocks_Stays_As_It_Is` keeps a Handlebars sample's `{{#each items}}`, `{{else}}` and `{{/each}}` on Aspose, Mini, Spire and Syncfusion. `GotenbergUnitTests.A_Document_That_Opens_No_Block_Needs_No_Creator` converts the same sample without a creator. The guide and `docs/word/README.md` define a document that uses blocks by `{{#if}}` alone, and the 6.5.0 bullet says a document that merely writes about markers converts as on 6.4.0 | Adding `#each` to what makes a document use blocks changes all of these. 6.5.0 is unpublished, so decision 5 settles which version carries the change. The two Handlebars cases are the only end-to-end tests of a bare `{{else}}` staying as text |
| In a document that uses blocks, `{{#each}}` throws | `AnyMarker` with `IsWholeMarker` rejects it as not a conditional marker | No template that works on 6.5.0 holds a loop marker in a document that uses blocks |

Vendor loops were weighed on the same grounds as vendor conditions were (`ai/learnings.md`, 2026-09-27):

- Aspose's reporting engine (`<<foreach>>`) exists on Aspose only.
- Mail-merge regions (`TableStart:` / `TableEnd:` merge fields) appear in the Spire, Syncfusion and Aspose documentation. They need fields inserted through Word's field dialog, which an author cannot simply type, and Word.Mini has no merge engine.
- MiniWord's own `@foreach` and `{{foreach` exist on Word.Mini only.

None of them reads the same on all four backends, so the loop uses Regira's own markers, as the condition does.

## Design

### Syntax

- **Markers.** A paragraph holding only `{{#each Key}}` opens a loop and one holding only `{{/each}}` closes it. An optional `{{else}}` starts the content written when there are no rows.
- **Closers match their opener.** `{{/if}}` closes only an `{{#if}}` and `{{/each}}` only an `{{#each}}`. A mismatch throws `FormatException`.
- **What `Key` names.** A `CollectionParameters` entry, matched regardless of case. Inside a loop it can also name a field of an enclosing row whose value is a collection of rows; that is a nested loop. The enclosing rows are searched first, innermost out, then `CollectionParameters`, the same order as field lookup (decision 2).
- **A missing key gives zero rows,** just as a missing key is false for `#if`.
- **`GlobalParameters` values are not iterated.** A list of values belongs in `CollectionParameters` as rows. Decision 6 settles what a key naming a `GlobalParameters` entry gives.
- **Field lookup.** Inside a loop, `{{Field}}` reads the current row first, then the enclosing loops' rows from the innermost out. A field that none of them has is left for the global pass, so `{{Currency}}` still reaches `GlobalParameters`.
- **`{{row_number}}`** is the row's 1-based position, as in a titled table's template row.
- **Conditions inside a loop.** `{{#if Field}}` reads the same chain of rows, then `GlobalParameters` and `CollectionParameters` as it does today.
- **Block form only,** as for `#if`. In a document that uses blocks, a marker that shares its paragraph with other text throws. Inline loops belong with inline conditions in phase 2 of the `#if` work.
- **One container, one section.** A loop opens and closes in one container within one section, as an `#if` does. A page per item therefore puts a page break inside the loop, never a section break.
- **Writing values.** A row value is written the way the global pass writes a value: `ToString()` in the current culture, with line endings turned into line breaks. A titled table writes the same value without line breaks, so one key can read differently in a titled table and in a loop. The guide will say so.

Top-level rows are `ICollection<IDictionary<string, object>>`, as they are now. A nested row collection can be any sequence of:

- dictionaries;
- objects, read through `DictionaryUtility.ToDictionary` as the titled table reads them;
- a JSON array of objects, which is how a nested list arrives through the Office API. `ToDictionary` cannot read a `JsonElement`, so JSON rows get a reader of their own, which reads scalars by kind, as `IsTrue` already does.

A row whose keys differ only in case (`Name`, `name`) throws `ArgumentException` from `ToDictionary`, for a loop as for a titled table.

### What makes a document use blocks

A document uses blocks when one of its paragraphs holds `{{#if Key}}`, `{{#if !Key}}` or `{{#each Key}}` and nothing else.

This is the same trade-off as for `#if`. A finished document with a line reading only `{{#each items}}`, such as an article about Handlebars, is now read as a template. Its loop has no rows, so it is dropped. Word.Gotenberg follows the same test, so such a document needs an `IWordCreator` to convert, and throws `NotSupportedException` without one. 6.5.0 says the opposite in its XML docs, tests, guides and changelog (see *What exists today*); decision 5 settles which version carries the change.

### The shared contract

`ConditionalBlocks.Resolve` answers "which children are removed?". A loop needs more: which child is written, how many times, and with which row. So `Resolve` returns placements:

<!-- no-compile -->
```csharp
internal static class TemplateBlocks
{
    public static bool OpensBlock(string? text);        // #if, #if !, #each
    public static bool ContainsMarker(string? text);
    // the container's new children in order: the original child each one copies, and the scope it is filled in
    public static IReadOnlyList<Placement> Resolve(IReadOnlyList<string?> children, TemplateScope scope);
}

internal readonly record struct Placement(int Child, TemplateScope Scope);

internal sealed class TemplateScope
{
    public static TemplateScope Root(WordTemplateInput input);
    public bool IsRoot { get; }
    public int RowNumber { get; }
    public bool TryGetField(string key, out object? value);   // this row, then the enclosing rows
    public bool Evaluate(string key);                         // the rows, then the input: today's Evaluate
    public IEnumerable<TemplateScope> Rows(string key);       // the rows the key names, one child scope each
}
```

For a document with conditions but no loops, the placements are exactly the children today's `Resolve` keeps, each written once in the root scope. The `#if` behaviour is unchanged, and `ConditionalBlocksTests` carries over.

### The backend walk runs top-down

Today each backend collects the containers that hold a marker (`paragraph.Owner`) and resolves each one on its own. That works because a condition's outcome depends only on the input. A loop's content does not: a cell in a looped table has to be resolved once per row, against that row. So the walk starts at each story and works downward:

```text
Resolve(container, scope):
    texts      = the container's children: a paragraph's visible text, null for anything else
    placements = TemplateBlocks.Resolve(texts, scope)
    rebuild the children: a child's first placement moves it, each further placement clones it
    for each (child, childScope):
        a paragraph: fill childScope's fields in its own runs      (nothing to fill in the root scope)
        each container inside the child (cells, text boxes, content controls): Resolve(inner, childScope)
    end with a paragraph where the container requires one           (as today)
```

At each level, markers are read before any field is filled. A row value such as `{{/each}}` is therefore written as text and never parsed as a marker. The global, image and nested-document passes run afterwards, unchanged. A field is filled with the pattern the global pass uses: `{{ *key *}}`, regardless of case.

What each backend already has:

- **Spire:** `DocumentObject.Clone()`, which `InsertDocumentContent` already uses; `Paragraph.Replace(Regex, string)`; and the container walk in `ResolveConditions`.
- **Syncfusion:** `Entity.Clone()`, `WParagraph.Replace`, and `BlockParagraphs`, which already enters text boxes and shapes.
- **Aspose:** `Node.Clone(true)`, and `Range.Replace` with a literal replacement.
- **Mini:** the Open XML SDK's `CloneNode(true)`, which Word.Mini does not call yet, and the `Segments` split at section breaks. `TrimTags`, which runs on the block pass's result today, moves ahead of it. After it, every tag sits whole in one `w:t`, so filling a field is a plain string replace.

The walk keeps each backend's current scope:

- Spire and Aspose walk the whole tree and filter out footnotes and comments.
- Syncfusion names its stories (each section's body and the eight header and footer slots) and enters text boxes and shapes through `BlockParagraphs`.
- Mini reads the body, header and footer parts, split by `Segments`, with its own section-break handling.

### Clone hygiene

Copying content duplicates things Word expects to be unique. The object-model backends may renumber some of them on save, but Word.Mini's raw Open XML pass will not. Each item below needs a rule and a test:

| In the copied content | Rule |
|---|---|
| Bookmarks | Keep them in the first copy and drop them from the others. A duplicate name is invalid, and the global pass fills a bookmark by its name |
| Drawing ids (`wp:docPr/@id`) | Renumber in every copy |
| Footnote and endnote references, comment anchors | Refuse a loop that holds one (`FormatException`). Copying the note or comment for every copy can come later |
| Content-control ids | Renumber |
| Numbered paragraphs | Leave as they are. Numbering continues across copies, as a list built from the rows would |

The tests check each backend's output with `OpenXmlValidator` from the Open XML SDK, next to the text assertions. The Word tests already reach the SDK through Word.Mini and Word.Gotenberg (DocumentFormat.OpenXml 3.5.1).

Measured on 2026-10-01 with a scratch console app outside the repo (net10.0, DocumentFormat.OpenXml 3.5.1, the Office2019 and Microsoft365 formats alike), the validator reports a duplicate `wp:docPr/@id` and a duplicate bookmark id, and misses a duplicate bookmark name and a duplicate content-control id. The tests assert those two are distinct themselves. An id that a backend failed to renumber then fails a test instead of breaking a customer's document in Word.

### Marker rows (step 3)

A block cannot open between two table rows with a marker paragraph. A table's children are rows, so there is no paragraph between them. Marker rows fill that gap: a row whose only text is a single marker, in any one of its cells, is a marker row. The table then acts as a container whose children are its rows, and the same `Resolve` runs over them:

```text
| Description     | Qty     | Price     |
| {{#each Lines}} |         |           |
| {{Description}} | {{Qty}} | {{Price}} |
| {{#if Note}}    |         |           |
| {{Note}}                              |   <- a merged row, written only for lines with a note
| {{/if}}         |         |           |
| {{/each}}       |         |           |
| Total           |         | {{Total}} |
```

This cannot clash with today's in-cell blocks. A valid in-cell block needs at least two marker paragraphs in one cell. So in a document that uses blocks, a row whose only text is a single marker is never valid today: its cell holds an unclosed block, a closer with no opener, or a marker the syntax does not know (`{{#each}}` today), and it throws. No test covers a block across cells yet. In a document that uses no blocks, such a row stays as text, as any lone marker does. Marker rows turn that throw into working behaviour, and they add `{{#if}}` per row.

Invoice lines are where most loops are wanted. Marker rows give a table the two things the titled form lacks, several rows per item and per-row conditions, and they need no Alt Text title. The step can ship separately: without it, step 2 can still repeat a whole table, but not individual rows.

### Out of scope

- **Inline loops and separators.** An inline loop such as `{{#each Tags}}{{Name}}, {{/each}}`, and `@first` / `@last` for separators, come in phase 2 together with inline conditions.
- **A titled table or MiniWord table inside a loop.** Such a table reads the top-level `CollectionParameters` entry, and only the first table with the title is filled. Inside a loop, use marker rows instead. The guide will say so.
- **Images per row.** `Images` are matched by name across the whole document.
- **`html_` fields in rows.** HTML stays a `GlobalParameters` feature.
- **The titled table's own quirks.** It keeps filling only the first paragraph of a cell, blanking unknown tags and filling only the first table with the title. Changing any of these would change the output of templates already in use.

## Decisions

1. **Marker rows (step 3): now or later?** Recommendation: now. Without them, repeating rows still needs the titled table, and the loop would cover the less common cases first.
2. **Field and collection lookup inside a loop.** Recommendation: fall through the scopes as Mustache does: the row, then the enclosing rows, then the global pass for a field, or `CollectionParameters` for a loop's key. Strict Handlebars scoping would need `{{../Key}}` or `{{@root.Key}}` to reach a global. Losing a global silently inside a loop is the likelier mistake.
3. **Loop variables.** Recommendation: `{{row_number}}` only, the name the titled table already uses. Handlebars' `@index` is 0-based, and `@first` / `@last` serve inline separators, which wait for phase 2.
4. **Footnotes and comments inside a loop.** Recommendation: throw in step 2, and copy them later if someone asks.
5. **Which version changes the 6.5.0 texts.** 6.5.0 is unpublished, and it keeps a lone `{{#each}}` as text in its XML docs, three tests and a changelog bullet (see *What exists today*).
   - **Option 1: neutral before 6.5.0 ships.** Before 6.5.0 ships, drop the `{{#each}}` sentences from the XML docs and the `OpensBlock("{{#each items}}")` assertion from `ConditionalBlocksTests`. In the shared test and `GotenbergUnitTests`, replace the Handlebars sample with one that keeps a bare `{{else}}` without `{{#each}}`, such as a Go template's `{{range .Items}}` … `{{else}}` … `{{end}}`, so the bare-`{{else}}` coverage stays. 6.5.1 then changes nothing a published version promised. The guides' definition of a document that uses blocks is accurate for 6.5.0, and changes with 6.5.1.
   - **Option 2: ship them.** Ship them as they are, and list the change, Word.Gotenberg's included, as a 6.5.1 behaviour change.

   Recommendation: option 1.
6. **A key that names a `GlobalParameters` entry.** A list there is most likely meant as rows. Zero rows, as for a missing key, keeps the rule simple. A `FormatException` naming the key would catch the mistake. Recommendation: zero rows, with the guide saying where rows go, in line with a missing key being false for `#if`.

Decided: the loop ships in 6.5.1. It changes internals that every Word backend binds to (`ai/learnings.md`, 2026-09-30), so the whole Word family ships on that number.

## Steps

1. **`Regira.Office`.**
   - Rename `ConditionalBlocks` to `TemplateBlocks` (it stays internal) and add `TemplateScope`.
   - Parse `#each`, `else` and `/each`, with closers matched to their openers, and return placements.
   - Read nested rows from dictionaries, objects and JSON arrays.
   - The `Evaluate` and `IsTrue` rules stay the same.
   - Update the XML docs of `TemplateBlocks` and `OpensBlock`, which say a `{{#each}}` does not open a block.
   - `ConditionalBlocksTests` becomes `TemplateBlocksTests` and adds: loops, `else` with no rows, nesting, lookup order, JSON rows, `row_number` and mismatched closers.
2. **The four creating backends.**
   - Build the top-down walk and the clone hygiene into Spire, Syncfusion, Aspose and Mini. Mini runs `TrimTags` before the block pass.
   - Shared scenarios in `WordTestsBase`, run by the four creating backends. `GotenbergTests` derives from it but runs only the conversion scenarios, so Gotenberg's block behaviour stays pinned in `GotenbergUnitTests`. The scenarios:
     - paragraphs per row, a table per row, and `else` with no rows;
     - a missing key, nesting, and a nested JSON array;
     - `{{#if}}` reading the row, and a global inside a loop;
     - a loop inside a cell, and a loop in a header;
     - a row value holding marker text;
     - `OpenXmlValidator`-clean output, with bookmark names and content-control ids asserted distinct.
   - Unless decision 5 already changed them:
     - `Marker_Text_In_A_Document_Without_Blocks_Stays_As_It_Is` keeps its `{{#if}}` samples, and swaps its Handlebars sample for one that keeps a bare `{{else}}` without `{{#each}}`;
     - `ConditionalBlocksTests`' `OpensBlock("{{#each items}}")` assertion flips.
3. **Marker rows.**
   - Each walk treats a table as a container whose children are its rows.
   - Scenarios: several rows per item, a conditional row, and a loop beside a titled table fed by the same key.
4. **Word.Gotenberg.** The `NotSupportedException` names *template blocks*. `ConditionalMarkers` follows `OpensBlock` and needs no code change of its own. The behaviour does change: an OOXML document with a lone `{{#each}}` paragraph needs a creator to convert, and throws without one. `GotenbergUnitTests` gains that case, its Handlebars case changes as decision 5 says, and the changelog bullet says so.
5. **Docs.**
   - `office.word.instructions`: one *Template Blocks* section that covers conditions and loops, and a pointer from *Collection Tables* to marker rows.
   - `office.word.examples`.
   - `docs/word`: the README and the examples.
   - The FreeSpire note: a loop that copies a table counts toward the table cap. `office.word.instructions.md` already states the 500-paragraph and 25-table caps, which this repo has not measured (`ai/learnings.md` measures only the PDF page cap). Measure them, and correct the guide where they differ.
   - CHANGELOG bullets for each changed package.
   - `ai/learnings.md`, if the clone hygiene exposes a difference between vendors.

## Risks

- **FreeSpire.Doc caps.** A loop copies content, and the free edition stops at 500 paragraphs or 25 tables, by the vendor's account (not measured here; see step 5). A loop with a table per item would hit the table cap at 25 items. The titled table adds rows rather than tables, so it is far less exposed.
- **Values that hold `{{Key}}`.** Row fields are filled before the global pass, so a row value containing `{{Currency}}` gets the global's value, just as a titled table's values do today. This is not new, but a loop makes it reachable from more places.
- **The walk rewrite touches `#if`.** The top-down walk replaces code that has just passed two pre-PR reviews. The shared `#if` scenarios in `WordTestsBase` run unchanged against the new walk and are the guard.

<!-- {% endraw %} -->
