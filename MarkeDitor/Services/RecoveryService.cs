using System.Text.Json;
using MarkeDitor.ViewModels;

namespace MarkeDitor.Services;

public class RecoveryService
{
    private static readonly string RecoveryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "MarkeDitor", "Recovery");

    public async Task SaveRecoveryAsync(EditorTabViewModel tab)
    {
        Directory.CreateDirectory(RecoveryDir);

        var data = new RecoveryData
        {
            Id = tab.RecoveryId,
            OriginalPath = tab.FilePath,
            FileName = tab.FileName,
            Content = tab.Content,
            SavedAt = DateTime.Now
        };

        var json = JsonSerializer.Serialize(data);
        var filePath = Path.Combine(RecoveryDir, $"{tab.RecoveryId}.recovery");
        await File.WriteAllTextAsync(filePath, json);
    }

    public Task DeleteRecoveryAsync(string recoveryId)
    {
        var filePath = Path.Combine(RecoveryDir, $"{recoveryId}.recovery");
        if (File.Exists(filePath))
            File.Delete(filePath);
        return Task.CompletedTask;
    }

    public async Task<List<RecoveryData>> GetPendingRecoveriesAsync()
    {
        var results = new List<RecoveryData>();
        if (!Directory.Exists(RecoveryDir))
            return results;

        foreach (var file in Directory.GetFiles(RecoveryDir, "*.recovery"))
        {
            try
            {
                var json = await File.ReadAllTextAsync(file);
                var data = JsonSerializer.Deserialize<RecoveryData>(json);
                if (data != null && !string.IsNullOrEmpty(data.Content))
                    results.Add(data);
            }
            catch
            {
                // Skip corrupted recovery files
            }
        }

        return results;
    }

    public Task CleanupAllAsync()
    {
        if (Directory.Exists(RecoveryDir))
        {
            foreach (var file in Directory.GetFiles(RecoveryDir, "*.recovery"))
            {
                try { File.Delete(file); } catch { }
            }
        }
        return Task.CompletedTask;
    }
}

public class RecoveryData
{
    public string Id { get; set; } = string.Empty;
    public string? OriginalPath { get; set; }
    public string FileName { get; set; } = "Untitled";
    public string Content { get; set; } = string.Empty;
    public DateTime SavedAt { get; set; }
}
