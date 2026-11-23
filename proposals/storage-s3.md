# An S3-compatible storage backend

As of 2026-11-23. Sources: the `Regira-Packages` repository, branch `wip` at `60b3414`; the nuget.org nuspecs of AWSSDK.S3 4.1.2, AWSSDK.Core 4.1.1.2, Minio 7.0.0, Testcontainers 4.17.0, Testcontainers.Minio 4.15.0 and Testcontainers.LocalStack 4.17.0, read on 2026-11-23; and `ClientConfig.cs` and `AmazonS3Config.cs` on the `main` branch of aws-sdk-net, read the same day for option names only. How S3 and the S3-compatible servers behave is taken from their documentation as remembered and was not measured here. Nothing on this page has been built.

**Status: proposal, not scheduled. Seven decisions are open (see *Decisions*).**

## Recommendation

Add `Regira.IO.Storage.S3`, an `IFileService` backend on Amazon's own SDK (`AWSSDK.S3`, Apache-2.0). One package serves AWS S3 and every store that speaks its protocol: MinIO, Cloudflare R2, Backblaze B2, DigitalOcean Spaces, Wasabi, Scaleway, Ceph, Garage and SeaweedFS. The difference between them is two options, `ServiceUrl` and `ForcePathStyle`.

The backend copies the shape of `Regira.IO.Storage.Azure`: an options class, a communicator that builds the client once and checks the bucket on first use, and a service. It keeps the two rules that backend already follows, containment and typing a file by its name. It adds one member the Azure backend has no counterpart for: a presigned URL, as a method on the S3 service (decision 5).

The gap this closes: `IO.Storage` has backends for the local disk, a Windows share, Azure Blob, SFTP, GitHub and a raw TCP port. An application hosted on AWS, on another cloud's object store or on a self-hosted one has none. Entity attachments are stored through `IFileService` (`AttachmentFileService` in `Entities.EFcore`), so that application also has no attachment store until it writes an `IFileService` itself.

The steps, in order:

1. A spike against one S3-compatible server and AWS S3: checksums, dot segments in a key, the stream a download returns.
2. The package, options, communicator, and the read path.
3. The write path: `Save`, `Move`, `Delete`.
4. Listing.
5. Presigned URLs.
6. Tests in `IO.Testing`, against a server in Docker.
7. Guides, docs, routing table and the changelog.

## What exists today

| What | Where | Effect |
|---|---|---|
| The contract a backend implements | `IFileService` in `src/Common.IO.Storage/Abstractions`: `Root`, `Exists`, `GetBytes`, `GetStream`, `GetInfo`, `List`, `ListAsync` (`net10.0` and later), `Move`, two `Save` overloads, `Delete`, `GetAbsoluteUri`, `GetIdentifier`, `GetRelativeFolder` | The S3 service implements these and nothing in the hub changes |
| `GetInfo` has a default implementation | It opens `GetStream` and measures it, reading the content through when the stream cannot seek | A backend that can ask the server for size and time overrides it, as the Azure backend does |
| No member takes a `CancellationToken` | The whole interface | The S3 calls run without one, as the Azure calls do (see *Considered and left out*) |
| A URL-addressed backend to copy | `AzureOptions`, `AzureCommunicator` and `BinaryBlobService` in `src/IO.Storage.Azure`, 302 lines in all | The same three types, the same lazy `Open()` behind a semaphore, the same registration |
| Shared path helpers | `UrlPath` in `src/Common.IO.Storage/Utilities`: `GetUri`, `GetRelativeUri`, `GetRelativeFolder`, `Combine`, `EnsureContained` | The S3 backend calls them directly; no copy of `FileNameUtility` is needed |
| Containment is a rule of the family | The local, SFTP, blob and GitHub backends throw `UnauthorizedAccessException` for an identifier that escapes `Root`. `ai/learnings.md` records why a URL-addressed store checks on the client | The S3 backend refuses the same identifiers with the same exception |
| A file is typed by its name | `BinaryBlobService.Save` uses the caller's `contentType`, or else `ContentTypeUtility.GetContentType(identifier)`. `ai/learnings.md` records the stored-XSS case behind it | `Save` sets the object's `Content-Type` by the same rule |
| Azure lists by reading every blob under a prefix | `ListBlobs` pages through the prefix; `Recursive`, `Extensions` and `Type` are applied in memory; folders are derived from blob names | S3 can do the non-recursive case on the server (see *Listing*) |
| The Azure fixtures need an account | `tests/IO.Testing/Azure`, category `Network` | They do not run offline |
| The SFTP fixtures run against Docker | `tests/IO.Testing/SSH/SftpServer.cs`: an image pinned by digest through the generic `Testcontainers` package (4.16.0), category `Containers` | The S3 fixtures take this shape and run wherever Docker does |
| The shared storage scenarios | `StorageTestHelper` in `tests/IO.Testing/Helpers` builds a context from any `IFileService` | One more caller |

