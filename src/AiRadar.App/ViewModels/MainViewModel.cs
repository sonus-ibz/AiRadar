using System.Collections.ObjectModel;
using System.Diagnostics;
using AiRadar.App.Services;
using AiRadar.Core.Categories;
using AiRadar.Core.Data;
using AiRadar.Core.Sync;
using AiRadar.Core.Tools;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiRadar.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>How many tools per feed the local AI re-sorts in one pass, newest first.</summary>
    private const int AiBatchPerFeed = 300;

    private readonly IServiceScopeFactory _scopes;
    private readonly TokenStore _tokens;
    private readonly SettingsStore _settingsStore;
    private readonly LocalAi _localAi;
    private readonly TimeProvider _time;
    private readonly ILogger<MainViewModel> _logger;

    private DateTime _openedAtUtc;
    private bool _catalogueReady;
    private AppSettings _settings = new();
    private CancellationTokenSource? _aiWork;
    private Task _aiTask = Task.CompletedTask;

    public MainViewModel(
        IServiceScopeFactory scopes,
        TokenStore tokens,
        SettingsStore settingsStore,
        LocalAi localAi,
        TimeProvider time,
        ILogger<MainViewModel> logger)
    {
        _scopes = scopes;
        _tokens = tokens;
        _settingsStore = settingsStore;
        _localAi = localAi;
        _time = time;
        _logger = logger;

        Categories = [new CategoryItem(CategoryItem.AllKey, "All"), .. ToolCategories.Names.Select(n => new CategoryItem(n, n))];
        SelectedCategory = Categories[0];
    }

    public ObservableCollection<ToolRow> Tools { get; } = [];

    public IReadOnlyList<CategoryItem> Categories { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    [ObservableProperty]
    public partial bool IsDeveloperFeed { get; set; }

    [ObservableProperty]
    public partial CategoryItem? SelectedCategory { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Starting…";

    [ObservableProperty]
    public partial string AiStatusText { get; set; } = "";

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    [ObservableProperty]
    public partial int ProductHuntMinVotes { get; set; }

    [ObservableProperty]
    public partial int HackerNewsMinPoints { get; set; }

    [ObservableProperty]
    public partial int HuggingFaceMinLikes { get; set; }

    [ObservableProperty]
    public partial int GitHubMinStars { get; set; }

    [ObservableProperty]
    public partial bool UseLocalAi { get; set; }

    public async Task InitializeAsync()
    {
        _openedAtUtc = _time.GetUtcNow().UtcDateTime;
        _settings = _settingsStore.Load();
        ShowSettingsValues();
        IsSettingsOpen = !_tokens.HasToken;

        try
        {
            await using (var scope = _scopes.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<AiRadarDbContext>().Database.MigrateAsync();
                await scope.ServiceProvider.GetRequiredService<CategorizationService>().CategorizeWithKeywordsAsync(CancellationToken.None);
            }

            _catalogueReady = true;
            await LoadToolsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not open the local catalogue");
            StatusText = "Could not open the local catalogue. Details are in the log.";
            return;
        }

        await CheckNowAsync();
    }

    public async Task SaveTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            StatusText = "Paste your Product Hunt developer token, then select Save.";
            return;
        }

        _tokens.Save(token);
        IsSettingsOpen = false;
        await CheckNowAsync();
    }

    /// <summary>Called when the app closes, so the AI pass stops between tools instead of mid-write.</summary>
    public void StopBackgroundWork() => _aiWork?.Cancel();

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    private async Task CheckNowAsync()
    {
        IsBusy = true;
        StatusText = "Checking Product Hunt, Hacker News, Hugging Face and GitHub…";
        try
        {
            // One writer at a time: the AI pass pauses while new tools are saved.
            await StopAiAsync();

            SyncSummary summary;
            await using (var scope = _scopes.CreateAsyncScope())
            {
                summary = await scope.ServiceProvider.GetRequiredService<SyncService>().SyncAllAsync(CancellationToken.None);
                await scope.ServiceProvider.GetRequiredService<CategorizationService>().CategorizeWithKeywordsAsync(CancellationToken.None);
            }

            await LoadToolsAsync();

            var status = $"Last check {_time.GetLocalNow():HH:mm} · {summary.NewToolCount} new";
            StatusText = summary.Failures.Count == 0
                ? status
                : status + " · " + string.Join(" ", summary.Failures.Select(f => f.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Check failed");
            StatusText = "The check failed. Details are in the log.";
        }
        finally
        {
            IsBusy = false;
        }

        StartAi();
    }

    private bool CanCheckNow() => !IsBusy;

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    partial void OnIsDeveloperFeedChanged(bool value) => _ = ReloadSafelyAsync();

    partial void OnSelectedCategoryChanged(CategoryItem? value) => _ = ReloadSafelyAsync();

    [RelayCommand]
    private async Task SaveFiltersAsync()
    {
        if (ProductHuntMinVotes < 0 || HackerNewsMinPoints < 0 || HuggingFaceMinLikes < 0 || GitHubMinStars < 0)
        {
            StatusText = "Minimums must be 0 or more.";
            return;
        }

        _settings.ProductHuntMinVotes = ProductHuntMinVotes;
        _settings.HackerNewsMinPoints = HackerNewsMinPoints;
        _settings.HuggingFaceMinLikes = HuggingFaceMinLikes;
        _settings.GitHubMinStars = GitHubMinStars;
        _settings.UseLocalAi = UseLocalAi;
        _settingsStore.Save(_settings);
        StatusText = "Settings saved.";
        await LoadToolsAsync();
        await StopAiAsync();
        StartAi();
    }

    [RelayCommand]
    private void OpenTool(ToolRow? tool)
    {
        if (tool is null
            || !Uri.TryCreate(tool.Url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    private void StartAi()
    {
        _aiWork = new CancellationTokenSource();
        _aiTask = RefineWithAiAsync(_aiWork.Token);
    }

    private async Task StopAiAsync()
    {
        _aiWork?.Cancel();
        try
        {
            await _aiTask;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task RefineWithAiAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!_settings.UseLocalAi)
            {
                int reset;
                await using (var scope = _scopes.CreateAsyncScope())
                {
                    reset = await scope.ServiceProvider.GetRequiredService<CategorizationService>().ResetToKeywordsAsync(cancellationToken);
                }

                if (reset > 0)
                {
                    await LoadToolsAsync();
                }

                AiStatusText = "Sorted by keywords. Local AI sorting is off; you can try it in Settings.";
                return;
            }

            switch (await _localAi.CheckAsync(cancellationToken))
            {
                case LocalAiState.NotRunning:
                    AiStatusText = "Sorted by keywords. Install and start Ollama (ollama.com) for smarter sorting with free local AI.";
                    return;
                case LocalAiState.ModelMissing:
                    AiStatusText = $"Sorted by keywords. Run \"ollama pull {LocalAi.Model}\" once to turn on local AI sorting.";
                    return;
            }

            var refined = 0;
            foreach (var (label, filter) in new[] { ("Products", _settings.ProductsFilter()), ("For developers", _settings.DevelopersFilter()) })
            {
                var progress = new Progress<(int Done, int Total)>(p =>
                    AiStatusText = $"Local AI is sorting {label}: {p.Done} of {p.Total}…");

                AiRefineResult result;
                await using (var scope = _scopes.CreateAsyncScope())
                {
                    result = await scope.ServiceProvider.GetRequiredService<CategorizationService>()
                        .RefineWithAiAsync(filter, AiBatchPerFeed, progress, cancellationToken);
                }

                if (result.AiUnavailable)
                {
                    AiStatusText = "Local AI stopped answering; the rest stay sorted by keywords.";
                    return;
                }

                refined += result.Refined;
                await LoadToolsAsync();
            }

            AiStatusText = refined == 0
                ? $"Sorted by local AI ({LocalAi.Model})."
                : $"Sorted by local AI ({LocalAi.Model}). {refined} tools re-sorted just now.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Local AI sorting failed");
            AiStatusText = "Local AI sorting failed; tools stay sorted by keywords. Details are in the log.";
        }
    }

    private async Task ReloadSafelyAsync()
    {
        if (!_catalogueReady)
        {
            return;
        }

        try
        {
            await LoadToolsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load the selected view");
            StatusText = "Could not load this view. Details are in the log.";
        }
    }

    private void ShowSettingsValues()
    {
        ProductHuntMinVotes = _settings.ProductHuntMinVotes;
        HackerNewsMinPoints = _settings.HackerNewsMinPoints;
        HuggingFaceMinLikes = _settings.HuggingFaceMinLikes;
        GitHubMinStars = _settings.GitHubMinStars;
        UseLocalAi = _settings.UseLocalAi;
    }

    private async Task LoadToolsAsync()
    {
        var feed = IsDeveloperFeed ? _settings.DevelopersFilter() : _settings.ProductsFilter();
        var category = SelectedCategory is null || SelectedCategory.Key == CategoryItem.AllKey ? null : SelectedCategory.Key;
        var filter = feed with { Category = category };

        IReadOnlyList<ToolListItem> items;
        IReadOnlyDictionary<string, int> counts;
        int feedTotal;
        int shownTotal;
        await using (var scope = _scopes.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ToolCatalog>();
            items = await catalog.GetLatestAsync(filter, 0, ToolCatalog.MaxPageSize, CancellationToken.None);
            counts = await catalog.CountByCategoryAsync(feed, CancellationToken.None);
            feedTotal = await catalog.CountAsync(feed, CancellationToken.None);
            shownTotal = category is null ? feedTotal : await catalog.CountAsync(filter, CancellationToken.None);
        }

        foreach (var item in Categories)
        {
            item.Count = item.Key == CategoryItem.AllKey ? feedTotal : counts.GetValueOrDefault(item.Key);
        }

        var today = _time.GetLocalNow().Date;
        Tools.Clear();
        foreach (var item in items)
        {
            Tools.Add(new ToolRow(
                item.Name,
                item.Tagline,
                item.Url,
                item.Source,
                item.Score,
                DescribeDay(item.LaunchedAtUtc.ToLocalTime().Date, today),
                item.FirstSeenAtUtc >= _openedAtUtc,
                new[] { item.PrimaryCategory, item.SecondaryCategory }.OfType<string>().ToList()));
        }

        var newCount = Tools.Count(t => t.IsNew);
        SummaryText = $"{shownTotal:N0} tools · {newCount} new since you opened the app";
    }

    private static string DescribeDay(DateTime day, DateTime today) => (today - day).Days switch
    {
        0 => "Today",
        1 => "Yesterday",
        < 7 and > 1 and var days => $"{days} days ago",
        _ => day.ToString("dd MMM yyyy"),
    };
}

public sealed partial class CategoryItem(string key, string label) : ObservableObject
{
    public const string AllKey = "";

    public string Key { get; } = key;

    public string Label { get; } = label;

    [ObservableProperty]
    public partial int Count { get; set; }
}
