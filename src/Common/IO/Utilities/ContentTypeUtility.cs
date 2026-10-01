using System.Collections.Concurrent;

namespace Regira.IO.Utilities;

/// <summary>
/// Provides utility methods for handling content types and file type detection.
/// </summary>
/// <remarks>
/// This class includes methods for identifying content types based on file extensions or byte sequences,
/// extending MIME type mappings, and retrieving file extensions from MIME types.
/// It is designed to assist in scenarios where content type determination is required, such as file uploads or processing.
/// </remarks>
public static class ContentTypeUtility
{
    // one instance each, so Extend changes what every lookup reads
    private static readonly ConcurrentDictionary<string, byte[]> ByteSequences = new(new Dictionary<string, byte[]>
    {
        // https://stackoverflow.com/questions/58510/using-net-how-can-you-find-the-mime-type-of-a-file-based-on-the-file-signature#answer-13614746
        { "avi", [82, 73, 70, 70] },
        { "bmp", [66, 77] },
        { "dll", [77, 90] },
        { "doc", [208, 207, 17, 224, 161, 177, 26, 225] },
        { "docx", [80, 75, 3, 4] },
        { "exe", [77, 90] },
        { "gif", [71, 73, 70, 56] },
        { "ico", [0, 0, 1, 0] },
        { "jpeg", [255, 216, 255] },
        { "jpg", [255, 216, 255] },
        { "mp3", [255, 251, 48] },
        { "ogg", [79, 103, 103, 83, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0] },
        { "pdf", [37, 80, 68, 70, 45, 49, 46] },
        { "png", [137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 13, 73, 72, 68, 82] },
        { "rar", [82, 97, 114, 33, 26, 7, 0] },
        { "swf", [70, 87, 83] },
        { "tiff", [73, 73, 42, 0] },
        { "ttf", [0, 1, 0, 0, 0] },
        { "wav", [82, 73, 70, 70] },
        { "wma", [48, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108] },
        { "wmv", [48, 38, 178, 117, 142, 102, 207, 17, 166, 217, 0, 170, 0, 98, 206, 108] },
        { "zip", [80, 75, 3, 4] }
    }, StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, string[]> MimeTypes = new(new Dictionary<string, string[]>
    {
        { "7z", ["application/x-7z-compressed"] },
        { "aac", ["audio/aac"] },
        { "ai", ["application/postscript"] },
        { "aif", ["audio/x-aiff"] },
        { "aifc", ["audio/x-aiff"] },
        { "aiff", ["audio/x-aiff"] },
        { "apng", ["image/apng"] },
        { "asc", ["text/plain"] },
        { "atom", ["application/atom+xml"] },
        { "au", ["audio/basic"] },
        { "avi", ["video/x-msvideo"] },
        { "avif", ["image/avif"] },
        { "bcpio", ["application/x-bcpio"] },
        { "bin", ["application/octet-stream"] },
        { "bmp", ["image/bmp"] },
        { "cdf", ["application/x-netcdf"] },
        { "cgm", ["image/cgm"] },
        { "class", ["application/octet-stream"] },
        { "cpio", ["application/x-cpio"] },
        { "cpt", ["application/mac-compactpro"] },
        { "csh", ["application/x-csh"] },
        { "css", ["text/css"] },
        { "csv", ["text/csv"] },
        { "dcr", ["application/x-director"] },
        { "dif", ["video/x-dv"] },
        { "dir", ["application/x-director"] },
        { "djv", ["image/vnd.djvu"] },
        { "djvu", ["image/vnd.djvu"] },
        { "dll", ["application/octet-stream"] },
        { "dmg", ["application/octet-stream"] },
        { "dms", ["application/octet-stream"] },
        { "doc", ["application/msword"] },
        { "docm", ["application/vnd.ms-word.document.macroEnabled.12"] },
        { "docx", ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] },
        { "dotm", ["application/vnd.ms-word.template.macroEnabled.12"] },
        { "dotx", ["application/vnd.openxmlformats-officedocument.wordprocessingml.template"] },
        { "dtd", ["application/xml-dtd"] },
        { "dv", ["video/x-dv"] },
        { "dvi", ["application/x-dvi"] },
        { "dxr", ["application/x-director"] },
        { "eml", ["message/rfc822"] },
        { "eps", ["application/postscript"] },
        { "epub", ["application/epub+zip"] },
        { "etx", ["text/x-setext"] },
        { "exe", ["application/octet-stream"] },
        { "ez", ["application/andrew-inset"] },
        { "flac", ["audio/flac"] },
        { "gif", ["image/gif"] },
        { "gram", ["application/srgs"] },
        { "grxml", ["application/srgs+xml"] },
        { "gtar", ["application/x-gtar"] },
        { "gz", ["application/gzip"] },
        { "hdf", ["application/x-hdf"] },
        { "heic", ["image/heic"] },
        { "heif", ["image/heif"] },
        { "hqx", ["application/mac-binhex40"] },
        { "htm", ["text/html"] },
        { "html", ["text/html"] },
        { "ice", ["x-conference/x-cooltalk"] },
        { "ico", ["image/x-icon"] },
        { "ics", ["text/calendar"] },
        { "ief", ["image/ief"] },
        { "ifb", ["text/calendar"] },
        { "iges", ["model/iges"] },
        { "igs", ["model/iges"] },
        { "jnlp", ["application/x-java-jnlp-file"] },
        { "jp2", ["image/jp2"] },
        { "jpe", ["image/jpeg"] },
        { "jpeg", ["image/jpeg"] },
        { "jpg", ["image/jpeg"] },
        { "js", ["text/javascript", "application/x-javascript"] },
        { "json", ["application/json"] },
        { "jsonld", ["application/ld+json"] },
        { "kar", ["audio/midi"] },
        { "latex", ["application/x-latex"] },
        { "lha", ["application/octet-stream"] },
        { "lzh", ["application/octet-stream"] },
        { "m3u", ["audio/x-mpegurl"] },
        { "m4a", ["audio/mp4a-latm"] },
        { "m4b", ["audio/mp4a-latm"] },
        { "m4p", ["audio/mp4a-latm"] },
        { "m4u", ["video/vnd.mpegurl"] },
        { "m4v", ["video/x-m4v"] },
        { "mac", ["image/x-macpaint"] },
        { "man", ["application/x-troff-man"] },
        { "mathml", ["application/mathml+xml"] },
        { "md", ["text/markdown"] },
        { "me", ["application/x-troff-me"] },
        { "mesh", ["model/mesh"] },
        { "mid", ["audio/midi"] },
        { "midi", ["audio/midi"] },
        { "mif", ["application/vnd.mif"] },
        { "mjs", ["text/javascript"] },
        { "mkv", ["video/x-matroska"] },
        { "mov", ["video/quicktime"] },
        { "movie", ["video/x-sgi-movie"] },
        { "mp2", ["audio/mpeg"] },
        { "mp3", ["audio/mpeg"] },
        { "mp4", ["video/mp4"] },
        { "mpe", ["video/mpeg"] },
        { "mpeg", ["video/mpeg"] },
        { "mpg", ["video/mpeg"] },
        { "mpga", ["audio/mpeg"] },
        { "ms", ["application/x-troff-ms"] },
        { "msg", ["application/vnd.ms-outlook"] },
        { "msh", ["model/mesh"] },
        { "mxu", ["video/vnd.mpegurl"] },
        { "nc", ["application/x-netcdf"] },
        { "oda", ["application/oda"] },
        { "odp", ["application/vnd.oasis.opendocument.presentation"] },
        { "ods", ["application/vnd.oasis.opendocument.spreadsheet"] },
        { "odt", ["application/vnd.oasis.opendocument.text"] },
        { "oga", ["audio/ogg"] },
        { "ogg", ["application/ogg"] },
        { "ogv", ["video/ogg"] },
        { "opus", ["audio/opus"] },
        { "otf", ["font/otf"] },
        { "pbm", ["image/x-portable-bitmap"] },
        { "pct", ["image/pict"] },
        { "pdb", ["chemical/x-pdb"] },
        { "pdf", ["application/pdf"] },
        { "pgm", ["image/x-portable-graymap"] },
        { "pgn", ["application/x-chess-pgn"] },
        { "pic", ["image/pict"] },
        { "pict", ["image/pict"] },
        { "png", ["image/png", "image/x-png"] },
        { "pnm", ["image/x-portable-anymap"] },
        { "pnt", ["image/x-macpaint"] },
        { "pntg", ["image/x-macpaint"] },
        { "potm", ["application/vnd.ms-powerpoint.template.macroEnabled.12"] },
        { "potx", ["application/vnd.openxmlformats-officedocument.presentationml.template"] },
        { "ppam", ["application/vnd.ms-powerpoint.addin.macroEnabled.12"] },
        { "ppm", ["image/x-portable-pixmap"] },
        { "ppsm", ["application/vnd.ms-powerpoint.slideshow.macroEnabled.12"] },
        { "ppsx", ["application/vnd.openxmlformats-officedocument.presentationml.slideshow"] },
        { "ppt", ["application/vnd.ms-powerpoint"] },
        { "pptm", ["application/vnd.ms-powerpoint.presentation.macroEnabled.12"] },
        { "pptx", ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] },
        { "ps", ["application/postscript"] },
        { "qt", ["video/quicktime"] },
        { "qti", ["image/x-quicktime"] },
        { "qtif", ["image/x-quicktime"] },
        { "ra", ["audio/x-pn-realaudio"] },
        { "ram", ["audio/x-pn-realaudio"] },
        { "rar", ["application/vnd.rar", "application/x-rar-compressed"] },
        { "ras", ["image/x-cmu-raster"] },
        { "rdf", ["application/rdf+xml"] },
        { "rgb", ["image/x-rgb"] },
        { "rm", ["application/vnd.rn-realmedia"] },
        { "roff", ["application/x-troff"] },
        { "rtf", ["text/rtf"] },
        { "rtx", ["text/richtext"] },
        { "sgm", ["text/sgml"] },
        { "sgml", ["text/sgml"] },
        { "sh", ["application/x-sh"] },
        { "shar", ["application/x-shar"] },
        { "silo", ["model/mesh"] },
        { "sit", ["application/x-stuffit"] },
        { "skd", ["application/x-koan"] },
        { "skm", ["application/x-koan"] },
        { "skp", ["application/x-koan"] },
        { "skt", ["application/x-koan"] },
        { "smi", ["application/smil"] },
        { "smil", ["application/smil"] },
        { "snd", ["audio/basic"] },
        { "so", ["application/octet-stream"] },
        { "spl", ["application/x-futuresplash"] },
        { "src", ["application/x-wais-source"] },
        { "sv4cpio", ["application/x-sv4cpio"] },
        { "sv4crc", ["application/x-sv4crc"] },
        { "svg", ["image/svg+xml"] },
        { "swf", ["application/x-shockwave-flash"] },
        { "t", ["application/x-troff"] },
        { "tar", ["application/x-tar"] },
        { "tcl", ["application/x-tcl"] },
        { "tex", ["application/x-tex"] },
        { "texi", ["application/x-texinfo"] },
        { "texinfo", ["application/x-texinfo"] },
        { "tif", ["image/tiff"] },
        { "tiff", ["image/tiff"] },
        { "tr", ["application/x-troff"] },
        { "tsv", ["text/tab-separated-values"] },
        { "ttf", ["font/ttf"] },
        { "txt", ["text/plain"] },
        { "ustar", ["application/x-ustar"] },
        { "vcd", ["application/x-cdlink"] },
        { "vcf", ["text/vcard"] },
        { "vrml", ["model/vrml"] },
        { "vxml", ["application/voicexml+xml"] },
        { "wasm", ["application/wasm"] },
        { "wav", ["audio/x-wav"] },
        { "wbmp", ["image/vnd.wap.wbmp"] },
        { "wbmxl", ["application/vnd.wap.wbxml"] },
        { "weba", ["audio/webm"] },
        { "webm", ["video/webm"] },
        { "webmanifest", ["application/manifest+json"] },
        { "webp", ["image/webp"] },
        { "wma", ["audio/x-ms-wma"] },
        { "wml", ["text/vnd.wap.wml"] },
        { "wmlc", ["application/vnd.wap.wmlc"] },
        { "wmls", ["text/vnd.wap.wmlscript"] },
        { "wmlsc", ["application/vnd.wap.wmlscriptc"] },
        { "wmv", ["video/x-ms-wmv"] },
        { "woff", ["font/woff"] },
        { "woff2", ["font/woff2"] },
        { "wrl", ["model/vrml"] },
        { "xbm", ["image/x-xbitmap"] },
        { "xht", ["application/xhtml+xml"] },
        { "xhtml", ["application/xhtml+xml"] },
        { "xlam", ["application/vnd.ms-excel.addin.macroEnabled.12"] },
        { "xls", ["application/vnd.ms-excel"] },
        { "xlsb", ["application/vnd.ms-excel.sheet.binary.macroEnabled.12"] },
        { "xlsm", ["application/vnd.ms-excel.sheet.macroEnabled.12"] },
        { "xlsx", ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] },
        { "xltm", ["application/vnd.ms-excel.template.macroEnabled.12"] },
        { "xltx", ["application/vnd.openxmlformats-officedocument.spreadsheetml.template"] },
        { "xml", ["application/xml"] },
        { "xpm", ["image/x-xpixmap"] },
        { "xsl", ["application/xml"] },
        { "xslt", ["application/xslt+xml"] },
        { "xul", ["application/vnd.mozilla.xul+xml"] },
        { "xwd", ["image/x-xwindowdump"] },
        { "xyz", ["chemical/x-xyz"] },
        { "yaml", ["application/yaml"] },
        { "yml", ["application/yaml"] },
        { "zip", ["application/zip", "application/x-zip-compressed"] }
    }, StringComparer.OrdinalIgnoreCase);
    // the extension GetExtension answers for a type several extensions share, where the first in alphabetical order is
    // not the usual one
    private static readonly Dictionary<string, string> UsualExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        { "application/postscript", "ps" },
        { "application/xhtml+xml", "xhtml" },
        { "audio/midi", "mid" },
        { "audio/mpeg", "mp3" },
        { "image/jpeg", "jpg" },
        { "text/html", "html" },
        { "text/plain", "txt" },
        { "video/mpeg", "mpg" },
    };

    /// <summary>File signatures by extension, for <see cref="GetContentType(byte[], string?)"/>. <see cref="Extend(IEnumerable{KeyValuePair{string, byte[]}})"/> adds to it.</summary>
    public static IDictionary<string, byte[]> MimeTypeByteSequences => ByteSequences;
    /// <summary>MIME types by extension (without the dot, any case), the first one preferred. <see cref="Extend(IEnumerable{KeyValuePair{string, string[]}})"/> adds to it.</summary>
    public static IDictionary<string, string[]> MimeTypesDictionary => MimeTypes;

    /// <summary>
    /// Extends the mapping dictionary with new values
    /// </summary>
    /// <param name="mimeTypes">e.g. { "zip": ["application/zip", "application/x-zip-compressed"] }</param>
    public static void Extend(IEnumerable<KeyValuePair<string, string[]>> mimeTypes)
    {
        foreach (var entry in mimeTypes)
        {
            var newValue = MimeTypesDictionary.ContainsKey(entry.Key)
                ? MimeTypesDictionary[entry.Key].Concat(entry.Value).Distinct().ToArray()
                : entry.Value;
            MimeTypesDictionary[entry.Key] = newValue;
        }
    }
    /// <summary>
    /// Extends the mapping dictionary with new values (replaces existing values)
    /// </summary>
    /// <param name="byteSequences"></param>
    public static void Extend(IEnumerable<KeyValuePair<string, byte[]>> byteSequences)
    {
        foreach (var entry in byteSequences)
        {
            MimeTypeByteSequences[entry.Key] = entry.Value;
        }
    }

    /// <summary>
    /// Determines the MIME type of a file based on its file name.
    /// </summary>
    /// <param name="fileName">The name of the file, including its extension.</param>
    /// <returns>
    /// A string representing the MIME type of the file. 
    /// If the file extension is not recognized, returns "application/octet-stream".
    /// </returns>
    public static string GetContentType(string? fileName)
    {
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var extension = Path.GetExtension(fileName).TrimStart('.');
            if (extension.Length > 0 && MimeTypes.TryGetValue(extension, out var types))
            {
                return types.First();
            }
        }

        return "application/octet-stream";
    }
    /// <summary>
    /// Determines the MIME content type of a file based on its byte sequence and optional filename.
    /// </summary>
    /// <param name="bytes">The byte array representing the file content.</param>
    /// <param name="filename">
    /// An optional parameter specifying the file name, which is used to refine the content type detection
    /// when multiple matches are found or when no byte sequence match is identified.
    /// </param>
    /// <returns>
    /// A string representing the MIME content type of the file. If no match is found, the method attempts
    /// to determine the content type using the provided filename. If both methods fail, the result may be null or a default value.
    /// </returns>
    /// <remarks>
    /// This method first attempts to match the file's byte sequence against known patterns in 
    /// <see cref="MimeTypeByteSequences"/>. If multiple matches are found, the filename (if provided) is used to 
    /// prioritize the most appropriate MIME type. If no matches are found, the method falls back to 
    /// <see cref="GetContentType(string?)"/> to determine the MIME type based on the file name.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown if both <paramref name="bytes"/> and <paramref name="filename"/> are null.</exception>
    /// <example>
    /// Example usage:
    /// <code>
    /// byte[] fileBytes = File.ReadAllBytes("example.pdf");
    /// string contentType = ContentTypeUtility.GetContentType(fileBytes, "example.pdf");
    /// Console.WriteLine(contentType); // Outputs "application/pdf"
    /// </code>
    /// </example>
    public static string GetContentType(byte[] bytes, string? filename = null)
    {
        var matches = MimeTypeByteSequences
            .Where(x => bytes.Take(x.Value.Length).SequenceEqual(x.Value))
            .ToList();
        if (!matches.Any())
        {
            // no matches, try to find by filename (if provided)
            return GetContentType(filename!);
        }

        // multiple matches
        if (matches.Count > 1)
        {
            if (!string.IsNullOrWhiteSpace(filename))
            {
                var preferredExtension = matches
                    .Where(m => m.Key.Equals(Path.GetExtension(filename).TrimStart('.'), StringComparison.InvariantCultureIgnoreCase))
                    .Select(x => x.Key)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(preferredExtension) && MimeTypesDictionary.ContainsKey(preferredExtension))
                {
                    return MimeTypesDictionary[preferredExtension].First();
                }
            }
            // otherwise: continue and take first match
        }

        // 1 match (a signature added without a MIME type falls back to the file name)
        return MimeTypes.TryGetValue(matches.First().Key, out var matched) ? matched.First() : GetContentType(filename);
    }
    /// <summary>
    /// The extension (without the dot) a file of <paramref name="mimetype"/> takes, or else the type's subtype
    /// (<c>image/x-foo</c> gives <c>x-foo</c>). Where several extensions share the type, the answer is fixed: one whose
    /// first type it is goes before one that only lists it, then the usual one (<c>jpg</c> for <c>image/jpeg</c>,
    /// <c>html</c> for <c>text/html</c>), then the first in alphabetical order (<c>xml</c> before <c>xsl</c>, <c>js</c> before
    /// <c>mjs</c>). It reads the map as it is, <see cref="Extend(IEnumerable{KeyValuePair{string, string[]}})"/> included.
    /// </summary>
    /// <param name="mimetype">Content type, in any case</param>
    public static string? GetExtension(string mimetype)
    {
        var extension = MimeTypes
            .Where(x => x.Value.Contains(mimetype, StringComparer.OrdinalIgnoreCase))
            .OrderBy(x => string.Equals(x.Value.FirstOrDefault(), mimetype, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => UsualExtensions.TryGetValue(mimetype, out var usual) && string.Equals(usual, x.Key, StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Key)
            .FirstOrDefault();
        return extension ?? mimetype.Split('/').LastOrDefault();
    }
}