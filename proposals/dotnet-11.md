# .NET 11, C# 15 and EF Core 11

As of 2026-11-12. Sources: the `Regira-Packages` repository, branch `wip` at `52c3b87`; Microsoft's *Announcing .NET 11* post (2026-11-10), *What's new in C# 15*, the *Explore union types in C# 15* post, *What's new in EF Core 11* with its breaking-changes page, and the .NET 11 runtime, libraries and ASP.NET Core 11 pages, all read 2026-11-12; and the nuget.org nuspecs of `Microsoft.EntityFrameworkCore`, `System.Text.Json`, `Microsoft.Extensions.Hosting`, `Microsoft.AspNetCore.OpenApi` and `Swashbuckle.AspNetCore.SwaggerGen` at their 11.x releases. The C# 15 claims come from a throwaway spike (*Spike results*) run on 2026-11-12 with SDK 11.0.100 (Roslyn 5.12.0) beside the installed SDK 10.0.402 (Roslyn 5.9.0). Nothing in the repository has been built.

**Status: proposal, not scheduled. Step 1, the spike, is done. Six decisions are open (see *Decisions*).**

## Recommendation

Move every package from `net8.0;net10.0` to **`net10.0;net11.0`** in one major release, **7.0.0**.

- **.NET 8 support ended on 2026-11-10**, the same day .NET 11 shipped. The announcement says .NET 8 and .NET 9 "reach end of support on November 10, 2026".
- **EF Core 11 runs only on .NET 11** ("EF11 will not run on earlier .NET versions"; its nuspec has one group, `net11.0`). Today a .NET 11 app that references `Regira.Entities.EFcore` gets the `net10.0` build, which is compiled against EF Core 10. That build uses EF's convention, plugin and interceptor surface (`ArchivedQueryFilterConvention`, `ConcurrencyTokenConvention`, `UtcDateTimeConventionSetPlugin`, `EntityReactorInterceptor`). The Entities guide already warns that a mismatch "restores and builds cleanly, then throws on the first query at runtime". This is the one concrete defect behind the move. A .NET 11 consumer of the Entities stack has no supported combination today.
- **The `net8.0` build is the weaker build, not just the older one.** The archived query filter, `#if !NET10_0_OR_GREATER` in `SetArchivedQueryFilter` (`src/Entities.EFcore/Extensions/ModelBuilderExtensions.cs:87`), is left out on EF Core 9 on purpose. The comment there explains why: installing it would have turned the archived opt-ins into an untargeted `IgnoreQueryFilters()` and made a cross-tenant write possible. Dropping `net8.0` leaves one archived-filter semantics, so `QueryExtensions`, `ArchivedQueryFilterConvention`, `DbContextOptionsBuilderExtensions`, `ArchivedQueryFilterValidator` and `ArchivableReferenceDataValidator` each lose a branch. The guides lose their "on `net10.0` only" caveats with them.
- **The Microsoft 11.x packages no longer ship a `net8.0` asset.** `System.Text.Json` 11.0.0 and `Microsoft.Extensions.Hosting` 11.0.0 target `net462`, `netstandard2.0`, `net10.0` and `net11.0`. Keeping `net8.0` would mean either staying on the 10.x references or letting `net8.0` fall back to `netstandard2.0`.

Adopt C# 15 where it removes code, not where it adds API. **Do not put union types in the public API in 7.0.** The best union-shaped candidates are in hot public contracts: the write outcomes that are thrown as `EntityInputException`, `EntityConstraintException` and `EntityConcurrencyException`, and the bytes/stream/path content behind `IMemoryFile`. Turning them into unions breaks every consumer, while the shapes they replace have no defect behind them. A consumer still on the .NET 10 SDK cannot use a library's union at all: its compiler rejects the conversions and the pattern match as a preview feature (*Spike results*). Pilot unions and closed hierarchies on internal code only, where the `net10.0` build needs a three-type internal polyfill (decision 3).

Keep the consumer guides on `net10.0`. .NET 11 is an STS release with two years of support, and .NET 10 is still the LTS line. A consumer moves when their app does, and 7.0 makes both TFMs work.

## What .NET 11 is

