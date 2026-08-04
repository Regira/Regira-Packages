using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.System.Abstractions;

namespace Regira.System;

public static class ProcessHelperExtensions
{
    public static void OpenFileByOS(this IProcessHelper processHelper, string path)
    {
        var cmd = $@"start """" ""{path}""";
        processHelper.ExecuteCommand(cmd);
    }

    public static void OpenFileByOS(this IProcessHelper processHelper, IBinaryFile file)
    {
        var existedOnDisk = file.HasPath();
        var path = file.GetPath();
        OpenFileByOS(processHelper, path);
        if (!existedOnDisk && File.Exists(path))
        {
            File.Delete(path);
        }
    }
}