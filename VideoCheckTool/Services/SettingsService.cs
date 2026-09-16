using System.IO;
using System.Text.Json;
using VideoCheckTool.Models;

namespace VideoCheckTool.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _settingsPath;

    public SettingsService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoCheckTool");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath)) return new AppSettings();
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_settingsPath), JsonOptions);
            if (settings is null) return new AppSettings();
            settings.EnabledExtensions = new HashSet<string>(settings.EnabledExtensions, StringComparer.OrdinalIgnoreCase);
            return settings;
        }
        catch { return new AppSettings(); }
    }

    public void Save(AppSettings settings) =>
        File.WriteAllText(_settingsPath, JsonSerializer.Serialize(settings, JsonOptions));
}
