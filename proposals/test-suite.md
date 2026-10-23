# Test suite review
<!-- {% raw %} -->

As of 2026-10-23. Sources: the `Regira-Packages` repository, branch `wip` at `4d3d479`, all 37 test projects under `tests/` plus `Entities.TestApi` and `Testing.Library` (about 70k lines). Seven read-only reviews, one per area, read every test file and the source each test calls. A sample of their claims, every security claim among them, was rechecked against the code, and one was proven by a probe (W2). Timings were measured on a snapshot of the tree built from scratch: `dotnet test --no-build --filter "TestCategory!=Network"`, Docker and LocalDB up, a local MongoDB service running.

**Status: phase 1 (W1–W57) built, uncommitted; see *Outcome (phase 1)*. Phases 2–5 are proposals.**

## Answer

**The suite is healthy, and most of its weak spots sit in one older layer.** All 4,698 offline tests pass and 29 are skipped. The newer tests do what they should: exact values, a contract summary, a control that proves the hazard, and a read-back after a write. Examples are the Entities archive, attachment, concurrency and reactor fixtures, the Word scenarios, Invoicing, the IO containment tests and `LicenseStatusTests`. The problems cluster in the older tests: the Drawing operations, the Barcodes writers, the IO storage helper, the DI registration matrix, the Entities.Web CRUD classes, Normalizing and the Office client network tests.

| Lens | What was found | Where it matters most |
|---|---|---|
| Wrong | 57 findings, most about one test and some about a group, that cannot fail, pass for the wrong reason or pin a wrong value (W1–W57) | The refresh-token race (W1) and Entra expiry (W2) tests: they claim security properties they do not check |
| Useless | About 60 duplicates, tests of the BCL, EF or the test's own code, and dead files (U1–U9) | The PrimerTests/InterceptorTests pairs, the four superseded attachment tests |
| Not measurable | Outputs written for a person to look at, `length > 0`, `Is.Not.Null`, loose `Contains` (M1–M10) | Drawing operations, barcode writers, Word `Convert_To`, refused writes never re-read |
| Inconsistent | Three frameworks' assertion styles, five naming styles, five asset-path spellings, four gating patterns (C1–C10) | Asset paths that break under `--artifacts-path` and on Linux |
| Slow | Measured: 390 s run one project at a time, 136 s as the parallel full run | Five items take most of the time (P1–P5) |

Fixing the wrong tests (phase 1) is the work that pays most. Some of those fixes will turn red, because the weak tests were hiding library defects. S1–S8 lists those defects with the others found in passing. The speed-ups (phase 2) change no assertion. **Estimate:** the parallel full run drops from 136 s to roughly 80–90 s, and the machine time from 390 s to about 260 s. This is an estimate, to be measured after each phase.

## Measured

Each project was run on its own, one after the other, then together as `dotnet test Regira-Packages.slnx`.

| Project | Offline tests | Alone (wall) | In the parallel run¹ |
|---|---:|---:|---:|
| Office.PDF.Testing | 173 | 56.7 s | 106 s |
| Office.Word.testing | 631 | 52.9 s | 94 s |
| Drawing.Testing | 152 | 22.6 s | 83 s |
| Entities.Web.Testing | 305 | 22.4 s | 61 s |
| Entities.Providers.Testing | 36 | 21.3 s | 89 s |
| DAL.PostgreSQL.Testing | 27 | 17.9 s | 4 s |
| DAL.SqlServer.Testing | 37 | 17.4 s | 55 s |
| Web.Analytics.Testing | 148 | 14.8 s | 13 s |
| Entities.Testing (net8 + net10) | 1,197 | 13.2 s | 25 + 20 s |
| Media.Testing | 16 | 11.3 s | 38 s |
| Office.OCR.Testing | 17 | 11.0 s | 49 s |
| Web.Security.Testing (net8 + net10) | 479 | 10.9 s | 25 + 23 s |
| Office.Barcodes.Testing | 94 | 10.3 s | 16 s |
| 24 other projects | 1,386 | 2.8–8.2 s each | up to 31 s; System.Testing 49 s² |
| **Total** | **4,698** | **390 s** | **136 s wall** |

¹ The duration the test host reports under contention. It leaves out fixture set-up such as a container start, which is why DAL.PostgreSQL looks short.
² System.Testing starts processes, which slow down when the CPU is busy; alone it takes 5.6 s.

- **Fixed cost per assembly:** `dotnet test --no-build` on a 27-test project takes 1.7 s warm, of which the tests take 0.3 s.
- **Slowest single tests** (each project run alone):
  - Skia `Test_MakeOpaque` on `img-1..4.jpg`: 9.9–12.9 s each, 47 s in total
  - The first Playwright render: 12.8 s
  - `Add_Text_No_Params` on GDI and Skia: 11.9 s and 10.3 s; each drawn text is read back with a new PaddleOCR predictor
  - Syncfusion `A_Template_That_Includes_Itself_Fails`: 11.0 s
  - Syncfusion and Aspose `Nested_Documents_Do_Not_Wear_Out_The_Service`: 9.4 s and 8.9 s
  - PaddleOCR `Read_English` and `Read_Dutch`: 7.8 s each
  - The first test in each Entities.Web class, which builds the host: about 6 s
- **Disk per run:**
  - 240 MB written to `tests/*/Assets/Output`: Drawing 122 MB, Word 81 MB, PDF 35 MB.
  - Another 125 MB copied into `TestResults` when a TRX logger runs (measured; the IDE plausibly does the same), because Word and PDF attach every output (`WordAssetsTestsBase.cs:60,72`, `PdfAssetsTestsBase.cs:58,62`).
- **Build:** the test projects build with a few nullable warnings, `CA1416` on the GDI tests (no platform guard, see C6), and one `NUnit2009` false positive (`AES_Tests.cs:76` compares two encryptions on purpose). The package advisories (`NU1902`/`NU1903`) are the documented ImageSharp pin and AutoMapper range, plus `System.Security.Cryptography.Pkcs` 9.0.11, which `Testcontainers.MsSql` brings into Entities.Providers.Testing only.

## 1. Wrong tests

Each row is a test that cannot catch the regression it names. The fix makes it able to fail. In phase 1, every fixed test must be shown to fail against a deliberately broken copy of the source, in a scratch snapshot.

### Security (Web.Security.Testing, Security.Testing, Licensing.Testing)

| # | Test | Why it cannot catch the regression | Fix |
|---|---|---|---|
| W1 | `RefreshTokenTests.cs:244, 260`, the concurrent refresh "race" | The store and resolver return finished tasks, so the 8 (and 4) `Refresh` calls run one after another. Every loser is refused by the replay branch; the `TryRevoke` test-and-set is never contested | A resolver that waits on a shared gate until all N calls are inside; start the calls with `Task.Run`; add a direct `Parallel.For` test on `InMemoryRefreshTokenStore.TryRevoke` |
| W2 | `EntraIdBearerTests.cs:152`, `Test_Expired_Token_Is_Rejected` | **Probed.** `FakeAuthority.CreateToken(lifetime: -30 s)` writes `nbf = now`, after `exp`, so validation fails with `IDX10224` (invalid lifetime), skew or no skew. A well-formed token that expired 30 s ago is **accepted** under the default 5-minute skew, so a `ClockSkew` regression passes | Set `NotBefore` and `IssuedAt` 10 min back for a negative lifetime; assert `WWW-Authenticate` contains "The token expired". Same reason check for issuer (`:136`), audience (`:144`) and the multi-tenant cases (`:204`, `:215`) |
| W3 | `CookieAuthenticationTests.cs:133`, roles do not accumulate | `ClaimsReport.Role` is `FindFirstValue("role")`, which cannot see a duplicate | Report `FindAll("role").Count()` and assert 1 on every request |
| W4 | `CookieAuthenticationTests.cs:116`, `CookieSignInSchemeTests.cs:64`, sign-out | Neither checks that the cookie worked before logout, so a broken sign-in passes | `GET protected` is 200 before logout; the logout `Set-Cookie` expires the cookie |
| W5 | `JwtAuthenticationTests.cs:50`, "invalid token" | Tests expiry under another name; any 401 passes, with no control that the token worked | Rename it to expiry; a control request that gets 200; see P8 for dropping the wait |
| W6 | `JwtOptionsCharacterizationTests.cs:232`, secret length | The accented secret is 66 bytes in ASCII and 68 in UTF-8, so both clear 64 | `new string('k', 62) + "é"`: 63 ASCII bytes, 64 UTF-8 bytes; assert it throws naming "63 bytes" |
| W7 | `RefreshTokenTests.cs:47`; `RefreshTokenEndpointTests.cs:81, 122, 158` | The first refresh result is ignored. The refusals after a password change or an unconfirmed address are never preceded by a refresh that works. `:81` never sends the expired bearer token | Assert the first refresh; refresh once (200) before each change, then 401 after it |
| W8 | `IdentityControllerTests.cs:625, 638` | Pass or fail depending on the machine's `ASPNETCORE_ENVIRONMENT`: the startups call `UseDeveloperExceptionPage()` in Development. Plausible, not run | Pin the environment in `TestingWebApplicationFactory` |
| W9 | `AES_Tests.cs:22`, `Symmetric_Tests.cs:40`, `Decrypt_Encrypted_Failure` | Encrypts "wrong_password" and asserts the round trip is not "password" | Delete the AES one (covered by `Another_Secret_Throws`); for `SymmetricEncrypter`, decrypt with another secret |
| W10 | `LicenseDefaultsTests.cs:12, 19, 27` | The constants are compared with the constants they were initialised from | Assert the published literals: 5/2, 5/60/0, 30/60/0 |
| W11 | `LicensingTests.cs:119`, `Validate_TamperedPayload` | Flips the first JSON byte, so a parse failure alone passes | Change a field's value and keep the JSON valid |

