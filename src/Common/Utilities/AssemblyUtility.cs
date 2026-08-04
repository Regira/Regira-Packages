using System.Reflection;

namespace Regira.Utilities;

public static class AssemblyUtility
{
    // https://stackoverflow.com/questions/52797/how-do-i-get-the-path-of-the-assembly-the-code-is-in#answer-283917
    /// <summary>
    /// Retrieves the directory path of the specified assembly or the currently executing assembly if none is provided.
    /// </summary>
    /// <param name="assembly">
    /// The assembly whose directory path is to be retrieved. If <c>null</c>, the directory of the currently executing assembly is returned.
    /// </param>
    /// <returns>
    /// The directory path of the specified assembly, or <c>null</c> if the path cannot be determined.
    /// </returns>
    public static string? GetAssemblyDirectory(Assembly? assembly = null)
    {
        var codeBase = (assembly ?? Assembly.GetExecutingAssembly()).Location;
        var uri = new UriBuilder(codeBase);
        var path = Uri.UnescapeDataString(uri.Path);
        return Path.GetDirectoryName(path);
    }
}