using System.IO;
using System.Text.Json;
using CxDesktopWrapper.Models;

namespace CxDesktopWrapper.Services;

public static class AppSettingsService
{
    private static readonly string ConfigDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CxDesktopWrapper");

    private static readonly string ConfigFilePath = Path.Combine(ConfigDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static AppSettings? _instance;

    public static AppSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Load();
            }
            return _instance;
        }
    }

    public static AppSettings Load()
    {
        if (!File.Exists(ConfigFilePath))
            return new AppSettings();

        try
        {
            string json = File.ReadAllText(ConfigFilePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao carregar configurações: " + ex.Message);
            return new AppSettings();
        }
    }

    public static void Save()
    {
        if (_instance == null) return;
        Save(_instance);
    }

    public static void Save(AppSettings settings)
    {
        try
        {
            Directory.CreateDirectory(ConfigDirectory);
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(ConfigFilePath, json);
            _instance = settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("Erro ao salvar configurações: " + ex.Message);
        }
    }
}
