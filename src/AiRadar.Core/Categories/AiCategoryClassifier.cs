using System.Text.Json;
using Microsoft.Extensions.AI;

namespace AiRadar.Core.Categories;

/// <summary>
/// Asks a chat model (a local Ollama model in the app) which category a tool belongs to.
/// Works with any <see cref="IChatClient"/>, so the model can be swapped without code changes.
/// </summary>
public sealed class AiCategoryClassifier(IChatClient chat)
{
    private const string NoSecondary = "None";
    private const int MaxDescriptionLength = 600;

    private static readonly string SystemPrompt =
        "You sort new AI tools by what they are best at.\n"
        + "Choose the single best category as \"primary\". Choose a \"secondary\" category only when the tool clearly also fits another one; otherwise use \"None\".\n"
        + "Use the category names exactly as written. Categories:\n"
        + string.Join("\n", ToolCategories.All.Select(c => $"- {c.Name}: {c.Meaning}"))
        + "\nReply with JSON only, for example {\"primary\": \"Video\", \"secondary\": \"None\"}.";

    private static readonly ChatOptions Options = new()
    {
        Temperature = 0,
        ResponseFormat = ChatResponseFormat.ForJsonSchema(BuildSchema(), "classification"),
    };

    /// <returns>The categories, or null when the model's answer is not usable.</returns>
    public async Task<Classification?> ClassifyAsync(string name, string tagline, string? description, CancellationToken cancellationToken)
    {
        var details = string.IsNullOrWhiteSpace(description)
            ? ""
            : "\nDescription: " + (description.Length > MaxDescriptionLength ? description[..MaxDescriptionLength] : description);

        ChatMessage[] messages =
        [
            new(ChatRole.System, SystemPrompt),
            new(ChatRole.User, $"Name: {name}\nTagline: {tagline}{details}"),
        ];

        var response = await chat.GetResponseAsync(messages, Options, cancellationToken);
        return Parse(response.Text);
    }

    internal static Classification? Parse(string? text)
    {
        var start = text?.IndexOf('{') ?? -1;
        var end = text?.LastIndexOf('}') ?? -1;
        if (start < 0 || end <= start)
        {
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(text![start..(end + 1)]);
            var primary = ReadString(json.RootElement, "primary");
            if (!ToolCategories.IsKnown(primary))
            {
                return null;
            }

            var secondary = ReadString(json.RootElement, "secondary");
            return new Classification(
                primary!,
                ToolCategories.IsKnown(secondary) && secondary != primary ? secondary : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ReadString(JsonElement root, string property) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static JsonElement BuildSchema()
    {
        var names = ToolCategories.Names.ToList();
        var schema = new
        {
            type = "object",
            properties = new
            {
                primary = new { type = "string", @enum = names },
                secondary = new { type = "string", @enum = names.Append(NoSecondary).ToList() },
            },
            required = new[] { "primary", "secondary" },
        };
        return JsonSerializer.SerializeToElement(schema);
    }
}