| | |
|---|---|
| Released | 2026-11-10, STS, "two years of support" |
| C# | 15: union types, closed hierarchies, collection expression arguments, extension indexers, labeled `break`/`continue`, non-virtual static interface members on older runtimes, memory safety (preview). Only memory safety needs `<LangVersion>preview</LangVersion>` |
| EF Core 11 | `net11.0` only. Needs the .NET 11 SDK to build and the .NET 11 runtime to run |
| Runtime | x64 baseline raised to x86-64-v2 on every OS; Windows Arm64 requires LSE. Runtime async is still a preview, opt-in with `<Features>runtime-async=on</Features>` |
| SDK | Native-AOT CLI host by default; `dotnetup` toolchain manager (preview) |
| .NET 8 and 9 | End of support 2026-11-10 |
| .NET 10 | Supported LTS |

The April union post called union types a preview, and the ASP.NET Core 11 page calls them "a preview C# 14 feature". The shipped 11.0.100 SDK compiles them under `LangVersion` `latest` without a warning, so they are released in C# 15. The 10.0.402 SDK knows them, but only as a preview.

## Target frameworks

| Option | TFMs | EF Core per TFM | Cost |
|---|---|---|---|
| **A (recommended)** | `net10.0;net11.0` | 10.x / 11.x | Two TFMs, as today. `net8.0` branches go; `net11.0` branches come only where an API needs them |
| B | `net8.0;net10.0;net11.0` | 9.x / 10.x / 11.x | Three EF majors and three archived-filter paths. Microsoft 11.x references need a TFM condition. A third test leg. It keeps an out-of-support runtime alive in a security-sensitive layer |
| C | `net10.0` everywhere, `net11.0` added only to EF- and ASP.NET-bound packages | 10.x / 11.x | Least build time. But "which packages have `net11.0`" becomes a fact to remember, and the release tooling's dependent re-versioning sees mixed TFM sets |

A `net10.0` assembly runs on the .NET 11 runtime, so option C is technically sufficient outside the EF Core and ASP.NET Core packages. Option A keeps the rule that every package targets the same frameworks. That rule is cheaper to reason about than the build time option C saves. This is decision 1.

Dropping a target framework is a breaking change, so the move is **7.0.0** on every package. The obsolete Billit result types (`IApiResult`, `DefaultApiResult`, `FailedApiResult` in `src/Invoicing.Billit/Models/Results/`) are already marked for removal in v7, and nothing uses them. They go in the same release.

### What dropping `net8.0` removes

| Where | What goes |
|---|---|
| `src/DAL.EFcore/DAL.EFcore.csproj:21` | The EF Core 9.0.23 `ItemGroup`. Its condition also names `net9.0`, which is not a target. It is replaced by an EF Core 11 group for `net11.0` |
| `src/Security.Authentication/Security.Authentication.csproj:30-36` | JwtBearer and OpenIdConnect 8.0.34 |
| `src/Csv.CsvHelper/Csv.CsvHelper.csproj:24` | `System.Interactive.Async` (net8/net9 only) |
| Archived query filter, 9 `#if` sites in `Entities.EFcore` and `Entities.DependencyInjection` | The EF Core 9 path, and with it the guides' net8/net10 split for archived rows (e.g. a validator's `db` skips the archived filter on `net8.0` but not on `net10.0`) |
| `IFileService.ListAsync` and its 8 implementations (`Common.IO.Storage`, `IO.Storage.Azure`, `.GitHub`, `.SimpleTCP`, `.SSH`), `FileProcessor` | `#if NET10_0_OR_GREATER`. `ListAsync` becomes unconditional, and `FileProcessor` handles files the same way on every target (the 6.6.0 changelog notes it runs one file at a time on `net8.0`) |
| `Security.Authentication.Web/OpenApi/Transformers` | The `NET9`/`NET10` guards. The transformers become unconditional, and they will need a `net11.0` branch for OpenAPI 3.x instead (see *ASP.NET Core 11*) |
| Already always true on `net8.0` | `NETCOREAPP2_1/3_0/3_1` guards in `TypeUtility.cs:70`, `ZipFileService.cs:31`, `Drawing.SkiaSharp/Utilities/ConversionUtility.cs:15`, and whole-file guards in 7 `Entities.Web` files. These are dead today and can go independently of .NET 11 |
| Tests | `Entities.Testing`, `Testing.Library` and `Web.Security.Testing` multi-target `net8.0;net10.0`, with InMemory and Sqlite at 8.x/9.x for `net8.0`. They become `net10.0;net11.0`. The 27 single-target test projects stay on `net10.0` or move to `net11.0` (decision 5) |
| Docs | `README.md:103` ("All packages target `net8.0` and `net10.0`"), the EF Core major table in `entities.setup.md:258`, and every "on `net10.0`" caveat in the Entities guides and docs (about 90 mentions across 24 markdown files, part of them in the changelog, which stays as it is) |

