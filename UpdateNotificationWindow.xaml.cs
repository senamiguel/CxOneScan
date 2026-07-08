using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using CxDesktopWrapper.Common;
using CxDesktopWrapper.Models;

namespace CxDesktopWrapper;

public partial class UpdateNotificationWindow : Window
{
    public bool SkipThisVersion { get; private set; }

    private readonly UpdateInfo _info;

    public UpdateNotificationWindow(UpdateInfo info)
    {
        _info = info ?? throw new ArgumentNullException(nameof(info));
        InitializeComponent();

        lblVersionLine.Text = $"Você está na versão {AppConstants.CurrentVersion} — disponível: {_info.LatestVersion}.";
        lblReleaseName.Text = _info.DisplayTitle;
        lblPublished.Text = string.IsNullOrEmpty(_info.PublishedAtDisplay) ? string.Empty : $"Publicada em {_info.PublishedAtDisplay}";
        txtNotes.Text = StripMarkdown(_info.ReleaseNotes);

        btnDownload.IsEnabled = !string.IsNullOrEmpty(_info.InstallerUrl);
        btnDownload.ToolTip = _info.InstallerUrl;
    }

    private static string StripMarkdown(string md)
    {
        if (string.IsNullOrWhiteSpace(md)) return "Sem notas de release para esta versão.";

        var t = md;
        t = Regex.Replace(t, @"```[\s\S]*?```", m => m.Value.Replace("```", "").Trim());
        t = Regex.Replace(t, @"`([^`]+)`", "$1");
        t = Regex.Replace(t, @"^\s*#{1,6}\s*", "", RegexOptions.Multiline);
        t = Regex.Replace(t, @"^\s*[-*+]\s+", "• ", RegexOptions.Multiline);
        t = Regex.Replace(t, @"\*\*([^*]+)\*\*", "$1");
        t = Regex.Replace(t, @"__([^_]+)__", "$1");
        t = Regex.Replace(t, @"\*([^*]+)\*", "$1");
        t = Regex.Replace(t, @"(?<!\w)_([^_]+)_(?!\w)", "$1");
        t = Regex.Replace(t, @"\[([^\]]+)\]\(([^)]+)\)", "$1 ($2)");
        t = Regex.Replace(t, @"\r\n|\r|\n", Environment.NewLine);
        return t.Trim();
    }

    private void BtnDownload_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_info.InstallerUrl))
        {
            OpenUrl(_info.InstallerUrl);
        }
        else if (!string.IsNullOrEmpty(_info.HtmlUrl))
        {
            OpenUrl(_info.HtmlUrl);
        }
        SkipThisVersion = chkSkip.IsChecked == true;
        DialogResult = true;
    }

    private void BtnPage_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_info.HtmlUrl))
        {
            OpenUrl(_info.HtmlUrl);
        }
        SkipThisVersion = chkSkip.IsChecked == true;
        DialogResult = true;
    }

    private void BtnLater_Click(object sender, RoutedEventArgs e)
    {
        SkipThisVersion = chkSkip.IsChecked == true;
        DialogResult = false;
        Close();
    }

    private void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir o link:\n{url}\n\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
