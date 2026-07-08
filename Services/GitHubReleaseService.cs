using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CxDesktopWrapper.Common;
using CxDesktopWrapper.Models;

namespace CxDesktopWrapper.Services;

public class GitHubReleaseService
{
    private static readonly HttpClient _http = new()
    {
        Timeout = TimeSpan.FromSeconds(8)
    };

    static GitHubReleaseService()
    {
        _http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("CxOneScan", AppConstants.CurrentVersion));
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        _http.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
    }

    public async Task<UpdateInfo?> CheckForUpdateAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AppConstants.GitHubLatestReleaseUrl);
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var t) ? (t.GetString() ?? string.Empty) : string.Empty;
            if (string.IsNullOrWhiteSpace(tagName)) return null;

            bool preRelease = root.TryGetProperty("prerelease", out var p) && p.GetBoolean();
            if (preRelease) return null;

            string htmlUrl = root.TryGetProperty("html_url", out var h) ? (h.GetString() ?? string.Empty) : string.Empty;
            string name = root.TryGetProperty("name", out var n) ? (n.GetString() ?? string.Empty) : string.Empty;
            if (string.IsNullOrWhiteSpace(name)) name = tagName;

            string body = root.TryGetProperty("body", out var b) ? (b.GetString() ?? string.Empty) : string.Empty;
            DateTime published = root.TryGetProperty("published_at", out var dEl) && dEl.TryGetDateTime(out var dt) ? dt : DateTime.MinValue;

            string installerUrl = ExtractInstallerUrl(root);

            string latestNorm = NormalizeVersion(tagName);
            string currentNorm = NormalizeVersion(AppConstants.CurrentVersion);
            int cmp = CompareVersions(latestNorm, currentNorm);

            return new UpdateInfo
            {
                UpdateAvailable = cmp > 0,
                CurrentVersion = AppConstants.CurrentVersion,
                LatestVersion = StripPrefix(tagName),
                ReleaseName = name,
                ReleaseNotes = body,
                HtmlUrl = string.IsNullOrEmpty(htmlUrl) ? AppConstants.GitHubReleasesPageUrl : htmlUrl,
                InstallerUrl = installerUrl,
                PreRelease = preRelease,
                PublishedAt = published
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Update check failed: " + ex.Message);
            return null;
        }
    }

    private static string ExtractInstallerUrl(JsonElement root)
    {
        if (!root.TryGetProperty("assets", out var assets) || assets.ValueKind != JsonValueKind.Array) return string.Empty;
        foreach (var asset in assets.EnumerateArray())
        {
            string? name = asset.TryGetProperty("name", out var an) ? an.GetString() : null;
            if (name == null) continue;
            if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) continue;
            if (asset.TryGetProperty("browser_download_url", out var u))
            {
                return u.GetString() ?? string.Empty;
            }
        }
        return string.Empty;
    }

    private static string StripPrefix(string v) => v.TrimStart('v', 'V');

    private static string NormalizeVersion(string v)
    {
        string t = StripPrefix(v.Trim());
        int dash = t.IndexOf('-');
        if (dash >= 0) t = t.Substring(0, dash);
        int plus = t.IndexOf('+');
        if (plus >= 0) t = t.Substring(0, plus);
        return t;
    }

    private static int CompareVersions(string a, string b)
    {
        var pa = a.Split('.');
        var pb = b.Split('.');
        int len = Math.Max(pa.Length, pb.Length);
        for (int i = 0; i < len; i++)
        {
            int ai = (i < pa.Length && int.TryParse(pa[i], out var x)) ? x : 0;
            int bi = (i < pb.Length && int.TryParse(pb[i], out var y)) ? y : 0;
            if (ai != bi) return ai.CompareTo(bi);
        }
        return 0;
    }
}
