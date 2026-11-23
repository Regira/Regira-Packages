# Guides for the families that have none

As of 2026-11-23. Sources: the `Regira-Packages` repository, branch `wip` at `60b3414`: the routing table in `ai/AGENTS.md`, every `src/*/ai` folder, the `README.md` and `docs/` of the thirteen packages below, `tools/GuideVerifier/projects.json`, and the root `AGENTS.md`. The counts in *What exists today* were taken with `grep` and `wc` on that tree. The MCP server is built outside this repository, so how it indexes a new `ai/` folder is taken from the root `AGENTS.md` ("built from `ai/`") and was not observed. Nothing on this page has been written yet.

**Status: proposal, not scheduled. Six decisions are open (see *Decisions*).**

## Recommendation

Twelve rows of the routing table in `ai/AGENTS.md` say *No dedicated family guide*. They cover thirteen packages: `Regira.Common`, `Regira.Caching.Runtime`, `Regira.Serializing.Newtonsoft`, `Regira.Globalization.LibPhoneNumber`, `Regira.DAL.EFcore`, `Regira.DAL.MongoDB`, `Regira.DAL.MySQL`, `Regira.DAL.MySQL.MySqlBackup`, `Regira.DAL.PostgreSQL`, `Regira.DAL.SqlServer`, `Regira.Licensing`, `Regira.Office.Clients` and `Regira.Printing.GDI`.

An agent that works through the MCP reads guides, not READMEs: the knowledge base is built from `ai/`. For these packages it has the one line of the routing table and whatever another family's guide happens to say. Three of them, `Regira.Caching.Runtime`, `Regira.DAL.MongoDB` and `Regira.DAL.PostgreSQL`, are not named in any guide at all. Two are part of the paid product line: `Regira.Licensing` is how a key is registered, and `Regira.Office.Clients` is the hosted Office service's client.

Close the gap with **five guide sets, not thirteen**, placed by the rule the repository already has: a guide lives on the hub, and a provider gets a card that points to it.

| Guide | Home | Covers |
|---|---|---|
| `common.*` | `src/Common/ai` — `Regira.Common` becomes a hub | `Regira.Common`, and the three packages that implement its contracts: `Regira.Caching.Runtime`, `Regira.Serializing.Newtonsoft`, `Regira.Globalization.LibPhoneNumber` |
| `licensing.*` | `src/Common.Licensing/ai` | `Regira.Licensing` |
| `office.clients.*` | `src/Common.Office/ai`, beside the other Office guides | `Regira.Office.Clients`; `Regira.Printing.GDI` gets a section in `office.instructions.md` |
| `dal.efcore.*` | `src/DAL.EFcore/ai` | `Regira.DAL.EFcore` |
| `dal.*` | Decision 2 | Backup and restore: `Regira.DAL.MongoDB`, `Regira.DAL.MySQL`, `Regira.DAL.MySQL.MySqlBackup`, `Regira.DAL.PostgreSQL`, `Regira.DAL.SqlServer` |

Each provider package gets a `*.card.md` in an `ai/` folder of its own, as ten packages have today.

Most of the content exists. The READMEs of `Common` (447 lines), `DAL.EFcore` (173), `DAL.MongoDB` (146), `DAL.PostgreSQL` (133) and `DAL.SqlServer` (107) are manuals, and their snippets already compile under the `core` and `dal` groups of the guide verifier. The root `AGENTS.md` says a README is an index, not the manual. So each family is one change: the manual moves into a guide for agents and a `docs/` page for developers, and the README shrinks to the index it should be (decision 4).

Before writing, decide which of the thirteen stay (decision 1). A guide is a promise to keep it true.

The steps, in order:

1. Decide what stays.
2. Licensing.
3. Common, with its three providers.
4. Office.Clients and printing.
5. DAL.EFcore.
6. DAL backup and restore.
7. The routing table, and a live run per family.

## What exists today

