# Implementation plan — `jobmatch`

Companion to `PRD.md`. Contains the `.NET`-specific architecture, library choices, project layout, and build phases. `PRD.md` defines **what** the tool does; this doc defines **how** it's built.

---

## 1. Tech stack (locked)

| Concern | Choice | Why |
|---|---|---|
| Runtime | **.NET 10** | LTS (released 2025-11, supported to 2028-11). |
| Language | C# 13+ | Pairs with .NET 10. |
| CLI framework | **Spectre.Console.Cli** | Class-based command tree; integrates with Spectre.Console. |
| Rich output (tables, markup, prompts) | **Spectre.Console** | Bundled with the CLI framework. |
| YAML parsing | **YamlDotNet** | De facto standard on .NET. |
| RSS / Atom | **CodeHollow.FeedReader** | Simple; handles RSS 2.0 and Atom. |
| HTTP | `HttpClient` (built-in) | `GetFromJsonAsync` covers the API adapter cleanly. |
| JSON | `System.Text.Json` (built-in) | No Newtonsoft required. |
| Browser automation | **Microsoft.Playwright** | Phase 5 only; not referenced until then. |
| Test framework | **xUnit** | Idiomatic on .NET; built-in asserts are sufficient. |
| Logging | `Microsoft.Extensions.Logging.Abstractions` + Spectre sink | Lightweight, no ILogger lock-in. |
| Package management | **Central Package Management** (`Directory.Packages.props`) | Single version manifest across projects. |

Package versions are pinned during Phase 1 scaffolding and recorded in `Directory.Packages.props`. No floating versions.

---

## 2. Solution layout

Matches PRD §5 responsibilities. Two source projects (library + CLI) plus a test project.

```
jobmatch/
├── Jobmatch.sln
├── Directory.Build.props
├── Directory.Packages.props
├── PRD.md
├── implementation-plan.md
├── README.md
├── .gitignore
├── .claude/
│   └── commands/
│       ├── generate-skillset.md
│       ├── generate-job-listings.md
│       └── verify-config.md
├── config/
│   ├── skillset.example.md       (committed)
│   ├── portals.example.yml       (committed)
│   └── ranking.yml               (committed)
├── src/
│   ├── Jobmatch/                 (class library, net10.0)
│   │   ├── Jobmatch.csproj
│   │   ├── Models/
│   │   │   ├── Skillset.cs
│   │   │   ├── PortalConfig.cs
│   │   │   ├── Listing.cs
│   │   │   ├── Match.cs
│   │   │   └── MatchReasoning.cs
│   │   ├── Configuration/
│   │   │   ├── SkillsetParser.cs
│   │   │   ├── PortalConfigLoader.cs
│   │   │   └── RankingConfigLoader.cs
│   │   ├── Adapters/
│   │   │   ├── IJobPortalAdapter.cs
│   │   │   ├── BaseAdapter.cs
│   │   │   ├── ApiAdapter.cs
│   │   │   ├── RssAdapter.cs
│   │   │   ├── ManualAdapter.cs
│   │   │   └── HtmlAdapter.cs            (Phase 5 only)
│   │   ├── Ranking/
│   │   │   └── Ranker.cs
│   │   ├── Deduplication/
│   │   │   └── Deduper.cs
│   │   ├── Output/
│   │   │   ├── JsonReportWriter.cs
│   │   │   └── MarkdownReportWriter.cs
│   │   └── Verification/
│   │       └── ConfigVerifier.cs
│   └── Jobmatch.Cli/             (console exe, net10.0)
│       ├── Jobmatch.Cli.csproj
│       ├── Program.cs
│       └── Commands/
│           ├── SkillsetCommand.cs
│           ├── ListingsCommand.cs
│           └── VerifyCommand.cs
├── tests/
│   └── Jobmatch.Tests/
│       ├── Jobmatch.Tests.csproj
│       ├── Fixtures/
│       │   ├── sample-skillset.md
│       │   ├── sample-listings.json
│       │   └── malformed-skillset.md
│       ├── Configuration/
│       │   └── SkillsetParserTests.cs
│       ├── Deduplication/
│       │   └── DeduperTests.cs
│       └── Ranking/
│           └── RankerTests.cs
└── data/                         (gitignored, created on first run)
    ├── imports/
    ├── raw/
    ├── all_listings.json
    ├── ranked_listings.json
    ├── top_jobs.md
    └── verification-report.md
```

