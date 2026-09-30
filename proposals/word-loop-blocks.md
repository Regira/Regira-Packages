# Loop blocks in Word templates

As of 2026-09-30. Sources: the `Regira-Packages` repository, branch `wip`, as read on 2026-09-30. The working tree's uncommitted changes touch no Word source. Vendor loop features are named from their own documentation and were not measured here. Nothing on this page has been built.

**Status: proposal, targeted at 6.5.1. Four decisions are open (see *Decisions*).**

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
3. **Per-row conditions.** An `{{#if}}` inside a loop reads the row. An `{{#if}}` inside a titled table's template row is decided once for every row (`office.word.instructions.md:225`).
4. **Several rows per item.** Marker rows (step 3) give this. The titled table has exactly one template row.
5. **A visible loop.** A titled table is marked only by its Alt Text title, which a reader of the template never sees.

The steps, in order:

1. `Regira.Office`: `ConditionalBlocks` becomes `TemplateBlocks`. It handles two kinds of block and a chain of scopes, and its `Resolve` returns placements instead of removals.
2. Word.Spire, Word.Syncfusion, Word.Aspose and Word.Mini: the block walk runs top-down. It clones a block's content once per row and fills that row's fields.
3. Marker rows in a table. This step can ship separately (decision 1).
4. Word.Gotenberg: only an exception message changes.
5. Guides, docs and the changelog.

## What exists today

| What | Where | Effect |
|---|---|---|
| A collection fills the table whose Alt Text title is its key | `FindTable` in each backend's `Extensions/WordDocumentExtensions.cs` (Spire `:67`, Syncfusion `:49`, Aspose `:47`) | Nothing visible in the template says which table repeats. Only the first table with that title is filled |
| The table's second row is the template row. Rows before and after it stay | `table.Rows[1]` (Spire `WordService.cs:569`, Syncfusion `:644`, Aspose `:547`) | One row per item, and one template row per table |
| Only a cell's first paragraph is filled | `FirstParagraph` (Spire `:580`, Aspose `:558`), `Paragraphs[0]` (Syncfusion) | A second paragraph in the cell keeps its `{{Field}}` text |
| Every `{{tag}}` in the row reads the item, regardless of case, and a tag the item lacks becomes empty | `itemDic.TryGetValue` (Spire `:592`, Syncfusion `:666`, Aspose `:574`) over `DictionaryUtility.ToDictionary`, which ignores case by default | A global `{{Currency}}` in the row is blanked before the global pass reaches it |
| `{{#if}}` blocks are resolved first, against the input | `ResolveConditions` opens each `CreateDocument` pipeline | A block in a titled table's template row is decided once for all rows |
| Word.Mini loops through MiniWord | `GetMiniValue` hands the collection to MiniWord, whose table row holds `{{Items.Name}}` tags | Its template syntax differs from the other three backends', and it has no `{{row_number}}` |
| The titled table is documented as it works | *Collection Tables* in `office.word.instructions.md` and `docs/word/README.md`, added in 6.5.0 | The loop's docs sit beside it |
| The 6.5.0 texts make no promise about a lone `{{#each}}` line | The *Conditional Blocks* rules and the 6.5.0 changelog bullets list only marker text among other text and a stray `{{else}}` or `{{/if}}` as staying | Adding `#each` to what makes a document use blocks breaks nothing a published version promised |

Vendor loops were weighed on the same grounds as vendor conditions were (`ai/learnings.md`, 2026-09-27):

- Aspose's reporting engine (`<<foreach>>`) exists on Aspose only.
- Mail-merge regions (`TableStart:` / `TableEnd:` merge fields) appear in the Spire, Syncfusion and Aspose documentation. They need fields inserted through Word's field dialog, which an author cannot simply type, and Word.Mini has no merge engine.
- MiniWord's own `@foreach` and `{{foreach` exist on Word.Mini only.

