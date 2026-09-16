namespace VideoCheckTool.Models;

public sealed class AppSettings
{
    public string SourceFolder { get; set; } = string.Empty;
    public string AFolder { get; set; } = string.Empty;
    public string DFolder { get; set; } = string.Empty;
    public bool IncludeSubfolders { get; set; } = true;
    public HashSet<string> EnabledExtensions { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".avi", ".mkv", ".jpg", ".jpeg", ".png"
    };
}
