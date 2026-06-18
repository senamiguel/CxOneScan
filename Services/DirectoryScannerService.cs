using System.IO;
using CxDesktopWrapper.Models;

namespace CxDesktopWrapper.Services;

public static class DirectoryScannerService
{
    public static List<ProjectItem> ScanDirectory(string rootPath)
    {
        var result = new List<ProjectItem>();

        if (!Directory.Exists(rootPath))
            return result;

        var csprojFiles = Directory.EnumerateFiles(rootPath, "*.csproj", SearchOption.AllDirectories);

        foreach (string csprojPath in csprojFiles)
        {
            string projectName = Path.GetFileNameWithoutExtension(csprojPath);
            string projectDirectory = Path.GetDirectoryName(csprojPath) ?? rootPath;

            if (IsExcludedPath(projectDirectory))
                continue;

            result.Add(new ProjectItem
            {
                Name = projectName,
                IsSelected = true,
                LocalPath = projectDirectory,
                Branch = "main",
                RunSast = true
            });
        }

        return result;
    }

    private static bool IsExcludedPath(string path)
    {
        string normalized = path.Replace('\\', '/').ToLowerInvariant();
        return normalized.Contains("/obj/")
            || normalized.Contains("/bin/")
            || normalized.Contains("/node_modules/")
            || normalized.Contains("/.vs/");
    }
}