### Entities (Entities.Testing, Entities.DependencyInjection, Mapster, Providers, Mediator)

| # | Test | Why | Fix |
|---|---|---|---|
| W12 | `DbContextNormalizerTests.cs:134, 137` | Expects "INSTRUCTING 6 COURSES" for both instructors; the data gives 4 and 3. The test normalizer (`Infrastructure/Normalizers/InstructorNormalizer.cs:34`) matches pending courses by `Id`, which is 0 for all six during `SavingChanges` | Match by reference; expect 4 and 3 |
| W13 | `StartupValidationTests.cs:40, 72, 287` | Assert `DoesNotThrow` on a validator that only ever warns (`InterceptorWiringValidator.cs:72–93`) | Capture logs; assert the exact entries (none, none about the primer, none naming `NoteContext`) |
| W14 | `ArchivableReferenceDataValidatorTests.cs:295` | Only `For<Status>()` is registered, so the validator has nothing to report | Register `Status` and `Asset`; assert `Category` is reported and `Status` is not |
| W15 | `QuerySortingTests.cs:54`, `OrderOrThenBy_Continues_Ordering_As_ThenBy` | The seed sorts the same by `Created` as by `Title, Created` | Give "Bravo" the earliest date |
| W16 | `AttachmentFilterTests.cs:257` | One owner is seeded, so the `ObjectId` half filters nothing | Seed a second owner with a `.pdf` |
| W17 | `RelatedIntShortcutTests.cs:70, 98` | EF inserts the graph with or without `Related()` | Add a `Modify` that drops a child; only the related sync deletes it |
| W18 | `DeleteCycleTests.cs:574` | `loaded > 0` always holds (default 1, the seed moves it to 2) | Compare the token before and after, or assert the UPDATE was sent |
| W19 | `DbContextNormalizerTests.cs:57` | `NormalizedContent == NormalizedContent?.ToUpper()` passes when the normalizer never ran | Assert the exact string |
| W20 | `Infrastructure/Primers/NormalizingPrimer.cs:14, 22` | Discards the `HandleNormalize` task; works only because the default completes synchronously | Await it |
| W21 | `ProcessorTests.cs:27` | Calls the test's own `CategoryProcessor` directly; no Regira dispatch runs | Register it on `For<Category>()` and assert through `IEntityService.List` |
| W22 | `TestFor4Services.cs`, `TestFor5Services.cs`: 28 processor checks (e.g. `:41`) | Resolve `IEntityProcessor<Course, EntityIncludes>`; complex shapes register `CourseIncludes`, so `Is.Empty` always holds. The typed `SortBy((q, sortBy) => …)` is never asserted | Resolve `CourseIncludes`; add a typed-sort case |
| W23 | `SharedConfigTests.cs:52, 70` (Mapster) | Every DTO mirrors its entity, so mapping works under any config, including the old last-wins one | A renamed-property rule per stack, asserted in both registration orders; fold `Single_Context` in |
| W24 | `ProviderQueryPipelineTests.cs:135, 210` | Seeded in title order, so id order and title order agree, and a lost sort still yields the expected rows | Seed Charlie, Bravo, Alpha |
| W25 | `ProviderQueryPipelineTests.cs:101` | Never requests the archived id it claims to exclude | Request `_bravoArchivedId` too |
| W26 | `TestPagingOptions.cs:64` | "After defaults" never calls `UseDefaults()` | Call it |
| W27 | `LicenseEnforcementTests.cs:57–72`, "WithFreeLicense" | The signed free key carries the same limits as no key, so ignoring the key passes | Sign it with lower limits (3/1); assert the 4th simple entity throws |
| W28 | `DAL.MongoDB.Testing/ReadPathTests.cs:115` | Asserts the test's copy of `Save`'s two lines | Extract an internal `ToDocument` in the source and test that, or move the check to a gated container suite |

### Entities.Web.Testing

| # | Test | Why | Fix |
|---|---|---|---|
| W29 | `StartupValidationTests.cs:258, 277` | `UseDefaults()` wires the primer interceptor, so the "missing interceptor" premise is false and nothing is reported | Wire without `PrimerInterceptors`; assert the one Warning (or Info) naming `ContosoContext` |
| W30 | `PersonControllerTests.cs:339`, `Filter_Partial_SearchObject` | Course id 3 matches no enrolment, so a filter reading only the first id passes; `Assert.Contains(…)` passes on one match | Real ids, a student only in the third course, the exact name set |
| W31 | `CourseAttachmentsControllerTests.cs:212` | `Assert.Equal(item.ObjectId, item.ObjectId)` | `Assert.Equal(courseId, item.ObjectId)`; compare file names |
| W32 | `CourseAttachmentsControllerTests.cs:1227` | Pins EF key order for included attachments, against the `SortOrder` the save stamped | Assert `SortOrder` and file name per attachment |
| W33 | `GetRequiredEntityServiceTests.cs:75` | `Contains("SearchObject")` is satisfied by the requested type's own name | Assert each path's full message (this exposes S5) |

### Office, Drawing, Media, Invoicing, Payments

| # | Test | Why | Fix |
|---|---|---|---|
| W34 | `ExcelTestExtensions.cs:97, 100, 119, 122, 147` | `read - source < 1e-9` is a signed difference: values read back as 0, or dates read back later, pass | `Is.EqualTo(source).Within(1e-9m)`, `Within(1).Milliseconds`, and the row count |
| W35 | `DrawingImageExtensions.cs:11–24, 89–123` (To_Png, To_Jpeg, CropRect, MakeTransparent, MakeOpaque) | Save a file and assert nothing. The outputs show GDI `MakeOpaque` keeps the alpha channel and Skia/GDI `MakeTransparent` disagree (S2, S3) | See M1 |
| W36 | `VCardsTestsBase.cs:32`, `Can_Read_V4_0` | Reads the 3.0 sample as 3.0, so the 4.0 path never runs | Read `V4_0`; the expected TEL type varies by version |
| W37 | `OCRTestsExtensions.cs:37` | `service.Read(img)` passes no language, so PaddleOCR "Dutch" runs the English model | Pass the language through |
| W38 | `MsgReaderTests.cs:38, 58`, `MessageObjectTests.cs:115, 141` | Loops bounded by the parsed count (zero attachments passes); bytes compared with order-blind `Is.EquivalentTo`; `Subject` never asserted | Assert counts first, bytes with `Is.EqualTo`, and the subject |
| W39 | `ExcelTestExtensions.cs:150` duplicate headers; `ZXingBarcodeTests.cs:70`; `ImageBuilderExtensions.cs:87–93, 237–277` | The name promises more than the assertion: "not empty", `Not.Null`, a drawn blue layer and label never checked (one check commented out) | Assert the `Title_2` key and count, the decoded content, `#0000FF` at its position |
| W40 | `SpireBarcodeTests.cs:57`; `BillitRegistrationTests.cs:25` | True at compile time; true by the interface hierarchy | Delete those assertions |

### Word and PDF

| # | Test | Why | Fix |
|---|---|---|---|
| W41 | `WordTestsBase.cs:609` (`EnforceEvenAmountOfPages…`), `:536` (`Merged_Documents_Start_On_A_New_Page`), `:2216` (`Page_Settings…`) | The padding-page checks run only `if (pages.Pages >= 5)`, so a backend that stops padding skips them. CHARLIE's page and pages after the first are never checked | `protected virtual int MaxPdfPages` (Spire: 3), exact page counts, later-page checks whenever not capped |
| W42 | `AsposeTests.cs:141`, `Odt_Template_Is_Read` | `Is.Not.Empty` passes on the evaluation banner; duplicates `From_File("template.odt")` | Delete |
| W43 | PDF `SpireTests.cs:22, 31`, `Free_Edition_Refuses_*` | `Assert.CatchAsync` accepts any exception, a missing asset included | Pin the exception type and message |
| W44 | `SyncfusionTests.cs:79`, licence check | Reads only the Word text; the PDF library's own trial banner is unchecked, and there is no unlicensed control | Check a PDF conversion too; add the negative control |
| W45 | `HtmlToPdfTestsBase.cs:47, 56`; `PdfTestsBase.cs:108`; `WordTestsBase.cs:2528` | `The_File_Is_A_Pdf` checks only `ContentType`; the blank-page test only the page count; `RemoveEmptyPages_Leaves_A_Pdf_Without_Them` tests a PDF that has none; `DocumentBuilder_Joins…` only section starts | Move the content-type check into `Create` and drop the test; assert no words or images; rename; assert the texts |
| W46 | `WordTestsBase.cs:2451`, `ToImages_Returns_One_Image_Per_Page` | Compares with the page count of the backend's own conversion | `multipage.docx` has 5 pages: assert 5 (merge with `To_Images`, M5) |