## Design

### Package and registration

- `src/IO.Storage.S3/`, package and assembly `Regira.IO.Storage.S3`, namespace `Regira.IO.Storage.S3`, Apache-2.0. Target frameworks as the rest of the family.
- Three public types: `S3Options`, `S3Communicator`, `S3FileService`.
- No DI extension. The storage backends are registered by hand, options first, and the guide shows the lines.

<!-- no-compile -->
```csharp
services.AddSingleton(new S3Options
{
    BucketName = "uploads",
    Region     = "eu-west-1"            // AWS: credentials come from the SDK's default chain
});
services.AddSingleton<S3Communicator>();
services.AddSingleton<IFileService, S3FileService>();
```

<!-- no-compile -->
```csharp
// an S3-compatible server
new S3Options
{
    BucketName     = "uploads",
    ServiceUrl     = "https://minio.internal:9000",
    ForcePathStyle = true,
    AccessKey      = configuration["Storage:AccessKey"],
    SecretKey      = configuration["Storage:SecretKey"]
};
```

### Options

| Option | Default | Meaning |
|---|---|---|
| `BucketName` | *(required)* | The bucket |
| `Region` | `null` | The AWS region. An S3-compatible server takes whatever it documents, often `us-east-1` or `auto` |
| `ServiceUrl` | `null` | `null` addresses AWS. Set, it addresses an S3-compatible server |
| `ForcePathStyle` | `false` | Addresses the bucket as a path segment rather than a host name. Most self-hosted servers need it |
| `AccessKey`, `SecretKey` | `null` | Static credentials. Both `null` leaves it to the SDK's default chain: environment, shared profile, container or instance role |
| `Prefix` | `null` | A key prefix that becomes part of `Root`, so several applications or tenants share one bucket |
| `CreateBucketIfNotExists` | `false` | See decision 2 |
| `Contained` | `true` | Refuses an identifier that leaves `Root`, as `AzureOptions.Contained` does |

`S3Communicator` takes the options and, optionally, an `IAmazonS3`. An application that already builds its client, for example through `AWSSDK.Extensions.NETCore.Setup`, passes it in, and a test passes a double. Otherwise the communicator builds one `AmazonS3Client` and keeps it: the client is safe to share, and holds the connection pool.

### Root, identifiers and containment

- `Root` is the https address of the bucket, plus `Prefix`. It is built from the options without a request, as the Azure backend reads `Container.Uri`: `https://{bucket}.s3.{region}.amazonaws.com/` on AWS, `{ServiceUrl}/{bucket}/` with `ForcePathStyle`.
- `GetAbsoluteUri`, `GetIdentifier` and `GetRelativeFolder` are the `UrlPath` calls the Azure backend makes.
- An S3 key is an opaque string. S3 does not resolve `../` in it, so `tenant-a/../tenant-b/x` is a key of its own. Three things still read it as a path: `System.Uri` when `GetAbsoluteUri` builds the address, a CDN or proxy in front of the bucket, and whoever reads the identifier later. With `Prefix` in play, a key that leaves the prefix is another tenant's file.
- So the backend refuses what the other URL-addressed backends refuse, before any request is made: an identifier whose address leaves `Root` once dot segments are resolved (`%2e` counts as a dot), and one holding a null character. It throws `UnauthorizedAccessException`.

