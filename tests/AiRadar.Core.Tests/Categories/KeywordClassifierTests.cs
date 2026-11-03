using AiRadar.Core.Categories;

namespace AiRadar.Core.Tests.Categories;

public sealed class KeywordClassifierTests
{
    [Theory]
    [InlineData("commitwise", "Writes commit messages and PR summaries from a diff", "Coding")]
    [InlineData("ClipForge", "Cuts long videos into short clips with captions", "Video")]
    [InlineData("Parla TTS", "Small text-to-speech model with Indian English voices", "Voice & audio")]
    [InlineData("Sketchloom", "Turn rough sketches into UI mockups", "Design")]
    [InlineData("PaperPilot", "Summarises research papers and finds related ones", "Research & search")]
    [InlineData("QueryMate", "Ask questions about your SQL database in plain English", "Data & analytics")]
    [InlineData("Beatsmith", "Make songs and beats from a hummed melody", "Music")]
    public void Classify_ClearTagline_PicksExpectedPrimary(string name, string tagline, string expected)
    {
        var result = KeywordClassifier.Classify(name, tagline, null);

        Assert.Equal(expected, result.Primary);
    }

    [Fact]
    public void Classify_TwoStrongTopics_ReturnsSecondCategoryToo()
    {
        var result = KeywordClassifier.Classify("Ledgerly", "An agent that automates reconciling bank data in spreadsheets", null);

        Assert.Equal("Agents & automation", result.Primary);
        Assert.Equal("Data & analytics", result.Secondary);
    }

    [Fact]
    public void Classify_NoKeywordMatches_ReturnsOtherWithoutSecondary()
    {
        var result = KeywordClassifier.Classify("Zyx", "Something completely different", null);

        Assert.Equal(ToolCategories.Other, result.Primary);
        Assert.Null(result.Secondary);
    }

    [Fact]
    public void Classify_KeywordInsideLongerWord_DoesNotMatch()
    {
        // "art" must not match "start", "ai" must not decide anything on its own.
        var result = KeywordClassifier.Classify("Kickoff", "Start your day", null);

        Assert.Equal(ToolCategories.Other, result.Primary);
    }

    [Fact]
    public void Classify_UsesDescriptionWhenTaglineIsEmpty()
    {
        var result = KeywordClassifier.Classify("Nace.ai Document Intelligence 1.0", "", "Extracts tables and fields from invoices and spreadsheets.");

        Assert.Equal("Data & analytics", result.Primary);
    }
}
