using System;

namespace CxDesktopWrapper.Models;

public class UpdateInfo
{
    public bool UpdateAvailable { get; set; }
    public string CurrentVersion { get; set; } = string.Empty;
    public string LatestVersion { get; set; } = string.Empty;
    public string ReleaseName { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public string InstallerUrl { get; set; } = string.Empty;
    public bool PreRelease { get; set; }
    public DateTime PublishedAt { get; set; }

    public string DisplayTitle => string.IsNullOrWhiteSpace(ReleaseName) ? $"Versão {LatestVersion}" : ReleaseName;
    public string PublishedAtDisplay => PublishedAt == DateTime.MinValue ? string.Empty : PublishedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
