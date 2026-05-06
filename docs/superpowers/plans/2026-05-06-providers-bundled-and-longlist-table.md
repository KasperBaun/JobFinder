# Providers as bundled JSON + Longlist filter table — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the provider catalog from per-user `data/<email>/portals.yml` into a bundled, read-only `portals.json` shipped with the app, with per-user state reduced to opt-outs and provider secrets. Replace the longlist tab's small ranking table with a filterable, sortable view.

**Architecture:** Catalog + overlay split. `PortalCatalogLoader` reads bundled JSON; `ProviderStateLoader` reads per-user `provider-state.json`; `ProviderStateMerger` combines them into the existing `IReadOnlyList<PortalConfig>` shape so `SearchService`/adapters are unchanged. The longlist table is a single `LonglistTable.tsx` component reading client-side from `data.scored`, with filter state encoded in the URL hash.

**Tech Stack:** C# 13 / .NET 10 (minimal APIs, xUnit, `System.Text.Json`, YamlDotNet for shim only); React 19 + TypeScript + Vite + react-router + react-query.

**Spec:** `docs/superpowers/specs/2026-05-06-providers-bundled-and-longlist-table-design.md`

---

## File map

### New
- `src/Jobmatch/Configuration/PortalCatalogLoader.cs` — JSON catalog loader.
- `src/Jobmatch/Configuration/ProviderState.cs` — overlay model (record).
- `src/Jobmatch/Configuration/ProviderStateLoader.cs` — read/write `provider-state.json`.
- `src/Jobmatch/Configuration/ProviderStateMerger.cs` — merges catalog + state → effective `PortalConfig` list.
- `src/Jobmatch/Configuration/PortalsMigrationShim.cs` — one-shot `portals.yml` → `provider-state.json`.
- `src/Jobmatch/Configuration/portals.json` — committed bundled catalog (data file).
- `src/Jobmatch.Tests/Configuration/PortalCatalogLoaderTests.cs`
- `src/Jobmatch.Tests/Configuration/ProviderStateTests.cs`
- `src/Jobmatch.Tests/Configuration/ProviderStateMergerTests.cs`
- `src/Jobmatch.Tests/Configuration/PortalsMigrationShimTests.cs`
- `src/Jobmatch.Gui/Client/src/components/LonglistTable.tsx` — filter table component.
- `src/Jobmatch.Gui/Client/src/components/LonglistTable.module.css` — scoped styles (or extend `app.css` — match what the codebase already uses; the codebase uses a single `app.css`, so follow that).

### Modify
- `src/Jobmatch/Models/PortalConfig.cs` — add `RequiresSecret`.
- `src/Jobmatch/Search/ScoredEntry.cs` — add `PrimaryStackHits`, `SecondaryStackHits`.
- `src/Jobmatch/Search/SearchService.cs` — call merger; map stack hits in `ToScoredEntry`.
- `src/Jobmatch/UserContext.cs` — add `ProviderStatePath`; stop seeding `portals.yml`.
- `src/Jobmatch.Gui/Server/Program.cs` — run migration shim once at startup.
- `src/Jobmatch.Gui/Server/Routes.cs` — drop Create/Delete; add Secrets.
- `src/Jobmatch.Gui/Server/Endpoints/ProvidersEndpoints.cs`
- `src/Jobmatch.Gui/Server/Handlers/ProvidersHandler.cs` — drop Create/Delete; narrow Update; add SetSecrets.
- `src/Jobmatch.Gui/Server/Models/ProviderSummary.cs` — add `requiresSecret`, `hasSecret`.
- `src/Jobmatch.Gui/Client/src/api/types.ts`
- `src/Jobmatch.Gui/Client/src/api/client.ts`
- `src/Jobmatch.Gui/Client/src/pages/ProvidersPage.tsx`
- `src/Jobmatch.Gui/Client/src/pages/ProviderDetailPage.tsx`
- `src/Jobmatch.Gui/Client/src/pages/HistoryPage.tsx`
- `src/Jobmatch.Gui/Client/src/App.tsx` — drop `/providers/new` route.
- `docs/requirements.md` — add R-085, R-086; tweak R-024.
- `todo.md` — close existing-user portal migration item; note one-shot shim.

### Delete
- `src/config/portals.example.yml` — replaced by `src/Jobmatch/Configuration/portals.json`.
- `src/Jobmatch.Tests/Configuration/PortalConfigLoaderTests.cs::Parse_*Yaml*` tests for fields no longer reachable. (Keep YAML parsing tests that exercise the migration shim path.)

---

## Phase A — Backend: catalog + overlay

### Task A1: Add `RequiresSecret` to `PortalConfig`

**Files:**
- Modify: `src/Jobmatch/Models/PortalConfig.cs`

- [ ] **Step 1: Add the field**

```csharp
public sealed record PortalConfig(
    string Name,
    PortalType Type,
    bool Enabled = true,
    Uri? Endpoint = null,
    IReadOnlyDictionary<string, object?>? QueryParams = null,
    IReadOnlyDictionary<string, string>? Headers = null,
    IReadOnlyDictionary<string, string>? ResponseMapping = null,
    HtmlSelectors? Html = null,
    double RateLimitRps = 1.0,
    string? Notes = null,
    IReadOnlyDictionary<string, string>? StaticFields = null,
    string? Method = null,
    IReadOnlyDictionary<string, object?>? BodyTemplate = null,
    PaginationConfig? Pagination = null,
    int Id = 0,
    string? RequiresSecret = null);
```

- [ ] **Step 2: Build to verify the existing tests still compile**

