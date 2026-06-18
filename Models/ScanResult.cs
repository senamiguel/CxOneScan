namespace CxDesktopWrapper.Models;

public class ScanResult
{
    public string ProjectName { get; set; } = string.Empty;
    public int HighCount { get; set; }
    public int MediumCount { get; set; }
    public int LowCount { get; set; }
    public int InfoCount { get; set; }
    public string ReportJsonPath { get; set; } = string.Empty;
    public string ReportHtmlPath { get; set; } = string.Empty;
    public DateTime ScanDate { get; set; } = DateTime.Now;
    public bool Success { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}