### Common, IO, TreeList, Normalizing

| # | Test | Why | Fix |
|---|---|---|---|
| W47 | `RegexTests.cs:120–123, 155, 157`; `NormalizingTests.cs:47` | Four IP cases commented out because the validators have no anchors (S1); phone extraction expected truncated (S7) | Fix S1 and restore the cases; S7 per D1 |
| W48 | `ZipStorageTests.cs:43, 97` | `List_Nested_Folder` expects nested files with `Recursive = false`; `Filter_By_EntryType` commented out: `ZipFileService.List` ignores both options (S6) | Per D1; add a non-recursive scenario to the shared helper |
| W49 | `DictionaryUtilityTests.cs:26`; `CreationTests.cs:125, 135`; `ZipUtilityTests.cs:112` | Read `../../../assets` (the folder is `Assets`) and split on `'\\'`: fail or test something else on Linux | C3's asset helper; `Path.GetRelativePath` and `Path.DirectorySeparatorChar` |
| W50 | `FsFileProcessorTests.cs:12–19` | Seeds `folder` but lists `folder/file_processor/file_processor`: empty compared with empty. The same traversal-vs-traversal check in `StorageTestHelper.cs:264` has no fixed list | Root at `folder`; assert the 10 expected URIs |
| W51 | `CountryUtilityTests.cs:95`; `TimerTests.cs:55` | The "xx" case loops over nothing; `Simple_HierarchySort` has no count and an invariant almost any order meets | `Is.Empty`; each node's parent has a lower index, count equals the tree |
| W52 | `ZipStorageTests.cs:87, 107`, forward/backward slashes | Both read `file1.txt`, which has no slash, and assert `Not.Null` | Use `dir1\file1.1.txt` and compare bytes |
| W53 | `CreationTests.cs:261`; `CookbookTests.cs:370` | `Is.Not.EqualTo(reverseTree)` holds by reference equality; a "returns other recipes" test asserts `Is.Empty` | Delete the lines; rename the test |
| W54 | `DictionaryUtilityTests.cs:130, 736` | Wrong wiring passes because the values coincide (both 1, both "1st nested") | Distinct fixture values |
| W55 | `DimensionsTests.cs:20–37` | Expected values recomputed with the code's own constant, and the `"In"` key never binds | Literal reference values under `"Inches"` |
| W56 | `DateTimeDefaultsTests.cs:16–28` | Tests an auto-property; nothing in Common reads it | Delete |
| W57 | `DateTimeUtilityTests.cs:20`; Globalization fixtures | Pass trivially on a UTC host; `DisplayName` follows the UI culture (plausible) | `Assume` a non-zero offset; `[SetUICulture("en-US")]` |

## 2. Useless tests

Delete or merge. None of these carries a check that another test lacks, unless the row says so.

- **U1. Duplicated pairs.**
  - Entities.Testing:
    - The 8 PrimerTests/InterceptorTests pairs: parameterize over container and interceptor path, as `PrimerDiscoveryContractTests` does.
    - `ArchivedRoundTripTests.cs:413` = `:188`.
    - `StartupValidationTests.cs:89` ≈ `:54`.
    - `AttachmentFilterTests.cs:71` ⊂ `:59`.
    - `AttachmentFileNameTests.cs:51` ⊂ `AttachmentConversionTests.cs:15`.
    - The fixture pairs NonIntKeyed/SoftDeleteLeak and AttachmentSync/ArchivedOwnerSync: merge each into one fixture.
  - Entities.Web: `Update_FileName`, `Update_File`, `Update_Entity_And_Replace_Attachment` and `Update_FileName_By_ObjectEntity_Update` are covered by the newer route tests; fold `Update_File`'s new-id check into `New_Bytes_Replace_The_File("file")`. Also `EntityValidatorWebTests.cs:209` = `:237`, and the `Get_404` tests ≈ `Insert_And_Force_404` (one `Details_Of_A_Missing_Id_Is_404` theory per controller).
  - Word and PDF:
    - `Convert_To(Html)` = `Convert_To_Html_Is_Self_Contained` (byte-identical outputs).
    - `Page_Takes_Format_And_Orientation(A4, Portrait)` = the default test: make it `(A4, Landscape)`, which is untested.
  - Office:
    - `OcrClientTests.Read_Returns_Text`.
    - `MessageParserClientTests.Parse_Msg_HasFrom`.
    - `TesseractTests.Test_With_Defaults` = `Read_English`.
    - Four `MessageObjectTests` covered by `Create_MessageObject`.
    - `GdiDrawPositionTests.Build_Images` = `GdiImageBuilderTests.Build_Images`.
    - `CsvTests.Write_Products_Dic`.
    - `DrawingPositionExtensions.cs:380–381` checks the same pixel twice.
  - Common and IO: `StorageTestHelper.Test_Filter_By_Folder` = `Test_Filter_Recursive`; `EnumTests.cs:52–92, 101` repeat earlier tests; `FileNameTests.EnsureContained_*` overlaps `ContainmentTests`.
  - Web.Security: four copies of "anonymous welcome"; `LicensingTests.cs:138` = `:161`.
- **U2. Tests of something other than Regira code.**
  - `EntityTests.Test_Types` (interface declarations).
  - `ZipUtilityTests.Test_ZipArchive` (`System.IO.Compression`).
  - `MinimalApiTests.Create_Client` and `Get_404` (ASP.NET routing).
  - `ServiceTests`' 7 resolution tests (`Is.Not.Null`; DI's matrix covers them).
  - `AccountEmailOptionsTests` (a lambda the test wrote): keep it as a guide-snippet test and say so.
  - `NormalizingTests.cs:47–48` asserts the test's own `PeopleNormalizer`.
- **U3. Assertions of the arrange step.**
  - `PrimerTests.cs:487–489`.
  - `RepositoryTests.cs:62, 108, 126` (MongoDB).
  - `TestFor1Services.cs:69–70` and `TestFor4Services.cs:243/245` asserted twice.
  - `TestWithoutDbContext.cs:130`.
  - `ConcurrencyTokenMarkerTests.cs:244`.
- **U4. Permanently ignored or commented-out tests with no stated reason.**
  - Commented out: `ZXingQRCodeTests.cs:30`, `SpireBarcodeTests.cs:34`, the EPPlus/ClosedXML typed tests, `MollieTests.cs:30`, `SkiaImageBuilderTests.cs:19`, `RegexTests.cs:145, 154`, `DictionaryUtilityTests.cs:460–501`.
  - Ignored with a reason that no longer holds: `SshStorageTests`/`SshFileProcessorTests` `[Ignore("wait for proper config")]`. Do not just remove the attribute: the dispose deletes everything it can list on the SFTP account (C9).
  - Per AGENTS.md, a backend gap becomes an ignored test that says why; everything else is deleted.
- **U5. Dead files.**
  - Empty `[SetUpFixture]`: `Entities.Testing/GlobalTestSetup.cs`, `Entities.DependencyInjection.Testing/GlobalTestSetup.cs`.
  - An empty `[ModuleInitializer]`: `Entities.Web.Testing/GlobalTestSetup.cs`.
  - A "ToDo" fixture: `DAL.EFcore.Testing/QueryBuilderTests.cs`.
  - Fully commented out: `PeopleContext.cs`, `DepartmentNormalizer.cs`, `ItemWithCoursesNormalizer.cs`.
  - Unused helpers: `CourseQueryFilter3`, `CourseRepository3A`, `CourseManager3A`, `EntitiesStartup.cs`, `PersonNames.NL`, `Docx.HeaderText`, `DummyPersonNormalizer`, `AnyNormalizer`.
  - TestApi:
    - `PersonSortQueryBuilder`.
    - The Mapster and Azure project references with their commented code.
  - Testing.Library: `Blogs/` and `Farm/`.
  - `ProviderHarness.cs:109–112`, an `Assert.Ignore` that `Assume` before it makes unreachable.
- **U6. Unused output.**
  - `WordClientTests`/`PdfClientTests` create an Output folder and never write to it.
  - `transparent-400x300.jpg` is identical to `white-400x300.jpg` and never used.
- **U7. `Testing.Library`.** `People.All` (`Data/People.cs:14`) omits David, so `DbContextNormalizerTests` never checks him. `Contoso/Diagram.cd` is stale.
- **U8. Timing measured and never asserted.** `TreeList.Testing/TimerTests` records Min/Max/Avg with `DateTime.Now`. Keep the hierarchy check (W51) and drop the timing, or mark it `[Explicit, Category("Perf")]`.
- **U9. EF premises.**
  - `DeleteCycleTests.cs:185, 200, 217, 429`.
  - `AttachmentForeignKeyValidatorTests.cs:135`.
  - `QuerySortingTests.cs:80`.
  - `RelatedCollectionTempIdTests.cs:162`.

  Keep them, and name them `Control_…` as `SynchronousSaveChangesTests` does, so a failure reads "EF changed".

## 3. Results not measurable enough

Each item gets an independent check: a read-back through a separate reader, an exact value from the fixture, or a pixel or size fact.

