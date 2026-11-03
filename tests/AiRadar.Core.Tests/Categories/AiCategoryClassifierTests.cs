using AiRadar.Core.Categories;
using AiRadar.Core.Tests.TestSupport;
using Microsoft.Extensions.AI;

namespace AiRadar.Core.Tests.Categories;

public sealed class AiCategoryClassifierTests
{
    private static async Task<(Classification? Result, FakeChatClient Chat)> ClassifyAsync(string reply)
    {
        var chat = new FakeChatClient(_ => reply);
        var result = await new AiCategoryClassifier(chat).ClassifyAsync("ClipForge", "Cuts long videos into clips", "Long description", CancellationToken.None);
        return (result, chat);
    }

    [Fact]
    public async Task ClassifyAsync_ValidAnswer_ReturnsBothCategories()
    {
        var (result, _) = await ClassifyAsync("""{ "primary": "Video", "secondary": "Marketing & sales" }""");

        Assert.Equal(new Classification("Video", "Marketing & sales"), result);
    }

    [Theory]
    [InlineData("""{ "primary": "Video", "secondary": "None" }""")]
    [InlineData("""{ "primary": "Video" }""")]
    [InlineData("""{ "primary": "Video", "secondary": "Video" }""")]
    [InlineData("""{ "primary": "Video", "secondary": "Cooking" }""")]
    public async Task ClassifyAsync_SecondaryMissingSameOrUnknown_ReturnsPrimaryOnly(string reply)
    {
        var (result, _) = await ClassifyAsync(reply);

        Assert.Equal(new Classification("Video", null), result);
    }

    [Fact]
    public async Task ClassifyAsync_AnswerWrappedInCodeFence_StillParses()
    {
        var (result, _) = await ClassifyAsync("""
            Here you go:
            ```json
            { "primary": "Image", "secondary": "None" }
            ```
            """);

        Assert.Equal("Image", result?.Primary);
    }

    [Theory]
    [InlineData("""{ "primary": "Cooking" }""")]
    [InlineData("""{ "secondary": "Video" }""")]
    [InlineData("I think this is a video tool.")]
    [InlineData("")]
    public async Task ClassifyAsync_NoValidPrimary_ReturnsNull(string reply)
    {
        var (result, _) = await ClassifyAsync(reply);

        Assert.Null(result);
    }

    [Fact]
    public async Task ClassifyAsync_AsksDeterministicallyWithEveryCategoryAndTheToolText()
    {
        var (_, chat) = await ClassifyAsync("""{ "primary": "Video" }""");

        var options = Assert.Single(chat.Options);
        Assert.Equal(0f, options!.Temperature);
        Assert.IsType<ChatResponseFormatJson>(options.ResponseFormat);
        var prompt = string.Join("\n", chat.Requests[0].Select(m => m.Text));
        Assert.All(ToolCategories.Names, name => Assert.Contains(name, prompt));
        Assert.Contains("ClipForge", prompt);
        Assert.Contains("Cuts long videos into clips", prompt);
    }

    [Fact]
    public async Task ClassifyAsync_VeryLongDescription_IsShortened()
    {
        var chat = new FakeChatClient(_ => """{ "primary": "Coding" }""");

        await new AiCategoryClassifier(chat).ClassifyAsync("X", "Y", new string('a', 5000), CancellationToken.None);

        Assert.True(chat.Requests[0].Sum(m => m.Text.Length) < 3000);
    }
}