The TFM list can move to `Directory.Build.props` as one `<TargetFrameworks>` property, with `PDF.PockyBum522` (the one project on `net8.0-windows;net10.0-windows`) overriding it. Today each of the 87 `.csproj` files repeats the list. This is decision 2.

## EF Core 11

**Reference shape.** `DAL.EFcore` references EF Core 10.x on `net10.0` and 11.x on `net11.0`. Everything else in the Entities stack inherits that through its project references. The Npgsql EF provider has reached 11.0.0-rc.3 but no stable 11.0 yet. This repo references only the ADO.NET `Npgsql` (10.0.3) in `DAL.PostgreSQL`, so nothing here waits on it, but the consumer table in `entities.setup.md` has to say which provider majors exist. Pomelo's MySQL provider has stopped at 9.0.0, and `DAL.MySQL` uses `MySqlConnector` with Dapper, so this repo is unaffected.

**Breaking changes that reach this repo:**

| EF Core 11 change | Exposure here | Action |
|---|---|---|
| Complex types and JSON columns on TPT/TPC; keys and indexes on complex-type paths | `EntityChangeCapture.cs:58,104` and `StoredRowReader.cs:145` read `ComplexProperty` metadata for reactors | Add a reactor test with a complex property and a JSON complex collection on `net11.0`. The capture walks metadata, so new shapes reach it without a code change, and whether it handles them is unverified |
| Split queries throw `DbQueryConcurrencyException` | Includes in the Entities read path can be split by the app | Check `EntityExceptionMapping` and `EntityExceptionEndpointFilter`: today the exception falls through as a 500, which is correct for a concurrency bug in the app's own code. No change unless the test shows a regira path triggers it |
| `Migrate()` throws when no migrations are found | No `Migrate()` call in `src/` | None. The consumer guides that show `Migrate()` versus `EnsureCreated()` get a line |
| `Property()` no longer configures primitive collections | No primitive collections configured in `src/` | None |
| `Microsoft.Data.SqlClient` 7.0 | `DAL.SqlServer` already references 7.1.1 | None |
| SQL Server default compatibility level 160 | The provider is the consumer's | Guide line only |
| Microsoft.Data.Sqlite: SQLitePCLRaw bundles unmaintained | Six test projects use the Sqlite provider | Watch the advisory the guide already tracks (NU1902/NU1903); no code change |

**Features worth using.**

- **Nothing in EF Core 11 replaces code here.** `FullJoin`, the JSON functions (`JsonPathExists`, `JsonContains`), vector search, full-text catalogs and the migrations tooling (`database update --add`, `ExcludeForeignKeyFromMigrations()`) are consumer features. The guides can mention the ones that touch Entities modelling (complex types on inheritance, `ExcludeForeignKeyFromMigrations()` for a reference kept in the model only) where a reader would otherwise model around them.
- **`DeleteCycleExtensions` keeps its raw SQL.** It issues `ExecuteSqlRaw` (`DeleteCycleExtensions.cs:139,162`) because it builds the `UPDATE` per change-tracker entry from metadata. `ExecuteUpdate` needs a typed lambda per entity type and gains nothing here. EF Core 11 does not change this.

## ASP.NET Core 11