| Package | Public types | README lines | `docs/` pages | Guide files that name it |
|---|---:|---:|---:|---|
| `Regira.Common` | 79 | 447 | 0 | 7, as a dependency |
| `Regira.Caching.Runtime` | 2 | 17 | 0 | 0 |
| `Regira.Serializing.Newtonsoft` | 6 | 67 | 0 | 5 (storage, invoicing, mail, web) |
| `Regira.Globalization.LibPhoneNumber` | 1 | 56 | 0 | 1 (`entities.blueprints.md`) |
| `Regira.DAL.EFcore` | 13 | 173 | 0 | 4 (Entities) |
| `Regira.DAL.MongoDB` | 9 | 146 | 1 | 0 |
| `Regira.DAL.MySQL` | 5 | 90 | 1 | 1 (`entities.setup.md`) |
| `Regira.DAL.MySQL.MySqlBackup` | 3 | 19 | 0 | 1 |
| `Regira.DAL.PostgreSQL` | 8 | 133 | 1 | 0 |
| `Regira.DAL.SqlServer` | 5 | 107 | 1 | 1 (`entities.setup.md`) |
| `Regira.Licensing` | 15 | 30 | 0 | 3 (Entities) |
| `Regira.Office.Clients` | 14 | 22 | 0 | 4 (Office) |
| `Regira.Printing.GDI` | 2 | 21 | 0 | 1 (a row in `office.namespaces.md`) |

| What | Where | Effect |
|---|---|---|
| The rule for where a guide lives | Root `AGENTS.md`, *Adding a module*: "Guides live on the hub, not on every package"; a provider gets an `ai/` of its own only for a `namespaces.md` or a package card | Five guide homes follow from it; thirteen guides would break it |
| What a hub ships | At least `{module}.instructions.md` and `{module}.examples.md`, a `build/` props and targets pair that extracts them, the pack entries, both routing tables, and a group in the guide verifier | The checklist for `Common`, `Common.Licensing` and `DAL.EFcore` |
| Cards on providers | Ten packages pack one `*.card.md` with no `build/` folder. Most are 15 to 30 lines that name the registration call and point at the hub guide's section (`Mail.MailKit`, `PDF.PdfPig`, `Drawing.SkiaSharp`, …) | The shape for the eight provider packages here |
| A provider row that points at another family's guide | The `Regira.IO.Compression.SharpZipLib` row: "The `IO.Storage` guide, § `ZipManager` … read it over MCP when only this package is installed" | The wording for the Caching, Serializing and Globalization rows |
| The READMEs are compiled | `core` compiles the READMEs of `Common`, `Serializing.Newtonsoft` and `Globalization.LibPhoneNumber`; `dal` compiles those of the five DAL projects and their `docs/examples.md` | The snippets that move into a guide are known to build |
| Common's IO types are documented in another family's guide | `io.storage.instructions.md`, *IO Abstractions (from `Regira.Common`)*, lines 8 to 119: the interface hierarchy, `BinaryFileItem`, the extension methods, `ContentTypeUtility`, `FileUtility` | The same material is in `src/Common/README.md`. Two copies, and Office and Media consumers find neither |
| Licensing is explained in four places | `entities.instructions.md` (*License requirement*), the pre-flight checklist in `ai/AGENTS.md`, the root `licensing.md`, and the `Regira.Office.Clients` README | An agent building with the Office clients reads the Entities guide to register a key |
| `Regira.Common` and `Regira.DAL.EFcore` have no `build/` folder | `src/Common`, `src/DAL.EFcore` | Neither extracts anything into a consumer today (decision 3) |
| The DAL contracts live in `Regira.Common` | `IDbSettings`, `IDbCommunicator`, `IDbBackupService`, `IDbRestoreService` and `PagingInfo` in `src/Common/DAL` | There is no `Common.DAL` project for a DAL guide to sit in (decision 2) |
| Guides are checked by use | The last commit on `wip`, `60b3414`, is a set of guide fixes from a live test run | The same check closes each family here |

## What each guide has to answer

A guide is written for an agent that has the package installed and a task to do. Each of these starts as a card of about ten bullets, then the instructions, then examples.

### Licensing