Run: `dotnet build src/Jobmatch.slnx`
Expected: `Build succeeded`. (Existing YAML loader doesn't yet set `RequiresSecret`, but the field is nullable and defaults to null, so all existing tests stay green.)

- [ ] **Step 3: Commit**

```bash
git add src/Jobmatch/Models/PortalConfig.cs
git commit -m "PortalConfig: add RequiresSecret field"
```

---

### Task A2: `PortalCatalogLoader` — JSON loader (TDD)

**Files:**
- Create: `src/Jobmatch.Tests/Configuration/PortalCatalogLoaderTests.cs`
- Create: `src/Jobmatch/Configuration/PortalCatalogLoader.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

public sealed class PortalCatalogLoaderTests
{
    [Fact]
    public void Parse_MinimalProvider()
    {
        var json = """
            {
              "version": 1,
              "providers": [
                {
                  "id": 1,
                  "name": "greenhouse-pleo",
                  "type": "api",
                  "enabled": true,
                  "endpoint": "https://boards-api.greenhouse.io/v1/boards/pleo/jobs",
                  "queryParams": { "content": "true" },
                  "responseMapping": {
                    "items_path": "jobs",
                    "id": "id",
                    "title": "title",
                    "url": "absolute_url"
                  },
                  "staticFields": { "company": "Pleo" },
                  "rateLimitRps": 1.0
                }
              ]
            }
            """;

        var portals = PortalCatalogLoader.Parse(json);

        Assert.Single(portals);
        var p = portals[0];
        Assert.Equal(1, p.Id);
        Assert.Equal("greenhouse-pleo", p.Name);
        Assert.Equal(PortalType.Api, p.Type);
        Assert.True(p.Enabled);
        Assert.Equal("https://boards-api.greenhouse.io/v1/boards/pleo/jobs", p.Endpoint?.ToString());
        Assert.Equal("true", p.QueryParams!["content"]?.ToString());
        Assert.Equal("Pleo", p.StaticFields!["company"]);
        Assert.Null(p.RequiresSecret);
    }

    [Fact]
    public void Parse_ProviderRequiringSecret()
    {
        var json = """
            {
              "version": 1,
              "providers": [
                {
                  "id": 5,
                  "name": "jooble",
                  "type": "api",
                  "enabled": true,
                  "endpoint": "https://jooble.org/api/{api_key}",
                  "method": "post",
                  "bodyTemplate": { "keywords": "developer" },
                  "queryParams": { "api_key": "" },
                  "responseMapping": { "items_path": "jobs", "id": "id", "title": "title", "url": "link" },
                  "rateLimitRps": 1.0,
                  "requiresSecret": "api_key"
                }
              ]
            }
            """;

        var portals = PortalCatalogLoader.Parse(json);
        Assert.Equal("api_key", portals[0].RequiresSecret);
        Assert.Equal("post", portals[0].Method);
    }

    [Fact]
    public void Parse_MissingProvidersKey_Throws()
    {
        var json = """{ "version": 1 }""";
        var ex = Assert.Throws<ConfigException>(() => PortalCatalogLoader.Parse(json));
        Assert.Contains("providers", ex.Message);
    }

    [Fact]
    public void Parse_DuplicateIds_Throws()
    {
        var json = """
            { "version": 1, "providers": [
              { "id": 1, "name": "a", "type": "manual", "enabled": true },
              { "id": 1, "name": "b", "type": "manual", "enabled": true }
            ] }
            """;
        var ex = Assert.Throws<ConfigException>(() => PortalCatalogLoader.Parse(json));
        Assert.Contains("duplicate", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_InvalidType_Throws()
    {
        var json = """
            { "version": 1, "providers": [
              { "id": 1, "name": "x", "type": "carrier-pigeon", "enabled": true }
            ] }
            """;
        Assert.Throws<ConfigException>(() => PortalCatalogLoader.Parse(json));
    }
}
```

- [ ] **Step 2: Verify the tests fail**

Run: `dotnet test src/Jobmatch.Tests --filter PortalCatalogLoaderTests`
Expected: 5 failures with "type or namespace name 'PortalCatalogLoader' could not be found".

- [ ] **Step 3: Implement the loader**

Implementation in `src/Jobmatch/Configuration/PortalCatalogLoader.cs`. The shape is similar to the existing YAML loader, but `System.Text.Json`-based. Sketch:

```csharp
using System.Text.Json;
using Jobmatch.Models;

namespace Jobmatch.Configuration;

public static class PortalCatalogLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    public static IReadOnlyList<PortalConfig> Load(string path)
        => Parse(File.ReadAllText(path));

    public static IReadOnlyList<PortalConfig> Parse(string json)
    {
        using var doc = JsonDocument.Parse(json, new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
        });

        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("providers", out var providersEl)
            || providersEl.ValueKind != JsonValueKind.Array)
        {
            throw new ConfigException("portals.json: top-level 'providers' array is required");
        }

        var list = new List<PortalConfig>(providersEl.GetArrayLength());
        var seenIds = new HashSet<int>();
        var index = 0;
        foreach (var el in providersEl.EnumerateArray())
        {
            var p = BuildPortal(el, index);
            if (!seenIds.Add(p.Id))
                throw new ConfigException($"portals.json: duplicate id {p.Id} (provider '{p.name}')");
            list.Add(p);
            index++;
        }
        return list;
    }

    private static PortalConfig BuildPortal(JsonElement el, int index)
    {
        // Required: id, name, type. Pull each via helpers and throw ConfigException
        // on missing or invalid. Optional fields use null/default.
        // - typeRaw must parse to PortalType (enum, ignore case)
        // - endpoint must parse via Uri.TryCreate(absolute) when present
        // - rate_limit_rps default 1.0
        // - requires_secret reads "requiresSecret"
        // - queryParams/headers/responseMapping/staticFields use small dict helpers
        //   that walk the JsonElement object and return IReadOnlyDictionary<string, object?>
        //   (or string-string variant). If a field is absent, return null.
        // - bodyTemplate keeps JsonElement objects/scalars as object? for round-trip into ApiAdapter
        // - pagination has its own object: param (string, required), start (int=1), step (int=1),
        //   sizeParam (string?), size (int?), maxPages (int=5)
        // - html block has its own selectors (mirrors YAML loader's behavior)
        ...
    }
}
```

The helper implementations are mechanical; mirror the existing YAML loader's normalisation logic. Ensure `name` and `type` are lower-cased / parsed identically.

- [ ] **Step 4: Verify tests pass**

Run: `dotnet test src/Jobmatch.Tests --filter PortalCatalogLoaderTests`
Expected: 5 passing.

- [ ] **Step 5: Commit**

```bash
git add src/Jobmatch/Configuration/PortalCatalogLoader.cs src/Jobmatch.Tests/Configuration/PortalCatalogLoaderTests.cs
git commit -m "PortalCatalogLoader: read providers from JSON catalog"
```

---

### Task A3: `ProviderState` model + loader (TDD)

**Files:**
- Create: `src/Jobmatch.Tests/Configuration/ProviderStateTests.cs`
- Create: `src/Jobmatch/Configuration/ProviderState.cs`
- Create: `src/Jobmatch/Configuration/ProviderStateLoader.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Jobmatch.Configuration;

namespace Jobmatch.Tests.Configuration;

public sealed class ProviderStateTests
{
    [Fact]
    public void LoadOrEmpty_ReturnsEmptyWhenFileMissing()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".json");
        var state = ProviderStateLoader.LoadOrEmpty(path);
        Assert.Empty(state.Disabled);
        Assert.Empty(state.Secrets);
    }

    [Fact]
    public void RoundTrip_DisabledIdsAndSecrets()
    {
        var dir = Directory.CreateTempSubdirectory("provider-state-test");
        var path = Path.Combine(dir.FullName, "provider-state.json");
        var input = new ProviderState(
            Disabled: new[] { 3, 7 },
            Secrets: new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "abc123" },
            });

        ProviderStateLoader.Save(path, input);
        var loaded = ProviderStateLoader.LoadOrEmpty(path);

        Assert.Equal(new[] { 3, 7 }, loaded.Disabled);
        Assert.Equal("abc123", loaded.Secrets[5]["api_key"]);
    }

    [Fact]
    public void Save_AtomicWrite()
    {
        // Saving over an existing file should not leave a .tmp artifact behind.
        var dir = Directory.CreateTempSubdirectory("provider-state-atomic");
        var path = Path.Combine(dir.FullName, "provider-state.json");
        ProviderStateLoader.Save(path, ProviderState.Empty);
        ProviderStateLoader.Save(path, ProviderState.Empty);
        Assert.False(File.Exists(path + ".tmp"));
    }
}
```

- [ ] **Step 2: Verify failure**

Run: `dotnet test src/Jobmatch.Tests --filter ProviderStateTests`
Expected: 3 failures with "could not be found".

- [ ] **Step 3: Implement**

`ProviderState.cs`:
```csharp
namespace Jobmatch.Configuration;

public sealed record ProviderState(
    IReadOnlyList<int> Disabled,
    IReadOnlyDictionary<int, IReadOnlyDictionary<string, string>> Secrets)
{
    public static ProviderState Empty { get; } = new(
        Array.Empty<int>(),
        new Dictionary<int, IReadOnlyDictionary<string, string>>());
}
```

`ProviderStateLoader.cs`: `System.Text.Json` round-trip. Note that the JSON keys for `Secrets` are stringified ints (`"5"`); deserialise to `Dictionary<string, Dictionary<string, string>>` and project to `Dictionary<int, ...>`. `Save` writes via `path.tmp` → `File.Move(temp, path, overwrite: true)`. `LoadOrEmpty` returns `ProviderState.Empty` if the file is missing or empty.

- [ ] **Step 4: Verify tests pass**

Run: `dotnet test src/Jobmatch.Tests --filter ProviderStateTests`
Expected: 3 passing.

- [ ] **Step 5: Commit**

```bash
git add src/Jobmatch/Configuration/ProviderState.cs src/Jobmatch/Configuration/ProviderStateLoader.cs src/Jobmatch.Tests/Configuration/ProviderStateTests.cs
git commit -m "ProviderState: per-user opt-outs + secrets storage"
```

---

### Task A4: `ProviderStateMerger` (TDD)

**Files:**
- Create: `src/Jobmatch.Tests/Configuration/ProviderStateMergerTests.cs`
- Create: `src/Jobmatch/Configuration/ProviderStateMerger.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Jobmatch.Configuration;
using Jobmatch.Models;

namespace Jobmatch.Tests.Configuration;

public sealed class ProviderStateMergerTests
{
    private static PortalConfig Catalog(int id, string name, bool enabled = true, string? requiresSecret = null) =>
        new(Name: name, Type: PortalType.Manual, Enabled: enabled, Id: id, RequiresSecret: requiresSecret);

    [Fact]
    public void DisabledIdInState_OverridesEnabledTrue()
    {
        var catalog = new[] { Catalog(1, "a"), Catalog(2, "b") };
        var state = new ProviderState(new[] { 2 }, new Dictionary<int, IReadOnlyDictionary<string, string>>());

        var merged = ProviderStateMerger.Merge(catalog, state);
        Assert.True(merged.First(p => p.Id == 1).Enabled);
        Assert.False(merged.First(p => p.Id == 2).Enabled);
    }

    [Fact]
    public void RequiresSecret_WithoutSecret_ProducesEffectivelyDisabled()
    {
        var catalog = new[] { Catalog(5, "jooble", requiresSecret: "api_key") };
        var merged = ProviderStateMerger.Merge(catalog, ProviderState.Empty);
        Assert.False(merged[0].Enabled);
    }

    [Fact]
    public void RequiresSecret_WithSecret_StaysEnabled()
    {
        var catalog = new[] { Catalog(5, "jooble", requiresSecret: "api_key") };
        var state = new ProviderState(
            Array.Empty<int>(),
            new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "abc" },
            });
        var merged = ProviderStateMerger.Merge(catalog, state);
        Assert.True(merged[0].Enabled);
    }

    [Fact]
    public void RequiresSecret_EmptyStringSecret_TreatedAsMissing()
    {
        var catalog = new[] { Catalog(5, "jooble", requiresSecret: "api_key") };
        var state = new ProviderState(
            Array.Empty<int>(),
            new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "" },
            });
        var merged = ProviderStateMerger.Merge(catalog, state);
        Assert.False(merged[0].Enabled);
    }

    [Fact]
    public void SecretsSubstitutedIntoQueryParams()
    {
        var catalog = new[]
        {
            new PortalConfig(
                Name: "jooble", Type: PortalType.Api, Enabled: true, Id: 5,
                RequiresSecret: "api_key",
                QueryParams: new Dictionary<string, object?> { ["api_key"] = "" }),
        };
        var state = new ProviderState(
            Array.Empty<int>(),
            new Dictionary<int, IReadOnlyDictionary<string, string>>
            {
                [5] = new Dictionary<string, string> { ["api_key"] = "real-secret-value" },
            });

        var merged = ProviderStateMerger.Merge(catalog, state);
        Assert.Equal("real-secret-value", merged[0].QueryParams!["api_key"]?.ToString());
    }
}
```

- [ ] **Step 2: Verify failure**

Run: `dotnet test src/Jobmatch.Tests --filter ProviderStateMergerTests`
Expected: 5 failures with "could not be found".

- [ ] **Step 3: Implement merger**

```csharp
namespace Jobmatch.Configuration;

using Jobmatch.Models;

public static class ProviderStateMerger
{
    public static IReadOnlyList<PortalConfig> Merge(
        IReadOnlyList<PortalConfig> catalog,
        ProviderState state)
    {
        var disabled = new HashSet<int>(state.Disabled);
        var result = new List<PortalConfig>(catalog.Count);
        foreach (var p in catalog)
        {
            var hasSecretValue = false;
            IReadOnlyDictionary<string, string>? secrets = null;
            if (state.Secrets.TryGetValue(p.Id, out var s) && s is not null)
            {
                secrets = s;
                if (p.RequiresSecret is not null
                    && s.TryGetValue(p.RequiresSecret, out var v)
                    && !string.IsNullOrEmpty(v))
                {
                    hasSecretValue = true;
                }
            }

            var effectiveEnabled = p.Enabled
                && !disabled.Contains(p.Id)
                && (p.RequiresSecret is null || hasSecretValue);

            var resolvedQuery = SubstituteSecrets(p.QueryParams, secrets);
            var resolvedBody = SubstituteSecrets(p.BodyTemplate, secrets);

            result.Add(p with
            {
                Enabled = effectiveEnabled,
                QueryParams = resolvedQuery,
                BodyTemplate = resolvedBody,
            });
        }
        return result;
    }

    private static IReadOnlyDictionary<string, object?>? SubstituteSecrets(
        IReadOnlyDictionary<string, object?>? source,
        IReadOnlyDictionary<string, string>? secrets)
    {
        if (source is null || secrets is null) return source;
        var copy = new Dictionary<string, object?>(source);
        foreach (var (k, v) in secrets)
        {
            if (copy.ContainsKey(k)) copy[k] = v;
        }
        return copy;
    }
}
```

- [ ] **Step 4: Verify tests pass**

Run: `dotnet test src/Jobmatch.Tests --filter ProviderStateMergerTests`
Expected: 5 passing.

- [ ] **Step 5: Commit**

```bash
git add src/Jobmatch/Configuration/ProviderStateMerger.cs src/Jobmatch.Tests/Configuration/ProviderStateMergerTests.cs
git commit -m "ProviderStateMerger: combine catalog + per-user state"
```

---

### Task A5: Convert `portals.example.yml` → `portals.json`

**Files:**
- Create: `src/Jobmatch/Configuration/portals.json`
- Modify: `src/Jobmatch/Jobmatch.csproj` (CopyToOutputDirectory)
- Delete: `src/config/portals.example.yml`

- [ ] **Step 1: Generate the JSON catalog from the existing YAML**

Run a one-shot conversion. Use this throwaway script (don't commit it):

```csharp
// scratch/convert-portals.csx — `dotnet script` or paste into a console app and run.
// Reads src/config/portals.example.yml via PortalConfigLoader, then re-serialises
// to JSON with camelCase keys matching the new PortalCatalogLoader.Parse contract.
// For providers known to require a secret today (jooble: api_key,
// careerjet-dk: affid), set requiresSecret accordingly and ensure those keys
// appear in queryParams (with empty string sentinel) or bodyTemplate.
```

The simpler, safer path: hand-write `portals.json` by translating each provider in `src/config/portals.example.yml`. Field mapping:

| YAML key | JSON key |
| --- | --- |
| `name`, `type`, `enabled`, `endpoint`, `notes`, `method` | same |
| `query_params` | `queryParams` |
| `response_mapping` | `responseMapping` |
| `static_fields` | `staticFields` |
| `body_template` | `bodyTemplate` |
| `rate_limit_rps` | `rateLimitRps` |
| `pagination.size_param` | `pagination.sizeParam` |
| `pagination.max_pages` | `pagination.maxPages` |

For the two providers that need keys today, add `"requiresSecret": "api_key"` (jooble) / `"requiresSecret": "affid"` (careerjet-dk). Keep their `enabled: true` in the catalog — the merger demotes them at runtime when no secret is set.

- [ ] **Step 2: Update `Jobmatch.csproj` to copy the catalog into the output**

Add to `src/Jobmatch/Jobmatch.csproj`:

```xml
<ItemGroup>
  <None Update="Configuration/portals.json">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </None>
</ItemGroup>
```

The Gui project references Jobmatch and inherits the copy. Verify by checking `bin/Debug/net10.0/Configuration/portals.json` after build, OR copy to `AppContext.BaseDirectory` directly (no subdirectory) — pick the simpler one. Recommend: copy to the bin root using `<TargetPath>portals.json</TargetPath>` so the loader path is `Path.Combine(AppContext.BaseDirectory, "portals.json")`.

- [ ] **Step 3: Add a smoke test that the shipped catalog parses**

In `src/Jobmatch.Tests/Configuration/PortalCatalogLoaderTests.cs`, add:

```csharp
[Fact]
public void Bundled_PortalsJson_Parses()
{
    var path = Path.Combine(AppContext.BaseDirectory, "portals.json");
    Assert.True(File.Exists(path), $"missing: {path}");
    var portals = PortalCatalogLoader.Load(path);
    Assert.NotEmpty(portals);
    Assert.All(portals, p => Assert.True(p.Id > 0, $"provider '{p.Name}' missing id"));
    Assert.Equal(portals.Count, portals.Select(p => p.Id).Distinct().Count());
}
```

- [ ] **Step 4: Run all tests**

Run: `dotnet test src/Jobmatch.Tests`
Expected: smoke test passes; 137 + ~13 new tests green.

- [ ] **Step 5: Delete the YAML example**

```bash
git rm src/config/portals.example.yml
```

- [ ] **Step 6: Commit**

```bash
git add src/Jobmatch/Configuration/portals.json src/Jobmatch/Jobmatch.csproj src/Jobmatch.Tests/Configuration/PortalCatalogLoaderTests.cs
git commit -m "ship portals.json as bundled catalog (replaces portals.example.yml)"
```

---

### Task A6: Migration shim (TDD)

**Files:**
- Create: `src/Jobmatch.Tests/Configuration/PortalsMigrationShimTests.cs`
- Create: `src/Jobmatch/Configuration/PortalsMigrationShim.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using Jobmatch.Configuration;

namespace Jobmatch.Tests.Configuration;

public sealed class PortalsMigrationShimTests
{
    [Fact]
    public void NoYaml_NoOp()
    {
        var dir = Directory.CreateTempSubdirectory("shim-noop");
        var stateBefore = ProviderStateLoader.LoadOrEmpty(Path.Combine(dir.FullName, "provider-state.json"));
        var migrated = PortalsMigrationShim.RunIfNeeded(dir.FullName);
        Assert.False(migrated);
        Assert.False(File.Exists(Path.Combine(dir.FullName, "provider-state.json")));
    }

    [Fact]
    public void DisabledFlagsCopiedIntoState()
    {
        var dir = Directory.CreateTempSubdirectory("shim-disabled");
        var yaml = """
            portals:
              - id: 1
                name: greenhouse-pleo
                type: api
                enabled: true
                endpoint: https://x/y
              - id: 2
                name: greenhouse-wolt
                type: api
                enabled: false
                endpoint: https://x/y
              - id: 3
                name: jobnet
                type: manual
                enabled: false
            """;
        File.WriteAllText(Path.Combine(dir.FullName, "portals.yml"), yaml);

        var migrated = PortalsMigrationShim.RunIfNeeded(dir.FullName);
        Assert.True(migrated);

        var state = ProviderStateLoader.LoadOrEmpty(Path.Combine(dir.FullName, "provider-state.json"));
        Assert.Contains(2, state.Disabled);
        Assert.Contains(3, state.Disabled);
        Assert.DoesNotContain(1, state.Disabled);

        Assert.True(File.Exists(Path.Combine(dir.FullName, "portals.yml.bak")));
        Assert.False(File.Exists(Path.Combine(dir.FullName, "portals.yml")));
    }

    [Fact]
    public void Idempotent_OnSecondRun()
    {
        var dir = Directory.CreateTempSubdirectory("shim-idempotent");
        File.WriteAllText(
            Path.Combine(dir.FullName, "portals.yml"),
            "portals:\n  - id: 1\n    name: a\n    type: manual\n    enabled: false\n");
        Assert.True(PortalsMigrationShim.RunIfNeeded(dir.FullName));
        Assert.False(PortalsMigrationShim.RunIfNeeded(dir.FullName)); // no yaml left
    }
}
```

- [ ] **Step 2: Verify failure**

Run: `dotnet test src/Jobmatch.Tests --filter PortalsMigrationShimTests`
Expected: 3 failures.

- [ ] **Step 3: Implement**

```csharp
using Jobmatch.Models;

namespace Jobmatch.Configuration;

public static class PortalsMigrationShim
{
    /// <summary>
    /// One-shot migration: if <c>{userDataDir}/portals.yml</c> exists, parse it,
    /// translate <c>enabled: false</c> entries into <c>provider-state.json.disabled[]</c>,
    /// rename the yaml to <c>portals.yml.bak</c>, and return true.
    /// Returns false if nothing to do. Safe to call on every startup.
    /// </summary>
    public static bool RunIfNeeded(string userDataDir)
    {
        var yamlPath = Path.Combine(userDataDir, "portals.yml");
        if (!File.Exists(yamlPath)) return false;

        IReadOnlyList<PortalConfig> portals;
        try { portals = PortalConfigLoader.Load(yamlPath); }
        catch { return false; }  // unreadable yaml — leave it; user can sort out manually

        var statePath = Path.Combine(userDataDir, "provider-state.json");
        var existing = ProviderStateLoader.LoadOrEmpty(statePath);
        var disabledIds = new HashSet<int>(existing.Disabled);
        foreach (var p in portals)
        {
            if (!p.Enabled && p.Id > 0) disabledIds.Add(p.Id);
        }
        var newState = new ProviderState(
            Disabled: disabledIds.OrderBy(i => i).ToArray(),
            Secrets: existing.Secrets);
        ProviderStateLoader.Save(statePath, newState);

        var backupPath = Path.Combine(userDataDir, "portals.yml.bak");
        if (File.Exists(backupPath)) File.Delete(backupPath);
        File.Move(yamlPath, backupPath);
        Console.WriteLine($"[migration] portals.yml → provider-state.json ({disabledIds.Count} disabled); backup at portals.yml.bak");
        return true;
    }
}
```

- [ ] **Step 4: Run tests**

Run: `dotnet test src/Jobmatch.Tests --filter PortalsMigrationShim`
Expected: 3 passing.

- [ ] **Step 5: Commit**

```bash
git add src/Jobmatch/Configuration/PortalsMigrationShim.cs src/Jobmatch.Tests/Configuration/PortalsMigrationShimTests.cs
git commit -m "PortalsMigrationShim: one-shot yaml→state.json on startup"
```

---

### Task A7: Wire `SearchService` and `UserContext` through the new loader chain

**Files:**
- Modify: `src/Jobmatch/UserContext.cs`
- Modify: `src/Jobmatch/Search/SearchService.cs`

- [ ] **Step 1: Update `UserContext`**

Add a new path; keep `PortalsPath` for the migration shim's reference. Drop the seed-from-portals.example.yml call.

```csharp
public required string ProviderStatePath { get; init; }
```

In `Resolve`, set:
```csharp
ProviderStatePath = Path.Combine(rootDir, "provider-state.json"),
```

In `SeedFromExamples`, remove the `portals.example.yml` copy block; only `skillset.example.md` is seeded now. Update the log message accordingly.

- [ ] **Step 2: Update `SearchService.RunAsync` to use the new loader chain**

Replace:
```csharp
var allPortals = PortalConfigLoader.Load(_ctx.PortalsPath);
```
with:
```csharp
var catalogPath = Path.Combine(AppContext.BaseDirectory, "portals.json");
var catalog = PortalCatalogLoader.Load(catalogPath);
var state = ProviderStateLoader.LoadOrEmpty(_ctx.ProviderStatePath);
var allPortals = ProviderStateMerger.Merge(catalog, state);
```

- [ ] **Step 3: Build, run all tests**

Run: `dotnet test src/Jobmatch.Tests`
Expected: green. (Some `SearchService` tests may need fixture updates if they wrote `portals.yml` into the test user dir — switch them to write `provider-state.json` and rely on a test-only catalog override. If `SearchServiceTests` constructs catalog/state directly and bypasses the loader, no change needed.)

- [ ] **Step 4: Commit**

```bash
git add src/Jobmatch/UserContext.cs src/Jobmatch/Search/SearchService.cs
git commit -m "SearchService: load portals via catalog + per-user overlay"
```

---

## Phase B — Backend: ScoredEntry stack hits

### Task B1: Extend `ScoredEntry` with stack hits (TDD)

**Files:**
- Modify: `src/Jobmatch/Search/ScoredEntry.cs`
- Modify: `src/Jobmatch/Search/SearchService.cs:218-227` (`ToScoredEntry`)
- Modify: `src/Jobmatch.Tests/Search/SearchServiceTests.cs`

- [ ] **Step 1: Add a failing test in `SearchServiceTests`**

The existing harness builds a temp `UserContext` and runs the pipeline against fixtures (see top of `SearchServiceTests.cs`). The simplest test: extend an existing happy-path test that already runs a full pipeline against a manual portal to additionally read the produced history JSON and assert the new fields landed.

```csharp
[Fact]
public async Task History_ScoredEntries_CarryStackHits()
{
    var email = "stack-hits@example.com";
    var ctx = JobmatchUserContext.Resolve(
        emailOverride: email, repoRoot: _tempRoot, seedExamples: false);
    Directory.CreateDirectory(ctx.RootDir);
    File.WriteAllText(ctx.SkillsetPath, MinimalSkillset);  // primary: Python, TypeScript
    Directory.CreateDirectory(Path.GetDirectoryName(ctx.RankingPath)!);
    File.WriteAllText(ctx.RankingPath, MinimalRanking);
    Directory.CreateDirectory(ctx.ImportsDir);
    File.WriteAllText(Path.Combine(ctx.ImportsDir, "stack.csv"),
        "id,title,company,url,description\n" +
        "1,Senior Python Engineer,Acme,https://acme/jobs/1,\"We use Python and Kubernetes\"\n");
    // For this test, write a portal-state.json that points at a manual portal in the bundled
    // catalog OR override the catalog by running the merger directly. If the bundle doesn't
    // include a manual provider matching this fixture, add an overload to SearchService that
    // accepts a pre-built portal list (test-only entry point).

    var svc = new SearchService(ctx);
    await foreach (var _ in svc.RunAsync(new SearchRequest())) { }

    var historyFile = Directory.GetFiles(ctx.HistoryDir, "*.json").Single();
    using var doc = JsonDocument.Parse(File.ReadAllText(historyFile));
    var scored = doc.RootElement.GetProperty("scored")[0];
    var primary = scored.GetProperty("primaryStackHits").EnumerateArray().Select(e => e.GetString()).ToArray();
    var secondary = scored.GetProperty("secondaryStackHits").EnumerateArray().Select(e => e.GetString()).ToArray();
    Assert.Contains("Python", primary);
    Assert.Contains("Kubernetes", secondary);
}
```

If wiring this into the full pipeline is too entangling (the catalog override is non-trivial), prefer a unit test instead: make `SearchService.ToScoredEntry` `internal` and add `[InternalsVisibleTo("Jobmatch.Tests")]` to `Jobmatch.csproj`, then directly construct a `Match` with a known `MatchReasoning.PrimaryStackHits` and assert the produced `ScoredEntry` carries it.

- [ ] **Step 2: Verify failure**

Run: `dotnet test src/Jobmatch.Tests --filter ScoredEntry`
Expected: compile error or assert failure on the missing fields.

- [ ] **Step 3: Add the fields**

```csharp
public sealed record ScoredEntry(
    string Id,
    string Title,
    string? Company,
    string? Location,
    string Url,
    DateTimeOffset? PostedAt,
    string Portal,
    double Score,
    ScoreBreakdown Breakdown,
    IReadOnlyList<string> PrimaryStackHits,
    IReadOnlyList<string> SecondaryStackHits);
```

- [ ] **Step 4: Update `ToScoredEntry`**

```csharp
private static ScoredEntry ToScoredEntry(Match m) => new(
    Id: m.Listing.Id,
    Title: m.Listing.Title,
    Company: m.Listing.Company,
    Location: m.Listing.Location,
    Url: m.Listing.Url.ToString(),
    PostedAt: m.Listing.PostedAt,
    Portal: m.Listing.Portal,
    Score: m.Score,
    Breakdown: m.Breakdown,
    PrimaryStackHits: m.Reasoning.PrimaryStackHits,
    SecondaryStackHits: m.Reasoning.SecondaryStackHits);
```

- [ ] **Step 5: Run tests**

Run: `dotnet test src/Jobmatch.Tests`
Expected: green.

- [ ] **Step 6: Commit**

```bash
git add src/Jobmatch/Search/ScoredEntry.cs src/Jobmatch/Search/SearchService.cs src/Jobmatch.Tests/Search/SearchServiceTests.cs
git commit -m "ScoredEntry: carry primary/secondary stack hits into history"
```

---

## Phase C — Backend: providers handler + secrets endpoint

### Task C1: Drop Create/Delete endpoints; narrow Update to enabled-only

**Files:**
- Modify: `src/Jobmatch.Gui/Server/Routes.cs`
- Modify: `src/Jobmatch.Gui/Server/Endpoints/ProvidersEndpoints.cs`
- Modify: `src/Jobmatch.Gui/Server/Handlers/ProvidersHandler.cs`

- [ ] **Step 1: Remove routes**

In `Routes.cs`, delete `Create` and `Delete` constants; leave `Get`, `GetOne`, `Update`, `Test`. Add:
```csharp
public const string SetSecrets = "/api/providers/{id:int}/secrets";
```

- [ ] **Step 2: Remove endpoint mappings**

In `ProvidersEndpoints.cs`:
```csharp
public static void Map(WebApplication app)
{
    app.MapGet(Routes.Providers.Get, (Jobmatch.UserContext ctx) => ProvidersHandler.GetList(ctx));
    app.MapGet(Routes.Providers.GetOne, (int id, Jobmatch.UserContext ctx) => ProvidersHandler.GetOne(id, ctx));
    app.MapPut(Routes.Providers.Update, (int id, ProviderUpsert? req, Jobmatch.UserContext ctx) => ProvidersHandler.Update(id, req, ctx));
    app.MapPost(Routes.Providers.Test, (int id, Jobmatch.UserContext ctx, CancellationToken ct) => ProvidersHandler.Test(id, ctx, ct));
    app.MapPut(Routes.Providers.SetSecrets, (int id, SetSecretsRequest? req, Jobmatch.UserContext ctx) => ProvidersHandler.SetSecrets(id, req, ctx));
}
```

- [ ] **Step 3: Rewrite `ProvidersHandler`**

Replace the YAML-mutating path with reads from the merged config and writes to `provider-state.json`. Drop `Create` and `Delete` methods entirely. Sketch:

```csharp
private static IReadOnlyList<PortalConfig> LoadMerged(Jobmatch.UserContext ctx)
{
    var catalog = PortalCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "portals.json"));
    var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
    return ProviderStateMerger.Merge(catalog, state);
}

public static IResult GetList(Jobmatch.UserContext ctx)
{
    var portals = LoadMerged(ctx);
    var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
    var lastByProvider = LoadLastFetchByProvider(ctx.HistoryDir);
    var summaries = portals.Select(p => MakeSummary(p, state, lastByProvider)).ToList();
    return Results.Ok(new ProvidersResponse(summaries));
}

public static IResult Update(int id, ProviderUpsert? req, Jobmatch.UserContext ctx)
{
    if (req is null) return Results.BadRequest(new SaveResponse(false, "request body is required"));

    var catalog = PortalCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "portals.json"));
    var portal = catalog.FirstOrDefault(p => p.Id == id);
    if (portal is null) return Results.NotFound();

    var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
    var disabled = state.Disabled.ToHashSet();
    if (req.Enabled) disabled.Remove(id);
    else disabled.Add(id);

    var next = state with { Disabled = disabled.OrderBy(i => i).ToArray() };
    ProviderStateLoader.Save(ctx.ProviderStatePath, next);
    return Results.Ok(new SaveResponse(true));
}
```

The other fields on `ProviderUpsert` (name, type, endpoint, etc.) are silently ignored. `MakeSummary` now also computes `requiresSecret` and `hasSecret = state.Secrets.GetValueOrDefault(id)?[requiresSecret] is { Length: > 0 }`.

- [ ] **Step 4: Build, run tests; remove handler tests for Create/Delete**

Run: `dotnet test src/Jobmatch.Tests`
Expected: previous tests for create/delete fail. Delete those test methods. Re-run; green.

- [ ] **Step 5: Commit**

```bash
git add src/Jobmatch.Gui/Server/Routes.cs src/Jobmatch.Gui/Server/Endpoints/ProvidersEndpoints.cs src/Jobmatch.Gui/Server/Handlers/ProvidersHandler.cs src/Jobmatch.Tests
git commit -m "ProvidersHandler: catalog read-only, Update toggles enabled only"
```

---

### Task C2: Secrets endpoint + handler (TDD)

**Files:**
- Create: `src/Jobmatch.Gui/Server/Models/SetSecretsRequest.cs`
- Modify: `src/Jobmatch.Gui/Server/Handlers/ProvidersHandler.cs`
- Create: `src/Jobmatch.Tests/Configuration/ProvidersHandlerSecretsTests.cs` (or extend an existing handler-test file if one exists; check `src/Jobmatch.Tests/` for handler tests first)

- [ ] **Step 1: Define the request model**

```csharp
namespace Jobmatch.Gui.Server.Models;

public sealed record SetSecretsRequest(IReadOnlyDictionary<string, string> Values);
```

- [ ] **Step 2: Write the failing tests**

The catalog is loaded from `AppContext.BaseDirectory/portals.json` — the test bin directory. The bundled catalog already includes `jooble` (id known after Task A5 — pick the actual id from the JSON; assume id=`JOOBLE_ID` here, replace before running). For a "no requiresSecret" case, pick any non-secret provider's id (e.g. greenhouse-pleo).

```csharp
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Jobmatch.Configuration;
using Jobmatch.Gui.Server.Handlers;
using Jobmatch.Gui.Server.Models;
using JobmatchUserContext = Jobmatch.UserContext;

namespace Jobmatch.Tests.Configuration;

public sealed class ProvidersHandlerSecretsTests : IDisposable
{
    private readonly string _tempRoot;
    private readonly string? _envBackup;
    // Replace these with the actual ids from src/Jobmatch/Configuration/portals.json
    private const int JoobleId = 0;          // provider with requiresSecret == "api_key"
    private const int NonSecretId = 0;       // provider with requiresSecret == null

    public ProvidersHandlerSecretsTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "ph-secrets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
        _envBackup = Environment.GetEnvironmentVariable("JOBFINDER_USER");
        Environment.SetEnvironmentVariable("JOBFINDER_USER", null);
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable("JOBFINDER_USER", _envBackup);
        try { if (Directory.Exists(_tempRoot)) Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private JobmatchUserContext NewCtx() =>
        JobmatchUserContext.Resolve(emailOverride: "x@y", repoRoot: _tempRoot, seedExamples: false);

    [Fact]
    public void SetSecrets_WritesValuesToProviderStateJson()
    {
        var ctx = NewCtx();
        var req = new SetSecretsRequest(new Dictionary<string, string> { ["api_key"] = "abc" });

        var result = ProvidersHandler.SetSecrets(JoobleId, req, ctx);

        Assert.IsType<Ok<SaveResponse>>(result);
        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        Assert.Equal("abc", state.Secrets[JoobleId]["api_key"]);
    }

    [Fact]
    public void SetSecrets_EmptyStringClearsValue()
    {
        var ctx = NewCtx();
        ProvidersHandler.SetSecrets(JoobleId, new SetSecretsRequest(new Dictionary<string, string> { ["api_key"] = "abc" }), ctx);
        ProvidersHandler.SetSecrets(JoobleId, new SetSecretsRequest(new Dictionary<string, string> { ["api_key"] = "" }), ctx);

        var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
        Assert.False(state.Secrets.ContainsKey(JoobleId));
    }

    [Fact]
    public void SetSecrets_UnknownProviderId_Returns404()
    {
        var ctx = NewCtx();
        var result = ProvidersHandler.SetSecrets(99999, new SetSecretsRequest(new Dictionary<string, string>()), ctx);
        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public void SetSecrets_ProviderWithoutRequiresSecret_Returns400()
    {
        var ctx = NewCtx();
        var result = ProvidersHandler.SetSecrets(NonSecretId,
            new SetSecretsRequest(new Dictionary<string, string> { ["whatever"] = "x" }), ctx);
        Assert.IsType<BadRequest<SaveResponse>>(result);
    }
}
```

Note: `JoobleId` and `NonSecretId` are placeholders the engineer must replace with the actual ids assigned in `portals.json` after Task A5 lands. Read those once and hard-code them in the test.

- [ ] **Step 3: Verify failure**

Run: `dotnet test src/Jobmatch.Tests --filter ProvidersHandlerSecretsTests`
Expected: failures.

- [ ] **Step 4: Implement `ProvidersHandler.SetSecrets`**

```csharp
public static IResult SetSecrets(int id, SetSecretsRequest? req, Jobmatch.UserContext ctx)
{
    if (req is null) return Results.BadRequest(new SaveResponse(false, "request body is required"));

    var catalog = PortalCatalogLoader.Load(Path.Combine(AppContext.BaseDirectory, "portals.json"));
    var portal = catalog.FirstOrDefault(p => p.Id == id);
    if (portal is null) return Results.NotFound();
    if (portal.RequiresSecret is null)
        return Results.BadRequest(new SaveResponse(false, $"provider '{portal.Name}' does not declare requiresSecret"));

    var state = ProviderStateLoader.LoadOrEmpty(ctx.ProviderStatePath);
    var secrets = state.Secrets.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyDictionary<string, string>)new Dictionary<string, string>(kvp.Value));

    var current = secrets.TryGetValue(id, out var c) ? new Dictionary<string, string>(c) : new Dictionary<string, string>();
    foreach (var (k, v) in req.Values)
    {
        if (string.IsNullOrEmpty(v)) current.Remove(k);
        else current[k] = v;
    }
    if (current.Count == 0) secrets.Remove(id);
    else secrets[id] = current;

    var next = state with { Secrets = secrets };
    ProviderStateLoader.Save(ctx.ProviderStatePath, next);
    return Results.Ok(new SaveResponse(true));
}
```

- [ ] **Step 5: Run tests, commit**

```bash
dotnet test src/Jobmatch.Tests
git add src/Jobmatch.Gui/Server/ src/Jobmatch.Tests/Configuration/ProvidersHandlerSecretsTests.cs
git commit -m "providers: PUT /api/providers/{id}/secrets writes provider-state.json"
```

---

### Task C3: Extend `ProviderSummary` with `requiresSecret` + `hasSecret`

**Files:**
- Modify: `src/Jobmatch.Gui/Server/Models/ProviderSummary.cs`
- Modify: `src/Jobmatch.Gui/Server/Handlers/ProvidersHandler.cs::MakeSummary`

- [ ] **Step 1: Add fields to the response record**

```csharp
public sealed record ProviderSummary(
    int Id,
    string Name,
    string Type,
    bool Enabled,
    string? Endpoint,
    double RateLimitRps,
    string? Notes,
    DateTimeOffset? LastFetchedAt,
    int? LastFetchCount,
    string? RequiresSecret,
    bool HasSecret);
```

(`ProviderDetail` extends `ProviderSummary` — confirm it inherits the new fields, or update it accordingly.)

- [ ] **Step 2: Populate in `MakeSummary`**

```csharp
private static ProviderSummary MakeSummary(PortalConfig p, ProviderState state, IReadOnlyDictionary<string, LastFetch> lastByProvider)
{
    lastByProvider.TryGetValue(p.Name, out var last);
    var hasSecret = p.RequiresSecret is not null
        && state.Secrets.TryGetValue(p.Id, out var secrets)
        && secrets.TryGetValue(p.RequiresSecret, out var v)
        && !string.IsNullOrEmpty(v);
    return new ProviderSummary(
        Id: p.Id,
        Name: p.Name,
        Type: p.Type.ToString().ToLowerInvariant(),
        Enabled: p.Enabled,
        Endpoint: p.Endpoint?.ToString(),
        RateLimitRps: p.RateLimitRps,
        Notes: p.Notes,
        LastFetchedAt: last?.FetchedAt,
        LastFetchCount: last?.FetchedCount,
        RequiresSecret: p.RequiresSecret,
        HasSecret: hasSecret);
}
```

- [ ] **Step 3: Run tests, commit**

```bash
dotnet test src/Jobmatch.Tests
git add src/Jobmatch.Gui/Server/Models/ProviderSummary.cs src/Jobmatch.Gui/Server/Handlers/ProvidersHandler.cs
git commit -m "ProviderSummary: expose requiresSecret + hasSecret to GUI"
```

---

### Task C4: Wire migration shim into Gui startup

**Files:**
- Modify: `src/Jobmatch.Gui/Server/Program.cs` (or wherever the host bootstrap lives — find via `grep -l UserContext.Resolve src/Jobmatch.Gui/Server`)

- [ ] **Step 1: Call the shim once after `UserContext` is resolved**

```csharp
var ctx = UserContext.Resolve();
PortalsMigrationShim.RunIfNeeded(ctx.RootDir);
```

- [ ] **Step 2: Smoke-test manually**

Place a fake `data/<email>/portals.yml` with one disabled entry. Run `dotnet run --project src/Jobmatch.Gui`. Expect a `provider-state.json` to appear with the disabled id and a `portals.yml.bak` to take the yaml's place. Verify by `cat data/<email>/provider-state.json`.

- [ ] **Step 3: Commit**

```bash
git add src/Jobmatch.Gui/Server/Program.cs
git commit -m "Gui: run portals.yml migration shim once at startup"
```

---

## Phase D — Frontend: providers UX

### Task D1: API client + types

**Files:**
- Modify: `src/Jobmatch.Gui/Client/src/api/types.ts`
- Modify: `src/Jobmatch.Gui/Client/src/api/client.ts`

- [ ] **Step 1: Update types**

In `types.ts`:

```ts
export type ProviderSummary = {
  id: number
  name: string
  type: ProviderType
  enabled: boolean
  endpoint?: string
  rateLimitRps: number
  notes?: string
  lastFetchedAt?: string
  lastFetchCount?: number
  requiresSecret?: string  // NEW
  hasSecret: boolean       // NEW
}

// Drop ProviderUpsert (no longer used by Create); keep a tiny type for the toggle:
export type ProviderEnabledUpdate = { enabled: boolean }

export type SetSecretsRequest = { values: Record<string, string> }

// Extend ScoredEntry:
export type ScoredEntry = {
  id: string
  title: string
  company?: string
  location?: string
  url: string
  postedAt?: string
  portal: string
  score: number
  breakdown: ScoreBreakdown
  primaryStackHits: string[]   // NEW
  secondaryStackHits: string[] // NEW
}
```

- [ ] **Step 2: Update `client.ts`**

Drop `createProvider`, `deleteProvider`. Add:

```ts
export async function setProviderEnabled(id: number, enabled: boolean): Promise<SaveResponse> {
  return apiFetch<SaveResponse>(`/api/providers/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ enabled }),
  })
}