### Read

| Member | S3 call | Notes |
|---|---|---|
| `Exists` | `GetObjectMetadata` | `false` on 404 |
| `GetInfo` | `GetObjectMetadata` | `ContentLength` and `LastModified` in UTC; `null` on 404, and for a zero-byte key ending in `/`, which a console writes as a folder marker |
| `GetBytes` | `GetObject` | `null` when the key does not exist |
| `GetStream` | `GetObject` | See decision 4 |

### Write

| Member | S3 call | Notes |
|---|---|---|
| `Save(bytes)` | `PutObject` | `Content-Type` by the family's rule |
| `Save(stream)` | `TransferUtility.UploadAsync` | A seekable stream is rewound first, as the Azure backend does. The transfer utility switches to a multipart upload for a large stream, so a file over the 5 GB limit of a single `PutObject` still uploads |
| `Move` | `CopyObject`, then `DeleteObject` | A server-side copy; the source is deleted only once the copy has answered. `CopyObject` takes at most 5 GB: a larger object throws `NotSupportedException` that names the limit |
| `Delete` | `DeleteObject` | S3 answers success for a key that does not exist, so `Delete` stays idempotent |

### Listing

- **Not recursive.** `ListObjectsV2` with the folder as `Prefix` and `/` as `Delimiter`. The server returns that level's keys and, as common prefixes, its folders. Nothing below the level is read.
- **Recursive.** `ListObjectsV2` with the prefix and no delimiter, paged to the end. Folders are derived from the keys, as the Azure backend derives them.
- `Extensions` is applied in memory; S3 has no suffix filter. `Type` selects files, folders or both.
- The result has the Azure backend's shape: identifiers relative to `Root`, distinct, sorted. The shared scenarios pin it.
- `ListAsync` yields page by page as the server answers. The Azure backend's `ListAsync` wraps `List`.

### Presigned URLs

`S3FileService` gets one member beyond the interface:

<!-- no-compile -->
```csharp
string GetPresignedUrl(string identifier, TimeSpan expiresIn);
```

It signs a `GET` for the object and makes no request. Two limits go in the guide: Signature Version 4 caps the lifetime at seven days, and a URL signed with temporary credentials (an instance or container role) stops working when those credentials expire, whatever lifetime was asked for.

A presigned URL hands out the object as stored, so the `Content-Type` set by `Save` is what the browser gets. That is the reason the typing rule matters here as it does behind a SAS link.

## Dependencies

- **AWSSDK.S3** 4.1.2, Apache-2.0. It depends on AWSSDK.Core `[4.1.1.1, 5.0.0)`, which ships `net8.0`, `net10.0` and `net11.0` assets beside `netstandard2.0` and `net472`, and depends on AWSSDK.Extensions.Numerics and System.IO.Hashing 11.0.0. Both of Regira's target-framework sets are covered: today's `net8.0;net10.0`, and `net10.0;net11.0` from `proposals/dotnet-11.md`.
- **Minio** 7.0.0, evaluated and not taken (decision 1). Apache-2.0; `net8.0` and `netstandard2.0`; it depends on System.Reactive 6.0.1, CommunityToolkit.HighPerformance and Microsoft.Extensions.Logging 9.0.4.
- **AWSSDK.Extensions.NETCore.Setup** 4.1.0.3 is not referenced. An application that uses it passes its `IAmazonS3` to the communicator.

## Considered and left out

