using System.Collections.ObjectModel;
using System.Diagnostics;
using AiRadar.App.Services;
using AiRadar.Core.Data;
using AiRadar.Core.Sync;
using AiRadar.Core.Tools;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AiRadar.App.ViewModels;

public sealed partial class MainViewModel(
    IServiceScopeFactory scopes,
    TokenStore tokens,
    SettingsStore settingsStore,
    TimeProvider time,
    ILogger<MainViewModel> logger) : ObservableObject
{
    private DateTime _openedAtUtc;
    private AppSettings _settings = new();

    public ObservableCollection<ToolRow> Tools { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    [ObservableProperty]
    public partial bool IsDeveloperFeed { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Starting…";

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

    public async Task InitializeAsync()
    {
        _openedAtUtc = time.GetUtcNow().UtcDateTime;
        _settings = settingsStore.Load();
        ShowSettingsValues();
        IsSettingsOpen = !tokens.HasToken;

        try
        {
            await using (var scope = scopes.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<AiRadarDbContext>().Database.MigrateAsync();
            }

            await LoadToolsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not open the local catalogue");
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

        tokens.Save(token);
        IsSettingsOpen = false;
        await CheckNowAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    private async Task CheckNowAsync()
    {
        IsBusy = true;
        StatusText = "Checking Product Hunt, Hacker News, Hugging Face and GitHub…";
        try
        {
            SyncSummary summary;
            await using (var scope = scopes.CreateAsyncScope())
            {
                summary = await scope.ServiceProvider.GetRequiredService<SyncService>().SyncAllAsync(CancellationToken.None);
            }

            await LoadToolsAsync();

            var status = $"Last check {time.GetLocalNow():HH:mm} · {summary.NewToolCount} new";
            StatusText = summary.Failures.Count == 0
                ? status
                : status + " · " + string.Join(" ", summary.Failures.Select(f => f.Message));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Check failed");
            StatusText = "The check failed. Details are in the log.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCheckNow() => !IsBusy;

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

    partial void OnIsDeveloperFeedChanged(bool value) => _ = ReloadAfterFeedChangeAsync();

    private async Task ReloadAfterFeedChangeAsync()
    {
        try
        {
            await LoadToolsAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Could not load the selected feed");
            StatusText = "Could not load this feed. Details are in the log.";
        }
    }

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
        settingsStore.Save(_settings);
        StatusText = "Filters saved.";
        await LoadToolsAsync();
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

    private void ShowSettingsValues()
    {
        ProductHuntMinVotes = _settings.ProductHuntMinVotes;
        HackerNewsMinPoints = _settings.HackerNewsMinPoints;
        HuggingFaceMinLikes = _settings.HuggingFaceMinLikes;
        GitHubMinStars = _settings.GitHubMinStars;
    }

    private async Task LoadToolsAsync()
    {
        var filter = IsDeveloperFeed ? _settings.DevelopersFilter() : _settings.ProductsFilter();
        IReadOnlyList<ToolListItem> items;
        int total;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ToolCatalog>();
            items = await catalog.GetLatestAsync(filter, 0, ToolCatalog.MaxPageSize, CancellationToken.None);
            total = await catalog.CountAsync(filter, CancellationToken.None);
        }

        var today = time.GetLocalNow().Date;
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
                item.FirstSeenAtUtc >= _openedAtUtc));
        }

        var newCount = Tools.Count(t => t.IsNew);
        SummaryText = $"{total:N0} tools in this feed · {newCount} new since you opened the app";
    }

    private static string DescribeDay(DateTime day, DateTime today) => (today - day).Days switch
    {
        0 => "Today",
        1 => "Yesterday",
        < 7 and > 1 and var days => $"{days} days ago",
        _ => day.ToString("dd MMM yyyy"),
    };
}