export async function setProviderSecrets(id: number, values: Record<string, string>): Promise<SaveResponse> {
  return apiFetch<SaveResponse>(`/api/providers/${id}/secrets`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ values }),
  })
}
```

Remove the `updateProvider` general-purpose function — `setProviderEnabled` replaces it. Where current callers send the full `ProviderUpsert`, switch them to send only `enabled`.

- [ ] **Step 3: Build to verify typecheck**

Run: `npm --prefix src/Jobmatch.Gui/Client run build`
Expected: TypeScript errors point to remaining callers that still use the dropped functions. Fix as part of D2/D3.

- [ ] **Step 4: Commit (after D2 — types and pages need to land together for the build to pass)**

---

### Task D2: Simplify `ProvidersPage`

**Files:**
- Modify: `src/Jobmatch.Gui/Client/src/pages/ProvidersPage.tsx`

- [ ] **Step 1: Drop the "+ Add provider" link**

Delete the `<Link to="/providers/new">` button block in the page header.

- [ ] **Step 2: Drop the Edit button on the tile**

In each `provider-tile`, remove the `<Link to={`/providers/${p.id}`} className="btn btn--secondary btn--sm">Edit</Link>` block. The tile's title `Link` already navigates to the detail page; that's enough.

- [ ] **Step 3: Replace `updateProvider(p.id, full ProviderUpsert)` with `setProviderEnabled(p.id, enabled)`**

In the `toggle` mutation:

```ts
const toggle = useMutation({
  mutationFn: async ({ p, enabled }: { p: ProviderSummary; enabled: boolean }) => {
    const res = await setProviderEnabled(p.id, enabled)
    if (!res.success) throw new Error(res.error ?? 'Save failed')
    return enabled
  },
  // onMutate / onError / onSuccess unchanged
})
```

- [ ] **Step 4: Add a "needs key" badge for `requiresSecret && !hasSecret`**

Inside the tile, near the health indicator:

```tsx
{p.requiresSecret && !p.hasSecret && (
  <Link to={`/providers/${p.id}`} className="provider-tile__needs-key">
    needs key →
  </Link>
)}
```

Add a CSS rule for `.provider-tile__needs-key` (small amber tag style) in the existing `app.css`.

- [ ] **Step 5: Build, test in browser**

Run `npm --prefix src/Jobmatch.Gui/Client run build` then start the Gui app and load the providers page; verify tiles render, toggle works, "needs key" badge shows for jooble/careerjet.

- [ ] **Step 6: Commit**

```bash
git add src/Jobmatch.Gui/Client/src/api src/Jobmatch.Gui/Client/src/pages/ProvidersPage.tsx
git commit -m "ProvidersPage: catalog read-only; toggle + needs-key only"
```

---

### Task D3: Simplify `ProviderDetailPage` + drop `/providers/new`

**Files:**
- Modify: `src/Jobmatch.Gui/Client/src/pages/ProviderDetailPage.tsx`
- Modify: `src/Jobmatch.Gui/Client/src/App.tsx` (or wherever the route table lives — likely `main.tsx` or `App.tsx`; find via `grep -l 'providers/new' src/Jobmatch.Gui/Client/src`)

- [ ] **Step 1: Strip the page to read-only + secrets form**

Remove: form `name/type/endpoint/notes/rateLimitRps` inputs (replace with read-only display rows), `SaveBar`, `Danger zone` (delete provider) section, "new mode" branch, the `BLANK` constant.

Keep: enabled toggle (writes via `setProviderEnabled`), Test connection card, Recent runs card.

Add: `Secrets` card visible only when `data?.requiresSecret`:

```tsx
{data?.requiresSecret && (
  <section className="card">
    <h2 className="card__title">Secret: {data.requiresSecret}</h2>
    <p className="field__hint">
      Provided per-user. Stored locally in <code>provider-state.json</code>.
      The provider stays disabled until a non-empty value is set.
    </p>
    <SecretsForm
      providerId={data.id}
      secretName={data.requiresSecret}
      hasSecret={data.hasSecret}
      onSaved={() => queryClient.invalidateQueries({ queryKey: ['provider', data.id] })}
    />
  </section>
)}
```

`SecretsForm` is a small inline component (or co-located file `components/SecretsForm.tsx`):

```tsx
function SecretsForm({ providerId, secretName, hasSecret, onSaved }: {
  providerId: number; secretName: string; hasSecret: boolean; onSaved: () => void
}) {
  const [value, setValue] = useState('')
  const [saving, setSaving] = useState(false)
  const [toast, setToast] = useState<string | null>(null)

  async function save() {
    setSaving(true)
    try {
      const res = await setProviderSecrets(providerId, { [secretName]: value })
      if (!res.success) throw new Error(res.error ?? 'Save failed')
      setValue('')
      setToast('Saved.')
      onSaved()
    } catch (e) {
      setToast(e instanceof Error ? e.message : String(e))
    } finally {
      setSaving(false)
    }
  }

  async function clear() {
    setSaving(true)
    try {
      const res = await setProviderSecrets(providerId, { [secretName]: '' })
      if (!res.success) throw new Error(res.error ?? 'Clear failed')
      setToast('Cleared.')
      onSaved()
    } catch (e) {
      setToast(e instanceof Error ? e.message : String(e))
    } finally {
      setSaving(false)
    }
  }

  return (
    <div className="secrets-form">
      <input
        className="input input--mono"
        type="password"
        autoComplete="off"
        placeholder={hasSecret ? '••••••••  (overwrite to update)' : 'paste value'}
        value={value}
        onChange={(e) => setValue(e.target.value)}
      />
      <button className="btn btn--primary btn--sm" disabled={saving || value.length === 0} onClick={save}>
        {saving ? <span className="spinner" /> : 'Save'}
      </button>
      {hasSecret && (
        <button className="btn btn--ghost btn--sm" disabled={saving} onClick={clear}>Clear</button>
      )}
      {toast && <span className="muted small">{toast}</span>}
    </div>
  )
}
```

- [ ] **Step 2: Drop `/providers/new` from the route table**

Find the file with `<Route path="providers/new"` (or similar), remove that route. The `<Link to="/providers/new">` was already removed in D2.

- [ ] **Step 3: Build and smoke-test in browser**

Run `npm --prefix src/Jobmatch.Gui/Client run build` and start the Gui. Click into a non-secret provider — verify catalog fields render read-only, no Save bar, no Delete button. Click into jooble — verify the Secret form appears, enter a value, save, see "Connected" / health update.

- [ ] **Step 4: Commit**

```bash
git add src/Jobmatch.Gui/Client/src/pages/ProviderDetailPage.tsx src/Jobmatch.Gui/Client/src/App.tsx
git commit -m "ProviderDetailPage: read-only + per-user secrets form"
```

---

## Phase E — Frontend: longlist filter table

### Task E1: Filter state types and URL hash codec

**Files:**
- Create: `src/Jobmatch.Gui/Client/src/components/longlist/filterState.ts`
- Create: `src/Jobmatch.Gui/Client/src/components/longlist/filterState.test.ts` (only if Vitest is wired; otherwise inline-verify)

- [ ] **Step 1: Define the filter state and codec**

```ts
export type LonglistFilters = {
  q: string
  portals: string[]            // empty = all
  posted: 'any' | '24h' | '7d' | '14d' | '30d'
  scoreMin: number             // 0..1
  scoreMax: number             // 0..1
  stackHits: string[]          // empty = all (OR semantics across selected)
  mark: 'all' | 'good' | 'bad' | 'unmarked'
  shortlistOnly: boolean
  sort: { key: SortKey; dir: 'asc' | 'desc' }
}

