using System.Net.Http;
using System.Net.Http.Headers;
using AiRadar.Core.Sources;

namespace AiRadar.App.Services;

/// <summary>Adds the saved Product Hunt token to each request, so a newly saved token applies without a restart.</summary>
public sealed class ProductHuntAuthHandler(TokenStore tokens) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = tokens.Read()
            ?? throw new ToolSourceException("Add your Product Hunt token in Settings to include Product Hunt.");

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, cancellationToken);
    }
}
