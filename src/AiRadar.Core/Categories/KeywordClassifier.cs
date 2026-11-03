using System.Text.RegularExpressions;

namespace AiRadar.Core.Categories;

/// <summary>
/// Instant, offline sorting by keywords. Used for every tool at sync time, and kept when the local AI
/// is not installed or gives no usable answer.
/// </summary>
public static class KeywordClassifier
{
    private static readonly IReadOnlyList<(string Category, Regex Pattern)> Rules =
    [
        Rule("Coding", @"code|coding|coder|developers?|devs?|programming|ide|github|git|apis?|sdks?|debug\w*|pr|prs|pull requests?|commits?|diffs?|terminal|cli|repos?|repository|software|compilers?|typescript|python|rust|javascript|backend|frontend|deploy\w*"),
        Rule("Agents & automation", @"agents?|agentic|automat\w*|workflows?|autonomous\w*|orchestrat\w*|mcp|bots?"),
        Rule("Chat & assistants", @"chat|chats|chatbots?|chatting|assistants?|companions?|conversations?|ask"),
        Rule("Writing", @"writ\w*|copywrit\w*|essays?|blogs?|grammar|rewrit\w*|summar\w*|translat\w*|documents?|docs"),
        Rule("Image", @"images?|photos?|pictures?|art|artwork|illustrat\w*|avatars?|text-to-image|diffusion|headshots?|logos?"),
        Rule("Video", @"videos?|clips?|films?|animat\w*|movies?|youtube|reels|shorts|lip-?sync"),
        Rule("Voice & audio", @"voices?|speech|audio|tts|text-to-speech|transcri\w*|podcasts?|calls?|dictat\w*|whisper"),
        Rule("Music", @"music\w*|songs?|beats?|melod\w*|singing"),
        Rule("Research & search", @"research\w*|search\w*|papers?|academic|knowledge|citations?|sources"),
        Rule("Data & analytics", @"data|analytics|sql|spreadsheets?|excel|dashboards?|charts?|databases?|csv|tables?|invoices?"),
        Rule("Design", @"design\w*|ui|ux|figma|mockups?|wireframes?|prototyp\w*|websites?|landing pages?|slides|presentations?"),
        Rule("Productivity", @"productivity|tasks?|calendars?|meetings?|emails?|inbox|todos?|schedul\w*|organi[sz]\w*|focus|notes?"),
        Rule("Marketing & sales", @"marketing|sales|seo|ads|leads?|outreach|crm|social media|growth|e-?commerce|customers?|support tickets?"),
        Rule("Education", @"learn\w*|education\w*|tutor\w*|courses?|students?|teach\w*|study|quiz\w*|lessons?"),
    ];

    public static Classification Classify(string name, string tagline, string? description)
    {
        var text = $"{name} {tagline} {description}";

        var ranked = Rules
            .Select((rule, order) => (rule.Category, Score: rule.Pattern.Matches(text).Count, order))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.order)
            .ToList();

        return ranked.Count switch
        {
            0 => new Classification(ToolCategories.Other, null),
            1 => new Classification(ranked[0].Category, null),
            _ => new Classification(ranked[0].Category, ranked[1].Category),
        };
    }

    private static (string, Regex) Rule(string category, string words) =>
        (category, new Regex($@"\b({words})\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled));
}