- **M1. Drawing operations.** Use a generated input with known alpha:
  - Alpha must be 0 exactly where the key colour was, and 255 everywhere after `MakeOpaque`.
  - Check the output format by its magic bytes.
  - Crops are half × half, and their centre pixel equals the source pixel.
  - `Resize` gives a long side of exactly 1024 with the aspect ratio kept to within 1 px.
  - `GdiImageTests.cs:20`: width and height `Within(1)` of 42.4.

  Expect red tests for S2 and S3.
- **M2. Barcode and QR writers** (`BarcodeTestsBase.cs:38–56`, `QRCodeTestsBase.cs:38–50`, Clients `Create_*`). These check only that the byte array is not empty. Decode with ZXing as an independent oracle and compare with the input; assert both width and height.
- **M3. Refused writes are never re-read** (Entities.Web):
  - `ConcurrencyTokenControllerTests.cs:55, 106`.
  - `EntityErrorResponseTests.cs:249`.
  - `EntityValidatorWebTests.cs:227, 272`: only Delete checks.
  - `PersonControllerTests.cs:233`.
  - `EntityEndpointTests.cs:278`.

  After every 400 or 409, read back through a fresh context and assert the row and the stored file are unchanged: "refused but partly written" is exactly the bug class the write pipeline guards against. Problem bodies get one shared `AssertProblem` (key, message, title, type) instead of key-only checks (`DomainActionExceptionTests.cs:33`, `ConcurrencyTokenControllerTests.cs:172`).
- **M4. Status-only refusals** (Web.Security). One helper asserts the 401 status plus the `WWW-Authenticate` `error_description`. The malformed-token 400s (`IdentityControllerTests.cs:306–382`) check the body shape, as `BadRequest_Bodies_Take_Three_Shapes` does. Parse cookie attributes with `SetCookieHeaderValue` (`CookieAuthenticationTests.cs:56`).
- **M5. Word conversions.**
  - `Convert_To` (`WordTestsBase.cs:2175`, 18 cases) checks only the format: read the content per format (PDF text and page count, the DOCX facts, HTML with tags stripped, ODT/EPub text).
  - `GotenbergTests.cs:119`: assert the excerpts, 2 A4 pages.
  - `GetText`: compare the word sequence.
  - `DocumentBuilder_Builds_Paragraphs_And_Headers` (`:2517`): `Has.Some.Contains("Header")` passes with one header missing.
  - The conditional-block scenarios (`:807, 854, 894, 1006, 1156, 1184`): use `Texts(facts)` as the loop scenarios do.
  - Exact counts where `>=` is used (`:484, 516, 572`).
  - The rewind check (`AssertReadableWithoutRewind`) on every Word output, not just Mini.
- **M6. PDF.**
  - `GetTextPerPage` compares sorted characters, an anagram check: compare sorted words.
  - `ImagesToPdf`: each page's image keeps its input's aspect ratio.
  - PDF Gotenberg header and body use the same text: use different strings.
- **M7. Office clients** (network suites).
  - Split, merge, image and HTML conversions: page counts, image sizes and extracted text.
  - Word: `%PDF` and the text.
  - Excel: read back sheet names and rows.
  - OCR: reuse `EXPECTED_EN`/`EXPECTED_NL`.
  - Mollie: `tr_` id plus a `Details` round trip that doesn't depend on existing account data.
  - MailKit: read the message back by subject from Mailpit; the helper exists.
- **M8. Storage scenarios** (`StorageTestHelper`, every backend).
  - Exact identifier sets from `TestFilesCreator`.
  - Bytes with `Is.EqualTo`, not the order-blind `EquivalentTo` (also `GitHubStorageTests.cs:81, 95`).
  - `LastModified` bounded on both sides.
  - Contents checked in the zip and containment tests.
- **M9. Entities and DAL.**
  - `With_Defaults` asserts exact counts, including the normalized-content builder, the concurrency primer and the 10/100 paging defaults.
  - `First()`-only checks become full type lists.
  - MongoDB CRUD and PostgreSQL overwrite tests read back (a marker table proves the restore recreated the database).
  - SQL Server `Clone` and EFcore `Diacretics`: per-property and literal expectations instead of values from the code under test.
  - Entities.Testing:
    - Fresh-scope read-backs in `PrepperTests.cs:195, 284`.
    - The 16 split constraints `Has.Some.Contains(a).And.Some.Contains(b)`, which two different log lines can satisfy, become the same-item form (list in the Entities review: `StartupValidationTests.cs:68, 144, 170, 260, 465, 757, 770`, the four validator fixtures, `ValidatorTests.cs:1106`).
    - `"|first|second"` exact in `PrimerDiscoveryContractTests.cs:112, 120`.
- **M10. Smaller exactness fixes.**
  - Normalizing `Is.Not.EqualTo(source)` becomes literal outputs.
  - `RefreshTokenTests.cs:20, 140, 224`: expiries near now + the lifespan; the family ceiling after one refresh; a 43-character token.
  - `AnalyticsEndpointsTests.cs:97, 120`: assert `store.LastStatsQuery`, not numbers the test store computed.
  - `PageViewWriterTests.cs:157, 307`: the stored masked IP and the warning.
  - Web.HTML `Razor_Order_Model` (×3): a fixed `Created` and the full HTML.
  - VCards writers: write then read, compared field by field.
  - CSV writers: seeded Faker plus a typed read-back.
  - Snapshot frames at 1.0 s and 2.5 s hash differently.
  - Unseeded `Random`/Bogus/`DateTime.Now` data (Entities.Web seeds, CSV, TreeList) becomes fixed values.

## 4. Consistency

The dominant pattern is already good. These rules write it down, and the list after them names what deviates. **Rename and restyle only when a file is touched for another reason**; a mass rename fixes no defect.

- **C1. Frameworks.**
  - NUnit 5 with `Assert.That` in 34 projects.
  - xUnit 2.9.3 in the three that host ASP.NET in process: Entities.Web (xUnit asserts), Web.Security (Shouldly) and Entities.Mediator. CONTRIBUTING names only two.
  - Recommendation: keep the split and write it down (D2).
  - Drop the roughly 55 `ClassicAssert`/`CollectionAssert` calls, mostly in Common, Barcodes, Mail, Payments and MongoDB `RepositoryTests`.
- **C2. Names.**
  - Test methods have five styles: `Test_Snake`, `TestPascal`, `Method_Scenario_Result`, short legacy names (`Resize`, `Read_Dutch`) and sentence style. One project, PostgreSQL, uses lower-case sentences. Sentence style (`A_Rejected_Delete_Of_A_Link_Leaves_Its_Attachment_Unmarked`) is the majority; use it for new and touched tests.
  - Classes: the `*Tests` suffix (not `TestFor1Services`), one class per file.
  - Files: `EPPlustTests.cs`, `UziGranotQRCodeTests.cs` holding `UziGranotTests`, `MailerTests.cs` holding `MailerTestsBase`.
  - The `Office.Word.testing` project and namespace use a lower-case `t`. Renaming them touches the solution and paths; do it once, with C3.
- **C3. Asset paths.** There are five spellings:
  - `AssemblyUtility.GetAssemblyDirectory() + "../../../"`
  - `AppContext.BaseDirectory + "../../../"`
  - `Assembly.Location`
  - `Directory.GetCurrentDirectory()` (`MsgReaderTests.cs:18`)
  - sibling projects (`../../../../Office.X.Testing/Assets`)

  All of them break under `--artifacts-path`, and one breaks on Linux (W49). Replace them with one `TestAssets` helper that reads the project directory from an `[AssemblyMetadata("ProjectDir", "$(MSBuildProjectDirectory)")]` that the build writes. It works from any output path and in the Linux container.
- **C4. Shared test support.** These pieces are copied between projects:
  - Gotenberg start-up, about 60 lines, Word and PDF.
  - Container gating and `REGIRA_CONTAINER_REUSE`, in five fixtures. The Mailpit fixture lacks reuse.
  - `OutputSetUp`, `Record`, `TestOutputPath`.
  - Two `Facts` readers: PDF uses PdfPig and an alpha-aware `ImageFacts`; Word uses Docnet, the deprecated backend's engine, and samples a 40×60 grid that ignores alpha.

  Recommendation: shared source files linked into the projects that need them (D4), the PdfPig reader for both, and drop Word's PDF.DocNET reference. In Entities.Web, one `InMemoryEntityHost`, one JSON mapper and one ProblemDetails reader instead of four copies. In Entities.Testing, `StartupValidationHarness.Run(...)` replaces five hand-written hosted-service loops and about 25 inline set-ups.
- **C5. Per-backend pattern.** There are four patterns in Office and Drawing:
  - an abstract base carrying the attributes (best);
  - virtual methods whose `[TestCase]` lists are copied into every backend;
  - extension methods plus hand-listed wrappers;
  - a hand-written duplicate (`QRCodeTestExtensions.cs`).

  Converge on the first. A gap is an override with `Assert.Ignore("why")`, or Word's `[LeavesOut]`. PDF Spire runs 13 "does not implement" cases that load the PDF before they skip (up to 0.3 s each). Check the capability first.
