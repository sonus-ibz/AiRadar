using System.IO;
using System.Windows;
using AiRadar.App.Services;
using AiRadar.App.ViewModels;
using AiRadar.Core.Data;
using AiRadar.Core.Sources;
using AiRadar.Core.Sync;
using AiRadar.Core.Tools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AiRadar.App;

public partial class App : Application
{
    private IHost? _host;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Directory.CreateDirectory(AppPaths.DataFolder);

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(TimeProvider.System);
                services.AddSingleton(new TokenStore(AppPaths.TokenFile));
                services.AddSingleton(new SettingsStore(AppPaths.SettingsFile));
                services.AddDbContext<AiRadarDbContext>(options =>
                    options.UseSqlite($"Data Source={AppPaths.DatabaseFile}"));

                services.AddTransient<ProductHuntAuthHandler>();
                services.AddHttpClient<ProductHuntSource>(client =>
                    {
                        client.BaseAddress = new Uri("https://api.producthunt.com/");
                        client.DefaultRequestHeaders.UserAgent.ParseAdd("AiRadar/0.1");
                    })
                    .AddHttpMessageHandler<ProductHuntAuthHandler>()
                    .AddStandardResilienceHandler();
                AddPublicSource<HackerNewsSource>(services, "https://hn.algolia.com/");
                AddPublicSource<HuggingFaceSpacesSource>(services, "https://huggingface.co/");
                AddPublicSource<GitHubSource>(services, "https://api.github.com/");

                // Sync order decides which copy of a tool found on two sources counts as the original.
                services.AddTransient<IToolSource>(sp => sp.GetRequiredService<ProductHuntSource>());
                services.AddTransient<IToolSource>(sp => sp.GetRequiredService<HackerNewsSource>());
                services.AddTransient<IToolSource>(sp => sp.GetRequiredService<HuggingFaceSpacesSource>());
                services.AddTransient<IToolSource>(sp => sp.GetRequiredService<GitHubSource>());

                services.AddScoped<SyncService>();
                services.AddScoped<ToolCatalog>();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<MainWindow>();
            })
            .Build();

        _host.Services.GetRequiredService<MainWindow>().Show();
    }

    private static void AddPublicSource<TSource>(IServiceCollection services, string baseAddress)
        where TSource : class =>
        services.AddHttpClient<TSource>(client =>
            {
                client.BaseAddress = new Uri(baseAddress);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AiRadar/0.2 (+https://github.com/sonus-ibz/AiRadar)");
            })
            .AddStandardResilienceHandler();

    protected override void OnExit(ExitEventArgs e)
    {
        _host?.Dispose();
        base.OnExit(e);
    }
}
