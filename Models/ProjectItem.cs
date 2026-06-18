using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace CxDesktopWrapper.Models;

public class ProjectItem : INotifyPropertyChanged
{
    private string _name = string.Empty;
    private bool _isSelected;
    private string _localPath = string.Empty;
    private string _branch = "main";
    private string _tags = string.Empty;
    private string _projectTags = string.Empty;
    private string _projectGroups = string.Empty;
    private bool _runSast = true;
    private bool _runSca;

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetField(ref _isSelected, value);
    }

    public string LocalPath
    {
        get => _localPath;
        set => SetField(ref _localPath, value);
    }

    public string Branch
    {
        get => _branch;
        set => SetField(ref _branch, value);
    }

    public string Tags
    {
        get => _tags;
        set => SetField(ref _tags, value);
    }

    public string ProjectTags
    {
        get => _projectTags;
        set => SetField(ref _projectTags, value);
    }

    public string ProjectGroups
    {
        get => _projectGroups;
        set => SetField(ref _projectGroups, value);
    }

    public bool RunSast
    {
        get => _runSast;
        set => SetField(ref _runSast, value);
    }

    public bool RunSca
    {
        get => _runSca;
        set => SetField(ref _runSca, value);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
