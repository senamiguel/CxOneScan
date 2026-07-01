using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using CxDesktopWrapper.Models;

namespace CxDesktopWrapper;

public partial class CopilotFilterWindow : Window
{
    private readonly List<VulnerabilityItem> _allVulns;
    public ObservableCollection<VulnerabilityItem> DisplayedVulns { get; } = new();

    public List<VulnerabilityItem> SelectedVulnerabilities => _allVulns.Where(v => v.IsSelected).ToList();
    public bool IsConfirmed { get; private set; }

    public CopilotFilterWindow(List<VulnerabilityItem> vulns)
    {
        InitializeComponent();
        _allVulns = vulns;
        
        dgVulnerabilities.ItemsSource = DisplayedVulns;
        ApplyFilters();
    }

    private void Filter_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        DisplayedVulns.Clear();
        
        bool showCritical = chkCritical.IsChecked == true;
        bool showHigh = chkHigh.IsChecked == true;
        bool showMedium = chkMedium.IsChecked == true;
        bool showLow = chkLow.IsChecked == true;
        
        bool showSast = chkSast.IsChecked == true;
        bool showSca = chkSca.IsChecked == true;

        foreach (var v in _allVulns)
        {
            bool matchSeverity = (v.Severity == "Critical" && showCritical) ||
                                 (v.Severity == "High" && showHigh) ||
                                 (v.Severity == "Medium" && showMedium) ||
                                 ((v.Severity == "Low" || v.Severity == "Info") && showLow);

            bool matchType = (v.Type == "SAST" && showSast) ||
                             (v.Type == "SCA" && showSca);

            if (string.IsNullOrEmpty(v.Type)) matchType = true;
            if (string.IsNullOrEmpty(v.Severity)) matchSeverity = true;

            if (matchSeverity && matchType)
            {
                DisplayedVulns.Add(v);
            }
            else
            {
                v.IsSelected = false; // automatically deselect filtered out items
            }
        }
        
        lblCount.Text = $"{DisplayedVulns.Count} vulnerabilidade(s) visíveis";
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = false;
        Close();
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        IsConfirmed = true;
        Close();
    }
}
