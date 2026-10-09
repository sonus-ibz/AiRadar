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

    /// <summary>Source-specific popularity: upvotes, points, likes or stars.</summary>
    public int Score { get; set; }

    public DateTime LaunchedAtUtc { get; set; }
    public DateTime FirstSeenAtUtc { get; set; }

    /// <summary>Normalised name and links used to recognise the same tool on another source.</summary>
    public string NameKey { get; set; } = "";
    public string? UrlKey { get; set; }
    public string? WebsiteKey { get; set; }

    /// <summary>Set when this tool was already found on another source earlier.</summary>
    public int? DuplicateOfId { get; set; }
    public Tool? DuplicateOf { get; set; }
}
