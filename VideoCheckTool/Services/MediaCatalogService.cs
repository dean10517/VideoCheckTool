using System.IO;
using VideoCheckTool.Models;

namespace VideoCheckTool.Services;

public sealed class MediaCatalogService
{
    public static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".avi", ".mkv", ".wmv", ".m4v" };
    public static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff" };

    public IReadOnlyList<MediaItem> Scan(string folder, bool recursive, IEnumerable<string> extensions,
        IEnumerable<string>? excludedFolders = null)
    {
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return [];
        var allowed = new HashSet<string>(extensions, StringComparer.OrdinalIgnoreCase);
        var exclusions = (excludedFolders ?? [])
            .Where(Directory.Exists)
            .Select(p => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .ToArray();
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = recursive,
            IgnoreInaccessible = true,
            ReturnSpecialDirectories = false
        };
        try
        {
            return Directory.EnumerateFiles(folder, "*", options)
                .Where(path => allowed.Contains(Path.GetExtension(path)))
                .Where(path => !exclusions.Any(ex => Path.GetFullPath(path).StartsWith(ex, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(path => path, StringComparer.CurrentCultureIgnoreCase)
                .Select(ToMediaItem).ToArray();
        }
        catch (UnauthorizedAccessException) { return []; }
        catch (IOException) { return []; }
    }

    public static MediaItem ToMediaItem(string path) => new()
    {
        FullPath = path,
        Kind = VideoExtensions.Contains(Path.GetExtension(path)) ? MediaKind.Video : MediaKind.Image
    };
}