- **`CancellationToken` on `IFileService`.** No member takes one, on any backend. Adding it is a change to the hub contract that every backend and every custom `IFileService` meets. It is its own proposal, and fits a major release.
- **A presigned upload**, where the browser sends the file straight to the bucket. It bypasses the upload path that types and validates a file, so it needs a design of its own.
- **Redirecting an attachment download to a presigned URL** in `Regira.Entities.Web`. The API would stop streaming the bytes itself. It depends on decision 5's abstraction, and on what an attachment's authorization means once the URL is out.
- **Versioning, object lock, lifecycle rules, storage classes, ACLs.** These are bucket policy, set where the bucket is managed.
- **Server-side encryption settings per object.** Bucket-default encryption covers the usual case. If an application needs SSE-KMS with a key per object, an `Action<PutObjectRequest>` on the options would carry it without the package modelling each setting. Not added until someone asks.
- **Google Cloud Storage.** Its XML API is S3-interoperable with HMAC keys, so `ServiceUrl` may be all it takes. That is not tested here, and the guide makes no claim about it.

## Decisions

1. **The client library: AWSSDK.S3 or Minio?**
   - AWSSDK.S3 is the reference client for the protocol, and brings AWS's credential chain: roles, profiles and web identity work with no code here.
   - Minio is smaller and built for the compatible servers, but an AWS deployment on a role would need its credential providers wired by hand, and it brings System.Reactive.

   Recommendation: AWSSDK.S3.
2. **`CreateBucketIfNotExists`: `false` or `true`?** `AzureOptions` defaults it to `true`. On AWS a bucket name is global, creating one needs a permission most application roles should not hold, and a bucket created by accident starts with default settings for region, encryption and public access. Recommendation: `false`, so a missing bucket fails on first use with a message that names it and the option. The guide says where the two backends differ.
3. **Checksums on S3-compatible servers.** Since its 2025 data-integrity change the AWS SDK adds a CRC checksum to uploads by default and validates one on downloads. Servers that do not implement it are reported to refuse such uploads. `ClientConfig` has `RequestChecksumCalculation` and `ResponseChecksumValidation` to send and check one only when the operation requires it.
   - Option 1: when `ServiceUrl` is set, the backend selects `WHEN_REQUIRED` for both, and an `S3Options` property turns the SDK default back on.
   - Option 2: always the SDK default, with the property as an opt-out the guide's troubleshooting row points to.

   Recommendation: option 1, if the spike confirms the refusal on at least one server. An S3-compatible backend that fails its first upload on most of the servers it names is the wrong default.
4. **What `GetStream` returns.**
   - Option 1: the response stream, wrapped so that disposing it disposes the response. It cannot seek, and it holds a connection until it is disposed. A large file is never in memory whole.
   - Option 2: the content copied into a `MemoryStream`, which is in effect what the Azure backend returns (`DownloadContentAsync`).

   Recommendation: option 1. The interface's own `GetInfo` default already reads a stream that cannot seek, and files too large for memory are the reason to use an object store. Step 2 checks the callers in this repository that assume `Length` or `Position`.
5. **Presigned URLs: on the S3 service, or in the hub?** A hub contract, for example a small interface next to `IFileService` with one method, would let the Azure backend answer with a SAS link and let `Regira.Entities.Web` redirect a download. That contract has two implementers in view and no consumer yet. Recommendation: the method on `S3FileService` now. Add the hub contract with its first consumer, and move the method under it then.
6. **The server the tests run against.** The fixtures need one S3-compatible server in Docker.
   - The generic `Testcontainers` builder with an image pinned by digest, as `SftpServer` does. `IO.Testing` references it already.
   - `Testcontainers.LocalStack` 4.17.0, which emulates AWS itself.
   - `Testcontainers.Minio`. Its latest release is 4.15.0 while Testcontainers is at 4.17.0, so whether the module is still maintained has to be checked first.

   Recommendation: the generic builder. Step 1 picks the image from what still publishes one and passes the spike. A second fixture in the `Network` category runs the same scenarios against AWS S3 from user secrets, since AWS is the reference for the protocol.