export type SortKey = 'title' | 'company' | 'portal' | 'location' | 'posted' | 'score'

export const DEFAULT_FILTERS: LonglistFilters = {
  q: '',
  portals: [],
  posted: 'any',
  scoreMin: 0,
  scoreMax: 1,
  stackHits: [],
  mark: 'all',
  shortlistOnly: false,
  sort: { key: 'score', dir: 'desc' },
}

export function isDefault(f: LonglistFilters): boolean {
  return f.q === ''
    && f.portals.length === 0
    && f.posted === 'any'
    && f.scoreMin === 0 && f.scoreMax === 1
    && f.stackHits.length === 0
    && f.mark === 'all'
    && !f.shortlistOnly
    && f.sort.key === 'score' && f.sort.dir === 'desc'
}

export function encodeToHash(f: LonglistFilters): URLSearchParams {
  const p = new URLSearchParams()
  p.set('tab', 'longlist')
  if (f.q) p.set('q', f.q)
  if (f.portals.length) p.set('portal', f.portals.join(','))
  if (f.posted !== 'any') p.set('posted', f.posted)
  if (f.scoreMin > 0 || f.scoreMax < 1) p.set('score', `${f.scoreMin.toFixed(2)}-${f.scoreMax.toFixed(2)}`)
  if (f.stackHits.length) p.set('stack', f.stackHits.join(','))
  if (f.mark !== 'all') p.set('mark', f.mark)
  if (f.shortlistOnly) p.set('shortlist', 'true')
  if (f.sort.key !== 'score' || f.sort.dir !== 'desc') p.set('sort', `${f.sort.key}-${f.sort.dir}`)
  return p
}