- Which packages check a key, and under which product code.
- `UseRegira(configuration)` and `UseRegira(licenseKey)`, and that either comes before any module's setup call.
- The configuration shape: `Regira:LicenseKeys` is an array, where it is stored in development and in production, and that a key is never committed.
- The free tier per product, and that it needs no call.
- A key's binding to a major version, and how several keys combine.
- The start-up line and its fields; `LicenseStatus`, and `ILicenseStatusClient` for the hosted services.
- A troubleshooting table from symptom to cause: the free tier applying despite a key, a key for another major, an expired key.

### Common

- The IO types every other family passes around: the file interfaces, `BinaryFileItem`, the extension methods, `ContentTypeUtility`, `FileUtility`.
- The utilities by task ("turn an object into a dictionary", "validate an e-mail address"), since nobody searches for `ObjectUtility` by name.
- `Dimensions`, normalizing (`INormalizer`, `[Normalized]`), and `PagingInfo`.
- Caching: `ICacheProvider`, `DictionaryCacheProvider`, and `MemoryCacheProvider` from `Regira.Caching.Runtime`.
- Serializing: `ISerializer`, the XML serializer, and the Newtonsoft implementation with its converters and its camel-case default, which the MongoDB row of the routing table already has to warn about.
- Globalization: the country and language utilities, and `PhoneNumberFormatter` from `Regira.Globalization.LibPhoneNumber`.

### Office.Clients

- `AddOfficeClients` and `OfficeClientOptions`.
- Which Office interface each client implements, so that code written against `IWordCreator` or `IPdfMerger` moves between a local backend and the hosted one by registration alone.
- The free tier's rate limit, and that the key travels with each call.
- What the hosted service does not take, which the README today links to in the Word docs.
- Printing: `IPrintService` and the Windows-only `PrintService`, as a section of `office.instructions.md`.

### DAL.EFcore

- What it adds to a `DbContext`: the UTC date-time convention, the decimal-precision convention, auto-truncate as an extension and as an interceptor, `GetPendingEntries`, `SaveAndCleanUpOnError`.
- What `UseEntities(…).UseDefaults()` already wires from it, and what an application without `Regira.Entities` calls itself.
- The obsolete members, named as such.

### DAL backup and restore

- The contract: `IDbBackupService.Backup()` returns an `IMemoryFile`; `IDbRestoreService` takes one.
- Per database: the settings type, the external tool it needs on the machine if any (`pg_dump` and `pg_restore` for PostgreSQL), what a native SQL Server `.bak` requires of the server's file system, and the credentials each needs.
- `MongoDbRepositoryBase` and `MySqlCommunicator`, which are data access rather than backup.
- One worked example across families: a backup saved to an `IFileService`.

## Design

### Files and packaging

- **`src/Common/ai/`**: `common.card.md`, `common.instructions.md`, `common.examples.md`, with `build/Regira.Common.props` and `.targets` copied from an existing pair (decision 3).
- **`src/Common.Licensing/ai/`**: `licensing.card.md`, `licensing.instructions.md`, `licensing.examples.md`, with a `build/` pair.
- **`src/Common.Office/ai/`**: `office.clients.instructions.md` and `office.clients.examples.md`, added to the pack entries of `Common.Office.csproj`; the printing section in `office.instructions.md`.
- **`src/DAL.EFcore/ai/`**: `dal.efcore.instructions.md` and `dal.efcore.examples.md`, with a `build/` pair, as `TreeList` has.
- **DAL backup**: `dal.instructions.md` and `dal.examples.md`, where decision 2 puts them.
- **Cards**, packed with no `build/` folder: `Caching.Runtime`, `Serializing.Newtonsoft`, `Globalization.LibPhoneNumber`, `Common.Office.Clients`, `Printing.GDI`, `DAL.MongoDB`, `DAL.MySQL`, `DAL.PostgreSQL`, `DAL.SqlServer`. `DAL.MySQL.MySqlBackup` is covered by the `DAL.MySQL` card.

### Docs for developers

The second layer follows the same reading of the code, and never refers to the first.

