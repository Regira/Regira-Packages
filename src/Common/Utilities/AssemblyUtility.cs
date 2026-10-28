using System.Reflection;

namespace Regira.Utilities;

public static class AssemblyUtility
{
    /// <summary>
    /// Retrieves the directory of the specified assembly's file, or of the currently executing assembly if none is provided.
    /// </summary>
    /// <param name="assembly">
    /// The assembly whose directory is to be retrieved. If <c>null</c>, the currently executing assembly is used: the one
    /// holding this method, <c>Regira.Common</c>, which normally sits in the app's output directory.
    /// </param>
    /// <returns>
    /// The directory of the assembly's file, as written on disk: any path the platform accepts, a network share included.
    /// An assembly without a file, bundled into a single-file app or loaded from bytes, answers the app's base directory
    /// (<see cref="AppContext.BaseDirectory"/>, without its trailing separator).
    /// </returns>
    public static string? GetAssemblyDirectory(Assembly? assembly = null)
    {
        var location = (assembly ?? Assembly.GetExecutingAssembly()).Location;
        return string.IsNullOrEmpty(location)
            ? Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory)
            : Path.GetDirectoryName(location);
    }
}