**Project references**
- `Jobmatch.Cli` → `Jobmatch`
- `Jobmatch.Tests` → `Jobmatch` **and** `Jobmatch.Cli` — the CLI smoke test exercises `CliApp.Create()` directly, which is the simplest way to prove wiring without subprocess launches or `TestConsole` boilerplate.

---

## 3. `.gitignore` essentials

```
# .NET build output
bin/
obj/
*.user
.vs/

# User configs (examples are committed; active configs are not)
config/skillset.md
config/portals.yml

# Runtime data
data/

# Playwright browsers
.playwright/
```

---

## 4. Data models

C# records, immutable, with factory methods that validate and throw `ConfigException` on bad input.

```csharp
public enum RemotePreference { Onsite, Hybrid, Remote, Any }
public enum Seniority        { Junior, Mid, Senior, Lead, Any }
public enum RemoteMode       { Onsite, Hybrid, Remote, Unknown }
public enum PortalType       { Api, Rss, Html, Manual }

public sealed record Skillset(
    string Name,
    string Location,
    int ExperienceYears,
    IReadOnlyList<string> TargetRoles,
    RemotePreference RemotePreference,
    Seniority Seniority,
    IReadOnlyList<string> PrimaryStack,
    IReadOnlyList<string> SecondaryStack,
    IReadOnlyList<string> Domains,
    IReadOnlyList<string> Disqualifiers,
    IReadOnlyList<string> Languages,
    IReadOnlyList<string> EmploymentTypes);

public sealed record PortalConfig(
    string Name,
    PortalType Type,
    bool Enabled = true,
    Uri? BaseUrl = null,
    Uri? Endpoint = null,
    IReadOnlyDictionary<string, object?>? QueryParams = null,
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyDictionary<string, string>? ResponseMapping = null,
    double RateLimitRps = 1.0,
    string? Notes = null);

public sealed record Listing(
    string Id,                    // stable hash of (portal, source_id ?? url)
    string Portal,
    string Title,
    string? Company,
    string? Location,
    RemoteMode RemoteMode,
    string Description,           // plain text, HTML stripped
    Uri Url,
    DateTimeOffset? PostedAt,
    DateTimeOffset FetchedAt,
    JsonElement Raw);

public sealed record Match(
    Listing Listing,
    double Score,                 // 0.0–1.0
    IReadOnlyDictionary<string, double> Breakdown,
    MatchReasoning Reasoning);

public sealed record MatchReasoning(
    IReadOnlyList<string> PrimaryStackHits,
    IReadOnlyList<string> SecondaryStackHits,
    IReadOnlyList<string> DomainHits,
    bool? SeniorityMatch,         // null when unknown
    bool? LocationMatch,
    bool? RemoteMatch,
    IReadOnlyList<string> DisqualifierHits,
    string Notes);
```

Consumers never construct records directly from untrusted input — they go through a factory on the type (`Skillset.FromParsed(...)`, etc.) which raises clear errors listing the offending field.

---

## 5. Adapter contract

```csharp
public interface IJobPortalAdapter
{
    string PortalName { get; }
    Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default);
}

public abstract class BaseAdapter(PortalConfig config, HttpClient http, ILogger logger)
    : IJobPortalAdapter
{
    protected PortalConfig Config { get; } = config;
    protected HttpClient   Http   { get; } = http;
    protected ILogger      Logger { get; } = logger;

    public string PortalName => Config.Name;
    public abstract Task<IReadOnlyList<Listing>> FetchAsync(CancellationToken ct = default);
}
```

**Per-type behaviour**
- **`ApiAdapter`** — `HttpClient.GetFromJsonAsync`, walks the configured dotted `items_path`, applies `response_mapping` to map source fields onto `Listing` fields. Supports offset/page pagination if configured.
- **`RssAdapter`** — `FeedReader.ReadAsync(endpoint)` → map `FeedItem` to `Listing`.
- **`ManualAdapter`** — enumerates `data/imports/<portal-name>-*.csv` and `*.json`, normalises rows to `Listing`.
- **`HtmlAdapter`** (Phase 5) — Playwright. Only reachable if Playwright browsers are installed; otherwise logs a skip warning and returns empty.

**Graceful degradation** lives in `ListingsCommand`, not in the adapters. Adapters throw. The command wraps every adapter call in try/catch, logs `[WARN] portal=<name> error=<summary>`, and continues with the rest.

---

## 6. Ranking formula

