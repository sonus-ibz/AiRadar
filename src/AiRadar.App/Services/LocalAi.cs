using System.Net.Http;
using System.Net.Http.Json;

namespace AiRadar.App.Services;

public enum LocalAiState
{
    NotRunning,
    ModelMissing,
    Ready,
}

/// <summary>Checks whether Ollama is running on this PC and has the model AI Radar uses.</summary>
public sealed class LocalAi(HttpClient http)
{
    public const string BaseAddress = "http://localhost:11434/";
    public const string Model = "llama3.2:3b";

    public async Task<LocalAiState> CheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tags = await http.GetFromJsonAsync<TagList>("api/tags", cancellationToken);
            return tags?.Models.Any(m => m.Name == Model || m.Name == Model + ":latest") == true
                ? LocalAiState.Ready
                : LocalAiState.ModelMissing;
        }
        catch (HttpRequestException)
        {
            return LocalAiState.NotRunning;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // Timed out: Ollama is not answering.
            return LocalAiState.NotRunning;
        }
    }

    private sealed record TagList(List<ModelInfo> Models);

    private sealed record ModelInfo(string Name);
}
