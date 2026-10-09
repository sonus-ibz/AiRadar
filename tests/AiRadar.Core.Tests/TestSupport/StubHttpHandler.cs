using System.Net;
using System.Text;

namespace AiRadar.Core.Tests.TestSupport;

/// <summary>Returns queued responses in order and records every request body.</summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<string> RequestBodies { get; } = [];

    public StubHttpHandler RespondWithJson(string json, HttpStatusCode status = HttpStatusCode.OK)
    {
        _responses.Enqueue(new HttpResponseMessage(status)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        });
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        RequestBodies.Add(request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken));
        return _responses.Count > 0
            ? _responses.Dequeue()
            : throw new InvalidOperationException("No stubbed response left for " + request.RequestUri);
    }
}
