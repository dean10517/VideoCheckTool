namespace VideoCheckTool.Models;

public sealed class MoveRecord
{
    public required string OriginalPath { get; set; }
    public required string CurrentPath { get; set; }
    public DateTime MovedAtUtc { get; set; } = DateTime.UtcNow;
}