| Change | Exposure here | Action |
|---|---|---|
| OpenAPI 3.2 by default; `Microsoft.AspNetCore.OpenApi` 11 depends on `Microsoft.OpenApi` 3.x (`[3.11.0, 4.0.0)` per the nuspec) | `Security.Authentication.Web` and `Web.Swagger` pin `Microsoft.OpenApi` 2.13.0 and build `OpenApiSecurityScheme` in 7 files (`OpenApi/Transformers/*`, `Web.Swagger/Security/*`, `ISecuritySchemeDescriptor`). The 3.x object model is not source-compatible with 2.x | **The largest porting cost.** A `net11.0` path on `Microsoft.OpenApi` 3.x beside the `net10.0` path on 2.x. `Swashbuckle.AspNetCore` 11.0.1 targets `net10.0;net11.0` and is the matching move for `Web.Swagger`; its OpenAPI dependency per TFM is to be confirmed in step 4. The `project.setup.md` warning that `Microsoft.OpenApi` "must stay on 2.x (while on .NET 10)" gets a `net11.0` row |
| Endpoint filters now run when parameter binding fails | `EntityExceptionEndpointFilter` only wraps `next` in a `try`/`catch` for the three entity exceptions | None. A binding failure throws none of them, so the filter passes the 400 through |
| Async validation (`AsyncValidationAttribute`, `IAsyncValidatableObject`, `AddValidation()`) | Entities validation runs in `IEntityValidator`, not in minimal-API binding | None in code. The Entities guide can say where each applies: request-shape checks on the binding layer, data rules in the entity validator |
| Union return types in minimal APIs and MVC (JSON body only; OpenAPI emits `anyOf`) | No union in our API | None (see *C# 15*) |
| Identity uses `TimeProvider`; `AuthorizationPolicy.CombineAsync`; Negotiate TLS channel binding | `Security.Authentication*` | Read the full Identity change list in step 4. The ASP.NET page was only partly read verbatim, so this list is incomplete |

## C# 15

`LangVersion` is `latest` in all 89 projects, so the .NET 11 SDK compiles **both** targets as C# 15. Any feature used in shared source has to compile for `net10.0` too. The language-only features (collection expression arguments, extension indexers, labeled `break`/`continue`) build for `net10.0` as they are. Unions and `closed` do not. They need `IUnion`, `UnionAttribute` and `IsClosedTypeAttribute`, three .NET 11 framework types, and build for `net10.0` only with an internal polyfill (*Spike results*).

### Union types: where they fit

The scan found no `OneOf`/`Either`-style type in the repository. The union-shaped code is all in four places:

| Shape | Where | Fits a union? | Verdict |
|---|---|---|---|
| Write outcomes thrown as exceptions: `EntityInputException` (400), `EntityConstraintException` and `EntityConcurrencyException` (409) | Thrown in `EntityWriteService.cs:188,256,264,272`, `ConcurrencyTokenExtensions.cs:178`, `EntityValidatorExtensions.cs:57`, `PatchHandler.cs:34,69`, `AttachmentHandlers.cs:136`, `EntityHandlerServices.cs:37`, `ControllerExtensions.cs:109`; caught by one closed switch in `EntityExceptionMapping.cs:21` and one `try`/`catch` in `EntityExceptionEndpointFilter` | Yes, closely: `union SaveOutcome<T>(Saved<T>, Invalid, Conflict, ConcurrencyConflict)` with an exhaustive switch in place of the catch | **No, not in 7.0.** It changes `IEntityService.Save`, every handler, every override an app writes, and every guide example. The exception path is documented, tested, and maps to the right status codes. Unions also box their cases. That is a lot of API for no defect (decision 4) |
| Content that is bytes, a stream or a path | `IMemoryFile` (`IMemoryBytesFile` + `IMemoryStreamFile`), `BinaryFileItem`, `MemoryFileExtensions.cs:51-90` (`HasBytes`, `HasStream`, `Bytes ?? …`); `ToBinaryFile(byte[], contentType)` / `(Stream, contentType)` / `(IMemoryFile, filename)`, whose second argument means different things per overload (`BinaryFileExtensions.cs:19-39`) | Yes, as `union FileContent(byte[], Stream)` | **No.** `IMemoryFile` is the parameter type of most of the Office API. Its "one of several" behaviour is already hidden behind `GetBytes()`/`GetStream()`. The `ToBinaryFile` overload wart is documented on the method. A union here rewrites the most-used contract in the library |
| Mediator results | `IEntityResult`, implemented only by `CountResult`, `DeleteResult<T>`, `DetailsResult<T>`, `ListResult<T>`, `SaveResult<T>`, `SearchResult<T>`; probed once in `EntitySender.cs:59` | No. These are separate response types per request, not alternatives of one response | Leave |
| Template block tree | `TemplateBlocks.cs:393-411`: `private abstract class Node`, `sealed Child`, `sealed Block` | Yes, and fully internal | **Pilot.** Either `closed class Node` or `union Node(Child, Block)` makes the walks over it exhaustive. Smallest possible blast radius, so it tests the net10 build question for real |

`MailResponse` (`bool Success` beside a nullable `Exception`, `Status` and `Content`) is union-shaped too. It is public, and its consumers read `Success`, so it falls under the same verdict as the write outcomes.

System.Text.Json serializes a union by writing the active case directly. Two object cases need `[JsonUnion(TypeClassifier = …)]`. No regira type is serialized as one, so nothing here uses it. `RuntimeTypePageViewConverter` (`Web.Analytics/Endpoints/AnalyticsEndpoints.cs:101`) is the one hand-written polymorphic converter, and it serializes an open interface, not a closed set.

### The other C# 15 features

| Feature | Use here |
|---|---|
| Collection expression arguments, `[with(StringComparer.OrdinalIgnoreCase), ..]` | 11 `new HashSet<>/Dictionary<>(comparer)` sites (`DictionaryUtility`, `TemplateScope`, `WordPackageStories`, `EntityChangeCapture`, `SheetRows`/`SheetNames`/`SheetHeaders`, `ExcelManager`). Cosmetic: convert when a file is touched anyway, not as a sweep |
| Closed hierarchies | The `Node` pilot. Not on the public exception or result families, because `closed` stops consumers from deriving (`EntityInputException<T>` is meant to be thrown by app validators) |
| Extension indexers, labeled `break`/`continue`, static interface members on older runtimes | No site found that needs them |

### Spike results

A scratch solution targeting `net10.0;net11.0` with `LangVersion` `latest`, built with SDK 11.0.100. Each consumer below references the libraries' `net10.0` or `net11.0` build as stated.

| Probe | Result |
|---|---|
| `union Pet(Cat, Dog)`, a generic `union OneOrMore<T>(T, IEnumerable<T>)`, a `closed class Node` | `net11.0` builds. `net10.0` fails: CS0518 *Predefined type 'System.Runtime.CompilerServices.IUnion' is not defined*, CS0656 *Missing compiler required member 'UnionAttribute..ctor'* and, for `closed`, *'IsClosedTypeAttribute..ctor'* |
| Collection expression `with(StringComparer.OrdinalIgnoreCase)`, labeled `break`/`continue`, an extension indexer | Builds for both targets, no polyfill |
| What a union compiles to | A `sealed` struct, `[Union]`, implementing `IUnion` (one member, `object? Value`), one public constructor per case and an `object?` backing field. No conversion operators are emitted; the compiler supplies the conversion. Value-type cases box |
| What `closed` compiles to | An abstract class with `[IsClosedType]` and a `protected` constructor. The restriction lives in the compiler, not in IL |
| An internal polyfill of the three types under `#if !NET11_0_OR_GREATER` | `net10.0` builds. A C# 15 consumer in another assembly gets the implicit conversion and exhaustiveness through the polyfilled `net10.0` build (CS8509 names the missing case). Code built this way runs on the .NET 10 runtime |
| A consumer on the **.NET 10 SDK** (Roslyn 5.9, C# 14) using a library union | `new Pet(new Cat(..))` compiles. The implicit conversion and every `switch` over the union fail with CS8652 *The feature 'unions' is currently in Preview and \*unsupported\**. Deriving from a `closed` class fails with CS9382 on that compiler too |
| `default(Pet)` through an exhaustive switch with no null arm | Compiles without a warning. At runtime it throws `SwitchExpressionException` on both runtimes |
| `JsonSerializer.Serialize(new { pet = (Pet)new Dog("Rex") })` | .NET 11: `{"pet":{"Name":"Rex"}}`. .NET 10: `{"pet":{"Value":{"Name":"Rex"}}}`. The same value has a different wire shape per runtime |

What this means here:

- **A union in the public API is unusable to a consumer still on the .NET 10 SDK**, which is exactly the consumer the `net10.0` target is there for. This holds whatever the TFM decision. It settles decision 4 for as long as `net10.0` is a target.
- **A union in a serialized type changes the JSON between the two builds.** No DTO, response or problem body may carry one while both targets ship.
- **Internally, a union or `closed` type works on both targets** with the polyfill: three small types, internal, compiled only into the `net10.0` build. Put the polyfill only in the assemblies that use the feature. Several assemblies grant `InternalsVisibleTo` to each other (`Common.Office` to the Word backends), and two visible internal copies of the same type name raise CS0436.
- **An unreachable `default` remains a union's sharp edge.** A field or array element of a union type starts as `default`, with a null `Value`. Use unions for values that are always constructed, such as the `TemplateBlocks` pilot, not for fields that may stay unset.

C# 14 is only partly in use: 8 `extension(...)` blocks, collection expressions throughout, no `field` keyword, no null-conditional assignment, no `params` spans. That needs no action here. The same "convert when touched" rule covers it.

## Runtime and libraries

**Behaviour changes to check on `net11.0`:**

- **`ZipArchive` validates CRC-32 and throws `InvalidDataException`.** `ZipUtility` (`ZipFile.OpenRead`), `ZipBuilder` and `ZipFileService` in `Common.IO.Storage` read archives the caller supplies. A corrupt archive that used to read silently now throws. That is the right behaviour, but it belongs in the IO guide, with a test that feeds a corrupt entry. `IO.Compression.SharpZipLib` has its own reader and is unaffected.
- **`DeflateStream`/`GZipStream` write a header for empty input.** This changes the bytes of an empty compressed payload. The only `DeflateStream` writer is the QR encoder's zlib in `Barcodes.UziGranot/QRCodeEncoder/ZLibCompression.cs:52`, which never compresses empty input.
- **Decimal and `BigInteger` conversions are correctly rounded, with no switch.** No `double`→`decimal` conversion was found in `src/` (the invoicing amounts are `decimal` end to end), so there is no exposure. Tests with computed decimal expectations are the place it would show.
- **x86-64-v2 baseline.** This applies to the consumer's hardware, not to code. One line in the setup guide.

**APIs that could replace code once `net10.0` is gone:**

- `Process.RunAndCaptureTextAsync` and its relatives, for `Common.System/ProcessHelper.cs`.
- Span-based Deflate/ZLib/GZip encoders.
- `Base64.EncodeToString`.

While `net10.0` is a target, each would add a `#if NET11_0_OR_GREATER` branch beside working code. Leave them until a later move drops `net10.0`, at .NET 12 (the next LTS) at the earliest.

## Considered and left out

- **Runtime async.** It is still a preview, and it is opt-in per project. A library cannot ship preview runtime behaviour into a consumer's process.
- **Zstandard, X25519, `Hpke`, composite ML-KEM, the experimental TLS APIs.** No package here needs them. `AesEncrypter` and `Hasher` use PBKDF2, HMAC-SHA256 and `FixedTimeEquals`, which are unchanged.
- **Unions in the public API.** See *Union types: where they fit*. Revisit for 8.0 if the write-outcome shape is wanted for its own sake. It would then be an opt-in result-returning surface beside the throwing one, not a replacement.
- **`[GeneratedRegex]`.** The scan found 71 runtime regex calls and none generated, including four per-call `Regex.Replace` in `DefaultNormalizer` (`src/Common/Normalizing/DefaultNormalizer.cs:27-30`), which runs on every normalized `SaveChanges`. That is a real performance point, but `[GeneratedRegex]` has existed since .NET 7, so it does not belong in a .NET 11 move. One of the four patterns is built from `CharsToSpace` per call and cannot be generated. Worth its own small change.
- **`TimeProvider`.** It has existed since .NET 8. 21 direct `DateTime.Now`/`UtcNow` calls remain, and the licensing code already takes an optional `now`. It has nothing to do with .NET 11.
- **EF Core `ExecuteUpdate` in `DeleteCycleExtensions`.** See *EF Core 11*.

## Decisions

1. **TFM set:** A (`net10.0;net11.0` everywhere, recommended), B (keep `net8.0` as a third target) or C (`net11.0` only where EF Core or ASP.NET Core needs it).
2. **Central TFMs:** move `<TargetFrameworks>` into `Directory.Build.props` with `PDF.PockyBum522` overriding it for `-windows` (recommended), or keep it per `.csproj`.
3. **Internal unions and `closed` on the `net10.0` build:** unions and `closed` need framework types that `net10.0` lacks. Either add an internal polyfill of the three types in each assembly that uses them, and pilot `TemplateBlocks.Node` (recommended: one file, `#if !NET11_0_OR_GREATER`, never in an assembly that another polyfilled assembly sees through `InternalsVisibleTo`), or wait with both features until `net10.0` is dropped.
4. **Unions in the public API:** none while `net10.0` is a target (recommended, and close to forced: .NET 10 SDK consumers cannot switch on or convert to a library union, and the JSON shape differs per runtime). The write outcomes as an opt-in result-returning surface would be its own proposal, once `net10.0` is gone.
5. **Test projects:** keep the 27 single-target test projects on `net10.0` and multi-target only the three that already multi-target (recommended), or move all of them to `net11.0`. The second choice runs EF Core 11 and OpenAPI 3.x in every test, at the cost of no longer testing `net10.0` outside those three.
6. **Version line:** 7.0.0 for all packages at once (recommended, since dropping a TFM breaks every package), with the Billit obsolete result types removed in the same release.

## Steps

1. **Spike — done 2026-11-12** (*Spike results*). It needed no change to the repository.
2. **TFMs and references.** Apply decision 1 and 2. In `DAL.EFcore`, replace the EF Core 9 `ItemGroup` with an EF Core 11 one for `net11.0`. Drop the `net8.0` `ItemGroup`s in `Security.Authentication` and `Csv.CsvHelper`. Move `Microsoft.Extensions.*`, `System.*` and `Microsoft.AspNetCore.*` references to 11.x on `net11.0` and keep 10.x on `net10.0`. CI's `release.yml` and the dev machine need the .NET 11 SDK.
3. **Remove the `net8.0` branches.** Remove the archived query filter's EF Core 9 path, the `ListAsync` guards, and the dead `NETCOREAPP*` guards. Rebuild and run the Entities suites on both targets.
4. **OpenAPI 3.x.** Add a `net11.0` path in `Security.Authentication.Web` and `Web.Swagger` on `Microsoft.OpenApi` 3.x and Swashbuckle 11. Read the rest of the ASP.NET Core 11 Identity and authentication changes. Run `Web.Security.Testing` on both targets.
5. **EF Core 11 verification.** Add reactor tests with complex and JSON complex-collection properties on `net11.0`. Run the full Entities suite on EF Core 11 with Sqlite. Check split-query behaviour on the Entities read path.
6. **Internal C# 15 pilot.** Following decision 3, add the polyfill to `Common.Office` and convert `TemplateBlocks.Node` to `closed` or a union. Nothing public.
7. **Library behaviour.** Add a corrupt-entry test for `ZipUtility`/`ZipFileService` on `net11.0`.
8. **Docs and guides.** Update `README.md:103`, `entities.setup.md` (EF Core major table with a `net11.0` → 11.x row, provider availability), the "on `net10.0`" caveats in the Entities guides and docs, `project.setup.md` (the OpenAPI 2.x/3.x note per TFM, the x86-64-v2 line, the TFM stays `net10.0`), `ai/AGENTS.md` (".NET 10 is the LTS", and when to pick `net11.0`), and the IO guide's CRC note. Run `tools/GuideVerifier` (its own TFM moves with decision 5).
9. **Versions and changelog.** 7.0.0 on every package, and one changelog bullet per change that ships: the TFM move and its consequences for consumers, the Billit removal, and the OpenAPI 3.x path.

## Risks

- **Union and `closed` behaviour on `net10.0` is undocumented.** The spike measured it on 11.0.100 and 10.0.402. A later SDK band could change it in either direction, for example by making a 10.0.5xx compiler accept unions as released. Re-run the consumer probe before relaxing decision 4.
- **OpenAPI 3.x is a rewrite of the security-scheme code, not a version bump.** Two object models live side by side until `net10.0` is dropped. If the `net11.0` path drifts from the `net10.0` one, the generated documents differ per TFM. `Web.Security.Testing` has to assert the same document shape on both.
- **EF Core 11 provider availability.** The Npgsql EF provider is at RC and Pomelo has stopped at 9.0. A consumer on PostgreSQL or MySQL may not be able to move to `net11.0` yet. That limits the consumer, not this repo, but the setup guide has to say it.
- **Dropping `net8.0` strands consumers still on .NET 8.** They stay on 6.x. Published versions are immutable, so 6.x keeps working. It just takes no more fixes unless a 6.x line is maintained, which is not proposed.
- **Microsoft's pages were partly summarised.** The tails of the libraries and ASP.NET Core 11 pages were not read verbatim. Step 4 reads the ASP.NET Identity changes in full before relying on this list.