PRD §6 in math form:

```
score = w_primary   * fraction_of_primary_stack_matched
      + w_secondary * fraction_of_secondary_stack_matched
      + w_seniority * seniority_match_score        // 1.0 exact, 0.5 adjacent, 0.0 else
      + w_loc       * location_remote_match_score  // 1.0 full, 0.5 partial, 0.0 none
      + w_domain    * fraction_of_domains_matched
      + w_fresh     * exp(-age_days / half_life)

if any disqualifier matches: score *= disqualifier_penalty  // default 0.0
if score < min_score_to_include: drop
return top_n (sorted desc)
```

**Seniority adjacency:** `junior ↔ mid`, `mid ↔ senior`, `senior ↔ lead`. `any` in either the listing or the skillset scores 1.0.

**Keyword matching:** case-insensitive, whole-word (regex `\b<kw>\b` with escape), applied to `title + "\n" + description`.

**Reasoning:** no inventions. When a signal can't be evaluated (missing location, unknown seniority), the corresponding boolean is `null` and the notes sentence says so.

---

## 7. CLI wiring (Spectre.Console.Cli)

`Program.cs`:

```csharp
var app = new CommandApp();
app.Configure(config =>
{
    config.SetApplicationName("jobmatch");
    config.AddCommand<SkillsetCommand>("skillset")
          .WithDescription("Author or refresh the active skillset profile");
    config.AddCommand<ListingsCommand>("listings")
          .WithDescription("Fetch, deduplicate, rank, and write the top-N shortlist");
    config.AddCommand<VerifyCommand>("verify")
          .WithDescription("Validate config files and portal connectivity");
});
return await app.RunAsync(args);
```

Each command inherits `AsyncCommand<Settings>` with a nested `Settings : CommandSettings` class exposing `[CommandOption]` flags.

---

## 8. Slash command → subcommand mapping

Each `.claude/commands/*.md` file is a Claude Code slash command. It contains Claude-facing instructions (how to gather input, confirm before overwriting, summarise afterward) and **ends with the shell invocation** that does the real work.

| Slash command | CLI invocation |
|---|---|
| `/generate-skillset` | `dotnet run --project src/Jobmatch.Cli -- skillset [--from <url-or-path>]` |
| `/generate-job-listings` | `dotnet run --project src/Jobmatch.Cli -- listings [--portal <name>] [--top <n>]` |
| `/verify-config` | `dotnet run --project src/Jobmatch.Cli -- verify` |

Identical behaviour when invoked directly from the terminal — the slash command is a thin wrapper.

---

## 9. Build phases

Every phase ends with: `dotnet build` clean, `dotnet test` green, pause for user diff review. No phase skips ahead.

### Phase 1 — Skeleton
**Deliverable:** `dotnet run --project src/Jobmatch.Cli -- --help` lists three subcommands and exits cleanly; `dotnet test` passes the smoke test.

- `Jobmatch.sln`, `Directory.Build.props`, `Directory.Packages.props`, `.gitignore`
- Three project files (`Jobmatch`, `Jobmatch.Cli`, `Jobmatch.Tests`)
- All record types and enums in `Models/` — complete, we don't revisit them
- Three commands as no-op shells that print `"<sub> not yet implemented"`
- `config/skillset.example.md`, `config/portals.example.yml`, `config/ranking.yml` (committed, generic)
- `.claude/commands/*.md` stubs — valid slash commands that shell out correctly, minimal prompt text
- `README.md` with install + quickstart
- One smoke test: `Cli_Help_Lists_All_Subcommands` that runs the CLI in-process and asserts the help output names all three

### Phase 2 — Skillset + verify (no connectivity)
**Deliverable:** `skillset` writes a valid active skillset (interactive or from `--from`). `verify` runs every structural check and writes a report. Both idempotent.

- `SkillsetParser` — frontmatter via YamlDotNet, body scanner for `## Section` + bullet extraction
- `PortalConfigLoader`, `RankingConfigLoader`
- `ConfigVerifier` implementing FR-016 checks 1, 2, 3, 4, 6, 7 (connectivity deferred to Phase 5)
- `SkillsetCommand`: Spectre prompts per field; `--from` branch fetches (HttpClient) or reads and drafts
- `VerifyCommand`: writes `data/verification-report.md`, stdout summary, non-zero exit on fail
- Tests:
  - `SkillsetParserTests` — happy path, missing required field, malformed YAML, empty primary stack, unknown section tolerated, section with no bullets
  - `ConfigVerifierTests` — missing config file, bad YAML, weights not summing to 1

