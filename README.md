# AI Radar

A free Windows desktop app that tracks new AI tools. Open it and it checks for AI products launched since your last visit, then keeps them in a local catalogue you can browse.

> Status: Phase 3 of 5. Four sources are connected and every tool is sorted by what it is good at; the full UI and an installer are on the way.

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

### "Good at" categories

Every tool is sorted into one or two of 15 categories (Coding, Agents & automation, Chat & assistants, Writing, Image, Video, Voice & audio, Music, Research & search, Data & analytics, Design, Productivity, Marketing & sales, Education, Other). The sidebar shows how many tools each category has in the current feed; select one to filter.

- **Keywords (default):** every new tool is sorted by keyword rules as soon as it is saved. Works offline with nothing installed.
- **Local AI (experimental, off by default):** with [Ollama](https://ollama.com) running `llama3.2:3b` and the option ticked in Settings, the tools you can see are re-sorted by the model, newest first, with progress shown at the top. It runs entirely on your PC and costs nothing. The model is reached through `Microsoft.Extensions.AI`'s `IChatClient`, so another model or provider can be swapped in. Turning the option off puts AI-sorted tools back on keyword categories.
- The AI's answer is constrained to the category list with a JSON schema and checked again in code; a missing or invalid answer keeps the keyword result.

#### Why AI sorting is off by default

`eval/` holds 95 real tools from the four sources, each labelled by hand with the expected category and, where two answers are fair, the acceptable alternatives. `dotnet run --project eval/AiRadar.Eval` measures each approach:

| Approach | Exact | Exact or acceptable |
|---|---|---|
| Keyword rules | 60% | **84%** |
| Local AI, `llama3.2:3b` | 58% | 76% |
| Hybrid: keywords, AI only when keywords find nothing | 56% | 80% |

The 3B model overuses "Coding" and "Agents & automation" and confuses image-to-video tools with image tools; the hybrid replaces correct "Other" answers with wrong categories. So the more accurate keyword rules stay the default. The prompt was improved once using this same set, so the AI figures are, if anything, slightly optimistic. The AI takes about 6 seconds per tool on a laptop CPU. A larger local model is the next thing to measure.

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
| AI | Ollama (`llama3.2:3b`) through `Microsoft.Extensions.AI` and OllamaSharp, JSON-schema constrained output |
| Tests | xUnit, real in-memory SQLite, recorded real API responses, fake `IChatClient`, `FakeTimeProvider` |
| CI | GitHub Actions on `windows-latest`, warnings treated as errors |

```
src/AiRadar.Core         sources, sync, categories, database, queries (no UI)
src/AiRadar.App          WPF app, token storage, local AI check, dependency injection
tests/AiRadar.Core.Tests unit and database tests
eval/                    95 hand-labelled real tools and an accuracy runner
```

Each source implements `IToolSource`. `SyncService` asks each source for tools since its last successful check, removes duplicates, updates scores of known tools and records every run, so one failing source never blocks the others.

## Roadmap

1. **Skeleton:** Product Hunt end to end, local catalogue, CI *(done)*
2. **All sources:** Hacker News (products feed); Hugging Face Spaces and GitHub (developer feed); duplicate detection; minimums in Settings *(done)*
3. **"Good at":** categories from a local model through Ollama, with a keyword fallback, and a category sidebar *(done)*
4. **The UI:** search, detail panel, 12-month backfill, Windows notifications
5. **Ship:** installer with auto-update through GitHub Releases

## Licence

MIT
