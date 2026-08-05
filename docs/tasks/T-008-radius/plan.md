# T-008 — Radius filter from the user's home address (R-105)

Tester feedback: listings from Bangladesh, Warsaw and Århus show up; wants results
limited to a radius around their home address.

## Design decisions

### D1. Where address + radius live: skillset frontmatter (all of it)

`address`, `radius_km`, and the server-computed geocode result (`latitude`, `longitude`,
`resolved_address`) all go in the skillset — not ranking.yml, not a hybrid.

- The "hard filters live in ranking.yml" precedent (`max_age_days`,
  `require_primary_stack_hit`) covers impersonal tuning knobs with no GUI. The home
  address is personal data, the radius a personal commuting tolerance, and the feature
  is unusable without a GUI (the user must type an address and see whether it resolved).
  The skillset is the GUI-editable personal-profile file; ranking.yml has no editor.
- New frontmatter keys are optional → additive-only convention holds; old skillset.md
  files keep parsing.

The filter is **active** only when `latitude`, `longitude` AND `radius_km > 0` all
exist. Address present but not geocoded → filter inactive (GUI shows a hint).

### D2. Gazetteer: one bundled file, three layers, one code path

No separate "foreign country" branch — country names resolve to a centroid like any
city and get dropped by ordinary haversine. `SplitCityCountry` / `NormaliseCountry`
stay untouched.

Bundled file — `src/backend/Jobmatch/Geo/gazetteer.tsv` (committed, curated once by a
throwaway script that is NOT shipped or committed):

| Layer | Source | Rows (est.) |
|---|---|---|
| DK fine-grained: all postal codes (4-digit + name), city names, 5 regions, Greater Copenhagen/Storkøbenhavn/Hovedstaden | DAWA `/postnumre` + GeoNames DK cities pop ≥ 1,000 | ~1,600 |
| World cities pop ≥ 100,000 (main name + ASCII name + endonym alias only) | GeoNames `cities15000.txt` filtered | ~4,900 |
| Country names (English + native + ISO-2) → capital-city centroid | GeoNames `countryInfo.txt` joined to cities | ~250 |

Format: TSV `name<TAB>aliases(|-sep)<TAB>lat<TAB>lon<TAB>cc<TAB>type(postal|city|region|country)<TAB>population`,
`#` header comments carrying attribution. Target ~300–400 KB, hard ceiling 500 KB.

Licensing: GeoNames is CC-BY 4.0 — attribution in the TSV header + a visible notice
(README line). DAWA/DAR data is free Danish public-sector data (SDFI) — attribution in
the same header.

Bundling: same idiom as `portals.json` in `src/backend/Jobmatch/Jobmatch.csproj`
(`<None Update="Geo/gazetteer.tsv"><CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory><TargetPath>geo/gazetteer.tsv</TargetPath></None>`),
loaded from `Path.Combine(AppContext.BaseDirectory, "geo", "gazetteer.tsv")`.

Resolution rules (deterministic, offline):

- Normalize `listing.Location`: lowercase invariant, split segments on `,` `/` `·` ` - `;
  also test each segment ASCII-folded (æ→ae, å→aa/a…) so "Århus" hits "aarhus".
- Skip resolution entirely if the string contains `worldwide|anywhere|global|remote`
  tokens (mirrors `Ranker.Location.cs`) → falls through un-dropped.
- Per segment: 4-digit DK postal regex → postal lookup; else exact whole-segment
  dictionary lookup (never substring — avoids "Malé" in "male nurse"-class false hits).
- Specificity: postal > city > region > country. Ambiguity (same name, several
  countries): prefer entry in the user's home country, else highest population (bake
  the ordering into the index at load).
- Multi-location listings ("Copenhagen or Aarhus"): resolve every segment, take the
  minimum distance — pass if any site is within radius.

### D3. Filter semantics

Evaluated only in `ClassifyDrop` (the SearchService path). Given an active filter:

1. `RemoteMode.Remote` → exempt (never dropped by radius).
2. Location null/whitespace or unresolvable → falls through un-dropped (all plain-RSS
   listings today).
3. Resolved and `haversine(home, place) > radius_km` → drop `outside_radius`.
   Hybrid, Onsite and Unknown are all subject to the filter.

### D4. Drop-reason precedence

`above_max_age` → **`outside_radius`** → `missing_required_primary` → `disqualifier` →
`below_min_score` (then `beyond_top_n` post-sort, unchanged). Radius is a hard spatial
infeasibility analogous to the hard temporal cutoff, so it slots directly after it.
Update the `DropClassification` doc comment and `DroppedEntry.cs` reason list.

Entry shape (R-103 key+args pattern — numbers stay JSON numbers):

```csharp
return new("outside_radius",
    $"located ~{km} km away ({place}), max {maxKm} km",
    new Dictionary<string, object> { ["km"] = km, ["maxKm"] = maxKm, ["place"] = place });
```

`km` = `(int)Math.Round(distance)`, `maxKm` from the skillset, `place` = matched
gazetteer name.

