using System.Text.Json;
using XtractForge.Core.Models;

namespace XtractForge.Services;

/// <summary>Loads/saves AppSettings as JSON in %LOCALAPPDATA%\XtractForge\config.json.</summary>
public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private static string ConfigPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "XtractForge", "config.json");

    public AppSettings Current { get; private set; } = Load();

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch (Exception)
        {
            // Corrupt config → fall back to defaults.
        }
        return new AppSettings();
    }

    /// <summary>Mutate + persist in one step.</summary>
    public void Update(Action<AppSettings> mutate)
    {
        mutate(Current);
        Save();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(Current, JsonOptions));
        }
        catch (Exception)
        {
            // Non-fatal: settings just won't persist this session.
        }
    }
}
