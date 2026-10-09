using AiRadar.Core.Tools;

namespace AiRadar.Core.Tests.Tools;

public sealed class ToolKeysTests
{
    [Theory]
    [InlineData("Sketch-Loom", "sketchloom")]
    [InlineData("  Mosaic Chat 2.0 ", "mosaicchat20")]
    [InlineData("Nace.ai", "naceai")]
    [InlineData("!!!", "")]
    public void ForName_KeepsLowercaseLettersAndDigits(string name, string expected)
    {
        Assert.Equal(expected, ToolKeys.ForName(name));
    }

    [Theory]
    [InlineData("https://www.usemagpie.ai/", "usemagpie.ai")]
    [InlineData("http://UseMagpie.ai", "usemagpie.ai")]
    [InlineData("https://github.com/Yetone/Magpie/", "github.com/yetone/magpie")]
    [InlineData("https://github.com/yetone/magpie?tab=readme#top", "github.com/yetone/magpie")]
    public void ForUrl_NormalisesSchemeHostPathAndCase(string url, string expected)
    {
        Assert.Equal(expected, ToolKeys.ForUrl(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    [InlineData("ftp://example.com/file")]
    public void ForUrl_MissingOrNotWeb_ReturnsNull(string? url)
    {
        Assert.Null(ToolKeys.ForUrl(url));
    }
}
