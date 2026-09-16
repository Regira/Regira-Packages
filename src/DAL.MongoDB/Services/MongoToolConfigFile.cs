using Regira.DAL.MongoDB.Core;

namespace Regira.DAL.MongoDB.Services;

/// <summary>
/// The connection mongodump and mongorestore use — the URI and the password — written to the YAML file the tools read
/// through <c>--config</c>, and deleted again when the tool has run.
/// </summary>
/// <remarks>
/// The tools take both on the command line as well, but a command line is readable by every process on the machine
/// and is picked up by process monitoring, for as long as the dump runs — and a URI carries secrets of its own
/// (<c>tlsCertificateKeyFilePassword</c>, an <c>AWS_SESSION_TOKEN</c> in <c>authMechanismProperties</c>). Nothing
/// else reaches them: unlike <c>pg_dump</c> they read no environment variable, and their interactive prompt reads the
/// console directly, so a redirected stdin cannot answer it. That leaves a file only this process can read as the
/// narrowest channel. <c>--config</c> needs MongoDB Database Tools 100.3.0 or later.
/// </remarks>
internal sealed class MongoToolConfigFile : IDisposable
{
    /// <summary>
    /// The mechanisms that authenticate without a password, so a username alone is complete.
    /// </summary>
    private static readonly HashSet<string> PasswordlessMechanisms = new(StringComparer.OrdinalIgnoreCase)
    {
        "MONGODB-X509", "MONGODB-AWS", "GSSAPI", "MONGODB-OIDC"
    };

    private MongoToolConfigFile(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    /// <summary>
    /// Writes the connection of <paramref name="settings"/> to a temporary file.
    /// </summary>
    /// <exception cref="ArgumentException">A password without a username, which cannot authenticate anything; or a
    /// username without a password for a mechanism that needs one, which would leave the tool waiting at its prompt</exception>
    public static MongoToolConfigFile Create(MongoSettings settings)
    {
        var hasUsername = !string.IsNullOrEmpty(settings.Username);
        var hasPassword = !string.IsNullOrEmpty(settings.Password);
        if (hasPassword && !hasUsername)
        {
            throw new ArgumentException($"{nameof(MongoSettings.Password)} is set without a {nameof(MongoSettings.Username)}, so there is nothing to authenticate as.", nameof(settings));
        }
        if (hasUsername && !hasPassword && !PasswordlessMechanisms.Contains(settings.GetUriOption("authMechanism") ?? ""))
        {
            throw new ArgumentException(
                $"{nameof(MongoSettings.Username)} is set without a {nameof(MongoSettings.Password)}, so mongodump and mongorestore would wait for one at their console prompt. " +
                $"Set the password, or an authMechanism that needs none ({string.Join(", ", PasswordlessMechanisms)}).", nameof(settings));
        }

        var lines = new List<string> { $"uri: {ToYamlScalar(settings.BuildConnectionString(includePassword: false), "URI")}" };
        if (hasPassword)
        {
            lines.Add($"password: {ToYamlScalar(settings.Password!, nameof(MongoSettings.Password))}");
        }

        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.yaml");
        var file = new MongoToolConfigFile(filePath);
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
            File.WriteAllLines(filePath, lines);
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
            // a config file we cannot remove must not fail a backup that succeeded
        }
    }

    private static string ToYamlScalar(string value, string name)
    {
        if (value.Contains('\r') || value.Contains('\n'))
        {
            throw new ArgumentException($"A {name} containing a line break cannot be passed to mongodump/mongorestore.", nameof(value));
        }

        // single-quoted YAML is literal throughout, apart from a quote of its own
        return $"'{value.Replace("'", "''")}'";
    }
}