None of them reads the same on all four backends, so the loop uses Regira's own markers, as the condition does.

## Design

### Syntax

- **Markers.** A paragraph holding only `{{#each Key}}` opens a loop and one holding only `{{/each}}` closes it. An optional `{{else}}` starts the content written when there are no rows.
- **Closers match their opener.** `{{/if}}` closes only an `{{#if}}` and `{{/each}}` only an `{{#each}}`. A mismatch throws `FormatException`.
- **What `Key` names.** A `CollectionParameters` entry, matched regardless of case. Inside a loop it can also name a field of an enclosing row whose value is a collection of rows; that is a nested loop.
- **A missing key gives zero rows,** just as a missing key is false for `#if`.
- **`GlobalParameters` values are not iterated.** A list of values belongs in `CollectionParameters` as rows.
- **Field lookup.** Inside a loop, `{{Field}}` reads the current row first, then the enclosing loops' rows from the innermost out. A field that none of them has is left for the global pass, so `{{Currency}}` still reaches `GlobalParameters`.
- **`{{row_number}}`** is the row's 1-based position, as in a titled table's template row.
- **Conditions inside a loop.** `{{#if Field}}` reads the same chain of rows, then `GlobalParameters` and `CollectionParameters` as it does today.
- **Block form only,** as for `#if`. In a document that uses blocks, a marker that shares its paragraph with other text throws. Inline loops belong with inline conditions in phase 2 of the `#if` work.
- **Writing values.** A row value is written the way the global pass writes a value: line endings become line breaks.

Top-level rows are `ICollection<IDictionary<string, object>>`, as they are now. A nested row collection can be any sequence of:

- dictionaries;
- objects, read through `DictionaryUtility.ToDictionary` as the titled table reads them;
- a JSON array of objects, which is how a nested list arrives through the Office API.

### What makes a document use blocks

A document uses blocks when one of its paragraphs holds `{{#if Key}}`, `{{#if !Key}}` or `{{#each Key}}` and nothing else.

This is the same trade-off as for `#if`. A finished document with a line reading only `{{#each items}}`, such as an article about Handlebars, is now read as a template. Its loop has no rows, so it is dropped. The 6.5.0 docs make no promise about such a line.

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
- **Mini:** `CloneNode(true)` and the `Segments` split at section breaks. `TrimTags` moves ahead of the block pass. After it, every tag sits whole in one `w:t`, so filling a field is a plain string replace.

### Clone hygiene

Copying content duplicates things Word expects to be unique. The object-model backends may renumber some of them on save, but Word.Mini's raw Open XML pass will not. Each item below needs a rule and a test:

| In the copied content | Rule |
|---|---|
| Bookmarks | Keep them in the first copy and drop them from the others. A duplicate name is invalid, and the global pass fills a bookmark by its name |
| Drawing ids (`wp:docPr/@id`) | Renumber in every copy |
| Footnote and endnote references, comment anchors | Refuse a loop that holds one (`FormatException`). Copying the note or comment for every copy can come later |
| Content-control ids | Renumber |
| Numbered paragraphs | Leave as they are. Numbering continues across copies, as a list built from the rows would |

The tests check each backend's output with `OpenXmlValidator` from the Open XML SDK, next to the text assertions. An id that a backend failed to renumber then fails a test instead of breaking a customer's document in Word.

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

This cannot clash with today's in-cell blocks. A valid in-cell block needs at least two marker paragraphs in one cell. So a row whose only text is a single marker is never valid today: it throws. Marker rows turn that throw into working behaviour, and they add `{{#if}}` per row.

Invoice lines are where most loops are wanted. Marker rows give a table the two things the titled form lacks, several rows per item and per-row conditions, and they need no Alt Text title. The step can ship separately: without it, step 2 can still repeat a whole table, but not individual rows.

### Out of scope