### D5. Legacy `Ranker.Filter` / `Rank`

Untouched — `Ranker.Rank` has zero production callers (tests only). Note the divergence
in the ClassifyDrop doc comment.

### D6. DAWA geocoding (save-time only — NEVER at rank time)

- `GET https://api.dataforsyningen.dk/adresser?q={address}&struktur=mini&per_side=1`;
  0 rows → one retry against `/adgangsadresser?q=…&struktur=mini&per_side=1`.
  **`x` is longitude, `y` is latitude** — cover the swap with a unit test on a captured
  payload. `betegnelse` = canonical display string. Danish addresses only (GUI copy
  says so).
- New `IGeocodingService` (`Task<GeocodeResult?> GeocodeAsync(string, CancellationToken)`;
  `record GeocodeResult(double Latitude, double Longitude, string ResolvedAddress)`),
  implementation `DawaGeocodingService(HttpClient)`. Registered via
  `services.AddHttpClient<…>(c => c.Timeout = TimeSpan.FromSeconds(5))` — same typed-client
  precedent as `LlmModelDownloader`. Fully fakeable; no live network in CI.
- **Save must always succeed**: not-found → null; timeout/DNS/non-2xx/parse error →
  caught inside the service, logged as warning, null. Null just means lat/lon stay empty
  and the filter is inactive.
- Re-geocode only when `request.Address` is non-blank AND (differs from stored address
  OR stored coords are null). Unchanged address with stored coords → no network call.
  Blank address → clear address/lat/lon/resolved_address.
- Coordinates are server-computed only — `SkillsetUpdateRequest` carries `address` +
  `radiusKm`, never lat/lon.
- CV extraction (`CvProfileExtractor`) is out of scope (fills city, not street address).

## Implementation steps

1. **Geo services** — new `src/backend/Jobmatch/Geo/`:
   `gazetteer.tsv`; `GeoDistance.cs` (`static double HaversineKm(...)`);
   `Gazetteer.cs` (`static Gazetteer LoadBundled()` lazy+cached, `Parse(string)` /
   `FromEntries(...)` test seams, `GeoPlace? Resolve(string? location, string? homeCc)`,
   `record GeoPlace(string Name, double Latitude, double Longitude, string CountryCode, GeoPlaceType Type)`);
   `RadiusFilter.cs` (`static RadiusFilter? Create(Skillset, Gazetteer)` — null when
   inactive; `RadiusVerdict? Evaluate(Listing)`; `record RadiusVerdict(int Km, int MaxKm, string Place)`).
   Respect the 300-line limit (split `Gazetteer.Resolve.cs` partial if needed).
   Add the csproj bundling item.
2. **Skillset** — `Models/Skillset.cs`: init props `string? Address`, `double? RadiusKm`,
   `double? Latitude`, `double? Longitude`, `string? ResolvedAddress`.
   `Configuration/SkillsetParser.cs`: read via `OptionalString` + new `OptionalDouble`
   (InvariantCulture); serialize only when present (Country/Region idiom).
3. **Filter wiring** — `Search/SearchService.Ranking.cs`: `ClassifyDrop(..., RadiusFilter? radius)`
   with the new branch per D4; `BuildShortlist` passes it through.
   `Search/SearchService.cs` `RunAsync`: `RadiusFilter.Create(prep.Skillset, _gazetteer ?? Gazetteer.LoadBundled())`;
   optional `Gazetteer?` ctor param as test seam. `DroppedEntry.cs` doc comment.
4. **Geocoding** — `Services/IGeocodingService.cs` + `Services/DawaGeocodingService.cs`;
   `ISkillsetService`/`SkillsetService.Merge` gains address/radius (+ validate
   `radiusKm >= 0` via `ConfigException`); `Jobmatch.Api/Handlers/SkillsetHandler.cs`
   applies the re-geocode policy (extract `ResolveGeocodeAsync` helper — 50-line method
   limit); `Jobmatch.Api/Models/Skillset.cs` DTOs (+ response-only lat/lon/resolvedAddress);
   DI registration in `JobmatchApiExtensions.cs`.
5. **Frontend** — `api/types.ts` (skillset fields; `DropReason` += `'outside_radius'`);
   `SkillsetPage.tsx` address + radius inputs + resolved/not-resolved status line
   (extract `AddressFields` component if the 300-line limit threatens);
   `SetupPage.tsx` optional fields on the profile step; i18n both locales:
   `server.ts` `dropContext.outside_radius` (en:
   `` located ~${km} km away (${place}), max ${maxKm} km ``), `history.tsx`
   `dropReason.outside_radius` ('too far away' / 'for langt væk'), `skillset` + `setup`
   namespaces; `components.css` `.reason-badge--outside_radius`.
6. **Docs** — R-105 in `docs/requirements.md`; CHANGELOG line; drop the T-008 entry
   from todo.md; GeoNames CC-BY notice.

Draft R-105:

