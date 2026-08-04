using System.Text;
using Regira.Utilities;

namespace Regira.IO.Utilities;

public static class FileUtility
{
    //https://stackoverflow.com/questions/3825390/effective-way-to-find-any-files-encoding#19283954
    /// <summary>
    /// Reads the first 4 bytes (bom) to analyze for the Encoding<br />
    /// Possible encodings:<br />
    /// <list type="bullet">
    ///     <item>UTF7</item>
    ///     <item>UTF8</item>
    ///     <item>Unicode</item>
    ///     <item>BigEndianUnicode</item>
    ///     <item>UTF32</item>
    ///     <item>ASCII</item>
    /// </list>
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns>Encoding (ASCII when not match is found)</returns>
    public static Encoding GetEncoding(this byte[] bytes)
    {
        if (bytes.Length >= 2)
        {
            // Read the BOM
            var bom = bytes.Take(4).ToArray();

            if (bytes.Length >= 3)
            {
                // Analyze the BOM
#pragma warning disable SYSLIB0001
                if (bom[0] == 0x2b && bom[1] == 0x2f && bom[2] == 0x76) return Encoding.UTF7;
#pragma warning restore SYSLIB0001
                if (bom[0] == 0xef && bom[1] == 0xbb && bom[2] == 0xbf) return Encoding.UTF8;
            }

            if (bom[0] == 0xff && bom[1] == 0xfe) return Encoding.Unicode; //UTF-16LE
            if (bom[0] == 0xfe && bom[1] == 0xff) return Encoding.BigEndianUnicode; //UTF-16BE


            if (bytes.Length >= 4)
            {
                if (bom[0] == 0 && bom[1] == 0 && bom[2] == 0xfe && bom[3] == 0xff) return Encoding.UTF32;
            }
        }

        return Encoding.Default;
    }

