using System.Text.Json;

namespace Jobmatch.Models;

public enum RemoteMode { Onsite, Hybrid, Remote, Unknown }

public sealed record Listing(
    string Id,
    string Portal,
    string Title,
    string? Company,
    string? Location,
    RemoteMode RemoteMode,
    string Description,
    Uri Url,
    DateTimeOffset? PostedAt,
    DateTimeOffset FetchedAt,
    JsonElement Raw);
