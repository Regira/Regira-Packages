using Regira.IO.Storage.Abstractions;

namespace Regira.IO.Storage.Helpers;

public class FileNameHelper(IFileService fileService, FileNameHelper.Options? options = null)
{
    public class Options
    {
        /// <summary>
        /// Defaults to "-({0})"
        /// </summary>
        public string? NumberPattern { get; set; }
    }

    //https://stackoverflow.com/questions/1078003/c-how-would-you-make-a-unique-filename-by-adding-a-number#answer-1078016
    private readonly string _numberPattern = options?.NumberPattern ?? "-({0})";


    public async Task<string> NextAvailableFileName(string identifier)
    {
        async Task<string> GetNextFilename(string identifierFormat)
        {
            var tmp = string.Format(identifierFormat, 1);
            if (tmp == identifierFormat)
            {
                throw new ArgumentException("The pattern must include an index placeholder", nameof(identifierFormat));
            }

            var nextIdentifier = identifier;
            if (!await fileService.Exists(nextIdentifier))
            {
                return nextIdentifier;
            }

            int min = 1, max = 2; // min is inclusive, max is exclusive/untested
            nextIdentifier = string.Format(identifierFormat, max);
            while (await fileService.Exists(nextIdentifier))
            {
                min = max;
                max *= 2;
                nextIdentifier = string.Format(identifierFormat, max);
            }

            while (max != min + 1)
            {
                var pivot = (max + min) / 2;
                nextIdentifier = string.Format(identifierFormat, pivot);
                if (await fileService.Exists(nextIdentifier))
                {
                    min = pivot;
                }
                else
                {
                    max = pivot;
                }
            }

            return string.Format(identifierFormat, max);
        }

        if (!await fileService.Exists(identifier))
        {
            return identifier;
        }

        var newIdentifierFormat = identifier;
        if (Path.HasExtension(newIdentifierFormat))
        {
            var fileExtension = Path.GetExtension(newIdentifierFormat);
            // ReSharper disable once AssignNullToNotNullAttribute
            var fileExtensionIndex = newIdentifierFormat.LastIndexOf(fileExtension, StringComparison.Ordinal);
            return await GetNextFilename(newIdentifierFormat.Insert(fileExtensionIndex, _numberPattern));
        }

        return await GetNextFilename(newIdentifierFormat + _numberPattern);
    }

    public static string? GetBaseFolder(IEnumerable<string> paths)
    {
        string? baseDir = null;
        foreach (var path in paths)
        {
            var dir = Path.GetDirectoryName(path);
            if (baseDir == null)
            {
                baseDir = dir;
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(dir) && baseDir.StartsWith(dir, StringComparison.InvariantCultureIgnoreCase))
                {
                    baseDir = dir;
                }
            }
        }

        return baseDir;
    }
}