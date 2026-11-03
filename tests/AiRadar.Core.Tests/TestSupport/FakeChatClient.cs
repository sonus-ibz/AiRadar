using Microsoft.Extensions.AI;

namespace AiRadar.Core.Tests.TestSupport;

/// <summary>Answers every chat request with a scripted reply and records what was asked.</summary>
internal sealed class FakeChatClient(Func<IReadOnlyList<ChatMessage>, string> reply) : IChatClient
{
    public List<IReadOnlyList<ChatMessage>> Requests { get; } = [];

    public List<ChatOptions?> Options { get; } = [];

    public Exception? FailWith { get; set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var list = messages.ToList();
        Requests.Add(list);
        Options.Add(options);
        return FailWith is null
            ? Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, reply(list))))
            : Task.FromException<ChatResponse>(FailWith);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
