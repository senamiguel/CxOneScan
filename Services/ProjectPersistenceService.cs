using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using CxDesktopWrapper.Models;

namespace CxDesktopWrapper.Services;

public static class ProjectPersistenceService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CxDesktopWrapper");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "projects.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string GetConfigDirectory() => ConfigDirectory;

    public static List<ProjectItem> Load()
    {
        if (!File.Exists(ConfigFilePath))
            return new List<ProjectItem>();

        try
        {
            string json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<List<ProjectItem>>(json, JsonOptions) ?? new List<ProjectItem>();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao carregar projetos: " + ex.Message);
            return new List<ProjectItem>();
        }
    }

    public static void Save(ObservableCollection<ProjectItem> projects)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            string json = JsonSerializer.Serialize(projects, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao salvar projetos: " + ex.Message);
        }
    }
}