export function decodeFromHash(params: URLSearchParams): LonglistFilters {
  const f = { ...DEFAULT_FILTERS }
  f.q = params.get('q') ?? ''
  const portal = params.get('portal'); if (portal) f.portals = portal.split(',').filter(Boolean)
  const posted = params.get('posted'); if (posted && ['24h','7d','14d','30d'].includes(posted)) f.posted = posted as LonglistFilters['posted']
  const score = params.get('score')
  if (score) {
    const [lo, hi] = score.split('-').map(Number)
    if (Number.isFinite(lo)) f.scoreMin = clamp01(lo)
    if (Number.isFinite(hi)) f.scoreMax = clamp01(hi)
  }
  const stack = params.get('stack'); if (stack) f.stackHits = stack.split(',').filter(Boolean)
  const mark = params.get('mark'); if (mark && ['good','bad','unmarked'].includes(mark)) f.mark = mark as LonglistFilters['mark']
  if (params.get('shortlist') === 'true') f.shortlistOnly = true
  const sort = params.get('sort')
  if (sort) {
    const [key, dir] = sort.split('-')
    if (['title','company','portal','location','posted','score'].includes(key) && (dir === 'asc' || dir === 'desc')) {
      f.sort = { key: key as SortKey, dir }
    }
  }
  return f
}

