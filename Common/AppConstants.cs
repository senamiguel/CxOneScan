namespace CxDesktopWrapper.Common;

public static class AppConstants
{
    public const string CliDownloadUrl = "https://github.com/Checkmarx/ast-cli/releases/latest/download/ast-cli_windows_x64.zip";
    public const string DefaultCliDirectory = @"C:\Checkmarx";
    public const string DefaultCliPath = @"C:\Checkmarx\cx.exe";
    public const string DefaultBaseUri = "https://ast.checkmarx.net";
    public const string DefaultBaseAuthUri = "https://iam.checkmarx.net";
    public const string DefaultBranch = "main";
    public const string SettingsFileName = "settings.json";
    public const string ProjectsFileName = "projects.json";
    public const string AppFolderName = "CxOneScan";
    public const string CurrentVersion = "2.2.0";
    public const string GitHubOwner = "senamiguel";
    public const string GitHubRepo = "CxOneScan";
    public const string GitHubLatestReleaseUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
    public const string GitHubReleasesPageUrl = $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases";
}
