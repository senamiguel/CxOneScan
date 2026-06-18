using System.IO;
using CxDesktopWrapper.Models;

namespace CxDesktopWrapper.Services;

public static class SolutionParserService
{
    public static List<ProjectItem> ParseSolution(string slnPath)
    {
        var result = new List<ProjectItem>();

        if (!File.Exists(slnPath))
            return result;

        string? slnDirectory = Path.GetDirectoryName(slnPath);
        if (string.IsNullOrEmpty(slnDirectory))
            return result;

        string slnName = Path.GetFileNameWithoutExtension(slnPath);
        var settings = AppSettingsService.Instance;

        result.Add(new ProjectItem
        {
            Name = slnName,
            IsSelected = true,
            LocalPath = slnDirectory,
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