- **C6. Gating.** One pattern per kind:
  - The `Containers` category plus `Assert.Ignore` in `[OneTimeSetUp]`, as MailKit does.
  - Missing secrets skip; today `MollieTests` throws and Office clients fail on a missing `ApiServices:BaseUrl`.
  - `[Platform("Win")]` on the GDI, Spire PDF, SelectPdf and UziGranot fixtures, so a Linux run skips them.
  - The Providers' `Containers` category goes on the PostgreSQL and SQL Server instances only, so the SQLite run stays in the fast loop.
  - `LicenseStatusClientTests` and `WordClientRequestTests` use canned handlers but are tagged `Network`, so the offline run drops them; untag them.
  - MongoDB `RepositoryTests` needs a local server with no gate: a Testcontainers MongoDB behind `REGIRA_PROVIDER_TESTS`, as PostgreSQL does.
  - A `License` category on `GotenbergUnitTests`.
- **C7. Parallelism declarations.**
  - CONTRIBUTING wants `Properties/AssemblyInfo.cs` in every project; DAL.MySQL and Media lack it.
  - Stale reasons:
    - Entities.Testing's AssemblyInfo names two `UseUtc` writers where one writes.
    - `DbContextNormalizerTests` is `ParallelScope.None` with no reason.
    - PostgreSQL's comment counts one argument fixture where there are two.
  - Common.Testing mixes `All`, `Self` (the assembly default) and nothing without a reason. Rule: `All` for stateless fixtures, `InstancePerTestCase` plus `All` for per-test state, nothing for remote stores.
- **C8. Lifetimes and temp files.**
  - About 45 Entities tests never dispose their `ServiceProvider` and resolve scoped services from the root. Use `using`, a scope per operation, and `ValidateScopes = true` in the shared helper.
  - Temp folders come in five patterns, including fixed `cwd/assets/zip-*` folders that collide when sessions share `bin`, and temp files with no `finally`. Use `Directory.CreateTempSubdirectory` with cleanup.
  - Excel writes `Output/TestData.xlsx` from two backends that run in parallel, and `countries-output.xlsx` from two tests: give each its own name.
- **C9. Remote fixtures.**
  - Azure re-seeds and empties the shared real container around every test, and is `[NonParallelizable]` for it. Give each fixture its own `regira-test-{guid}` container: seed once, delete it in one call.
  - SSH needs the same before its `[Ignore]` can go.
