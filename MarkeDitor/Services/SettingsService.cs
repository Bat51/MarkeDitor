using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace MarkeDitor.Services;

public class AppSettings
{
    public string FontFamily { get; set; } = "Cascadia Code,Consolas,Menlo,Monospace";
    public double FontSize { get; set; } = 14;
    public string Theme { get; set; } = "Dark";          // "Dark" | "Light" | "System"
    public bool ReopenLastTabs { get; set; } = true;
    public List<string> RecentFiles { get; set; } = new();
    public List<string> LastOpenTabs { get; set; } = new();
    public int MaxRecent { get; set; } = 10;

    // Auto-completion
    public bool AutoCompleteEnabled { get; set; } = true;
    public int AutoCompleteMinChars { get; set; } = 4;

    // Spell check
    public bool SpellCheckEnabled { get; set; } = true;
    public List<string> SpellCheckLanguages { get; set; } = new() { "fr_FR", "en_US" };
    public List<string> CustomDictionary { get; set; } = new();
}

public class SettingsService
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarkeDitor");
    private static readonly string SettingsFile = Path.Combine(SettingsDir, "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public event EventHandler? Changed;

    public void Load()
    {
        try
        {
            if (!File.Exists(SettingsFile)) return;
            var json = File.ReadAllText(SettingsFile);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded != null) Settings = loaded;
        }
        catch
        {
            // Fall back to defaults on any read/parse error.
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(SettingsFile, json);
            Changed?.Invoke(this, EventArgs.Empty);
        }
        catch { /* don't break the app on disk errors */ }
    }

    public void RegisterRecent(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        var list = Settings.RecentFiles;
        list.RemoveAll(p => string.Equals(p, path, StringComparison.Ordinal));
        list.Insert(0, path);
        if (list.Count > Settings.MaxRecent)
            list.RemoveRange(Settings.MaxRecent, list.Count - Settings.MaxRecent);
        Save();
    }

    public void ClearRecent()
    {
        Settings.RecentFiles.Clear();
        Save();
    }

    public void SaveLastOpenTabs(IEnumerable<string?> filePaths)
    {
        Settings.LastOpenTabs = filePaths.Where(p => !string.IsNullOrEmpty(p))
                                          .Select(p => p!)
                                          .Distinct()
                                          .ToList();
        Save();
    }

    public void AddToCustomDictionary(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        if (Settings.CustomDictionary.Contains(word)) return;
        Settings.CustomDictionary.Add(word);
        Save();
    }
}
