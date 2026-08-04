namespace Regira.System.Hosting.WindowsService;

public class WindowsServiceOptions
{
    public string? ServiceName { get; set; }
    public string InstallFilename { get; set; } = "install.bat";
    public string UninstallFilename { get; set; } = "uninstall.bat";
}