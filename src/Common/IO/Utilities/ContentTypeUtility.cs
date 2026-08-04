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
    public static IDictionary<string, byte[]> MimeTypeByteSequences => new ConcurrentDictionary<string, byte[]>(new Dictionary<string, byte[]>
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
    });
    public static IDictionary<string, string[]> MimeTypesDictionary
    {
        get
        {
            return new ConcurrentDictionary<string, string[]>(new Dictionary<string, string[]>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "ai", ["application/postscript"] },
                { "aif", ["audio/x-aiff"] },
                { "aifc", ["audio/x-aiff"] },
                { "aiff", ["audio/x-aiff"] },
                { "asc", ["text/plain"] },
                { "atom", ["application/atom+xml"] },
                { "au", ["audio/basic"] },
                { "avi", ["video/x-msvideo"] },
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
                { "docx", ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] },
                { "dotx", ["application/vnd.openxmlformats-officedocument.wordprocessingml.template"] },
                { "docm", ["application/vnd.ms-word.document.macroEnabled.12"] },
                { "dotm", ["application/vnd.ms-word.template.macroEnabled.12"] },
                { "dtd", ["application/xml-dtd"] },
                { "dv", ["video/x-dv"] },
                { "dvi", ["application/x-dvi"] },
                { "dxr", ["application/x-director"] },
                { "eml", ["message/rfc82"] },
                { "eps", ["application/postscript"] },
                { "etx", ["text/x-setext"] },
                { "exe", ["application/octet-stream"] },
                { "ez", ["application/andrew-inset"] },
                { "gif", ["image/gif"] },
                { "gram", ["application/srgs"] },
                { "grxml", ["application/srgs+xml"] },
                { "gtar", ["application/x-gtar"] },
                { "hdf", ["application/x-hdf"] },
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
                { "js", ["application/x-javascript"] },
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
                { "me", ["application/x-troff-me"] },
                { "mesh", ["model/mesh"] },
                { "mid", ["audio/midi"] },
                { "midi", ["audio/midi"] },
                { "mif", ["application/vnd.mif"] },
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
                { "ogg", ["application/ogg"] },
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
                { "ppm", ["image/x-portable-pixmap"] },
                { "ppt", ["application/vnd.ms-powerpoint"] },
                { "pptx", ["application/vnd.openxmlformats-officedocument.presentationml.presentation"] },
                { "potx", ["application/vnd.openxmlformats-officedocument.presentationml.template"] },
                { "ppsx", ["application/vnd.openxmlformats-officedocument.presentationml.slideshow"] },
                { "ppam", ["application/vnd.ms-powerpoint.addin.macroEnabled.12"] },
                { "pptm", ["application/vnd.ms-powerpoint.presentation.macroEnabled.12"] },
                { "potm", ["application/vnd.ms-powerpoint.template.macroEnabled.12"] },
                { "ppsm", ["application/vnd.ms-powerpoint.slideshow.macroEnabled.12"] },
                { "ps", ["application/postscript"] },
                { "qt", ["video/quicktime"] },
                { "qti", ["image/x-quicktime"] },
                { "qtif", ["image/x-quicktime"] },
                { "ra", ["audio/x-pn-realaudio"] },
                { "ram", ["audio/x-pn-realaudio"] },
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
                { "txt", ["text/plain"] },
                { "ustar", ["application/x-ustar"] },
                { "vcd", ["application/x-cdlink"] },
                { "vcf", ["text/vcard"] },
                { "vrml", ["model/vrml"] },
                { "vxml", ["application/voicexml+xml"] },
                { "wav", ["audio/x-wav"] },
                { "wbmp", ["image/vnd.wap.wbmp"] },
                { "wbmxl", ["application/vnd.wap.wbxml"] },
                { "wml", ["text/vnd.wap.wml"] },
                { "wmlc", ["application/vnd.wap.wmlc"] },
                { "wmls", ["text/vnd.wap.wmlscript"] },
                { "wmlsc", ["application/vnd.wap.wmlscriptc"] },
                { "wrl", ["model/vrml"] },
                { "xbm", ["image/x-xbitmap"] },
                { "xht", ["application/xhtml+xml"] },
                { "xhtml", ["application/xhtml+xml"] },
                { "xls", ["application/vnd.ms-excel"] },
                { "xml", ["application/xml"] },
                { "xpm", ["image/x-xpixmap"] },
                { "xsl", ["application/xml"] },
                { "xlsx", ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"] },
                { "xltx", ["application/vnd.openxmlformats-officedocument.spreadsheetml.template"] },
                { "xlsm", ["application/vnd.ms-excel.sheet.macroEnabled.12"] },
                { "xltm", ["application/vnd.ms-excel.template.macroEnabled.12"] },
                { "xlam", ["application/vnd.ms-excel.addin.macroEnabled.12"] },
                { "xlsb", ["application/vnd.ms-excel.sheet.binary.macroEnabled.12"] },
                { "xslt", ["application/xslt+xml"] },
                { "xul", ["application/vnd.mozilla.xul+xml"] },
                { "xwd", ["image/x-xwindowdump"] },
                { "xyz", ["chemical/x-xyz"] },
                { "zip", ["application/zip", "application/x-zip-compressed"] }
            });
        }
    }

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
            var extension = Path.GetExtension(fileName).ToLowerInvariant().TrimStart('.');
            if (extension.Length > 0 && MimeTypesDictionary.ContainsKey(extension))
            {
                return MimeTypesDictionary[extension].First();
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

        // 1 match
        return MimeTypesDictionary[matches.First().Key].First();
    }
    /// <summary>
    /// 
    /// </summary>
    /// <param name="mimetype">Content type</param>
    /// <returns></returns>
    public static string? GetExtension(string mimetype)
    {
        var extension = MimeTypesDictionary
            .Where(x => x.Value.Contains(mimetype))
            .Select(x => x.Key)
            .FirstOrDefault();
        return extension ?? mimetype.Split('/').LastOrDefault();
    }
}