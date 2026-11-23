# .NET 11, C# 15 and EF Core 11

As of 2026-11-18. Revised from the 2026-11-12 version after two changes: 6.6.3 was released, and the owner decided that .NET 11 support ships in the 6.x line **with `net8.0` kept**, while dropping `net8.0` waits for a later 7.0.

Sources: the `Regira-Packages` repository, `main` at `2aff410` (*Release 6.6.3*); the nuget.org version indexes and nuspecs of `Microsoft.EntityFrameworkCore`, `Microsoft.AspNetCore.OpenApi`, `Microsoft.AspNetCore.Authentication.JwtBearer`, `Microsoft.Extensions.Hosting`, `Microsoft.OpenApi`, `Swashbuckle.AspNetCore(.Swagger/.SwaggerGen)`, `Npgsql.EntityFrameworkCore.PostgreSQL`, `MySql.EntityFrameworkCore` and `Pomelo.EntityFrameworkCore.MySql`, read 2026-11-18. The Microsoft release pages (*Announcing .NET 11*, *What's new in C# 15*, *What's new in EF Core 11* and its breaking changes, the runtime, libraries and ASP.NET Core 11 pages) were read on 2026-11-12 and not again. The C# 15 claims come from the 2026-11-12 spike (*Spike results*), run with SDK 11.0.100 beside SDK 10.0.402. That SDK is no longer installed on this machine (only 9.0.319 and 10.0.402 are), so nothing in this revision was built or run.

**Status: proposal, not scheduled. Four decisions are open (see *Decisions*). Decided 2026-11-18: 6.7.0 is held until `Npgsql.EntityFrameworkCore.PostgreSQL` 11.0.0 is stable.**

## What changed since 2026-11-12

- **6.6.3 moves nothing this proposal depends on.** No package reference, TFM list or `#if` site changed; every `.csproj` diff is the version number. The line references below were re-checked against `2aff410`.
- **The 6.6.3 guides already handle .NET 11 for a `net10.0` app.** `entities.setup.md` and `project.setup.md` now add every `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore.*` and EF provider package with the TFM's major (`--version "10.*"`), because unversioned they take 11.x and "restore into `net10.0` silently (OpenApi) or fail with NU1202 (EF Core, Identity)".
- **A .NET 11 app is still not covered.** The guides' rule is "the TFM's major", and their EF Core table (`entities.setup.md:258`, Checklist 0.5 at `:221`) has rows for `net8.0`/`net9.0` → 9.x and `net10.0` → 10.x only. A `net11.0` app that follows the rule adds EF Core 11, while every Regira package gives it the `net10.0` build, compiled against EF Core 10. That is the mismatch the guide itself says "restores and builds cleanly, then throws on the first query at runtime". The same holds for `Microsoft.AspNetCore.OpenApi` 11, which brings `Microsoft.OpenApi` 3.x under a `Security.Authentication.Web` build compiled against 2.x.
- **Licence keys are now per major** (6.6.2): Regira Entities refuses a key issued for another major at startup. A 7.0 makes every paying consumer change their key. Shipping .NET 11 in 6.x avoids tying that to a framework update.
- **nuget.org on 2026-11-18:** the Microsoft 11.x packages are at 11.0.0, with no 11.0.1 yet. The 10.x line is at 10.0.15, which is what the repo references. `Microsoft.OpenApi` is at 3.11.1 and 2.13.1 (the repo pins 2.13.0). `Npgsql.EntityFrameworkCore.PostgreSQL` is still at 11.0.0-rc.3, `MySql.EntityFrameworkCore` at 11.0.0-preview, and `Pomelo.EntityFrameworkCore.MySql` at 9.0.0.
- **Correction to the 2026-11-12 version.** It said a .NET 11 consumer of the Entities stack "has no supported combination today". More precisely: the documented combination fails, and the undocumented one, a `net11.0` app that pins EF Core 10.x and a 10.x provider, should work, since EF Core 10 targets `net10.0` and that asset runs on the .NET 11 runtime. That is inferred and was not run.

## Recommendation

