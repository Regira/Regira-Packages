using Regira.IO.Models;

namespace IO.Testing.Helpers;

public class TestFilesCreator
{
    public static string[] TestFiles =
    [
        @"file1.txt",
        @"file2.txt",
        @"dir1\file1.1.txt",
        @"dir1\file1.2.txt",
        @"dir2\dir2.1\file2.1.1.log",
        @"dir2\dir2.1\file2.1.2.txt",
        @"dir2\dir2.2\file2.2.1.log",
        @"dir2\dir2.2\file2.2.2.text",
        @"dir2\dir2.1\dir2.1.1\file2.1.1.1.txt",
        @"dir3\file3.1.txt"
    ];

    public static IEnumerable<BinaryFileItem> Create(string root)
    {
        foreach (var file in TestFiles)
        {
            // the identifiers keep '\', which every file service accepts; the files on disk use the platform's separator
            var path = Path.Combine(root, file.Replace('\\', Path.DirectorySeparatorChar));
            var filename = Path.GetFileName(path);
            var dir = Path.GetDirectoryName(path);
            Directory.CreateDirectory(dir ?? throw new InvalidOperationException());
            File.WriteAllText(path, filename);
            var bytes = File.ReadAllBytes(path);
            yield return new BinaryFileItem
            {
                Identifier = file,
                FileName = filename,
                Path = path,
                Bytes = bytes,
                Length = bytes.Length,
            };
        }
    }
    public static void Clear(string root)
    {
        var dirs = Directory.GetDirectories(root);
        var files = Directory.GetFiles(root);
        foreach (var dir in dirs)
        {
            Directory.Delete(dir, true);
        }
        foreach (var file in files)
        {
            File.Delete(file);
        }
    }
}