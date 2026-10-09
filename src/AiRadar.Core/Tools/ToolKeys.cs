namespace AiRadar.Core.Tools;

/// <summary>Normalised forms of a tool's name and links, so the same tool matches across sources.</summary>
public static class ToolKeys
{
    public static string ForName(string name) =>
        string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant();

    public static string? ForUrl(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return (host + uri.AbsolutePath.TrimEnd('/')).ToLowerInvariant();
    }
}
