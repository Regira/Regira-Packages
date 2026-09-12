using Regira.DAL.MongoDB.Core;

namespace Regira.DAL.MongoDB.Services;

/// <summary>
/// The password, written to the YAML file mongodump and mongorestore read through <c>--config</c>, and deleted again
/// when the tool has run.
/// </summary>
/// <remarks>
/// The tools take a password on the command line as well, but a command line is readable by every process on the
/// machine and is picked up by process monitoring, for as long as the dump runs. They accept it nowhere else: unlike
/// <c>pg_dump</c> they read no environment variable, and their interactive prompt reads the console directly, so a
/// redirected stdin cannot answer it. That leaves a file only this process can read as the narrowest channel.
/// </remarks>
internal sealed class MongoPasswordFile : IDisposable
{
    private MongoPasswordFile(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }
    /// <summary>
    /// The argument that points the tool at this file, with a leading space
    /// </summary>
    public string ConfigArgument => $@" --config=""{FilePath}""";

    /// <summary>
    /// Writes <paramref name="settings"/>' password to a temporary file, or returns <c>null</c> when there is no
    /// password to pass.
    /// </summary>
    /// <exception cref="ArgumentException">A password without a username, which cannot authenticate anything</exception>
    public static MongoPasswordFile? Create(MongoSettings settings)
    {
        if (string.IsNullOrEmpty(settings.Password))
        {
            return null;
        }
        if (string.IsNullOrEmpty(settings.Username))
        {
            throw new ArgumentException($"{nameof(MongoSettings.Password)} is set without a {nameof(MongoSettings.Username)}, so there is nothing to authenticate as.", nameof(settings));
        }

        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        var file = new MongoPasswordFile(filePath);
        try
        {
            using (new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                // narrow the permissions while the file is still empty
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
            File.WriteAllText(filePath, $"password: {ToYamlScalar(settings.Password!)}{Environment.NewLine}");
            return file;
        }
        catch
        {
            file.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        try
        {
            File.Delete(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // a password file we cannot remove must not fail a backup that succeeded
        }
    }

    private static string ToYamlScalar(string password)
    {
        if (password.Contains('\r') || password.Contains('\n'))
        {
            throw new ArgumentException($"A {nameof(MongoSettings.Password)} containing a line break cannot be passed to mongodump/mongorestore.", nameof(password));
        }

        // single-quoted YAML is literal throughout, apart from a quote of its own
        return $"'{password.Replace("'", "''")}'";
    }
}
