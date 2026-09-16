using System.IO;
using System.Text.Json;
using VideoCheckTool.Models;

namespace VideoCheckTool.Services;

public sealed class FileMoveService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _journalPath;
    private readonly List<MoveRecord> _records;

    public FileMoveService()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VideoCheckTool");
        Directory.CreateDirectory(folder);
        _journalPath = Path.Combine(folder, "moves.json");
        _records = LoadRecords();
    }

    public MoveRecord? FindByCurrentPath(string path) => _records.LastOrDefault(r =>
        string.Equals(r.CurrentPath, path, StringComparison.OrdinalIgnoreCase));

    public string MoveToFolder(string sourcePath, string destinationFolder)
    {
        if (!File.Exists(sourcePath)) throw new FileNotFoundException("找不到要搬移的檔案。", sourcePath);
        Directory.CreateDirectory(destinationFolder);
        var destination = GetAvailablePath(Path.Combine(destinationFolder, Path.GetFileName(sourcePath)));
        var existing = FindByCurrentPath(sourcePath);
        var originalPath = existing?.OriginalPath ?? sourcePath;
        File.Move(sourcePath, destination);
        if (existing is not null) _records.Remove(existing);
        _records.Add(new MoveRecord { OriginalPath = originalPath, CurrentPath = destination });
        SaveRecords();
        return destination;
    }

    public string Restore(string currentPath)
    {
        var record = FindByCurrentPath(currentPath)
            ?? throw new InvalidOperationException("這個檔案沒有搬移紀錄，無法判斷原始路徑。");
        if (!File.Exists(currentPath)) throw new FileNotFoundException("找不到要還原的檔案。", currentPath);
        var originalFolder = Path.GetDirectoryName(record.OriginalPath)
            ?? throw new InvalidOperationException("原始路徑無效。");
        Directory.CreateDirectory(originalFolder);
        var destination = File.Exists(record.OriginalPath) ? GetAvailablePath(record.OriginalPath) : record.OriginalPath;
        File.Move(currentPath, destination);
        _records.Remove(record);
        SaveRecords();
        return destination;
    }

    private List<MoveRecord> LoadRecords()
    {
        try
        {
            return File.Exists(_journalPath)
                ? JsonSerializer.Deserialize<List<MoveRecord>>(File.ReadAllText(_journalPath), JsonOptions) ?? []
                : [];
        }
        catch { return []; }
    }

    private void SaveRecords() => File.WriteAllText(_journalPath, JsonSerializer.Serialize(_records, JsonOptions));

    private static string GetAvailablePath(string desiredPath)
    {
        if (!File.Exists(desiredPath)) return desiredPath;
        var folder = Path.GetDirectoryName(desiredPath)!;
        var stem = Path.GetFileNameWithoutExtension(desiredPath);
        var extension = Path.GetExtension(desiredPath);
        for (var index = 1; ; index++)
        {
            var candidate = Path.Combine(folder, $"{stem} ({index}){extension}");
            if (!File.Exists(candidate)) return candidate;
        }
    }
}