function clamp01(v: number) { return Math.max(0, Math.min(1, v)) }
```

- [ ] **Step 2: Verify round-trip manually if no Vitest**

In a scratch console: `decodeFromHash(encodeToHash(DEFAULT_FILTERS))` should equal `DEFAULT_FILTERS`. Try a non-default and confirm round-trip stable.

- [ ] **Step 3: Commit**

```bash
git add src/Jobmatch.Gui/Client/src/components/longlist/filterState.ts
git commit -m "longlist: filter state model + URL hash codec"
```

---

### Task E2: `LonglistTable` component

**Files:**
- Create: `src/Jobmatch.Gui/Client/src/components/LonglistTable.tsx`

- [ ] **Step 1: Skeleton**

```tsx
import { useMemo, useState } from 'react'
import type { RunDetail, ScoredEntry, ScoreBreakdown } from '../api/types'
import { DEFAULT_FILTERS, type LonglistFilters, type SortKey } from './longlist/filterState'

interface Props {
  data: RunDetail
  filters: LonglistFilters
  onChange: (next: LonglistFilters) => void
  shortlistIds: Set<string>
}

export function LonglistTable({ data, filters, onChange, shortlistIds }: Props) {
  const portalCounts = useMemo(() => countBy(data.scored ?? [], (e) => e.portal), [data.scored])
  const stackCounts = useMemo(() => countStackHits(data.scored ?? []), [data.scored])

  const filtered = useMemo(
    () => applyFilters(data.scored ?? [], filters, data.marks, shortlistIds),
    [data.scored, filters, data.marks, shortlistIds],
  )

  if (!data.scored) return <div className="muted">No ranking data recorded for this run.</div>

  return (
    <section className="longlist">
      <FilterBar
        filters={filters}
        onChange={onChange}
        portalCounts={portalCounts}
        stackCounts={stackCounts}
      />
      <div className="longlist__strip muted">
        {filtered.length} of {data.scored.length}
        {' · sorted by '}{filters.sort.key}{' '}{filters.sort.dir === 'desc' ? '↓' : '↑'}
      </div>
      <div className="table-wrap">
        <table className="table longlist__table">
          <thead>
            <tr>
              <SortableHeader sortKey="title" filters={filters} onChange={onChange}>Title</SortableHeader>
              <SortableHeader sortKey="company" filters={filters} onChange={onChange}>Company</SortableHeader>
              <SortableHeader sortKey="portal" filters={filters} onChange={onChange}>Portal</SortableHeader>
              <SortableHeader sortKey="location" filters={filters} onChange={onChange}>Location</SortableHeader>
              <SortableHeader sortKey="posted" filters={filters} onChange={onChange}>Posted</SortableHeader>
              <SortableHeader sortKey="score" filters={filters} onChange={onChange}>Score</SortableHeader>
              <th>Mark</th>
              <th aria-label="expand"></th>
            </tr>
          </thead>
          <tbody>
            {filtered.map((s) => (
              <Row key={s.id} entry={s} runId={data.runId} mark={data.marks[s.id]} />
            ))}
          </tbody>
        </table>
        {filtered.length === 0 && (
          <div className="muted longlist__empty">
            No listings match these filters.
            {' '}
            <button className="link-button" onClick={() => onChange(DEFAULT_FILTERS)}>Reset</button>
          </div>
        )}
      </div>
    </section>
  )
}
```

- [ ] **Step 2: Implement `FilterBar`**

Inside the same file (it's tightly coupled). Render:
- text input → `q`
- chip cluster from `portalCounts` → `portals`
- pill group `24h/7d/14d/30d/any` → `posted`
- score slider (use a basic `<input type="range">` pair for v1; can swap for a real range component later)
- chip cluster from `stackCounts` → `stackHits`
- segmented control (`all`/`good`/`bad`/`unmarked`) → `mark`
- toggle → `shortlistOnly`
- "Reset" link visible when `!isDefault(filters)`

Each onChange merges the patch and calls `onChange({ ...filters, ...patch })`.

- [ ] **Step 3: Implement `applyFilters`**

```ts
function applyFilters(
  rows: readonly ScoredEntry[],
  f: LonglistFilters,
  marks: Record<string, 'good' | 'bad'>,
  shortlistIds: Set<string>,
): ScoredEntry[] {
  const q = f.q.trim().toLowerCase()
  const portals = new Set(f.portals)
  const stack = new Set(f.stackHits.map((s) => s.toLowerCase()))
  const cutoff = postedCutoff(f.posted)

  const filtered = rows.filter((r) => {
    if (q && !(`${r.title} ${r.company ?? ''}`.toLowerCase().includes(q))) return false
    if (portals.size && !portals.has(r.portal)) return false
    if (cutoff && (!r.postedAt || new Date(r.postedAt).getTime() < cutoff)) return false
    if (r.score < f.scoreMin || r.score > f.scoreMax) return false
    if (stack.size) {
      const hits = [...r.primaryStackHits, ...r.secondaryStackHits].map((s) => s.toLowerCase())
      if (!hits.some((h) => stack.has(h))) return false
    }
    if (f.mark !== 'all') {
      const m = marks[r.id]
      if (f.mark === 'unmarked' ? m !== undefined : m !== f.mark) return false
    }
    if (f.shortlistOnly && !shortlistIds.has(r.id)) return false
    return true
  })

  const cmp = sortComparator(f.sort.key, f.sort.dir)
  return [...filtered].sort(cmp)
}

