namespace CxDesktopWrapper.Models;

public class AppSettings
{
    public string CliPath { get; set; } = @"C:\Checkmarx\cx.exe";
    public string Tenant { get; set; } = string.Empty;
    public string BaseUri { get; set; } = "https://ast.checkmarx.net";
    public string BaseAuthUri { get; set; } = "https://iam.checkmarx.net";
    public string DefaultBranch { get; set; } = "main";
    public bool DefaultRunSast { get; set; } = true;
    public bool DefaultRunSca { get; set; } = false;
    public string Theme { get; set; } = "Dark";
    public bool IsFirstRunCompleted { get; set; } = false;
    public bool BypassApiValidation { get; set; } = false;
}
