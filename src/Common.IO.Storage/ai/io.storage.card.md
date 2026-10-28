# Regira.IO.Storage — index card

> One `IFileService` in front of every file store: the local file system, a network share and ZIP archives in this
> package; Azure Blob Storage, SFTP, GitHub and a raw TCP port in the provider packages. The guide is
> `get_package(id: "Regira.IO.Storage", section: "io.storage.instructions")`; exact usings in `io.storage.namespaces`.

- **Every call takes an identifier**, the path relative to the store's root (`invoices/2024/inv-001.pdf`), never the
  absolute path: the identifier survives a swap of backend. `Save` returns the identifier it stored, which may differ
  from the one passed in; `GetAbsoluteUri` gives the full address.
- **Local disk:** `new BinaryFileService(new FileSystemOptions { RootFolder = … })`, registered as `IFileService`.
  `Contained` (default `true`) refuses an identifier that leaves `RootFolder`; the blob, GitHub and SFTP backends
  refuse it too, with `UnauthorizedAccessException`.
- **Identifiers use `/` between folders on every platform**, from `Save` and from `List`; either separator is
  accepted back.
- **Remote stores:** `BinaryBlobService` (`Regira.IO.Storage.Azure`), `SftpService` (`Regira.IO.Storage.SSH`),
  `GitHubService` (`Regira.IO.Storage.GitHub`, a commit per write), `TCPService` (`Regira.IO.Storage.SimpleTCP`,
  write-only, for a label printer). `NetworkFileService` reaches a Windows share with credentials.
- **Listing:** `List(new FileSearchObject { FolderUri, Extensions, Recursive, Type = FileEntryTypes.Files })`
  answers identifiers; `Type` defaults to files and folders both.
- **Reading:** `GetBytes` or `GetStream`. Check `Exists` first when the file may be missing: the local disk answers
  `null` for it, and not every remote store does (SFTP throws). `GetInfo` answers the size and last-modified time
  (`StoredFileInfo`) without the content, `null` for a missing file.
- **ZIP:** `ZipFileService` browses an archive through `IFileService`, `ZipBuilder` creates one, `ZipManager` handles
  password-protected archives.
- **Two stores in one app:** a second `IFileService` registration replaces the first, so register the second store
  by its own type (`io.storage.instructions` → *DI Registration*).
- **Serving a stored file from a controller:** Regira.Web's `this.File(file, inline)` answers 404 for a missing
  file and sends `nosniff` and, for every file but a PDF, a `sandbox` content security policy; ASP.NET's own `File(…)`
  sends neither (`io.storage.instructions` → *Local File System*).
- **A file that belongs to an entity row** (a product's pictures, an order's documents) goes through Regira.Entities
  attachments, which keep the row and the file together:
  `get_package(id: "Regira.Entities", section: "entities.instructions", heading: "Attachments")`.
