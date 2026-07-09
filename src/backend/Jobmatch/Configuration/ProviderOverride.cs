using System.Text.Json.Serialization;

namespace Jobmatch.Configuration;

/// <summary>Per-user tuning of a source's fetch knobs, layered over the shipped catalog defaults by
/// <see cref="ProviderStateMerger"/>. Every field is nullable — null means "use the catalog default".
/// MaxPages/PageSize are no-ops for sources without a pagination block.</summary>
public sealed record ProviderOverride(
    int? MaxPages = null,
    int? PageSize = null,
    double? RateLimitRps = null,
    bool? EnrichBody = null)
{
    [JsonIgnore]
    public bool IsEmpty => MaxPages is null && PageSize is null && RateLimitRps is null && EnrichBody is null;
}