- **C10. Smaller items.**
  - Expected and actual swapped: `NumberTests.cs:22, 38`, `DictionaryUtilityTests`, `DrawingImageExtensions.cs:50`, `MessageObjectTests.cs:174`, `CourseControllerTests.cs:274`, `PersonControllerTests.cs:447`.
  - `Assert.That(x == y, Is.True)` hides values (about 25 sites).
  - Blocking `.Wait()`/`.Result` in async tests (MongoDB `BackupRestoreTests`).
  - Tests that sit in another package's project:
    - `EntitySaveHelperTests`, `GetRequiredEntityServiceTests` and `CommonWebMiddlewareTests`.
    - The DAL.EFcore UTC tests in Entities.Testing.
  - Shared models borrowed across fixtures (`StartupValidationTests` uses three other fixtures' nested types): move them to `Infrastructure/`.
  - History-style comments ("Finding 5 (review)", "Repro for evaluation B3"): drop them.
  - New fixtures use the guide's example names (Product, Order, Category, `Administrator`, `AppUser`); leave Contoso as it is. Test data should not use a real person's name (MongoDB `RepositoryTests`).

## 5. Performance

Ranked by cost. Seconds are measured (summed test time per class, or wall time where it says "alone"). Counts of hosts, requests and keys come from reading the code. None of these changes weakens an assertion, and several strengthen one.

| # | Change | Cost today | Expected effect |
|---|---|---|---|
| P1 | **Drawing.** `MakeOpaque`/`MakeTransparent` only on inputs with alpha (`img-5/6.png`, `thumbs-up.jpg`, M1's generated image); about 480 px copies of `img-1..4.jpg` (1080×1920) for To_Png, Resize, Crop and Rotate, whose assertions are relative to size. `Add_Text_With_Margin` asserts the padding geometry instead of a third OCR | 47 s for Skia MakeOpaque, 6.6 s for MakeTransparent, about 37 s of OCR in text tests; 122 MB of PNG per run | Drawing alone from 22.6 s to about 12 s; output in MB, not 122 MB. S4 makes the library itself fast |
| P2 | **Word.** Replace `Nested_Documents_Do_Not_Wear_Out_The_Service` (51 builds per backend, guarding a per-instance counter that is now an `AsyncLocal` depth) with a unit test of `NestedDocumentGuard`: 200 sequential enters, depth 101 throws, two flows don't share a count. Keep `A_Template_That_Includes_Itself_Fails` per backend. Drop the duplicate HTML conversion; small in-code documents for `Convert_To` and the page-size tests (`Convert_To(Rtf)` writes 20 MB) | About 23 s of CPU for the wear-out test; large conversions | Word alone from 53 s to about 40–45 s before P10; the Aspose fixture sets the floor |
| P3 | **PDF.** Fold `The_File_Is_A_Pdf` into `Create` and turn the duplicate A4 case into A4 landscape (2 fewer renders per HTML backend); cache `InputFacts` and DOCX facts in a static dictionary (`Merge…` parses six inputs twice); check the backend's capability before loading files; downscaled images for the image cases | Playwright about 4 s per render, 12 renders | Several seconds per HTML backend; the biggest gain is P10 |
| P4 | **Entities.Providers.** One container per provider per assembly, in a `[SetUpFixture]` with lazy start; each fixture keeps its own database name. Pin `mssql/server:2022-latest` to a version | Four containers per run: two SQL Server, about 2 GB RAM each | One SQL Server and one PostgreSQL start fewer; 89 s in the parallel run is mostly SQL Server start under contention |
| P5 | **Entities.Web.** Run `ContosoApiFactory` in a `Testing` environment, with one Development boot test per surface; one host per class for the read-only self-built tests (`ArchivedQueryBindingTests` 12 → 1, `EntityValidatorWebTests` 7 → 1); one instrumented factory instead of 10 derived ones in CourseAttachments; a template database copied per test instead of `EnsureCreated`/`EnsureDeleted`; 5–10 POSTs where an insert is tested, not 100 | About 78 host builds (33 factory, 45 self-built); about 190 SQLite file cycles; about 520 sequential POSTs | Entities.Web alone from 22 s to about 12 s (estimate) |
| P6 | **Web.Security.** The constructor's `WithWebHostBuilder` builds a new host per test, so the class fixture is never shared: set the content root once in the factory and use the fixture's host, clearing `SentEmailStore` per test. `PasswordHasherOptions.IterationCount = 1` in the test startups (hash strength is not under test). Split `IdentityControllerTests` (45 tests per TFM, run in sequence) along its section headers | About 140 host builds per TFM, about 70 full-strength hashes; `IdentityControllerTests` 13.3 s and `RefreshTokenEndpointTests` 11.6 s over both TFMs | Removes most of the suite's wall time; W8 goes with it |
| P7 | **Web.Analytics.** `PageViewWriterTests`: keep one or two timed-flush tests; the others use the shutdown flush (`StopAsync` writes what is queued). Replace `Task.Delay(500)` (`:295`) with start then stop | 11 of its 14 tests wait for the 1 s minimum flush interval: 12.2 s, serial | Web.Analytics from 14.8 s to about 5 s |
| P8 | **Security sleeps.** `JwtAuthenticationTests.cs:58` and `RefreshTokenEndpointTests.cs:88` wait 2.5 s for a token to expire. Mint it already expired with the host's own `JwtTokenOptions` secret (`nbf` and `iat` 10 min back) and remove `TokenLifeSpan`. No source change; no `TimeProvider` | 10 s of test time over both TFMs | 0 s |
| P9 | **Keys and hashes.** One RSA-2048 key per assembly in Licensing.Testing (about 60 generations today), then fixture parallelism with only the five console-capturing tests serial; the same in `LicenseEnforcementTests` (`[SetUp]` → `[OneTimeSetUp]`). Drop the hash cases that vary only the `Secret` both hashers ignore; low iterations in round trips, one test on the default | Licensing's tests 2.5 s in total, mostly key generation; `BCryptNet_Hash_Tests` 8.0 s and `Hash_Tests` 7.6 s (24 cases) | 1–2 s off Licensing; most of the 15.6 s of hashing |
| P10 | **Within-fixture parallelism**, trialled one fixture at a time, each run 5 times: `[Parallelizable(ParallelScope.Children)]` on the browser fixtures first, then the Word backends; `InstancePerTestCase` plus `ParallelScope.All` in Entities.Testing. Remove the stale `[NonParallelizable]` on `UseDefaultsAutoWiringTests` and `ParallelScope.None` on `DbContextNormalizerTests`, and narrow `UtcTimestampTests` to its 6 writer methods | Tests in one fixture run one at a time: Playwright 45 s, Aspose 60 s serial | Estimate: browser fixtures 45 → 15 s, Aspose 60 → 20 s. Unknowns: FreeSpire and SelectPdf thread safety, and the two `< 2 s` timing assertions in `TemplateBlocksTests.cs:262, 521` |
| P11 | **Small items.** `Task.Yield()` instead of `Task.Delay(5)` ×25 (`FsFileProcessorNestingTests.cs:47`); `IsCompleted == false` instead of a 200 ms wait (`BackgroundQueueTests.cs:93`); seed read-only Entities fixtures once (`MultiFieldKeywordSearchTests`, `QuerySortingTests`); in-memory SQLite instead of the 3 temp-file databases; `TimerTests` at 1,000 nodes with a fixed seed (10,000 today, O(n²)); `CollectionTests.Execute_List_In_Chunks` in memory (it reads a 37 KB file 1,000 times) | About 1–3 s each | Small |
| P12 | **Output and attachments.** Word and PDF attach every output and facts file to the result: attach only on failure, from `[TearDown]`. `Assets/Output` stays for people to inspect | 125 MB copied per run with a logger | 0 MB on green runs |
| P13 | **Remote fixtures.** Azure per-fixture container (C9): about 400 requests today, about a third after, and `[NonParallelizable]` goes. MongoDB in a container (C6) removes the 30 s server-selection timeout per test on a machine without MongoDB | Network categories; not in the offline numbers | Faster and safer live runs |

## 6. Library defects found in passing

These are product changes, separate from the test work (D1).

| # | Defect | Evidence | Size |
|---|---|---|---|
| S1 | `RegexUtility.IsValidIPv4Address`/`IsValidIPv6Address` call `Regex.IsMatch` without anchors, so `192.168.0.1.1` and `300.255.0.255` are "valid". Public API that a consumer may use for input validation | `src/Common/Utilities/RegexUtility.cs:146, 165`; the test cases are commented out | Small: wrap the patterns as the email and URL validators do |
| S2 | GDI `MakeOpaque` keeps the alpha channel: `ChangeOpacity(img, 1)` returns `new Bitmap(img)`. It also keeps the source format, so a `.png` output can hold a JPEG | `src/Drawing.GDI/Services/ImageService.cs:187`, `GdiUtility.ChangeOpacity`; the output PNG keeps 35,991 transparent pixels | Small |
| S3 | `MakeTransparent` differs per backend: GDI clears the exact colour (as the interface documents), Skia clears every pixel brighter than it on all three channels | `src/Drawing.SkiaSharp/Utilities/SkiaUtility.cs:96`; `IImageColorService` says "the specified color" | Small; a semantics choice |
| S4 | Skia `MakeOpaque` is a column-major `GetPixel`/`SetPixel` loop, about 11 s on a 1080p image, and never disposes `outputBitmap` | `src/Drawing.SkiaSharp/Services/ImageService.cs:229–256` | Small: bulk pixel access with the same per-pixel rule |
| S5 | `EntityServiceDiagnostics.DescribeMissingService` builds names from `Type.Name`, so users read "IEntityService\`3<Course, Int32, …>" and, on one path, "IEntityService\`2" with no arguments | `EntityServiceDiagnostics.cs:46, 52, 58, 63` | Small |
| S6 | `ZipFileService.List` ignores `Recursive` and `Type`, while `BinaryFileService` honours both | `src/Common.IO.Storage/Compression/ZipFileService.cs:51–70` | Medium; a semantics choice |
| S7 | Phone extraction truncates the last group (`"+32 3 384 30"` from a longer number), and the tests expect the truncation | `RegexTests.cs:155, 157`, `NormalizingTests.cs:47` | Pattern change or a documented limit |
| S8 | `UseMapping<,>()` for Mapster may configure nothing: `EntityMapConfigurator` registers lazy `NewConfig` factories that nothing in `src/` resolves. Plausible, to be checked first | `src/Entities.Mapping.Mapster/EntityMapConfigurator.cs:9–13` | Unknown |

## Considered and left out

- **Microsoft.Testing.Platform.** It would save about 1 s of runner start per assembly, mostly overlapped in the parallel run. A solution can't mix VSTest and MTP, so the three xUnit v2 projects would have to move first.
- **One `WebApplicationFactory` for all Entities.Web classes** (`ICollectionFixture`). A collection runs serially, which lengthens the slowest path. Per-class hosts are right; P5 cuts their count instead.
- **A clock (`TimeProvider`) or an `AsyncLocal` override in the packages for tests.** It adds API with no defect behind it. Already-expired tokens (P8) and narrowing the serial tests (P10) get the same speed.
- **A lower `NestedDocumentGuard.MaxDepth` for tests.** It would need a product hook. The 100-level self-inclusion test stays.
- **One browser shared across PDF tests.** The backends launch per call on purpose (no leaked `chrome.exe`). P10 parallelises the calls instead.
- **A mass rename to one naming style, and Contoso to the guide's names.** Churn without a defect (C2, C10).
- **Multi-targeting the six net10-only projects that test net8 packages** (Security, Licensing, Hosting, Web.Analytics, GeoIP2, Web.HTML). This is a real coverage gap, but a separate piece of work. Web.Analytics' test store needs `System.Threading.Lock` replaced first.

## Decisions

| # | Question | Recommendation |
|---|---|---|
| D1 | The library defects: in this work or apart from it? | Fix S1, S2, S4 and S5 now, each with the test that exposes it. S3, S6 and S7 need a semantics choice; until then, an ignored test that says why and a docs line. Check S8 before deciding |
| D2 | xUnit in the three ASP.NET-hosting projects | Keep it; name all three in CONTRIBUTING and give the rule (xUnit where a project hosts ASP.NET in process). Migrating fixes nothing |
| D3 | Naming | Sentence style for new and touched tests; no mass rename |
| D4 | Where shared test support lives (C3, C4) | Linked source files from a `tests/Shared/` folder, included by the projects that need them. That keeps Testcontainers and PdfPig out of `Testing.Library`, which the Entities suites share. Alternative: a small `Testing.Support` project |
| D5 | Within-fixture parallelism (P10) | Trial it fixture by fixture with repeated runs; keep it only where five runs stay green |
| D6 | Output files | Keep writing them (P1 and P2 shrink them); attach only on failure (P12) |

## Steps

1. **Wrong tests (W1–W57).** One commit per area. Prove each fix by running it against a broken copy of the source in a scratch snapshot. Tests that turn red on a real defect (S2, S3, possibly the vCard 4.0 TEL type) follow D1. Run the IO, Common and TreeList path fixes (W49) on Linux too (the `mcr.microsoft.com/dotnet/sdk:10.0` container).
2. **Speed without changing an assertion (P1–P9, P11, P12).** Measure each project before and after, alone and in the parallel run.
3. **Measurable results (M1–M10).**
4. **Useless tests and consistency (U1–U9, C1–C10)**, including the shared support (D4) and the CONTRIBUTING update (D2, C7).
5. **Parallelism trials (P10)**, then a final measurement of the full run, recorded under *Outcome*.

Each step is safe to commit on its own. The test projects ship nothing, so no `<Version>` bump or CHANGELOG line is needed. The exceptions are the S fixes and any `src/` change a test needs, such as the internal `ToDocument` that W28 suggests.

## Risks

- **Phase 1 may turn tests red** on behaviour that consumers rely on (S3, S6, S7). D1 keeps those as documented gaps until decided.
- **P10 can bring flaky tests.** Vendor libraries' thread safety is unknown. The trial runs each fixture 5 times, and any fixture that flakes goes back to serial with a note.
- **P5 and P6 change how the test hosts start.** One Development boot test per surface keeps startup validation and the OpenAPI wiring covered.

## Outcome (phase 1)

As of 2026-10-23, on `wip` at `37cbaa3`, uncommitted. Only `tests/` changed: 73 files, plus one new file, `Drawing.Testing/Abstractions/ImageReadBack.cs`.

**57 findings checked: 45 confirmed, 10 partly, 2 rejected.** Each fix was proven in a scratch copy of the tree. The old test passes on a broken copy of the source (a *mutant*), and the new test fails on that copy and passes on the real source. Ten new or corrected tests (12 cases) stay red on the real source because the library is wrong. They are marked ignored with the defect named (listed below). The full offline run (`dotnet build` then `dotnet test Regira-Packages.slnx --no-build --filter "TestCategory!=Network"`) is green: 4,671 passed, 0 failed, 42 skipped. That is the 29 skipped before plus these 13: the refresh-race test runs on net8 and net10. The path fixes (W49, W50, W52, W57) also ran on Linux in the `sdk:10.0` container: Common 308 passed and 5 skipped, TreeList 103 passed, IO 245 passed and 6 skipped.

Sections 2–6 were used in two places only. W35 builds the parts of M1 it needs: magic bytes, crops checked by pixel, the alpha after `MakeTransparent`/`MakeOpaque`. `Resize` and `GdiImageTests.cs:20` are still open in M1. W49 fixes only the asset folder's casing; the asset helper (C3) is phase 4.

| W | Verdict | What changed | Evidence |
|---|---|---|---|
| W1 | confirmed | `RefreshConcurrently` holds all N callers inside the resolver before any proceeds. New store test: one revocation per token over 8 threads × 200 rounds. The losing-race test is made deterministic with a store that holds the winner's write until a loser revoked the family, and is skipped on S9 | Mutant "service ignores the `TryRevoke` result": old 18/18 pass, new race tests fail. Mutant "store revokes without a lock": the new store test fails. A probe found S9 (below) |
| W2 | partly | `FakeAuthority` issues tokens 10 min back (`iat` = `nbf`). The expiry test asserts `WWW-Authenticate` says "The token expired" | Reproduced: the old token's header read "The token lifetime is invalid; NotBefore …" (IDX10224). Mutant "ClockSkew left at 5 min": old passes, new fails. Rejected for issuer, audience and tenant: each of those tests fails on its own mutant unchanged |
| W3 | confirmed | `ClaimsReport.RoleCount`. The accumulation test checks the role, a count of 1 and `IsInAdminRole` on 3 requests | Mutant "validation re-adds a role and renews the cookie": old 17/17 pass, new fails |
| W4 | rejected | none | The logout endpoint requires authentication, so its 204 already proves the cookie. Mutant "sign-in issues no cookie" fails both old tests at logout (401) |
| W5 | confirmed | Renamed `Test_Protected_With_Expired_Token_Returns_Unauthorized`. A control GET returns 200 before the wait, and the reason "The token expired" is checked after it. The wait stays (P8) | Mutant "lifespan in milliseconds": old 9/9 pass, new fails at the control |
| W6 | confirmed | The secret is 62 × `k` + `é` (63 ASCII, 64 UTF-8 bytes). The test asserts the message names "is 63 bytes" | Mutant "count UTF-8 bytes": old passes, new fails |
| W7 | partly | `:81` sends the expired bearer, as the endpoint documents. `:122` refreshes once (200) while the e-mail is confirmed, then expects the renewed token to be refused after the change. `:47` and `:158` are unchanged | Mutants for `:81` and `:122` pass the old tests and fail the new ones. `:47` and `:158` already fail on their own mutants |
| W8 | confirmed | `TestingWebApplicationFactory` pins Production in `ConfigureWebHost`. A pin in `CreateHostBuilder` loses: the factory adds its own web host builder, which re-reads `ASPNETCORE_` variables | Old: 2 tests fail with `ASPNETCORE_ENVIRONMENT=Development`. New: 246/246 under Development, `DOTNET_ENVIRONMENT` and none |
| W9 | confirmed | The AES failure test is deleted (`Another_Secret_Throws` covers it). The Symmetric one is replaced by `Decrypt_With_Another_Secret_Throws`, which uses a pinned ciphertext | Mutants "AES Decrypt returns empty" and "Symmetric swallows the exception": both old tests pass; the new Symmetric test fails |
| W10 | confirmed | Literals 5/2, 5/60/0 and 30/60/0 | Mutant "free tiers +1": old pass, new fail |
| W11 | confirmed | The key is signed for `regira.services` and its payload rewritten to `regira.entities` (valid JSON) | Mutant "signature never verified": old passes (a JSON error made the exception), new fails |
| W12 | confirmed | The test normalizer matches a pending course by reference (ids are 0 while saving). Expected values are 4 and 3 courses | From `TestData.GetCourses`: Bob has 4 courses, Bill 3 |
| W13 | confirmed | `:40`, `:72` and `:287` assert on a captured log instead of `DoesNotThrow` | Three mutants pass all three old tests. Each new test fails on its own mutant only |
| W14 | confirmed | Registers Status and Asset. On net10 it asserts Category is reported (control) and Status is not; on net8, no hazard | Mutant "IArchivable filter dropped": old passes, new fails |
| W15 | confirmed | Bravo 2024-02 is seeded between the Alphas. Expected months: [1,3,2] (ThenBy) and [3,1,2] (descending). The proposal's "Bravo earliest" would have made the descending case blind | Mutants "ThenBy restarts" and "descending restarts" each fail the new test |
| W16 | confirmed | A second owner with `other-report.pdf`, which the result must leave out | Mutant "filter ignores ObjectId": old passes, new fails |
| W17 | confirmed | Shared helper: asserts the inserted graph, then that `Modify` keeps only the Desk with no notes | Mutants "no prepper" and "nested prepper only": old passes, new fails |
| W18 | partly | Control pinned to `loaded == 2` | Holds: `> 0` passes when the trigger never fires (mutant). Does not hold: the old test already fails on the named regression (no token refresh) |
| W19 | rejected | none | The search assertion before it needs the normalized content. Mutant "interceptor never normalizes" fails the unchanged test |
| W20 | partly | Both `NormalizingPrimer` classes return `HandleNormalize`'s task | Holds: the task was discarded. Does not hold as "cannot fail": with an awaiting normalizer (mutant), the old primers make 5 tests fail falsely; the new ones pass |
| W21 | confirmed | `A_Registered_Processor_Fills_The_Listed_Items` registers the processor through `For<Category>` and reads through `IEntityService<Category>.List` | Mutant "List runs no processor": old passes (it called the processor itself), new fails |
| W22 | partly | The 28 checks resolve `IEntityProcessor<Course, CourseIncludes>` | Holds: the old type is never registered. Mutant "stray processor registered": old 28 pass, new fail. Open: the typed `SortBy` is never asserted, a missing case (M9) |
| W23 | confirmed | Each stack adds its own renamed-property rule, asserted in both registration orders | Mutants "last wins" and "first wins": the new test fails in both orders; the old one caught only one order |
| W24 | confirmed | Seeded Charlie, Bravo, Alpha so that id order disagrees with title order, asserted in `OneTimeSetUp` | Mutant "default sort dropped": old 30/30 pass on SQLite, PostgreSQL and SQL Server; new 6 fail on each |
| W25 | confirmed | Requests alpha, bravo (archived) and charlie; expects alpha and charlie | Mutant "by-ids reads archived": old passes, new fails on all three providers |
| W26 | partly | Calls `UseDefaults()` then `SetPageSize()` | Holds: it never called `UseDefaults`. Does not hold: the old simulation is `UseDefaults`' mechanism, and no `UseDefaults` mutant got past the old test |
| W27 | confirmed | `MakeFreeLicenseKey(simple, complex)`. Two new tests use keys limited to 3 and to 1 | Mutant "a signed key's limits ignored": old 23/23 pass, new 2 fail |
| W28 | confirmed | Test-only route. The offline test is removed; `Save_Stores_Fields_Under_The_Serializers_Names` (MongoDb category) reads the stored document back. No `src/` change | The cited `:115` is `:37`. Mutant "capitalised field names": old passes, new fails (local MongoDB) |
| W29 | confirmed | `PrimerInterceptors` left out of the wiring and Course given a primer. Asserts the single entry: a Warning naming ContosoContext, or Information in the container case | Premise false twice: `UseDefaults` wires the interceptor, and no default primer targets Course. Mutants "never reported" and "container case as Warning": old pass, new fail |
| W30 | confirmed | `StudentCourseIds = [course3.Id, course4.Id]`, asserting the exact names | Id 1 holds Student1 and Student3; id 3 is "Course #2". Mutant in the test API's filter (first id only): old passes, new fails |
| W31 | confirmed | `ObjectId` and the file name per item, plus an upload on another course that the list must leave out | Mutant "list ignores objectId": old passes, new fails (controller and endpoints) |
| W32 | confirmed | Links looked up by file name: SortOrder 1 for the existing link, 0 for the new one. Include order no longer asserted (the guides leave it to the app) | Mutant "SortOrder not stamped": old passes, new fails. Including by SortOrder, as the guide shows, fails the old test |
| W33 | confirmed | Asserts on the alternatives' text. New skipped test over paths A, B and C (S5) | Mutant "lists only up to two type arguments": old passes, new fails |
| W34 | confirmed | `Within(1e-9m)`, `Within(1).Milliseconds` and the row count first | Mutants "a day late plus an extra row" and "numbers read as 0": old pass, new fail on all three round trips |
| W35 | confirmed | `ImageReadBack` uses the SkiaSharp codec as the oracle. A generated 64×64 PNG has four known quadrants. Per file: signature and size. Per backend: crop pixels, `MakeTransparent` (ignored on Skia, S3) and `MakeOpaque` (ignored on GDI, S2) | Four mutants: old 48/48 pass, each fails a new test |
| W36 | confirmed | `Can_Read_V4_0` reads the 4.0 sample. TEL types per version. The 4.0 sample's ADR LABEL is fixed (it was invalid and read back with no addresses) | Not a library defect. Mutant "tel: scheme dropped": old passes, new fails |
| W37 | confirmed | The Read tests pass the language. PaddleOCR `Read_Dutch` passes `nl` and is ignored on a model gap | Mutant "Paddle ignores lang": old passes; the new test passes on it and fails on the real source (LatinV5: "wilik") |
| W38 | confirmed | MsgReader subject, attachment count and bytes (`Is.EqualTo`); recipient counts before the loops | Four mutants: old pass, new fail |
| W39 | confirmed | Excel duplicate headers (100 rows, `Title_2`); ZXing without a format; ImageBuilder blue layer at (365, 45) and the label's dark pixels | Mutants per part: old pass, new fail |
| W40 | partly | Billit's tautological assertion deleted; Spire's kept | Billit: the "mutant" does not compile. Spire: a mutant compiles and fails the unchanged test |
| W41 | partly | `MaxPdfPages` (Spire 3), `PdfPages(n)`, exact page counts and CHARLIE on page 4 | Mutant "merge ends with a blank page": old 3/3 pass, new fail. `Page_Settings` rejected: one section, so one page size |
| W42 | confirmed | `Odt_Template_Is_Read` deleted | Mutant "GetText keeps only the banner line": it passes; the base test fails |
| W43 | confirmed | Assets read before the call; `ThrowsAsync<Exception>` with "limited to 10 pages" | A missing asset, a `NotSupportedException` and a merge mutant passed the old tests; each fails the new ones |
| W44 | confirmed | The licence test also checks the PDF banner. New control: both banners are present without a key | Mutant "banner line dropped": old passes (ignored), the new control fails. The licensed path is not runnable here (no key) |
| W45 | partly | `The_File_Is_A_Pdf` deleted, its ContentType check moved into `Create`. Blank page: no words or images. Renamed RemoveEmptyPages test with its premise. Builder asserts ALPHA…CHARLIE in order | (b) and (d) hold (mutants pass old, fail new). (a) and (c) were already guarded by sibling tests |
| W46 | confirmed | `ToImages` expects 5 images (four page breaks) | Mutant "loader drops the last page break": old 4 == 4, new fails |
| W47 | confirmed | S1 IP cases restored and ignored; the phone number kept whole; new `ExtractPhoneNumbers_Takes_The_Whole_Number` ignored on S7; the truncated normalizer expectation dropped | A sketched library fix fails the old tests and passes the new ones unignored |
| W48 | confirmed | Shared `Test_Filter_Non_Recursive` (File system, Zip ignored on S6); zip `Filter_By_EntryType` restored and ignored on S6; `List_Nested_Folder` sets `Recursive` | A sketched zip fix fails the old `List_Nested_Folder` (it pinned S6) |
| W49 | confirmed | `../../../Assets`; `Path.DirectorySeparatorChar` in TreeList; `Path.GetRelativePath` in ZipUtility | Linux before: 20 DictionaryUtility tests and `Ancestors` fail; the zip test checks a "/"-prefixed id. After: all pass on Linux and Windows |
| W50 | confirmed | `FsFileProcessorTests` rooted where the files are, with the expected paths given | The old root never existed. Mutants "never descends" and "ignores Recursive": old passes, new fails |
| W51 | confirmed | `Is.Empty` / `Is.SupersetOf`; hierarchy sort checked for count, once each and depth-first order (hash sets, ~1 s) | Mutants "unknown language filters nothing" and "drops nodes below level 2": old pass, new fail |
| W52 | confirmed | Both slash tests stream `dir1\file1.1.txt` and compare bytes | Neither spelling was exercised. Mutants: old pass, new fail |
| W53 | confirmed | The two always-true "not equal" lines deleted; Cookbook test renamed | Mutant "unreversed copy" passes both lines |
| W54 | confirmed | Root Id 2 compared with `obj.Parent.Parent`; line 2 gets its own nested value | Two mutants: old pass, new fail |
| W55 | confirmed | JSON key "In" renamed "Inches"; the recomputation removed (the literals are the reference) | Checked by hand (210 / 25.4 = 8.267716). Mutant "2.5 cm per inch": old 27/27 pass, new 16 fail |
| W56 | partly | `Can_Be_Disabled`, the TearDown and `[NonParallelizable]` removed; `Defaults_To_Utc` kept | Found: `Defaults_To_Utc` passed with the default flipped, because the sibling's TearDown reset it. Setting the policy is covered by 6 Entities UTC tests |
| W57 | confirmed | `AsUtc` assumes a non-zero offset (inconclusive on UTC); `[SetUICulture("en-US")]` on `CultureUtilityTests` | Linux UTC: the relabel-only mutant passes the old test. Under `LANG=nl_BE.UTF-8`, 3 culture cases fail. After: the mutant fails on Windows; Linux nl_BE passes with `AsUtc` inconclusive |

### Ignored on a library defect

| Test | Defect |
|---|---|
| `Web.Security.Testing` `RefreshTokenTests.Test_Losing_A_Refresh_Race_Ends_The_Chain` | **S9 (new)**, below. Fixed after phase 1; the test runs |
| `Entities.Web.Testing` `GetRequiredEntityServiceTests.Messages_Name_Generic_Types_As_Written` | S5. Fixed after phase 1: `EntityServiceDiagnostics` names types through `EntityRepositoryRegistry.FriendlyName`; the test runs and asserts the exact names |
| `Drawing.Testing` `GdiImageTests.MakeOpaque_Leaves_No_Alpha` | S2: GDI leaves alpha 128 on the half-transparent quadrant. Fixed after phase 1; the test runs. The shared test also checks that a half-transparent pixel keeps its colour (within one level), on both backends |
| `Drawing.Testing` `SkiaImageTests.MakeTransparent_Clears_The_Key_Colour_Only` | S3: Skia leaves the key colour opaque and clears the lighter grey |
| `Common.Testing` `RegexTests` IP cases `192.168.0.1.1` and `300.255.0.255` | S1. Fixed after phase 1. The cases run, plus every IPv6 notation (an embedded IPv4 part, a zone ID), brackets and trailing newlines. A review found the anchored IPv6 pattern refused valid notations the borrowed regex never covered, so `IsValidIPv6Address` reads the input with `IPAddress.TryParse` instead; the shared exact match ends in `\z` |
| `Common.Testing` `RegexTests.ExtractPhoneNumbers_Takes_The_Whole_Number` (3 cases) | S7 |
| `IO.Testing` `ZipStorageTests.Filter_By_EntryType` and `Filter_Non_Recursive` | S6 |
| `Office.OCR.Testing` `PaddleOCRTests.Read_Dutch` | Library gap: for `nl`, `Read` picks LatinV5, which reads "wil ik" as "wilik"; EnglishV5 reads the poem exactly |

S4 (no ignored test) was also fixed after phase 1. Skia `MakeOpaque` maps `SKBitmap.Pixels` in one pass into an explicit `Bgra8888` bitmap, which it disposes. `SkiaImageTests.Test_MakeOpaque` on `img-1..4.jpg` dropped from 8–9 s to 0.53–0.60 s each, and on `img-5.png` from 1 s to 49 ms, PNG encoding and file write included. Drawing.Testing as a whole went from 19 s to 7 s of test time. The rule is unchanged: the shared `MakeOpaque_Leaves_No_Alpha` passes on the old and the new code.

### Found in phase 1

- **S9. A refresh-token race leaves the winner's chain alive.** When two callers refresh the same token, the loser revokes the family before the winner stores its successor. The successor survives the revocation. A probe of two truly concurrent refreshes kept the winner's chain alive in 472 of 500 rounds. **Fixed after phase 1, uncommitted:**
  - `RefreshTokenService` stores the successor before it claims the rotation with `TryRevoke`, so the loser's `RevokeFamily` reaches it. The store contract is unchanged; the guides say a custom `Store` must have written the record when it returns.
  - With `RevokeFamilyOnReuse` off, the loser revokes the successor it stored.
  - The race test runs again, through an order-agnostic double that holds the winner just after its claim until the family is revoked. It fails on the old service. A second test pins that only the winner's token stays live without family revocation; it fails if the loser's successor is left live.
  - The CHANGELOG says 6.6.1. `Regira.Security.Authentication`'s `<Version>` is still the published 6.6.0.
- **S10 (found by the library review, open):** `IsValidPhoneNumber` accepts a newline or tab between digit groups (`"+32\n3 384 30 44"`), because `PhoneNumberPattern`'s separators use `\s`. Present in 6.6.0. A space instead of `\s` fixes it, and also changes what `ExtractPhoneNumbers` returns.
- `SymmetricEncrypter.Encrypt/Decrypt(text, secret)` derive the key into the instance's default key arrays. A call with an explicit secret changes the key that later calls without one use. The class is obsolete, so this is not tested here.
- PaddleOCR's `OcrResult.Language` is the detection model's type name for every language.
- Syncfusion PDF writes its own trial banner, separate from the Word one. W44's control now covers both.
- `GdiDrawPositionTests.Build_Images` and `GdiImageBuilderTests.Build_Images` (U1's duplicate pair) write the same output file in parallel. They collided once with an `IOException`.
- The vCard 4.0 sample was invalid (ADR LABEL broken over unfolded lines). It is fixed in the test sample, so no library defect.
- The culture search matches display and native names, which follow the server's UI culture. An app's `Q` search therefore depends on that culture. This is a docs line, not a test fix.
- `UseDefaults` wires the primer interceptor (W29's premise), and the Web.Security test factory only pins its environment from `ConfigureWebHost` (W8).

### Left open

- W22: the typed `SortBy((q, sortBy) => …)` is never asserted. This is a missing case for M9.
- W41: `Page_Settings_Set_The_Rendered_Page_Size` rejected (one section, so one page size).
- W44: the licensed Syncfusion path was not run (no licence key on this machine).
- W48 and W50: the new scenarios are not wired into the Azure and GitHub fixtures, which are in the Network category and can't run here.
- **After phase 1:** the SSH fixtures (U4, C9) run against an SFTP server in Docker, category `Containers`.
  - `SftpServer`: `atmoz/sftp` pinned by digest, one per fixture.
  - The host key the server presents is checked against its own key files read inside the container, then pinned for every test. A new test shows a wrong fingerprint is refused, and it fails with the fingerprint check removed.
  - Each test seeds its files. W48's non-recursive scenario and W50's exact paths are wired in.
  - The stale `[Ignore]` and the SSH user secrets are gone.
- W5: the 2.5 s wait stays until P8.
- S8 was not checked; no W fix needed it.
<!-- {% endraw %} -->
