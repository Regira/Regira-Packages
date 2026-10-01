using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;

namespace Regira.IO.Compression.SharpZipLib;

public class ZipManager
{
    /// <summary>
    /// The most bytes <see cref="Unzip"/> extracts from one archive, all entries together. An archive that unpacks to
    /// more throws <see cref="InvalidDataException"/>, counted while reading, so an entry's declared size cannot hide
    /// it. Left <c>null</c>, there is no limit: set one for an archive from an untrusted source, such as an upload.
    /// </summary>
    public long? MaxUnzippedSize { get; init; }

    /// <summary>
    /// Writes the files to a new ZIP archive, each under its <see cref="INamedFile.FileName"/> with <c>/</c> as the
    /// separator and no leading separator or drive. With a password, every entry is encrypted with AES-256.
    /// </summary>
    /// <returns>The archive, rewound and ready to read</returns>
    /// <exception cref="UnauthorizedAccessException">A file name has a <c>..</c> segment, which <see cref="Unzip"/> would refuse</exception>
    public Stream Zip(IEnumerable<IBinaryFile> files, string? password = null)
    {
        var ms = new MemoryStream();
        // Finish() writes the central directory, without which no reader opens the archive;
        // disposing the stream calls it, and IsStreamOwner = false keeps the archive's own stream open
        using (var zs = new ZipOutputStream(ms) { IsStreamOwner = false })
        {
            var encrypt = !string.IsNullOrEmpty(password);
            zs.Password = encrypt ? password : null;

            foreach (var file in files)
            {
                var name = file.FileName ?? throw new ArgumentException("Every file needs a FileName to name its entry.", nameof(files));
                // CleanName drops a leading separator or drive but keeps a '..', which Unzip refuses: refuse it here too
                var entry = new ZipEntry(GetContainedName(ZipEntry.CleanName(name)));
                if (encrypt)
                {
                    entry.AESKeySize = 256;
                }
                zs.PutNextEntry(entry);

                using var fileStream = file.GetStream();
                if (fileStream != null)
                {
                    StreamUtils.Copy(fileStream, zs, new byte[4096]);
                }
                zs.CloseEntry();
            }
        }

        ms.Position = 0;
        return ms;
    }

    /// <summary>
    /// Reads every file entry of a ZIP archive, decrypting AES and ZipCrypto entries with the password.
    /// Entries are named with <c>/</c> as the separator.
    /// </summary>
    /// <exception cref="UnauthorizedAccessException">An entry's name climbs out of the archive (a <c>..</c> segment) or is rooted</exception>
    /// <exception cref="InvalidDataException">The archive unpacks to more than <see cref="MaxUnzippedSize"/></exception>
    public async Task<BinaryFileCollection> Unzip(Stream zipStream, string? password = null)
    {
        // ZipFile reads the central directory at the end of the archive, so it needs a seekable stream;
        // unlike ZipInputStream it also decrypts AES entries
        var source = zipStream;
        if (!zipStream.CanSeek)
        {
            source = new MemoryStream();
            await zipStream.CopyToAsync(source);
            source.Position = 0;
        }

        var files = new List<BinaryFileItem>();
        long unzipped = 0;
        using (var zipFile = new ZipFile(source, leaveOpen: source == zipStream))
        {
            zipFile.Password = string.IsNullOrEmpty(password) ? null : password;

            foreach (ZipEntry entry in zipFile)
            {
                if (!entry.IsFile)
                {
                    continue;
                }
                // checked before anything of the entry is read
                var name = GetContainedName(entry.Name);

                // copied to the end: a single Read may return fewer bytes than the entry holds
                await using var entryStream = zipFile.GetInputStream(entry);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = await entryStream.ReadAsync(chunk)) > 0)
                {
                    unzipped += read;
                    if (unzipped > MaxUnzippedSize)
                    {
                        throw new InvalidDataException($"The archive unpacks to more than {MaxUnzippedSize} bytes.");
                    }
                    buffer.Write(chunk, 0, read);
                }
                files.Add(new BinaryFileItem
                {
                    FileName = name,
                    Bytes = buffer.ToArray()
                });
            }
        }

        return new BinaryFileCollection(files);
    }

    /// <summary>
    /// An entry name with <c>/</c> separators, refused when it would leave the folder it is extracted into, as
    /// extraction to disk refuses it: a <c>..</c> segment, a leading separator or a drive.
    /// </summary>
    private static string GetContainedName(string entryName)
    {
        var name = entryName.Replace('\\', '/');
        var escapes = name.StartsWith('/')
            || (name.Length > 1 && name[1] == ':')
            || name.Split('/').Contains("..");
        return escapes
            ? throw new UnauthorizedAccessException($"Zip entry '{entryName}' escapes the archive.")
            : name;
    }
}