- `Common`: the README keeps its projects table, installation, a short "what is in here", the `## Overview` links and the licence. Its sections become pages under `src/Common/docs/`: `io.md`, `utilities.md`, `normalizing.md`, `caching.md`, `serializing.md`, `dal.md`.
- `DAL.EFcore`: one `docs/` page; the four backup projects keep their `docs/examples.md` and gain a page for what their README holds now.
- `Common.Licensing`: its README already points at the root `licensing.md`, which stays the developer page.
- The small packages keep their README as it is.

### One home per explanation

- **IO types.** The *IO Abstractions* section of `io.storage.instructions.md` moves to `common.instructions.md`. The storage guide keeps a pointer where the section was.
- **Licensing.** `entities.instructions.md` keeps what is about Entities, the counting of registrations against the tier, and points at the licensing guide for registering a key, the configuration shape and the start-up line. The pre-flight bullet in `ai/AGENTS.md` shrinks to the rule and the pointer.

### The routing table

When a family's guide ships, its row changes in the same commit:

- `Common`, `Licensing`, `DAL.EFcore` and `Office.Clients`: *Dedicated module guides*.
- `Caching`, `Serializing` and `Globalization`: "The `Common` guide, § …, shipped with `Regira.Common`", as the SharpZipLib row words it.
- The four backup rows: one pointer to the DAL guide each, keeping the sentence that says what the package is not for (the PostgreSQL row's "Not needed for EF Core").
- `Printing`: "The `Office` guide, § Printing".

## Considered and left out

- **A guide per package.** Thirteen guides for thirteen packages, six of which have five public types or fewer. It breaks the hub rule and repeats each contract in every provider's guide.
- **`*.signatures.md` for Common.** 79 public types would make the largest file of the set. The MCP's `list_types` and `get_type` already serve signatures from the source map, and the bootstrap guide tells agents to prefer them.
- **`how_to` recipes.** Today they cover Entities tasks. A recipe such as "back up a database every night" spans three families and is better added once the guides it points into exist.
- **The other packages without an `ai/` folder.** About fifty providers have none and need none: their hub guide documents them.
- **Rewriting guides that exist.** Only the two sections under *One home per explanation* change.

## Decisions

1. **What stays, before anything is written.** Two of the thirteen are candidates to retire rather than document:
   - `Regira.Caching.Runtime` is 77 lines over `System.Runtime.Caching`, and no guide names it.
   - `Regira.Printing.GDI` is Windows-only and depends on `Regira.Drawing.GDI`.

   Recommendation: decide both first. A package that is kept gets its card and its section. One that is retired gets a line in its README and in the routing table saying so, as `Excel.EPPlus` has, and no guide.
2. **Where the backup guide lives.** The contracts are in `Regira.Common`; the four implementations are separate packages that an application rarely combines.
   - Option 1: `src/Common/ai`, beside `common.*`. The hub is the project that holds the contracts, which is the repository's rule.
   - Option 2: a packaging-only hub, `src/Common.DAL` (`Regira.DAL`), that the DAL packages reference, as `Common.Payments` and `Common.Setup` are packaging-only. Only DAL consumers get the guide extracted, at the price of one more package.
   - Option 3: a guide in each DAL package. Nothing new to reference, but the contract is explained four times.

   Recommendation: option 1, unless decision 3 goes against extracting from `Regira.Common`, in which case option 2.
3. **Does `Regira.Common` extract its guides?** Every Regira package references it, so `buildTransitive` targets there run in every consumer's build, and every `.regira/instructions/` folder gains its files.
   - Option 1: a full hub, with the props and targets pair. Offline consumers get the guide, and the IO section that moves out of the storage guide stays reachable for them.
   - Option 2: pack `ai/` without targets, as the provider cards are packed. The MCP serves the guide; local extraction does not.

   Recommendation: option 1. Local extraction is the documented fallback when the MCP is not configured, and with option 2 the move under *One home per explanation* would take the IO section away from exactly those consumers. The targets are a copy of a pair that every hub ships and that copies only files a consumer does not have yet.
4. **Split the manual READMEs now, or later?** The README is also the nuget.org page; `Regira.Common`'s is 447 lines, on the package every other one references. Splitting it in the family's own change means one reading of the code produces the guide, the `docs/` pages and the index. Splitting later means the guide and the README disagree in the meantime. Recommendation: now, per family.
5. **Move the two duplicated explanations?** See *One home per explanation*. Recommendation: yes, both, each in its family's change.
6. **Examples files for the small guides.** The root `AGENTS.md` asks a hub for at least `instructions` and `examples`. For `dal.efcore` and `office.clients` the examples fit in the instructions. Either follow the rule as written (recommended: `get_example` scopes a search to a guide's `examples` section), or fold them in and amend the rule.

## Steps

1. **Decide what stays** (decision 1), and decisions 2 and 3, which fix where files go.
2. **Licensing.** The smallest guide with the widest reach: every paid consumer registers a key once, and a mistake there shows up as a start-up error or as a free-tier limit nobody expected.
   - The three files, the `build/` pair, the pack entries.
   - The pointer in `entities.instructions.md` and the pre-flight bullet.
   - A `licensing` group in `tools/GuideVerifier/projects.json`.
3. **Common.**
   - The three files and the `build/` pair; the IO section moved out of the storage guide.
   - The README split into `docs/` pages. Check regira.com's views and the Regira-Blog posts for links into the README's anchors first, as the sync table in the root `AGENTS.md` asks for a moved page.
   - Cards for `Serializing.Newtonsoft`, `Globalization.LibPhoneNumber`, and `Caching.Runtime` if it stays.
   - The `core` group gains `src/Common/ai`.
4. **Office.Clients and printing.** The two files in `Common.Office/ai`, the card, the printing section and card, and the `Office.Clients` pointers in the PDF, Word and vCards guides turned into links to the new section. A group for the clients in the verifier.
5. **DAL.EFcore.** The two files, the `build/` pair, the `docs/` page, and the pointers from the four Entities guides that name the package today.
6. **DAL backup and restore.** The two files, four cards, the READMEs split, and the `dal` group pointed at the new files.
7. **Close out.**
   - The routing table in `ai/AGENTS.md`: no row says *No dedicated family guide*.
   - A live run per family: an agent with the MCP and nothing else does one task (register a key and read the status line; back up a PostgreSQL database to an `IFileService`; convert a Word template through the hosted service). Where it guessed is the fix list, as for `60b3414`.
   - `search_docs` returns a heading for each package's main type, and `get_package_card` answers for each of the thirteen.
   - A patch version and a CHANGELOG bullet for every package whose packed content changed.
   - `ai/learnings.md` for what the live runs teach.

## Risks

- **More guides to keep true.** Five guide sets and nine cards join the sync table's first row: a changed signature now has a guide to update in families that had none. The verifier compiles every snippet, which catches a renamed member; it does not catch prose that went stale. Small guides, and cards that point rather than repeat, keep that surface down.
- **A wrong guide is worse than none.** Today an agent that finds nothing reads the source or asks. A guide that states a default wrongly is followed. Every statement about behaviour in these guides is read from the code or a test at the commit it is written against, and the live run in step 7 is the check.
- **Targets in the base package.** With decision 3's option 1, a defect in `Regira.Common`'s targets reaches every consumer's build. The pair is copied unchanged from a hub that ships it today, and is tried in a consumer solution with and without a solution directory before it is packed.
- **Links into the READMEs.** Splitting a README moves its anchors. Links from regira.com and from blog posts go to this repository's Pages site with absolute URLs, so they are found by search in those two repositories, not here.
- **The MCP's indexing is not visible from this repository.** That a new `ai/` folder, and a card without build targets, appear in `get_package_toc` and `search_docs` is assumed from how the existing ones behave. Step 2 confirms it on the first guide, before the other four are written.
- **Documenting what is about to change.** `proposals/dotnet-11.md` adds a target framework in 6.7.0, a later 7.0 drops `net8.0` with its branches and guide caveats, and decision 1 may retire two packages. Writing step 2 and 3 first, which neither touches, avoids writing a guide twice.