### Phase 3 — Adapters + listings (no ranking yet)
**Deliverable:** `listings` fetches from `jobnet.dk` via the public API, deduplicates, writes `all_listings.json` and raw files. Ranker not yet wired — listings emit unranked.

- `IJobPortalAdapter`, `BaseAdapter`
- `ApiAdapter`, `RssAdapter`, `ManualAdapter`
- `Deduper` — URL first, then normalised (title + company)
- `JsonReportWriter` (raw per-portal files + `all_listings.json`)
- `MarkdownReportWriter` skeleton (listings only, no scores)
- `ListingsCommand` orchestrating adapters + deduper + writers, wrapping each adapter call for FR-019
- Tests:
  - `DeduperTests` — identical URL, title+company case variants, near-dupes, cross-portal merges
  - `ApiAdapterTests` — happy path with mocked `HttpMessageHandler` and a fixture JSON response

### Phase 4 — Ranking
**Deliverable:** `listings` produces a ranked `top_jobs.md` and `ranked_listings.json`. Reasoning populated truthfully. All three personas (§2 of PRD) produce sensible rankings against fixture listings.

- `Ranker` implementing §6 exactly
- `MatchReasoning` populated; unknown signals → `null`
- `MarkdownReportWriter` upgraded (table with score, breakdown, reasoning, link)
- Wire ranker into `ListingsCommand`
- Tests:
  - `RankerTests` with fixture skillsets for all three personas and fixture listings covering:
    - strong match (full primary, seniority & remote match)
    - partial match (half primary, no domain)
    - disqualifier hit → score 0
    - stale listing (180 days old) → freshness component near zero
    - `any` seniority / `any` remote preference → signal neutral
  - Sanity check: every score ∈ [0, 1]

### Phase 5 — Polish
**Deliverable:** all PRD §9 acceptance criteria pass end-to-end against real `jobnet.dk`.

- `HtmlAdapter` using Microsoft.Playwright; csproj reference added this phase
- Runtime guard: if Playwright browsers aren't installed, `HtmlAdapter` logs an install instruction and returns empty — never crashes
- FR-016 check 5 (connectivity) added to `ConfigVerifier`: minimal GET per enabled API/RSS portal, short timeout
- `top_jobs.md` formatting polish: markdown table, clickable links, per-match reasoning
- `.claude/commands/*.md` fleshed out: confirm-before-overwrite, richer prompts, summary of what was written

---

## 10. Test strategy

- Unit tests only in v1. No live-portal calls in CI.
- `dotnet test` total runtime target: under ~5 seconds.
- Fixture files under `tests/Jobmatch.Tests/Fixtures/`.
- After every phase: `dotnet build` → `dotnet test` → pause for diff review before starting the next phase.
- The Phase 1 smoke test stays in place from then on as a liveness check.

---

## 11. NuGet dependencies

Pinned in `Directory.Packages.props` at Phase 1 scaffolding. Latest stable compatible with .NET 10 at that time.

| Package | Project(s) | Added in phase |
|---|---|---|
| Spectre.Console | `Jobmatch`, `Jobmatch.Cli` | 1 |
| Spectre.Console.Cli | `Jobmatch.Cli` | 1 |
| YamlDotNet | `Jobmatch` | 1 (used from Phase 2) |
| CodeHollow.FeedReader | `Jobmatch` | 3 |
| Microsoft.Extensions.Logging.Abstractions | `Jobmatch` | 1 |
| xunit | `Jobmatch.Tests` | 1 |
| xunit.runner.visualstudio | `Jobmatch.Tests` | 1 |
| Microsoft.NET.Test.Sdk | `Jobmatch.Tests` | 1 |
| Microsoft.Playwright | `Jobmatch` | 5 |

---

## 12. Open questions (resolve at phase boundaries, not now)

- **Phase 2 — interactive skillset UX.** Spectre prompts per field, or open `$EDITOR` on a templated file? Default: Spectre prompts, with an optional "review in editor before writing" confirmation.
- **Phase 3 — dotted `items_path`.** How to traverse `"data.results.items"` on a `JsonElement`? Default: split on `.`, step-by-step traversal, clear error if a segment is missing.
- **Phase 5 — Playwright browsers.** The user runs `pwsh bin/Debug/net10.0/playwright.ps1 install` once. Document in README.
