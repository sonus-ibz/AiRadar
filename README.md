# AI Radar

A free Windows desktop app that tracks new AI tools. Open it and it checks for AI products launched since your last visit, then keeps them in a local catalogue you can browse.

> Status: Phase 1 of 5. Product Hunt is connected; more sources, "good at" categories and an installer are on the way.

## What works today

- Checks Product Hunt's "Artificial Intelligence" topic each time the app opens, or when you select **Check now**.
- First run loads the last 7 days; later runs fetch only what is new since the last successful check.
- Saves everything to a local SQLite database, so the list is there even offline.
- Marks tools found during the current session as **NEW**.
- Stores your Product Hunt token encrypted with Windows DPAPI, readable only by your Windows account.

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
2. **All sources:** Hacker News and Reddit (products feed); Hugging Face Spaces and GitHub (developer feed, strictly filtered)
3. **"Good at":** categories from a local model through Ollama, with a keyword fallback
4. **The UI:** category sidebar, search, detail panel, 12-month backfill, Windows notifications
5. **Ship:** installer with auto-update through GitHub Releases

## Licence

MIT
