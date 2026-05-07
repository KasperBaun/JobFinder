namespace Jobmatch.Json;

public static class StringTemplate
{
    public static string Render(string template, Func<string, string?> resolve)
    {
        var result = template;
        var start = result.IndexOf('{');
        while (start >= 0)
        {
            var end = result.IndexOf('}', start);
            if (end < 0) break;
            var key = result.Substring(start + 1, end - start - 1);
            var value = resolve(key) ?? string.Empty;
            result = string.Concat(result.AsSpan(0, start), value, result.AsSpan(end + 1));
            start = result.IndexOf('{', start + value.Length);
        }
        return result;
    }
}
