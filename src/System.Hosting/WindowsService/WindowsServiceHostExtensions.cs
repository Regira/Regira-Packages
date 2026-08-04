using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace Regira.System.Hosting.WindowsService;

public static class WindowsServiceHostExtensions
{
    /// <summary>
    /// Creates an install.bat and uninstall.bat file to register the program as a Windows Service.
    /// </summary>
    /// <param name="host"></param>
    /// <param name="options"></param>
    /// <returns></returns>
    public static IHost AddWindowsServiceInstaller(this IHost host, WindowsServiceOptions? options = null)
    {
        options ??= new WindowsServiceOptions();

        var appAssembly = Assembly.GetEntryAssembly()!;

        var serviceName = options.ServiceName ?? appAssembly.GetName().Name?.Replace(".", "_");
        var assemblyName = Path.GetFileNameWithoutExtension(appAssembly.Location);
        var dir = Path.GetDirectoryName(appAssembly.Location) ?? throw new NullReferenceException("No directory for assembly path");
        var installPath = Path.Combine(dir, options.InstallFilename);
        var uninstallPath = Path.Combine(dir, options.UninstallFilename);

        if (File.Exists(installPath))
        {
            return host;
        }

        var installTemplate = $@"sc create ""{serviceName}"" binPath= ""%~dp0{assemblyName}.exe""
sc failure ""{serviceName}"" actions= restart/60000/restart/60000/restart/60000 reset= 86400
sc start ""{serviceName}""
sc config ""{serviceName}"" start=auto";
        File.WriteAllText(installPath, installTemplate);

        var uninstallTemplate = $@"sc stop ""{serviceName}""
timeout /t 5 /nobreak > NUL
sc delete ""{serviceName}""";
        File.WriteAllText(uninstallPath, uninstallTemplate);

        return host;
    }
}