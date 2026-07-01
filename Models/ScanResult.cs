using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CxDesktopWrapper.Models;

public class ScanResult : INotifyPropertyChanged
{
    private string _projectName = string.Empty;
    private int _highCount;
    private int _mediumCount;
    private int _lowCount;
    private int _infoCount;
    private string _reportJsonPath = string.Empty;
    private string _reportHtmlPath = string.Empty;
    private DateTime _scanDate = DateTime.Now;
    private bool _success;
    private string _statusMessage = string.Empty;

    public string ProjectName
    {
        get => _projectName;
        set => SetProperty(ref _projectName, value);
    }

    public int HighCount
    {
        get => _highCount;
        set => SetProperty(ref _highCount, value);
    }

    public int MediumCount
    {
        get => _mediumCount;
        set => SetProperty(ref _mediumCount, value);
    }

    public int LowCount
    {
        get => _lowCount;
        set => SetProperty(ref _lowCount, value);
    }

    public int InfoCount
    {
        get => _infoCount;
        set => SetProperty(ref _infoCount, value);
    }

    public string ReportJsonPath
    {
        get => _reportJsonPath;
        set => SetProperty(ref _reportJsonPath, value);
    }

    public string ReportHtmlPath
    {
        get => _reportHtmlPath;
        set => SetProperty(ref _reportHtmlPath, value);
    }

    public DateTime ScanDate
    {
        get => _scanDate;
        set => SetProperty(ref _scanDate, value);
    }

    public bool Success
    {
        get => _success;
        set => SetProperty(ref _success, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
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