Add `net11.0` to every package in **6.7.0**, so the TFM list becomes **`net8.0;net10.0;net11.0`** (`net8.0-windows;net10.0-windows;net11.0-windows` for `PDF.PockyBum522`). Keep every `net8.0` and `net10.0` path as it is. Drop `net8.0` in 7.0 (see *Later: 7.0*).

- **Adding a TFM is additive.** A `net8.0` or `net10.0` consumer gets the same assets and the same dependencies as in 6.6.3. Only a .NET 11 app changes: it moves from the `net10.0` build to a `net11.0` build on EF Core 11, ASP.NET Core 11 and `Microsoft.OpenApi` 3.x. That is the build the guides' "TFM's major" rule assumes, so a minor version fits.
- **The one consumer it does break** is a `net11.0` app that pinned EF Core 10 or `Microsoft.AspNetCore.OpenApi` 10 against the guide. After the update, the Regira `net11.0` build requires 11.0.0, and restore fails with NU1605 (package downgrade). That is loud, at restore time, and the fix is to follow the guide. It earns a ⚠️ in the changelog bullet. A PostgreSQL app in that position cannot follow the guide yet, because the Npgsql EF Core 11 provider is still an RC (decision 4).
- **Every package gets `net11.0`, not only the EF Core and ASP.NET Core ones.** With three TFMs the build-time argument for option C (below) is stronger, but a partial set is a correctness trap here, not just a fact to remember: if `DAL.EFcore` has `net11.0` and `Entities.EFcore` does not, a .NET 11 app runs `Entities.EFcore`'s `net10.0` build, compiled against EF Core 10, on top of the EF Core 11 that `DAL.EFcore`'s `net11.0` dependency brings in. Every package that reaches `DAL.EFcore`, `Security.Authentication*` or `Web.Swagger` through a project reference would need `net11.0` as well, and that set is most of the repository.
- **`Microsoft.Extensions.*` and `System.*` stay on 10.x for all three TFMs.** The 10.0.15 packages ship a `net8.0` asset (checked for `Microsoft.Extensions.Hosting` 10.0.15: `net462`, `netstandard2.0`, `netstandard2.1`, `net8.0`, `net9.0`, `net10.0`), and the 11.x packages do not. A `net11.0` build uses their `net10.0` asset, which runs on .NET 11, and an app that references 11.x itself resolves 11.x, because NuGet takes the highest minimum. Moving them to 11.x on `net11.0` only would add a conditional `ItemGroup` to about 20 projects and gain nothing in 6.x.

Adopt C# 15 where it removes code, not where it adds API. That verdict is unchanged, and keeping `net8.0` makes it firmer: **no union types in the public API in 6.x** (it would be breaking anyway) **nor in 7.0** while `net10.0` is a target. A consumer on the .NET 10 SDK cannot switch on or convert to a library union, and a serialized union has a different JSON shape per runtime (*Spike results*). Pilot unions and closed hierarchies on internal code only, with an internal polyfill that now covers `net8.0` as well as `net10.0`.

Keep the consumer guides on `net10.0`. .NET 11 is an STS release with two years of support, and .NET 10 is the LTS. A consumer moves when their app does. 6.7 adds the `net11.0` row they need when they do.

## What .NET 11 is

| | |
|---|---|
| Released | 2026-11-10, STS, "two years of support" |
| C# | 15: union types, closed hierarchies, collection expression arguments, extension indexers, labeled `break`/`continue`, non-virtual static interface members on older runtimes, memory safety (preview). Only memory safety needs `<LangVersion>preview</LangVersion>` |
| EF Core 11 | `net11.0` only. Needs the .NET 11 SDK to build and the .NET 11 runtime to run |
| Runtime | x64 baseline raised to x86-64-v2 on every OS; Windows Arm64 requires LSE. Runtime async is still a preview, opt-in with `<Features>runtime-async=on</Features>` |
| SDK | Native-AOT CLI host by default; `dotnetup` toolchain manager (preview) |
| .NET 8 and 9 | End of support 2026-11-10. EF Core 9, which the `net8.0` build uses, ends with .NET 9 |
| .NET 10 | Supported LTS |

