using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CxDesktopWrapper.Models;

public class AppSettings : INotifyPropertyChanged
{
    private string _cliPath = @"C:\Checkmarx\cx.exe";
    private string _tenant = string.Empty;
    private string _baseUri = "https://ast.checkmarx.net";
    private string _baseAuthUri = "https://iam.checkmarx.net";
    private string _defaultBranch = "main";
    private bool _defaultRunSast = true;
    private bool _defaultRunSca = false;
    private bool _defaultIncremental = true;
    private string _defaultTags = string.Empty;
    private string _defaultProjectTags = string.Empty;
    private string _defaultProjectGroups = string.Empty;
    private string _theme = "Dark";
    private bool _isFirstRunCompleted = false;
    private bool _keepLoggedIn = true;

    public string CliPath
    {
        get => _cliPath;
        set => SetProperty(ref _cliPath, value);
    }

    public string Tenant
    {
        get => _tenant;
        set => SetProperty(ref _tenant, value);
    }

    public string BaseUri
    {
        get => _baseUri;
        set => SetProperty(ref _baseUri, value);
    }

    public string BaseAuthUri
    {
        get => _baseAuthUri;
        set => SetProperty(ref _baseAuthUri, value);
    }

    public string DefaultBranch
    {
        get => _defaultBranch;
        set => SetProperty(ref _defaultBranch, value);
    }

    public bool DefaultRunSast
    {
        get => _defaultRunSast;
        set => SetProperty(ref _defaultRunSast, value);
    }

    public bool DefaultRunSca
    {
        get => _defaultRunSca;
        set => SetProperty(ref _defaultRunSca, value);
    }

    public bool DefaultIncremental
    {
        get => _defaultIncremental;
        set => SetProperty(ref _defaultIncremental, value);
    }

    public string DefaultTags
    {
        get => _defaultTags;
        set => SetProperty(ref _defaultTags, value);
    }

    public string DefaultProjectTags
    {
        get => _defaultProjectTags;
        set => SetProperty(ref _defaultProjectTags, value);
    }

    public string DefaultProjectGroups
    {
        get => _defaultProjectGroups;
        set => SetProperty(ref _defaultProjectGroups, value);
    }

    public string Theme
    {
        get => _theme;
        set => SetProperty(ref _theme, value);
    }

    public bool IsFirstRunCompleted
    {
        get => _isFirstRunCompleted;
        set => SetProperty(ref _isFirstRunCompleted, value);
    }

    public bool KeepLoggedIn
    {
        get => _keepLoggedIn;
        set => SetProperty(ref _keepLoggedIn, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
