// Measures how often each sorter picks the right "good at" category on 95 hand-labelled real tools.
// Usage: dotnet run --project eval/AiRadar.Eval [-- <ollama model>]   (needs Ollama running for the AI column)
using System.Diagnostics;
using System.Text.Json;
using AiRadar.Core.Categories;
using OllamaSharp;

var model = args.Length > 0 ? args[0] : "llama3.2:3b";
var dataFile = Path.Combine(AppContext.BaseDirectory, "labelled-tools.json");
var tools = JsonSerializer.Deserialize<List<LabelledTool>>(File.ReadAllText(dataFile), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

var keywordScore = new Score();
var aiScore = new Score();
var hybridScore = new Score();
var aiMisses = new List<string>();
var ai = new AiCategoryClassifier(new OllamaApiClient(new Uri("http://localhost:11434/"), model));
var watch = Stopwatch.StartNew();

foreach (var tool in tools)
{
    var keyword = KeywordClassifier.Classify(tool.Name, tool.Tagline, tool.Description).Primary;
    keywordScore.Add(tool, keyword);

    var answer = await ai.ClassifyAsync(tool.Name, tool.Tagline, tool.Description, CancellationToken.None);
    var primary = answer?.Primary ?? "(no usable answer)";
    aiScore.Add(tool, primary);
    hybridScore.Add(tool, keyword == ToolCategories.Other && answer is not null ? primary : keyword);
    if (!tool.Accepts(primary))
    {
        aiMisses.Add($"  {tool.Name,-45} expected {tool.Expected,-20} got {primary}");
    }

    Console.Write($"\r{aiScore.Total} of {tools.Count}");
}

Console.WriteLine();
Console.WriteLine($"Tools: {tools.Count}   AI model: {model}   AI time: {watch.Elapsed.TotalSeconds / tools.Count:N1} s per tool");
Console.WriteLine($"                 exact   exact or acceptable");
Console.WriteLine($"Keywords        {keywordScore.Exact,4}%   {keywordScore.Lenient,4}%");
Console.WriteLine($"Local AI        {aiScore.Exact,4}%   {aiScore.Lenient,4}%");
Console.WriteLine($"Hybrid          {hybridScore.Exact,4}%   {hybridScore.Lenient,4}%   (keywords; AI only when keywords say Other)");
Console.WriteLine();
Console.WriteLine("AI misses:");
aiMisses.ForEach(Console.WriteLine);

internal sealed record LabelledTool(string Source, string Name, string Tagline, string Description, string Expected, List<string> AlsoOk)
{
    public bool Accepts(string category) => category == Expected || AlsoOk.Contains(category);
}

internal sealed class Score
{
    private int _exact;
    private int _lenient;

    public int Total { get; private set; }
    public int Exact => Total == 0 ? 0 : (int)Math.Round(100.0 * _exact / Total);
    public int Lenient => Total == 0 ? 0 : (int)Math.Round(100.0 * _lenient / Total);

    public void Add(LabelledTool tool, string category)
    {
        Total++;
        if (category == tool.Expected) _exact++;
        if (tool.Accepts(category)) _lenient++;
    }
}