    /// <summary>
    /// Reads all bytes from the specified <see cref="Stream"/> and returns them as a byte array.
    /// </summary>
    /// <param name="stream">The input <see cref="Stream"/> to read bytes from. If <c>null</c>, the method returns <c>null</c>.</param>
    /// <returns>
    /// A byte array containing the data read from the <paramref name="stream"/>, or <c>null</c> if the <paramref name="stream"/> is <c>null</c>.
    /// </returns>
    /// <remarks>
    /// If the <paramref name="stream"/> supports seeking, its position is reset to the beginning before reading.
    /// If the <paramref name="stream"/> is a <see cref="MemoryStream"/>, its internal buffer is directly returned.
    /// Otherwise, the method copies the contents of the <paramref name="stream"/> into a new <see cref="MemoryStream"/> and returns the resulting byte array.
    /// </remarks>
    public static byte[]? GetBytes(this Stream? stream)
    {
        if (stream == null)
        {
            return null;
        }

        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        {
            if (stream is MemoryStream ms)
            {
                return ms.ToArray();
            }
        }

        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
    /// <summary>
    /// Converts the specified Base64-encoded string into a byte array.
    /// </summary>
    /// <param name="base64String">The Base64-encoded string to convert. This string may contain whitespace or line breaks, which will be cleaned before conversion.</param>
    /// <returns>
    /// A byte array representing the decoded binary data from the <paramref name="base64String"/>.
    /// </returns>
    /// <exception cref="FormatException">
    /// Thrown if the <paramref name="base64String"/> is not a valid Base64-encoded string after cleaning.
    /// </exception>
    /// <remarks>
    /// The method first cleans the input string by removing any whitespace or line breaks, ensuring it is properly formatted for Base64 decoding.
    /// </remarks>
    public static byte[] GetBytes(string base64String)
    {
        var cleanBase64String = CleanBase64String(base64String);
        return Convert.FromBase64String(cleanBase64String);
    }
    /// <summary>
    /// Converts the specified string into a byte array using the provided encoding.
    /// </summary>
    /// <param name="contents">The string to convert to a byte array.</param>
    /// <param name="encoding">
    /// The encoding to use for the conversion. If <see langword="null"/>, the default encoding is used.
    /// </param>
    /// <returns>A byte array representing the encoded string.</returns>
    public static byte[] GetBytesFromString(this string contents, Encoding? encoding = null)
    {
        encoding ??= Encoding.Default;
        return encoding.GetBytes(contents);
    }

    /// <summary>
    /// Converts the specified byte array into a <see cref="Stream"/>.
    /// </summary>
    /// <param name="bytes">The byte array to convert into a <see cref="Stream"/>. If <c>null</c>, the method returns <c>null</c>.</param>
    /// <returns>
    /// A <see cref="Stream"/> containing the data from the <paramref name="bytes"/>, or <c>null</c> if the <paramref name="bytes"/> is <c>null</c>.
    /// </returns>
    /// <remarks>
    /// The returned <see cref="Stream"/> is a <see cref="MemoryStream"/> initialized with the contents of the <paramref name="bytes"/>.
    /// </remarks>
    public static Stream? GetStream(this byte[]? bytes)
    {
        if (bytes == null)
        {
            return null;
        }

        return new MemoryStream(bytes);
    }
    /// <summary>
    /// Converts the specified Base64-encoded string into a <see cref="Stream"/>.
    /// </summary>
    /// <param name="base64String">The Base64-encoded string to convert. This string may contain whitespace or line breaks, which will be cleaned before conversion.</param>
    /// <returns>
    /// A <see cref="Stream"/> containing the decoded binary data from the <paramref name="base64String"/>, 
    /// or <c>null</c> if the <paramref name="base64String"/> is <c>null</c> or empty.
    /// </returns>
    /// <exception cref="FormatException">
    /// Thrown if the <paramref name="base64String"/> is not a valid Base64-encoded string after cleaning.
    /// </exception>
    /// <remarks>
    /// This method first cleans the input string by removing any whitespace or line breaks, ensuring it is properly formatted for Base64 decoding.
    /// It then converts the cleaned string into a byte array and wraps it in a <see cref="MemoryStream"/>.
    /// </remarks>
    public static Stream? GetStream(string base64String)
        => GetStream(GetBytes(base64String));

    /// <summary>
    /// Converts the specified string to a <see cref="Stream"/> using the provided encoding.
    /// </summary>
    /// <param name="contents">The string to convert into a stream.</param>
    /// <param name="encoding">
    /// The character encoding to use. If <c>null</c>, the default encoding is used.
    /// </param>
    /// <returns>A <see cref="Stream"/> containing the encoded bytes of the input string.</returns>
    public static Stream GetStreamFromString(this string contents, Encoding? encoding = null)
    {
        encoding ??= Encoding.Default;
        return new MemoryStream(encoding.GetBytes(contents));
    }

    /// <summary>
    /// Reads the content of the specified <see cref="Stream"/> and returns it as a string.
    /// </summary>
    /// <param name="stream">The input stream to read from. If <c>null</c>, the method returns <c>null</c>.</param>
    /// <param name="encoding">
    /// The character encoding to use for reading the stream. If <c>null</c>, the default encoding is used.
    /// </param>
    /// <returns>
    /// A string containing the content of the stream, or <c>null</c> if the <paramref name="stream"/> is <c>null</c>.
    /// </returns>
    /// <remarks>
    /// If the stream supports seeking and its position is not at the beginning, the method resets the position to the start of the stream before reading.
    /// </remarks>
    public static string? GetString(this Stream? stream, Encoding? encoding = null)
    {
        if (stream == null)
        {
            return null;
        }

        if (stream.CanSeek && stream.Position != 0)
        {
            stream.Seek(0, SeekOrigin.Begin);
        }

        using var reader = encoding == null
            ? new StreamReader(stream)
            : new StreamReader(stream, encoding);
        return reader.ReadToEnd();
    }
    /// <summary>
    /// Converts the specified byte array to a string using the provided encoding.
    /// </summary>
    /// <param name="bytes">The byte array to convert. If <c>null</c>, the method returns <c>null</c>.</param>
    /// <param name="encoding">
    /// The <see cref="Encoding"/> to use for the conversion. If <c>null</c>, the encoding is determined
    /// by analyzing the byte array for a byte order mark (BOM). If no BOM is found, the default encoding is used.
    /// </param>
    /// <returns>
    /// A string representation of the byte array, or <c>null</c> if the <paramref name="bytes"/> parameter is <c>null</c>.
    /// </returns>
    public static string? GetString(this byte[]? bytes, Encoding? encoding = null)
    {
        if (bytes == null)
        {
            return null;
        }

        encoding ??= GetEncoding(bytes);
        return encoding.GetString(bytes);
    }

    /// <summary>
    /// Converts the specified byte array to a Base64-encoded string.
    /// </summary>
    /// <param name="bytes">The byte array to convert.</param>
    /// <param name="clean">
    /// A boolean value indicating whether to clean the resulting Base64 string.
    /// If <c>true</c>, the Base64 string will be cleaned; otherwise, it will remain unaltered.
    /// </param>
    /// <returns>
    /// A Base64-encoded string representation of the input byte array.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown if the <paramref name="bytes"/> parameter is <c>null</c>.
    /// </exception>
    public static string GetBase64String(this byte[] bytes, bool clean = true)
    {
        var base64 = Convert.ToBase64String(bytes);
        return clean ? CleanBase64String(base64) : base64;
    }
    /// <summary>
    /// Cleans a Base64-encoded string by removing unnecessary characters such as newlines and spaces.
    /// If the string contains a comma, it truncates the string to exclude everything before and including the first comma.
    /// </summary>
    /// <param name="base64String">The Base64-encoded string to clean.</param>
    /// <returns>A cleaned Base64-encoded string.</returns>
    public static string CleanBase64String(string base64String)
    {
        var sb = new StringBuilder(base64String, base64String.Length)
            .Replace(Environment.NewLine, string.Empty)
            .Replace(" ", string.Empty);
        var cleanedBase64 = sb.ToString();
        var firstChars = StringUtility.Truncate(cleanedBase64, 64);
        if (firstChars?.Contains(',') == true)
        {
            return cleanedBase64.Substring(cleanedBase64.IndexOf(',') + 1);
        }

        return cleanedBase64;
    }
}