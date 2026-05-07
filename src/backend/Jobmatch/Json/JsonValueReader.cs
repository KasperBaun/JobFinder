using System.Text.Json;

namespace Jobmatch.Json;

public static class JsonValueReader
{
    public static JsonElement Walk(JsonElement root, string? dottedPath)
    {
        if (string.IsNullOrEmpty(dottedPath)) return root;
        var current = root;
        foreach (var segment in dottedPath.Split('.'))
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out var next))
                return default;
            current = next;
        }
        return current;
    }

    public static string? AsString(JsonElement el) => el.ValueKind switch
    {
        JsonValueKind.String => el.GetString(),
        JsonValueKind.Number => el.ToString(),
        JsonValueKind.True or JsonValueKind.False => el.ToString(),
        _ => null,
    };

    public static string? ReadMappedString(JsonElement item, IReadOnlyDictionary<string, string> mapping, string field)
    {
        if (!mapping.TryGetValue(field, out var path)) return null;
        return AsString(Walk(item, path));
    }
}
