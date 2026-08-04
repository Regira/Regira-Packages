using Regira.IO.Abstractions;
using Regira.IO.Models;

namespace Regira.IO.Extensions;

public static class BinaryFileExtensions
{
    /// <summary>
    /// Converts a byte array to an <see cref="IBinaryFile"/> instance.
    /// </summary>
    /// <param name="bytes">The byte array to convert. Can be <c>null</c>.</param>
    /// <param name="contentType">
    /// The content type of the binary file, such as "application/pdf" or "image/png". 
    /// This parameter is optional and can be <c>null</c>.
    /// </param>
    /// <returns>
    /// An instance of <see cref="IBinaryFile"/> representing the provided byte array.
    /// </returns>
    public static IBinaryFile ToBinaryFile(this byte[]? bytes, string? contentType = null)
        => new BinaryFileItem { Bytes = bytes, Length = bytes?.Length ?? 0, ContentType = contentType };
    /// <summary>
    /// Converts the specified <see cref="Stream"/> to an <see cref="IBinaryFile"/> instance.
    /// </summary>
    /// <param name="stream">The input stream to be converted. Can be <c>null</c>.</param>
    /// <param name="contentType">The content type of the binary file. Optional and can be <c>null</c>.</param>
    /// <returns>An <see cref="IBinaryFile"/> instance representing the provided stream.</returns>
    public static IBinaryFile ToBinaryFile(this Stream? stream, string? contentType = null)
        => new BinaryFileItem { Stream = stream, Length = stream?.Length ?? 0, ContentType = contentType };
    /// <summary>
    /// Retains the source file's properties (no stream copy is created)
    /// </summary>
    /// <param name="file"></param>
    /// <param name="filename"></param>
    /// <returns></returns>
    public static IBinaryFile ToBinaryFile(this IMemoryFile file, string? filename = null)
    {
        if (file is IBinaryFile binaryFile)
        {
            binaryFile.FileName = filename ?? binaryFile.FileName;
            return binaryFile;
        }

        var item = new BinaryFileItem
        {
            Bytes = file.Bytes,
            Stream = file.Stream,
            Length = file.Length,
            ContentType = file.ContentType,
            FileName = filename
        };

        if (string.IsNullOrWhiteSpace(item.FileName) && file is INamedFile namedFile)
        {
            item.FileName = filename ?? namedFile.FileName;
        }

        return item;
    }

    /// <param name="file">The <see cref="IBinaryFile"/> instance to check.</param>
    extension(IBinaryFile file)
    {
        /// <summary>
        /// Determines whether the specified <see cref="IBinaryFile"/> has a valid, non-empty path.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the <see cref="IBinaryFile"/> has a non-null, non-whitespace path; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method checks the <see cref="IStorageFile.Path"/> property to determine if it contains a valid path.
        /// </remarks>
        public bool HasPath()
            => !string.IsNullOrWhiteSpace(file.Path);

        /// <summary>
        /// Retrieves the byte array representation of the specified <see cref="IBinaryFile"/>.
        /// </summary>
        /// <returns>
        /// A byte array containing the file's data if available; otherwise, <c>null</c>.
        /// </returns>
        /// <remarks>
        /// If the <see cref="IBinaryFile"/> has an associated path, the method attempts to read the file's bytes from the path.
        /// Otherwise, it delegates the retrieval to <see cref="MemoryFileExtensions.GetBytes(IMemoryFile)"/>.
        /// </remarks>
        public byte[]? GetBytes()
            => MemoryFileExtensions.GetBytes(file) ?? (file.HasPath() ? File.ReadAllBytes(file.Path!) : null);

        /// <summary>
        /// Retrieves a <see cref="Stream"/> representation of the specified <see cref="IBinaryFile"/>.
        /// <br />It will always create a new Stream.
        /// </summary>
        /// <returns>
        /// A <see cref="Stream"/> containing the data of the binary file, or <c>null</c> if the file has no associated stream or path.
        /// </returns>
        /// <remarks>
        /// If the binary file has an associated stream, it will be returned. Otherwise, if the file has a valid path, 
        /// the method will attempt to open and return a stream from the file at the specified path.
        /// </remarks>
        public Stream? GetStream()
            => MemoryFileExtensions.GetStream(file) ?? (file.HasPath() ? File.OpenRead(file.Path!) : null);

        /// <summary>
        /// Gets the full filename if present, otherwise a temporary file is written to disk and its path is returned
        /// </summary>
        /// <returns></returns>
        public string GetPath()
        {
            if (HasPath(file))
            {
                return file.Path!;
            }

            var path = Path.GetTempFileName();
            File.WriteAllBytes(path, GetBytes(file) ?? []);
            return path;
        }

        /// <summary>
        /// Retrieves the length of the specified <see cref="IBinaryFile"/>.
        /// </summary>
        /// <returns>
        /// The length of the binary file. If the file has a memory-based representation, 
        /// its length is returned. Otherwise, the length is determined from the file's path if available.
        /// </returns>
        /// <remarks>
        /// This method first attempts to retrieve the length from the memory representation of the file.
        /// If the memory representation is unavailable or has a length of 0, it checks the file's path
        /// and retrieves the length from the file system.
        /// </remarks>
        public long GetLength()
        {
            var length = MemoryFileExtensions.GetLength(file);
            if (length == 0)
                length = file.HasPath()
                    ? new FileInfo(file.Path!).Length
                    : 0;
            return length;
        }
    }
}