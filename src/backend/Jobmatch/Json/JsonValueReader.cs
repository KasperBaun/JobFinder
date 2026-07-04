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
            // Numeric segments index into arrays (e.g. Oracle Recruiting's
            // "items.0.requisitionList"); object properties — even ones literally
            // named "0" — keep resolving by name.
            if (current.ValueKind == JsonValueKind.Array)
            {
                if (!int.TryParse(segment, out var idx) || idx < 0 || idx >= current.GetArrayLength())
                    return default;
                current = current[idx];
                continue;
            }
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
