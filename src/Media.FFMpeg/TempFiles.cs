namespace Regira.Media.FFMpeg;

internal static class TempFiles
{
    /// <summary>
    /// Best effort: a temporary file left behind must not replace what the call it served reported.
    /// </summary>
    public static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
