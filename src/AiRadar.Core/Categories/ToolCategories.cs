namespace AiRadar.Core.Categories;

/// <summary>The fixed "good at" list. The AI must answer with one of these names exactly.</summary>
public static class ToolCategories
{
    public const string Other = "Other";

    /// <summary>Category name and a one-line meaning, shown to the AI so it sorts consistently.</summary>
    public static readonly IReadOnlyList<(string Name, string Meaning)> All =
    [
        ("Coding", "for software developers: writing, reviewing, testing or deploying code, coding agents and their plugins, developer infrastructure, model APIs, routers and SDKs"),
        ("Agents & automation", "general-purpose agents that do tasks for people, such as browsing, booking or running a business process; if the agent is for programming, choose Coding"),
        ("Chat & assistants", "chat apps, chatbots and personal assistants you talk to, and general-purpose AI models"),
        ("Writing", "writing, editing, rewriting, summarising or translating text and documents"),
        ("Image", "creating or editing still images, photos, art, avatars, logos"),
        ("Video", "anything that produces or edits video, including image-to-video, clips and animation"),
        ("Voice & audio", "speech, voice, text-to-speech, transcription, phone calls, podcasts"),
        ("Music", "creating or editing music and songs"),
        ("Research & search", "finding and comparing information: search, papers, benchmarks, model comparisons, news gathering"),
        ("Data & analytics", "data collection, scraping, spreadsheets, SQL, charts, statistics, trading and finance analysis"),
        ("Design", "UI/UX, websites, mockups, presentations, visual design"),
        ("Productivity", "office documents, notes, email, calendar, meetings, tasks, personal organisation"),
        ("Marketing & sales", "marketing, SEO, ads, social media, sales, customer support"),
        ("Education", "learning and teaching: tutorials, courses, learning roadmaps, studying"),
        (Other, "hardware, safety, security compliance, games and demos, or tools whose purpose is unclear"),
    ];

    public static IEnumerable<string> Names => All.Select(c => c.Name);

    public static bool IsKnown(string? name) => name is not null && All.Any(c => c.Name == name);
}

/// <summary>How a tool's categories were decided.</summary>
public enum CategoryMethod
{
    Keywords = 1,
    Ai = 2,

    /// <summary>The AI was asked but gave no usable answer; the keyword result is kept.</summary>
    KeywordsAiUnsure = 3,
}

public sealed record Classification(string Primary, string? Secondary);