7. **The name.** `Regira.IO.Storage.S3` names the protocol, which is what the package serves. `Regira.IO.Storage.AWS` would misname it for most of the stores in the first paragraph. Recommendation: `S3`.

## Steps

1. **Spike.** Against the chosen Docker image and one AWS bucket:
   - an upload and a download with the SDK's default checksum settings and with `WHEN_REQUIRED` (decision 3);
   - what request the SDK sends for a key holding `../` and `%2e%2e`, with and without `ForcePathStyle`;
   - whether the `GetObject` response stream can be handed out and disposed late (decision 4);
   - the shape `ListObjectsV2` returns for a folder marker and for an empty prefix.
2. **The package and the read path.**
   - `src/IO.Storage.S3/` with its `.csproj`, `README.md`, and the solution entry in `Regira-Packages.slnx`.
   - `S3Options`, `S3Communicator` with its `Open()`, `Root`, the URI helpers and the containment check.
   - `Exists`, `GetInfo`, `GetBytes`, `GetStream`.
   - `tests/IO.Testing/S3/`: the server fixture, `S3ContainmentTests` (no request is made, so they run without Docker) and `S3InfoTests`.
3. **The write path.** `Save`, `Move`, `Delete`. `S3StorageTests` and `S3FileProcessorTests` run the shared scenarios through `StorageTestHelper`, as the four Azure fixtures do. Add a content-type case: a file saved with a declared `text/html` under a `.png` name is served as `image/png`.
4. **Listing.** `List` and `ListAsync`. Run the scenarios `ZipStorageTests` and the SFTP fixtures run for `Recursive` and `Type`, and one over more keys than a page holds.
5. **Presigned URLs.** `GetPresignedUrl`, with a test that fetches the URL with a plain `HttpClient` and reads the content and its `Content-Type`, and one that a URL for an identifier outside `Root` is refused.
6. **The AWS fixture.** The same scenarios in the `Network` category, from user secrets.
7. **Docs.**
   - `io.storage.instructions.md`: the *Installation* block, a `### S3-compatible object storage — S3FileService` section under *Implementations* with the options table, the two differences from the Azure backend (decision 2 and 4) and a row per server whose settings were measured; the *Path containment* note's list of backends; *DI Registration*; the *Backend Comparison* row.
   - `io.storage.examples.md`, `io.storage.card.md` and `io.storage.namespaces.md`.
   - `src/Common.IO.Storage/README.md` and the `docs/` pages that list the backends.
   - The `IO.Storage` row of the routing table in `ai/AGENTS.md`, and the storage backends the Entities guides name for attachments.
   - The `storage` group in `tools/GuideVerifier/projects.json`.
   - CHANGELOG bullets for `Regira.IO.Storage.S3` and for `Regira.IO.Storage`, whose packed guides change.
   - `ai/learnings.md` for what the spike teaches.

## Risks

- **"S3-compatible" is a range, not a standard.** Servers differ on checksums, on path-style addressing, on the region they accept and on listing details. The guide names only the servers the fixtures ran against, with the settings that worked, and says the others are expected to work, not tested.
- **The SDK's defaults move.** The checksum default arrived in a minor release. A later one can change what an S3-compatible server sees again. The Docker fixture catches it at the next dependency bump.
- **Dot segments in a key are not measured.** The design refuses them before a request, so the backend does not depend on what the SDK or a server does with them. The spike records what they do, for the guide.
- **A stream that holds a connection.** With decision 4's option 1, a caller that does not dispose the stream holds a pooled connection. The guide says so beside `GetStream`: a leak shows up as requests that stall once the pool is used up, not as an error.
- **A bucket is public or private by policy, not by this package.** `GetAbsoluteUri` returns the object's address whether or not anyone can read it. The guide says a private bucket is the default to keep, and that a presigned URL is how one file leaves it.
- **Docker in CI.** The fixtures are in the `Containers` category with the SFTP ones, so they run where those run and are skipped where those are.
