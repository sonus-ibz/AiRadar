# AI Radar

A free Windows desktop app that tracks new AI tools. Open it and it checks for AI products launched since your last visit, then keeps them in a local catalogue you can browse.

> Status: Phase 2 of 5. Four sources are connected; "good at" categories and an installer are on the way.

## What works today

Two feeds, checked each time the app opens or when you select **Check now**:

| Feed | Source | What it finds | Default minimum |
|---|---|---|---|
| Products | Product Hunt | Launches in the "Artificial Intelligence" topic | 20 votes |
| Products | Hacker News | "Show HN" launches that mention AI | 10 points |
| For developers | Hugging Face | Trending Spaces (AI demo apps) created in the last 30 days | 50 likes |
| For developers | GitHub | Repos tagged `llm`, `ai-agents`, `generative-ai` or `ai`, created in the last 30 days | 200 stars |

- Minimums are editable in Settings. Tools below them are hidden, not deleted.
- Each check re-reads a recent window (1 day for Product Hunt, 2 for Hacker News, 30 for Hugging Face and GitHub), so scores keep updating and tools that become popular later still appear.
- The same tool found on two sources (same website, same repo link, or same name within 30 days) shows once per feed.
- If one source fails, the others still update and the reason is shown at the top.
- Everything is saved to a local SQLite database, so the list is there even offline.
- Your Product Hunt token is stored encrypted with Windows DPAPI, readable only by your Windows account. Without a token the other three sources still work.

Reddit was considered but left out: its data API now needs a login, and its RSS feeds carry no vote counts to filter noise with.

## Run it

Requirements: Windows 10 or 11 and the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```powershell
git clone <this repo>
cd AiRadar
dotnet run --project src/AiRadar.App
```

On first start the app asks for a Product Hunt developer token:

1. Sign in at <https://www.producthunt.com/v2/oauth/applications> and create an application (any redirect URL, such as `https://localhost`).
2. Copy the **Developer Token** and paste it into AI Radar's Settings.

The token never leaves your machine except in requests to Product Hunt. Product Hunt's API is free for non-commercial use.

Local data lives in `%LOCALAPPDATA%\AiRadar`.

## How it is built

| Part | Choice |
|---|---|
| UI | WPF on .NET 10, MVVM with CommunityToolkit.Mvvm |
| Storage | SQLite through EF Core, with migrations |
| HTTP | `IHttpClientFactory` with the standard resilience handler (retries, timeouts) |
| Tests | xUnit, real in-memory SQLite, recorded-shape API responses, `FakeTimeProvider` |
| CI | GitHub Actions on `windows-latest`, warnings treated as errors |

```
src/AiRadar.Core         sources, sync, database, queries (no UI)
src/AiRadar.App          WPF app, token storage, dependency injection
tests/AiRadar.Core.Tests unit and database tests
```

Each source implements `IToolSource`. `SyncService` asks each source for tools since its last successful check, removes duplicates, updates scores of known tools and records every run, so one failing source never blocks the others.

## Roadmap

1. **Skeleton:** Product Hunt end to end, local catalogue, CI *(done)*
2. **All sources:** Hacker News (products feed); Hugging Face Spaces and GitHub (developer feed); duplicate detection; minimums in Settings *(done)*
3. **"Good at":** categories from a local model through Ollama, with a keyword fallback
4. **The UI:** category sidebar, search, detail panel, 12-month backfill, Windows notifications
5. **Ship:** installer with auto-update through GitHub Releases

## Licence

MIT