- **Inline loops and separators.** An inline loop such as `{{#each Tags}}{{Name}}, {{/each}}`, and `@first` / `@last` for separators, come in phase 2 together with inline conditions.
- **A titled table or MiniWord table inside a loop.** Such a table reads the top-level `CollectionParameters` entry, and only the first table with the title is filled. Inside a loop, use marker rows instead. The guide will say so.
- **Images per row.** `Images` are matched by name across the whole document.
- **`html_` fields in rows.** HTML stays a `GlobalParameters` feature.
- **The titled table's own quirks.** It keeps filling only the first paragraph of a cell, blanking unknown tags and filling only the first table with the title. Changing any of these would change the output of templates already in use.

## Decisions

1. **Marker rows (step 3): now or later?** Recommendation: now. Without them, repeating rows still needs the titled table, and the loop would cover the less common cases first.
2. **Field lookup inside a loop.** Recommendation: fall through the scopes as Mustache does: the row, then the enclosing rows, then the global pass. Strict Handlebars scoping would need `{{../Key}}` or `{{@root.Key}}` to reach a global. Losing a global silently inside a loop is the likelier mistake.
3. **Loop variables.** Recommendation: `{{row_number}}` only, the name the titled table already uses. Handlebars' `@index` is 0-based, and `@first` / `@last` serve inline separators, which wait for phase 2.
4. **Footnotes and comments inside a loop.** Recommendation: throw in step 2, and copy them later if someone asks.

Decided: the loop ships in 6.5.1. It changes internals that every Word backend binds to (`ai/learnings.md`, 2026-09-30), so the whole Word family ships on that number.

## Steps

1. **`Regira.Office`.**
   - Rename `ConditionalBlocks` to `TemplateBlocks` (it stays internal) and add `TemplateScope`.
   - Parse `#each`, `else` and `/each`, with closers matched to their openers, and return placements.
   - The `Evaluate` and `IsTrue` rules stay the same.
   - `ConditionalBlocksTests` becomes `TemplateBlocksTests` and adds: loops, `else` with no rows, nesting, lookup order, `row_number` and mismatched closers.
2. **The four creating backends.**
   - Build the top-down walk and the clone hygiene into Spire, Syncfusion, Aspose and Mini. Mini runs `TrimTags` before the block pass.
   - Shared scenarios in `WordTestsBase`, run on all four backends: paragraphs per row, a table per row, `else` with no rows, a missing key, nesting, `{{#if}}` reading the row, a global inside a loop, a loop inside a cell, a loop in a header, a row value holding marker text, and `OpenXmlValidator`-clean output.
3. **Marker rows.**
   - Each walk treats a table as a container whose children are its rows.
   - Scenarios: several rows per item, a conditional row, and a loop beside a titled table fed by the same key.
4. **Word.Gotenberg.** The `NotSupportedException` names *template blocks*. `ConditionalMarkers` follows `OpensBlock` and needs no change of its own.
5. **Docs.**
   - `office.word.instructions`: one *Template Blocks* section that covers conditions and loops, and a pointer from *Collection Tables* to marker rows.
   - `office.word.examples`.
   - `docs/word`: the README and the examples.
   - The FreeSpire note: a loop that copies a table counts toward the 25-table cap.
   - CHANGELOG bullets for each changed package.
   - `ai/learnings.md`, if the clone hygiene exposes a difference between vendors.

## Risks

- **FreeSpire.Doc caps.** A loop copies content, and the free edition stops at 500 paragraphs or 25 tables, so a loop with a table per item hits the table cap at 25 items. The titled table adds rows rather than tables, so it is far less exposed.
- **Values that hold `{{Key}}`.** Row fields are filled before the global pass, so a row value containing `{{Currency}}` gets the global's value, just as a titled table's values do today. This is not new, but a loop makes it reachable from more places.
- **The walk rewrite touches `#if`.** The top-down walk replaces code that has just passed two pre-PR reviews. The shared `#if` scenarios in `WordTestsBase` run unchanged against the new walk and are the guard.