function postedCutoff(p: LonglistFilters['posted']): number | null {
  if (p === 'any') return null
  const days = p === '24h' ? 1 : p === '7d' ? 7 : p === '14d' ? 14 : 30
  return Date.now() - days * 86_400_000
}

function sortComparator(key: SortKey, dir: 'asc' | 'desc') {
  const sign = dir === 'asc' ? 1 : -1
  return (a: ScoredEntry, b: ScoredEntry) => {
    let v = 0
    switch (key) {
      case 'title':    v = a.title.localeCompare(b.title); break
      case 'company':  v = (a.company ?? '').localeCompare(b.company ?? ''); break
      case 'portal':   v = a.portal.localeCompare(b.portal); break
      case 'location': v = (a.location ?? '').localeCompare(b.location ?? ''); break
      case 'posted':   v = (a.postedAt ?? '').localeCompare(b.postedAt ?? ''); break
      case 'score':    v = a.score - b.score; break
    }
    return sign * v
  }
}

function countBy<T, K extends string>(rows: readonly T[], key: (row: T) => K): Map<K, number> {
  const m = new Map<K, number>()
  for (const r of rows) {
    const k = key(r)
    m.set(k, (m.get(k) ?? 0) + 1)
  }
  return m
}

function countStackHits(rows: readonly ScoredEntry[]): Map<string, number> {
  const m = new Map<string, number>()
  for (const r of rows) {
    for (const h of [...r.primaryStackHits, ...r.secondaryStackHits]) {
      m.set(h, (m.get(h) ?? 0) + 1)
    }
  }
  return m
}
```

- [ ] **Step 4: Implement `Row` (with breakdown bar reuse, mark thumbs, expand)**

Reuse `MarkButton` from `components/MarkButton.tsx`. Reuse `BreakdownBar` from `HistoryPage.tsx` — extract it into `components/BreakdownBar.tsx` so both `HistoryPage` and `LonglistTable` can import it. (Small refactor; do it as part of this step.)

```tsx
function Row({ entry, runId, mark }: { entry: ScoredEntry; runId: string; mark?: 'good' | 'bad' }) {
  const [open, setOpen] = useState(false)
  return (
    <>
      <tr>
        <td><a href={entry.url} target="_blank" rel="noreferrer">{entry.title}</a></td>
        <td>{entry.company ?? <span className="muted">—</span>}</td>
        <td><span className="badge badge--muted">{entry.portal}</span></td>
        <td>{entry.location ?? <span className="muted">—</span>}</td>
        <td className="tabular mono">{entry.postedAt ? formatRelative(entry.postedAt) : <span className="muted">—</span>}</td>
        <td className="tabular mono">
          <div className="longlist__score">
            <span>{entry.score.toFixed(2)}</span>
            <BreakdownBar b={entry.breakdown} />
          </div>
        </td>
        <td><MarkButton runId={runId} listingId={entry.id} current={mark} compact /></td>
        <td><button className="link-button" onClick={() => setOpen(!open)}>{open ? '▾' : '▸'}</button></td>
      </tr>
      {open && (
        <tr><td colSpan={8}><BreakdownDetail entry={entry} /></td></tr>
      )}
    </>
  )
}
```

`MarkButton` may need a `compact` prop — check the existing component, add it if absent. `BreakdownDetail` is the existing per-row expanded breakdown view extracted from `HistoryPage.tsx::RankingRow`; lift it into `components/BreakdownBar.tsx` as a co-located export.

- [ ] **Step 5: Build, smoke-test**

`npm --prefix src/Jobmatch.Gui/Client run build` to typecheck. Visit a run, click Longlist, exercise filters live.

- [ ] **Step 6: Commit**

```bash
git add src/Jobmatch.Gui/Client/src/components/LonglistTable.tsx src/Jobmatch.Gui/Client/src/components/BreakdownBar.tsx src/Jobmatch.Gui/Client/src/components/MarkButton.tsx
git commit -m "LonglistTable: filterable, sortable view of all scored listings"
```

---

### Task E3: Wire `LonglistTable` into `HistoryPage`

**Files:**
- Modify: `src/Jobmatch.Gui/Client/src/pages/HistoryPage.tsx`

- [ ] **Step 1: Replace `RankingTab`**

Delete the existing `RankingTab` component body (lines ~497-553 in the existing file) and `RankingRow` (~555-606). Replace with:

```tsx
function LonglistView({ data }: { data: RunDetail }) {
  const navigate = useNavigate()
  const location = useLocation()
  const filters = useMemo(() => decodeFromHash(new URLSearchParams(location.hash.slice(1))), [location.hash])
  const shortlistIds = useMemo(() => new Set(data.shortlist.map((m) => m.id)), [data.shortlist])

  function setFilters(next: LonglistFilters) {
    const params = encodeToHash(next)
    navigate(`${location.pathname}#${params.toString()}`, { replace: true })
  }

  return (
    <LonglistTable data={data} filters={filters} onChange={setFilters} shortlistIds={shortlistIds} />
  )
}
```

In the `RunDetailView`'s switch on `tab`, replace `tab === 'longlist'  && <RankingTab data={data} />` with `<LonglistView data={data} />`.

Ensure imports include `LonglistTable`, `decodeFromHash`, `encodeToHash`, `LonglistFilters`.

- [ ] **Step 2: Drop dead code**

Remove unused: `RankingTab`, `RankingRow`, `RankingSort` type, `BreakdownBar` (now lives in `components/`), `COMPONENT_LABELS` if exclusively used by the deleted code (audit other tabs first — `BreakdownBar` is also referenced by `RankingRow`'s expanded panel and possibly `DroppedTab`). If `COMPONENT_LABELS` is shared, move it into `components/BreakdownBar.tsx` next to `BreakdownBar`.

- [ ] **Step 3: Build and smoke-test**

`npm --prefix src/Jobmatch.Gui/Client run build` then start the Gui. Navigate to History → a past run → Longlist tab. Verify:
- Filters apply live.
- URL hash updates without page reload.
- Reload browser; filters survive.
- Empty state appears when filters yield 0.
- Sort headers toggle asc/desc with a visible arrow.

- [ ] **Step 4: Commit**

```bash
git add src/Jobmatch.Gui/Client/src/pages/HistoryPage.tsx
git commit -m "HistoryPage: longlist tab uses new LonglistTable"
```

---

### Task E4: CSS for filter bar + table

**Files:**
- Modify: `src/Jobmatch.Gui/Client/src/css/app.css` (or whichever CSS file is loaded; check `main.tsx` for the import)

- [ ] **Step 1: Add scoped styles**

Selectors needed: `.longlist`, `.longlist__strip`, `.longlist__table`, `.longlist__filter-bar`, `.longlist__chips`, `.longlist__chip`, `.longlist__chip--active`, `.longlist__pill-group`, `.longlist__pill--active`, `.longlist__score`, `.longlist__empty`, `.secrets-form`, `.provider-tile__needs-key`. Match the existing navy/white/grey palette and hairline-border idiom (look at `provider-tile`/`audit-tabs` styles for examples). Sticky filter bar: `position: sticky; top: 0; background: var(--c-bg); z-index: 1; padding: var(--space-3) 0; border-bottom: 1px solid var(--c-border);`.

- [ ] **Step 2: Verify visually**

Reload Gui, sanity-check spacing, sticky behavior on scroll, chip active states.

- [ ] **Step 3: Commit**

```bash
git add src/Jobmatch.Gui/Client/src/css/app.css
git commit -m "css: longlist filter bar + table; needs-key badge; secrets form"
```

---

## Phase F — Docs

### Task F1: Update `docs/requirements.md`

**Files:**
- Modify: `docs/requirements.md`

- [ ] **Step 1: Revise R-024**

Old:
> R-024 The system should ship a generic example provider list so a new user sees the expected shape.

New:
> R-024 The system should ship a curated provider catalog as part of the application bundle so a new user can run a search on first launch with no setup.

- [ ] **Step 2: Append two new requirements**

```
## Provider catalog & per-user state

