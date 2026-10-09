namespace AiRadar.Core.Tools;

public sealed class SyncRun
{
    public int Id { get; set; }
    public required string Source { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime FinishedAtUtc { get; set; }
    public bool Succeeded { get; set; }
    public int NewToolCount { get; set; }
    public string? Error { get; set; }
}
