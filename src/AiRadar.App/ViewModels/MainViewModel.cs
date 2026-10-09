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
    TimeProvider time,
    ILogger<MainViewModel> logger) : ObservableObject
{
    private DateTime _openedAtUtc;

    public ObservableCollection<ToolRow> Tools { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    public partial bool NeedsToken { get; set; }

    [ObservableProperty]
    public partial bool IsSettingsOpen { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = "Starting…";

    [ObservableProperty]
    public partial string SummaryText { get; set; } = "";

    public async Task InitializeAsync()
    {
        _openedAtUtc = time.GetUtcNow().UtcDateTime;
        NeedsToken = !tokens.HasToken;
        IsSettingsOpen = NeedsToken;

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

        if (NeedsToken)
        {
            StatusText = "Add your free Product Hunt token to start checking for new AI tools.";
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
        NeedsToken = false;
        IsSettingsOpen = false;
        await CheckNowAsync();
    }

    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    private async Task CheckNowAsync()
    {
        IsBusy = true;
        StatusText = "Checking Product Hunt…";
        try
        {
            SyncSummary summary;
            await using (var scope = scopes.CreateAsyncScope())
            {
                summary = await scope.ServiceProvider.GetRequiredService<SyncService>().SyncAllAsync(CancellationToken.None);
            }

            await LoadToolsAsync();

            var checkedAt = time.GetLocalNow().ToString("HH:mm");
            StatusText = summary.Failures.Count == 0
                ? $"Last check {checkedAt} · {summary.NewToolCount} new"
                : string.Join(" ", summary.Failures.Select(f => f.Message));
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

    private bool CanCheckNow() => !IsBusy && !NeedsToken;

    [RelayCommand]
    private void ToggleSettings() => IsSettingsOpen = !IsSettingsOpen;

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

    private async Task LoadToolsAsync()
    {
        IReadOnlyList<ToolListItem> items;
        int total;
        await using (var scope = scopes.CreateAsyncScope())
        {
            var catalog = scope.ServiceProvider.GetRequiredService<ToolCatalog>();
            items = await catalog.GetLatestAsync(0, ToolCatalog.MaxPageSize, CancellationToken.None);
            total = await catalog.CountAsync(CancellationToken.None);
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
        SummaryText = $"{total:N0} tools in your catalogue · {newCount} new since you opened the app";
    }

    private static string DescribeDay(DateTime day, DateTime today) => (today - day).Days switch
    {
        0 => "Today",
        1 => "Yesterday",
        < 7 and > 1 and var days => $"{days} days ago",
        _ => day.ToString("dd MMM yyyy"),
    };
}
