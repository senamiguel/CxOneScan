using System.IO;
using System.Text.RegularExpressions;
using CxDesktopWrapper.Models;

namespace CxDesktopWrapper.Services;

public static partial class SolutionParserService
{
    [GeneratedRegex(
        @"Project\(""\{[^}]+\}""\)\s*=\s*""([^""]+)""\s*,\s*""([^""]+\.csproj)""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex CsprojReferencePattern();

    public static List<ProjectItem> ParseSolution(string slnPath)
    {
        var result = new List<ProjectItem>();

        if (!File.Exists(slnPath))
            return result;

        string? slnDirectory = Path.GetDirectoryName(slnPath);
        if (string.IsNullOrEmpty(slnDirectory))
            return result;

        string[] lines = File.ReadAllLines(slnPath);
        var regex = CsprojReferencePattern();

        foreach (string line in lines)
        {
            var match = regex.Match(line);
            if (!match.Success) continue;

            string projectName = match.Groups[1].Value;
            string relativePath = match.Groups[2].Value;

            relativePath = relativePath.Replace('\\', Path.DirectorySeparatorChar);
            string absolutePath = Path.GetFullPath(Path.Combine(slnDirectory, relativePath));
            string projectDirectory = Path.GetDirectoryName(absolutePath) ?? slnDirectory;

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
}
