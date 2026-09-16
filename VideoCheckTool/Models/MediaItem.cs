using System.IO;

namespace VideoCheckTool.Models;

public enum MediaKind
{
    Video,
    Image
}

public sealed class MediaItem
{
    public required string FullPath { get; init; }
    public required MediaKind Kind { get; init; }
    public string Name => Path.GetFileName(FullPath);
    public string DirectoryName => Path.GetDirectoryName(FullPath) ?? string.Empty;
    public string KindGlyph => Kind == MediaKind.Video ? "▶" : "▧";
}
