using System.Text.Json;
using Jobmatch.Domain;

namespace Jobmatch.Pipeline.Adapters;

/// <summary>
/// Structured remote-mode extraction. Several ATS payloads state the work arrangement outright as a
/// field; reading it beats inferring it from ad prose, so <see cref="BaseAdapter.InferRemoteMode"/>
/// runs only when the source is silent. Entry point: <see cref="BaseAdapter.ResolveRemoteMode"/>.
/// </summary>
public abstract partial class BaseAdapter
{
    internal static RemoteMode ResolveRemoteMode(string? title, string? location, string? description, JsonElement raw) =>
        TryReadStructuredRemoteMode(raw) ?? InferRemoteMode(title, location, description);

    // Field names verified against live payloads on 2026-08-06:
    //   Oracle Recruiting Cloud  WorkplaceTypeCode  ORA_REMOTE / ORA_HYBRID / ORA_ON_SITE, or absent
    //   Ashby                    workplaceType      Remote / Hybrid / Onsite
    //   Lever                    workplaceType      remote / hybrid / onsite / unspecified
    //   Workday                  remoteType         only in the CXS *detail* JSON, applied during
    //                                               enrichment; harmless to look for here too
    //   SmartRecruiters          location.remote / location.hybrid — see SmartRecruitersLocationFlags
    // The Oracle text field is last because it is localisable; the code is stable.
    internal static RemoteMode? TryReadStructuredRemoteMode(JsonElement raw)
    {
        if (raw.ValueKind != JsonValueKind.Object) return null;
        return MapWorkplaceToken(FirstString(raw, WorkplaceTypeFields)) ?? SmartRecruitersLocationFlags(raw);
    }

    private static readonly string[] WorkplaceTypeFields =
        ["WorkplaceTypeCode", "workplaceType", "remoteType", "WorkplaceType"];

    // Every vendor spells the same three arrangements differently, and every one of them also has a
    // value that states nothing — "Flexible", "unspecified", "". Those map to null so inference still
    // gets its turn; claiming an arrangement the employer didn't state is the error that matters.
    internal static RemoteMode? MapWorkplaceToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var token = value.Trim().ToLowerInvariant().Replace('_', ' ').Replace('-', ' ');
        if (token.StartsWith("ora ", StringComparison.Ordinal)) token = token[4..];
        return token switch
        {
            "remote" or "fully remote" or "remote first" => RemoteMode.Remote,
            "hybrid" => RemoteMode.Hybrid,
            "onsite" or "on site" or "site based" or "office based" or "in office" => RemoteMode.Onsite,
            _ => null,
        };
    }

    // SmartRecruiters exposes location.remote and location.hybrid as plain booleans with no "unset"
    // state. Four of the five DK employers we poll leave both false on every posting they publish,
    // so a false is the editor's default rather than the employer saying "onsite" — only a true is
    // evidence. Reading them as onsite would zero the location score for a hybrid-seeking user.
    private static RemoteMode? SmartRecruitersLocationFlags(JsonElement raw)
    {
        if (!raw.TryGetProperty("location", out var location) || location.ValueKind != JsonValueKind.Object)
            return null;
        if (IsTrue(location, "remote")) return RemoteMode.Remote;
        if (IsTrue(location, "hybrid")) return RemoteMode.Hybrid;
        return null;
    }

    private static bool IsTrue(JsonElement obj, string name) =>
        obj.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static string? FirstString(JsonElement obj, string[] names)
    {
        foreach (var name in names)
        {
            if (obj.TryGetProperty(name, out var value)
                && value.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(value.GetString()))
            {
                return value.GetString();
            }
        }
        return null;
    }
}