> **R-105** The system should let a user set a home address (geocoded once at
> profile-save time via the public Danish DAWA address API; the resulting coordinates
> persist in the skillset, and a save must succeed even when the address cannot be
> resolved or the machine is offline — the filter simply stays inactive) and a radius
> in km, and at rank time hard-drop listings whose location resolves — offline, against
> a bundled gazetteer of Danish postal codes/cities plus world cities ≥ 100k population
> and country centroids (GeoNames, CC-BY 4.0) — to a point farther than the radius,
> with drop reason `outside_radius` (distance, limit and matched place as message args).
> Fully remote listings are exempt, and listings with no location or an unresolvable
> location pass through undropped. No network at rank time.

## Tests

- `Geo/GeoDistanceTests` — known pairs (Copenhagen↔Aarhus ≈ 157 km, ±3).
- `Geo/GazetteerTests` — postal hit; DK city; ASCII folding ("Århus"); world city
  without country ("Warszawa", "Dhaka"); country-only ("Poland"); home-country-first
  ambiguity; specificity (postal beats country); remote/worldwide → null; whole-segment
  only (no substring hits).
- `Geo/RadiusFilterTests` — inactive when coords/radius missing; remote exemption;
  null-location pass; multi-segment min distance; verdict args.
- `Search/SearchServiceTests` — end-to-end: Warsaw dropped with `{km,maxKm,place}`;
  remote Warsaw shortlisted; null-location shortlisted; old+far listing →
  `above_max_age` (precedence).
- `Configuration/SkillsetParserTests` — round-trip; absent keys → nulls.
- Skillset service/handler tests with fake `IGeocodingService` — changed address
  geocodes; unchanged skips; geocode-null still saves; blank clears.
- `Services/DawaGeocodingServiceTests` — fake `HttpMessageHandler`: x/y swap, fallback
  endpoint, 500/timeout → null.
- Frontend: serverText parity + `Record<DropReason, string>` are enforced by existing
  tests once both locales are updated.

## Known risks (accepted)

Centroid precision ±city size at small radii; DAWA is DK-only; ambiguous world-city
names (mitigated by whole-segment match, 100k population floor, home-country
preference; the History drop view makes any mistake visible).

## Amended 2026-08-05 — what shipping taught us

This document is the original design. An end-to-end pass over a real 2 330-listing
corpus found the resolution rules in **D2** and the semantics in **D3** too naive, and
they were changed. Read `R-105` in `docs/requirements.md` for the current contract; the
rationale is below and in the commit messages (`fix(geo): read a location as the sites
it names`, `feat(geo): answer to the names people actually type`).

- **D2's "take the minimum distance across every resolvable segment" was wrong.** A
  country named beside a city is a qualifier, not a second site, so `"Aarhus, Denmark"`
  measured 44 km to the Danish centroid and passed a 50 km filter (97 listings). Sites
  are now tiered: postal/city → region → country, and only the finest tier present
  counts. Postal and city rank *together*, because collapsing to a single most-specific
  *type* would drop `"Copenhagen, Aarhus or Aalborg"` to whichever site happened to sit
  in the postal layer.
- **D2's "exact whole-segment lookup, never substring" was too strict.** Danish ads write
  `"Silkeborg, Roskilde og mulighed for hjemmearbejde"`; the near site was invisible and
  the listing was hard-dropped at 169 km. Segments that fail as a whole are now split on
  `; & + |`, dashes and `og/eller/and/or/med` — whole-segment first, so `"Trinidad and
  Tobago"` and `"Aix-en-Provence"` survive — with fragments under three characters
  ignored so a split cannot surface the index's two-letter country aliases.
- **D2's DK postal regex `\b(\d{4})\b` fired on foreign postcodes.** `"Philippines,
  Pasig, 1600"` resolved to København V. The run must now stand as its own token (the
  canonical `DK-2800` prefix aside) in a string where nothing foreign resolved.
- **D3.1-3.3 needed a fourth rule.** A region or country *in the user's own country* is
  stored as one centroid but covers ground the user may live on — a Copenhagen user had
  Capital-Region jobs hidden as "~39 km away", and an Aarhus user lost every listing
  labelled only `"Danmark"`. Those now pass through as unstated, like any location the
  gazetteer cannot place. Foreign countries still filter normally.
- **The gazetteer's ≥ 100k floor was fine, but its *names* were not.** The index answered
  only to the exact GeoNames toponym: bare `"New York"`, `"USA"`, the Danish exonyms the
  UI itself ships, and `"Frankfurt"` (stored as `Frankfurt am Main`) all failed. See the
  curated block at the end of `gazetteer.tsv` for the 18 sub-floor rows added for the
  Øresund and Schleswig-Holstein commuter band, which a regeneration must preserve.
- **The remote exemption was the largest hole, and it was not in this feature at all.**
  `RemoteMode` came from a substring test over the whole ad, so 179 listings that
  explicitly *deny* remote work were exempt from the filter (R-110 fixed the inference).
  A hard filter is only as good as the flag that bypasses it.

Residual gaps, each measured, are listed in `todo.md` under the backlog.
