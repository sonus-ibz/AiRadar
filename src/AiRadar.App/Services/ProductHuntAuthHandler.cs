using System.Net.Http;
using System.Net.Http.Headers;

namespace AiRadar.App.Services;

/// <summary>Adds the saved Product Hunt token to each request, so a newly saved token applies without a restart.</summary>
public sealed class ProductHuntAuthHandler(TokenStore tokens) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = tokens.Read();
        if (token is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