- **R-085** The system should expose a filterable, sortable view of every deduped listing in a run, with at least: portal, score range, posting age, primary/secondary stack hit, mark state, shortlist membership, and free-text search across title and company.
- **R-086** The system should ship the provider catalog as part of the application bundle (read-only at runtime) and store only per-user opt-outs and provider secrets under `data/<email>/`.
```

- [ ] **Step 3: Commit**

```bash
git add docs/requirements.md
git commit -m "requirements: R-024 revised; R-085 longlist filters; R-086 catalog/state split"
```

---

### Task F2: Update `todo.md`

**Files:**
- Modify: `todo.md`

- [ ] **Step 1: Move "Existing-user portal migration" out of Backlog**

Add to Completed (recent) section:

> - **Provider catalog moved into app bundle; per-user state reduced to opt-outs + secrets.** Replaced `data/<email>/portals.yml` (per-user, gitignored, drift-prone) with `src/Jobmatch/Configuration/portals.json` (committed catalog) + `data/<email>/provider-state.json` (opt-out ids and secrets). Removes the existing-user portal migration gap entirely. One-shot startup shim translates any legacy `portals.yml` into the new state file and renames the yaml `.bak`. GUI loses the +Add/Edit/Delete affordances; the toggle and per-provider secrets form remain. New requirements R-085, R-086. Resolves the long-standing "existing-user portal migration" backlog item.

> - **Longlist filterable table.** Replaced the small ranking-table on the History run-detail's Longlist tab with `LonglistTable.tsx`, a filterable/sortable view (search, portal chips, posted-within, score range, stack-hit chips, mark, shortlist-only). State lives in the URL hash so it's bookmarkable and survives refresh. `ScoredEntry` extended with `primaryStackHits`/`secondaryStackHits` so the stack-hit filter has data to filter on.

Remove the line:
> - **Existing-user portal migration.** First-run seeding copies `portals.example.yml` once; existing `data/<email>/portals.yml` files miss new portals shipped after first-run. Add a "new portals available" diff/merge prompt in the GUI Providers page.

- [ ] **Step 2: Add a new Backlog entry**

> - **Remove migration shim.** `PortalsMigrationShim.RunIfNeeded` runs on every Gui startup. After all known users have run the new build at least once, delete the shim, its tests, and the YAML loader's only remaining caller path.

- [ ] **Step 3: Commit**

```bash
git add todo.md
git commit -m "todo: close existing-user portal migration; note shim removal followup"
```

---

## Final verification

- [ ] **Run the full test suite**

```bash
dotnet test src/Jobmatch.Tests
```
Expected: green (137 + ~17 new = ~154 tests).

- [ ] **Build the published binary, smoke-run end-to-end**

```bash
dotnet publish src/Jobmatch.Gui -c Release
# Run the published exe; expect:
# - browser opens
# - Providers page shows ~9 enabled providers (the bundled enabled set), 2 with "needs key"
# - Search runs against bundled providers, produces a shortlist
# - History → past run → Longlist tab → filters apply live, URL hash updates
```

- [ ] **Run `superpowers:verification-before-completion` skill** before declaring complete.

- [ ] **Commit and push**

```bash
git push
```
