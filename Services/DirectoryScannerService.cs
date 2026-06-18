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

        string folderName = Path.GetFileName(rootPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (string.IsNullOrEmpty(folderName))
        {
            folderName = new DirectoryInfo(rootPath).Name;
        }

        var settings = AppSettingsService.Instance;

        result.Add(new ProjectItem
        {
            Name = folderName,
            IsSelected = true,
            LocalPath = rootPath,
            Branch = settings.DefaultBranch,
            RunSast = settings.DefaultRunSast,
            RunSca = settings.DefaultRunSca,
            Tags = string.Empty,
            ProjectTags = string.Empty,
            ProjectGroups = string.Empty
        });

        return result;
    }
}
