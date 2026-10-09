namespace AiRadar.Core.Tools;

public sealed class Tool
{
    public int Id { get; set; }
    public required string Source { get; set; }
    public required string ExternalId { get; set; }
    public required string Name { get; set; }
    public required string Tagline { get; set; }
    public string? Description { get; set; }
    public required string Url { get; set; }

    /// <summary>Source-specific popularity: upvotes, stars or likes.</summary>
    public int Score { get; set; }

    public DateTime LaunchedAtUtc { get; set; }
    public DateTime FirstSeenAtUtc { get; set; }
}