## Target frameworks in 6.x

| Option | TFMs | EF Core per TFM | Cost |
|---|---|---|---|
| **B (recommended, follows the owner's decision)** | `net8.0;net10.0;net11.0` everywhere | 9.x / 10.x / 11.x | A third build per package and a third leg in the three multi-target test projects. `net11.0` reference groups in the four packages and three test projects that pin framework majors (below). No code branch removed |
| C | `net8.0;net10.0` everywhere, `net11.0` added only where a framework major is pinned | 9.x / 10.x / 11.x | Less build time, but every package that depends on those four packages through a project reference needs `net11.0` too, or a .NET 11 app mixes a `net10.0` build with EF Core 11 (see *Recommendation*). Not recommended |

### What the `net11.0` target needs

Every TFM-specific `ItemGroup` tests the TFM for equality (`$(TargetFramework) == 'net10.0'`). Adding `net11.0` to the list without a matching group leaves that target with no reference at all, so each one needs a decision:

| Project | `net8.0` (unchanged) | `net10.0` (unchanged) | `net11.0` (new) |
|---|---|---|---|
| `src/DAL.EFcore/DAL.EFcore.csproj:21,25` | EF Core + Relational 9.0.23 (the condition also names `net9.0`, which is not a target) | 10.0.15 | **11.0.0**. Without the group, `net11.0` fails to compile, which is the safe failure |
| `src/Security.Authentication/Security.Authentication.csproj:30,34` | JwtBearer, OpenIdConnect 8.0.34 | 10.0.15 | **11.0.0** (`net11.0` asset only) |
| `src/Security.Authentication.Web/Security.Authentication.Web.csproj:23` | none (the OpenAPI transformers are guarded out) | `Microsoft.AspNetCore.OpenApi` 10.0.15 + `Microsoft.OpenApi` 2.13.0 pin | **`Microsoft.AspNetCore.OpenApi` 11.0.0**, which floors `Microsoft.OpenApi` at 3.11.0 |
| `src/Web.Swagger/Web.Swagger.csproj:18,21` | Swashbuckle 10.3.1 (unconditional today) | Swashbuckle 10.3.1 + `Microsoft.OpenApi` 2.13.0 pin | **Swashbuckle 11.0.1**. Its `Swashbuckle.AspNetCore.Swagger` depends on `Microsoft.OpenApi` 3.11.0 on both its targets, so it cannot serve the `net10.0` build. Swashbuckle moves from the unconditional group into per-TFM groups |
| `src/Csv.CsvHelper/Csv.CsvHelper.csproj:24` | `System.Interactive.Async` 7.0.1 | none | none |
| `tests/Entities.Testing`, `tests/Testing.Library`, `tests/Web.Security.Testing` | InMemory/Sqlite 8.x/9.x | 10.x | 11.0.0 groups, and a third TFM |

**Code needs no TFM branch for `net11.0`, except the OpenAPI code.** All 83 TFM conditionals in `src` and `tests` use `_OR_GREATER` (62 `NET10_0_OR_GREATER`, 3 `!NET10_0_OR_GREATER`, 4 `NET9_0_OR_GREATER`, 14 `NETCOREAPP*`), and none tests an exact version. So `net11.0` takes the `net10.0` path everywhere: the named archived query filter, `IFileService.ListAsync`, `FileProcessor`. Whether the archived filter behaves on EF Core 11 as on EF Core 10 is verified in step 5. The OpenAPI transformers compile for `net11.0` under their `NET9`/`NET10` guards against `Microsoft.OpenApi` 3.x, whose object model is not source-compatible with 2.x, so they need a `NET11_0_OR_GREATER` branch (see *ASP.NET Core 11*).

**Build and test machines.** The .NET 11 SDK builds all three targets. Building `net8.0` with it should raise NETSDK1138 (target framework out of support). Nothing in the repo sets `TreatWarningsAsErrors`, so it is noise, and `<CheckEolTargetFramework>false</CheckEolTargetFramework>` in `Directory.Build.props` silences it. Running the `net8.0` test legs needs the .NET 8 runtime, which stays downloadable but unpatched. CI's `release.yml`, the dev machine and the Regira-Tools pre-release run need the .NET 11 SDK and all three runtimes.

**Central TFMs.** Today 89 projects repeat `net8.0;net10.0` and one repeats `net8.0-windows;net10.0-windows`. Moving the list to `Directory.Build.props` as one `<TargetFrameworks>` property, with `PDF.PockyBum522` overriding it, makes this change a one-line edit now and the 7.0 drop another. Do it in the same change (decision 2). The 37 single-target projects in `tests` and `tools` keep their own `<TargetFramework>` (decision 3).

### What `net8.0` keeps in 6.x

Nothing that the 2026-11-12 version removed is removed in 6.x: the EF Core 9 path of the archived query filter (9 `#if` sites in `Entities.EFcore` and `Entities.DependencyInjection`, `ModelBuilderExtensions.cs:87` among them), the `ListAsync` guards, `FileProcessor`'s one-file-at-a-time path, the `NETCOREAPP*` guards, and the guides' "on `net10.0` only" caveats. The `net8.0` build stays the weaker build: it has no archived query filter, on purpose, because on EF Core 9 the filter would have made the archived opt-ins an untargeted `IgnoreQueryFilters()`. The guides keep saying so.

### Security on `net8.0`

`SECURITY.md` says "the latest 6.x release line receives security fixes". With `net8.0` kept past its end of support, that holds for Regira's own code on every target, but not for the Microsoft packages the `net8.0` build is frozen on: JwtBearer and OpenIdConnect 8.0.34 and EF Core 9.0.23 are final. A future advisory in them cannot be fixed on `net8.0` in 6.x, and the remedy is for the consumer to move to `net10.0`. `SECURITY.md` and `README.md:103` should say this in one sentence each, so it is stated before it is needed.

## EF Core 11

**Reference shape.** `DAL.EFcore` references EF Core 9.x on `net8.0`, 10.x on `net10.0` and 11.x on `net11.0`. Everything else in the Entities stack inherits that through its project references.

**Providers.** Of the providers the guides name, only Microsoft's (Sqlite, SqlServer) have a stable 11.0.0. Npgsql's EF provider is at 11.0.0-rc.3, `MySql.EntityFrameworkCore` at 11.0.0-preview, and Pomelo stops at 9.0.0. This repo references none of them (`DAL.PostgreSQL` uses ADO.NET `Npgsql` 10.0.3, `DAL.MySQL` uses `MySqlConnector` with Dapper), but a consumer on PostgreSQL or MySQL cannot run a `net11.0` app on a stable provider after 6.7. Before 6.7 they could, with EF Core 10 pinned. That decides decision 4.

**Breaking changes that reach this repo** (from the 2026-11-12 reading):

| EF Core 11 change | Exposure here | Action |
|---|---|---|
| Complex types and JSON columns on TPT/TPC; keys and indexes on complex-type paths | `EntityChangeCapture.cs:58,104` and `StoredRowReader.cs:145` read `ComplexProperty` metadata for reactors, single and batch (6.6.3) | Add a reactor test with a complex property and a JSON complex collection on `net11.0`. The capture walks metadata, so new shapes reach it without a code change, and whether it handles them is unverified |
| Split queries throw `DbQueryConcurrencyException` | Includes in the Entities read path can be split by the app | No change unless a test shows a Regira path triggers it. It falls through `EntityExceptionMapping` as a 500, which is right for a concurrency bug in the app's own code |
| `Migrate()` throws when no migrations are found | No `Migrate()` call in `src/` | A line in the consumer guides that show `Migrate()` against `EnsureCreated()` |
| `Property()` no longer configures primitive collections | None configured in `src/` | None |
| `Microsoft.Data.SqlClient` 7.0 | `DAL.SqlServer` already references 7.1.1 | None |
| SQL Server default compatibility level 160 | The provider is the consumer's | Guide line only |
| Microsoft.Data.Sqlite: SQLitePCLRaw bundles unmaintained | Six test projects use the Sqlite provider | Watch the advisory the guide already tracks (NU1902/NU1903) |

**Features worth using:** none replace code here, and `DeleteCycleExtensions` keeps its raw SQL, as on 2026-11-12. The guides can mention complex types on inheritance and `ExcludeForeignKeyFromMigrations()` where a reader would otherwise model around them.

## ASP.NET Core 11

| Change | Exposure here | Action |
|---|---|---|
| OpenAPI 3.2 by default; `Microsoft.AspNetCore.OpenApi` 11 depends on `Microsoft.OpenApi` `[3.11.0, 4.0.0)` | `Security.Authentication.Web` and `Web.Swagger` build `OpenApiSecurityScheme` in 7 files (`OpenApi/Transformers/*`, `Web.Swagger/Security/*`, `ISecuritySchemeDescriptor`) against 2.x | **The largest porting cost.** A `net11.0` path on `Microsoft.OpenApi` 3.x beside the `net10.0` path on 2.x, until 7.0 or later drops `net10.0`. `Web.Swagger` moves to Swashbuckle 11.0.1 on `net11.0` only. `project.setup.md`'s "must stay on 2.x" note gets a per-TFM row |
| Endpoint filters run when parameter binding fails | `EntityExceptionEndpointFilter` catches only the entity exceptions (now five, with 6.6.3's `EntityForbiddenException` and `EntityNotFoundException`) | None. A binding failure throws none of them, so the 400 passes through |
| Async validation (`AsyncValidationAttribute`, `IAsyncValidatableObject`, `AddValidation()`) | Entities validation runs in `IEntityValidator` | None in code. The guide can say where each applies |
| Union return types in minimal APIs and MVC | No union in our API | None |
| Identity uses `TimeProvider`; `AuthorizationPolicy.CombineAsync`; Negotiate TLS channel binding | `Security.Authentication*`, which 6.6.3 changed (lockout on password change, audience check, `[MaxLength]`, ASCII secrets) | Read the full Identity change list in step 4 and run 6.6.3's new `Web.Security.Testing` cases on `net11.0`. The 2026-11-12 reading of this page was partial |

## C# 15

`LangVersion` is `latest` everywhere, so the .NET 11 SDK compiles **all three** targets as C# 15, `net8.0` included. Shared source must compile for `net8.0` and `net10.0`. The language-only features (collection expression arguments, extension indexers, labeled `break`/`continue`) built for `net10.0` without help in the spike. Unions and `closed` need `IUnion`, `UnionAttribute` and `IsClosedTypeAttribute`, three .NET 11 framework types, and build for older targets only with an internal polyfill under `#if !NET11_0_OR_GREATER`, which covers `net8.0` and `net10.0` alike. The spike never built `net8.0`, so the pilot (step 6) is the first check there.

### Union types: where they fit

Unchanged from 2026-11-12. The scan found no `OneOf`/`Either`-style type, and the union-shaped code is in four places:

| Shape | Where | Verdict |
|---|---|---|
| Write outcomes thrown as `EntityInputException` (400), `EntityConstraintException` and `EntityConcurrencyException` (409), and since 6.6.3 `EntityForbiddenException` (403) and `EntityNotFoundException` (404) | Thrown across `EntityWriteService`, the handlers and validators; caught by one switch in `EntityExceptionMapping` and one `try`/`catch` in `EntityExceptionEndpointFilter` | **No.** A `SaveOutcome` union would change `IEntityService.Save`, every handler, every override and every guide example, with no defect behind it. 6.6.3 added two cases by adding two exception types, which an exception family absorbs and a union would not. An opt-in result-returning surface beside the throwing one is its own proposal, once `net10.0` is gone |
| Content that is bytes, a stream or a path (`IMemoryFile`, `BinaryFileItem`, `MemoryFileExtensions`, the `ToBinaryFile` overloads) | `Common.IO` and most of the Office API | **No.** It is the most-used contract in the library, and its "one of several" behaviour is already behind `GetBytes()`/`GetStream()` |
| Mediator results (`IEntityResult` and its six implementations) | `EntitySender.cs:59` | No. Separate response types, not alternatives of one |
| Template block tree (`TemplateBlocks.cs:393-411`: `private abstract class Node`, `sealed Child`, `sealed Block`) | `Common.Office`, internal | **Pilot.** `closed class Node` or `union Node(Child, Block)` |

`MailResponse` falls under the write-outcome verdict. No Regira type is serialized as a union, and none may be while more than one runtime ships.

### The other C# 15 features

| Feature | Use here |
|---|---|
| Collection expression arguments, `[with(StringComparer.OrdinalIgnoreCase), ..]` | 11 `new HashSet<>/Dictionary<>(comparer)` sites. Convert when a file is touched anyway |
| Closed hierarchies | The `Node` pilot only. Not on the public exception or result families, since `closed` stops consumers from deriving |
| Extension indexers, labeled `break`/`continue`, static interface members on older runtimes | No site found that needs them |

### Spike results

From 2026-11-12: a scratch solution targeting `net10.0;net11.0`, `LangVersion` `latest`, built with SDK 11.0.100. `net8.0` was not a target.

| Probe | Result |
|---|---|
| `union Pet(Cat, Dog)`, a generic `union OneOrMore<T>(T, IEnumerable<T>)`, a `closed class Node` | `net11.0` builds. `net10.0` fails: CS0518 for `IUnion`, CS0656 for `UnionAttribute..ctor` and `IsClosedTypeAttribute..ctor` |
| Collection expression `with(...)`, labeled `break`/`continue`, an extension indexer | Builds for both targets, no polyfill |
| What a union compiles to | A `sealed` struct, `[Union]`, implementing `IUnion` (one member, `object? Value`), one public constructor per case and an `object?` backing field. Value-type cases box |
| What `closed` compiles to | An abstract class with `[IsClosedType]` and a `protected` constructor. The restriction is in the compiler, not in IL |
| An internal polyfill of the three types under `#if !NET11_0_OR_GREATER` | `net10.0` builds, and a C# 15 consumer gets conversions and exhaustiveness through it. It runs on the .NET 10 runtime |
| A consumer on the .NET 10 SDK using a library union | Construction compiles. The implicit conversion and every `switch` fail with CS8652 (preview feature). Deriving from a `closed` class fails with CS9382 |
| `default(Pet)` through an exhaustive switch with no null arm | No warning. `SwitchExpressionException` at runtime |
| `JsonSerializer.Serialize(new { pet = (Pet)new Dog("Rex") })` | .NET 11: `{"pet":{"Name":"Rex"}}`. .NET 10: `{"pet":{"Value":{"Name":"Rex"}}}` |

Put the polyfill only in assemblies that use it. Several assemblies grant `InternalsVisibleTo` to each other (`Common.Office` to the Word backends), and two visible internal copies of one type raise CS0436. Use unions only for values that are always constructed, never for fields that may stay `default`.

## Runtime and libraries

On `net11.0` (unchanged from 2026-11-12):

- **`ZipArchive` validates CRC-32 and throws `InvalidDataException`.** `ZipUtility`, `ZipBuilder` and `ZipFileService` read caller-supplied archives, so a corrupt archive that read silently on `net8.0`/`net10.0` throws on `net11.0`. The behaviour now differs per target in one package, which belongs in the IO guide with a test that feeds a corrupt entry. `IO.Compression.SharpZipLib` is unaffected.
- **`DeflateStream`/`GZipStream` write a header for empty input.** The only writer, the QR encoder's zlib (`ZLibCompression.cs:52`), never compresses empty input.
- **Decimal and `BigInteger` conversions are correctly rounded.** No `double`→`decimal` conversion in `src/`.
- **x86-64-v2 baseline.** A line in the setup guide.

New APIs (`Process.RunAndCaptureTextAsync`, span-based compression encoders, `Base64.EncodeToString`) stay unused: with `net8.0` and `net10.0` still targets, each would be a third branch beside working code.

## Considered and left out

- **Runtime async.** Still a preview, opt-in per project. A library cannot ship it into a consumer's process.
- **Zstandard, X25519, `Hpke`, composite ML-KEM, the experimental TLS APIs.** No package needs them.
- **Moving `Microsoft.Extensions.*` and `System.*` to 11.x.** No `net8.0` asset, nothing gained in 6.x (see *Recommendation*). Revisit in 7.0, where `net10.0`/`net11.0` per-TFM groups are the norm.
- **`[GeneratedRegex]`** in `DefaultNormalizer` (`src/Common/Normalizing/DefaultNormalizer.cs:27-30`), **`TimeProvider`** and **`ExecuteUpdate` in `DeleteCycleExtensions`.** Each predates .NET 11 and is its own small change, as on 2026-11-12.

## Later: 7.0

Not part of this proposal's steps; recorded so 6.7 does not have to decide it.

- **Drop `net8.0`**: TFMs become `net10.0;net11.0`. With the central `<TargetFrameworks>` from decision 2, that is one line plus the removals: the EF Core 9 `ItemGroup` in `DAL.EFcore`, the 8.0.34 group in `Security.Authentication`, `System.Interactive.Async` in `Csv.CsvHelper`, the archived filter's EF Core 9 path (9 sites), the `ListAsync` guards and `FileProcessor`'s `net8.0` path, the `NET9`/`NET10` guards on the OpenAPI transformers, the `net8.0` legs of the three multi-target test projects, and the "on `net10.0`" caveats in the guides (about 90 mentions across 24 markdown files, the changelog excluded). The `NETCOREAPP2_1/3_0/3_1` guards (`TypeUtility.cs:70`, `ZipFileService.cs:31`, `ConversionUtility.cs:15`, 7 whole-file guards in `Entities.Web`) are always true already and can go any time.
- **Remove what is marked for v7**: the obsolete Billit result types (`IApiResult`, `DefaultApiResult`, `FailedApiResult`) and `ConfirmEmailInput.Password` (obsolete since 6.6.2).
- **Licence keys**: since 6.6.2 a key is valid for its major, so 7.0 needs new keys issued for paying consumers before it ships.
- **Unions in the public API** stay out while `net10.0` is a target, which it will be in 7.0.

## Decisions

1. **TFM set in 6.x:** `net8.0;net10.0;net11.0` on every package (recommended), or `net11.0` only where needed (option C, not recommended: a .NET 11 app would mix a `net10.0` build with EF Core 11).
2. **Central TFMs:** move `<TargetFrameworks>` into `Directory.Build.props` with `PDF.PockyBum522` overriding it, and set `CheckEolTargetFramework` to `false` there (recommended), or keep the list per `.csproj`.
3. **Test projects:** keep the 37 single-target test and tool projects on `net10.0` and give the three multi-target ones a `net11.0` leg (recommended), or move the single-target ones to `net11.0`, which tests EF Core 11 and OpenAPI 3.x everywhere but `net10.0` only in those three.
4. **Release timing — decided 2026-11-18:** 6.7.0 is held until `Npgsql.EntityFrameworkCore.PostgreSQL` 11.0.0 is stable (11.0.0-rc.3 on 2026-11-18). A PostgreSQL app on `net11.0` that runs on EF Core 10 today would otherwise have no stable provider after updating. The work can be done and merged before then; only the publish waits.
5. **Internal unions and `closed`:** add the internal polyfill (`#if !NET11_0_OR_GREATER`, covering `net8.0` and `net10.0`) to `Common.Office` and pilot `TemplateBlocks.Node` (recommended), or wait until 7.0 or later. No public unions either way.

The 2026-11-12 decisions on the version line (7.0.0) and on dropping `net8.0` are settled by the owner: 6.7.0, `net8.0` kept.

## Steps

1. **Spike — done 2026-11-12.** Re-run the consumer probe and the polyfill probe with `net8.0` added before step 6, and install SDK 11 on the dev machine first.
2. **TFMs and references.** Apply decisions 1 to 3. Add the `net11.0` groups from *What the `net11.0` target needs*: EF Core 11.0.0 in `DAL.EFcore`, JwtBearer/OpenIdConnect 11.0.0 in `Security.Authentication`, Swashbuckle into per-TFM groups in `Web.Swagger`, and EF Core 11 InMemory/Sqlite in the three test projects. `Microsoft.Extensions.*` and `System.*` stay on 10.0.15. `release.yml`, the dev machine and the Regira-Tools test run get the .NET 11 SDK and the .NET 8 runtime. Build; `net11.0` builds everywhere except `Security.Authentication.Web` and `Web.Swagger`, which wait for step 4.
3. **EF Core 11 verification.** Run the Entities suites on all three targets. Add reactor tests (single and batch) with complex and JSON complex-collection properties on `net11.0`. Check that the named archived filter behaves on EF Core 11 as on EF Core 10. Check split-query behaviour on the read path.
4. **OpenAPI 3.x.** Add the `net11.0` path in `Security.Authentication.Web` (`Microsoft.AspNetCore.OpenApi` 11.0.0) and `Web.Swagger` (Swashbuckle 11.0.1) on `Microsoft.OpenApi` 3.x. Read the ASP.NET Core 11 Identity and authentication changes in full. Run `Web.Security.Testing` on all three targets, asserting the same document shape on `net10.0` and `net11.0`.
5. **Library behaviour.** Add a corrupt-entry test for `ZipUtility`/`ZipFileService`; it throws on `net11.0` only.
6. **Internal C# 15 pilot** (decision 5). Polyfill in `Common.Office`, `TemplateBlocks.Node` as `closed` or a union, built for all three targets. Nothing public.
7. **Docs and guides.** A `net11.0` → 11.x row in `entities.setup.md`'s EF Core table and Checklist 0.5, with provider availability (Npgsql, MySQL, Pomelo); the `--version "10.*"` rule extended to "11.* on `net11.0`" in `entities.setup.md` and `project.setup.md`; `project.setup.md`'s OpenAPI 2.x/3.x note per TFM and the x86-64-v2 line, with new projects still on `net10.0`; `README.md:103` ("All packages target `net8.0`, `net10.0` and `net11.0`"); `SECURITY.md`'s sentence on the frozen `net8.0` dependencies; `ai/AGENTS.md` (.NET 10 is the LTS, and when to pick `net11.0`); the IO guide's CRC note. Run `tools/GuideVerifier`.
8. **Version and changelog.** 6.7.0 on every package. One bullet for the `net11.0` target with what a .NET 11 consumer gets, a ⚠️ for an app that pinned 10.x packages on `net11.0` (NU1605 until it follows the guide), the PostgreSQL/MySQL provider status, and the `ZipArchive` CRC difference; one for the OpenAPI 3.x path; one saying `net8.0` stays, is out of support upstream and is dropped in 7.0.
9. **Publish** once `Npgsql.EntityFrameworkCore.PostgreSQL` 11.0.0 is stable on nuget.org (decision 4). Before publishing, update the guide's provider row from "RC" to the stable version, and re-check `MySql.EntityFrameworkCore` and Pomelo for an 11.x release.

## Risks

- **Three TFMs triple the matrix that can drift.** The OpenAPI documents of `net10.0` and `net11.0` come from two object models, and the archived filter has two semantics (`net8.0` against `net10.0`/`net11.0`). `Web.Security.Testing` and the Entities suites have to run every target, not only the newest.
- **`net8.0` ships on frozen dependencies.** See *Security on `net8.0`*. The longer 6.x lasts, the likelier a Microsoft advisory that 6.x cannot fix on `net8.0`. That is the argument for not leaving 7.0 too long.
- **Union and `closed` behaviour on older targets is undocumented**, and the spike never built `net8.0`. A later SDK band could change it. Re-run the probes before relaxing the public-API verdict.
- **Provider availability.** Npgsql's EF provider is at RC, MySQL's official one at preview, Pomelo at 9.0. Holding 6.7.0 for a stable Npgsql 11 (decision 4) covers PostgreSQL, at the cost of a release date set by another project. MySQL on `net11.0` has no stable option yet either way.
- **Microsoft's pages were partly summarised** on 2026-11-12 and not re-read. Step 4 reads the ASP.NET Identity changes in full before relying on the list above.
