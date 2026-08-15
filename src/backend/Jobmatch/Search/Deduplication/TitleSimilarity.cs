namespace Jobmatch.Search.Deduplication;

/// <summary>
/// Token-level title comparison for the probabilistic matcher. Titles are compared as sets of
/// tokens — portals re-punctuate freely ("C#/.NET" vs "C# .NET") but rarely re-word — with a
/// separate seniority signature, because "Senior X" vs "Lead X" is near-identical text
/// describing two different jobs.
/// </summary>
internal static class TitleSimilarity
{
    // '#' and '+' stay inside tokens ("c#", "c++"); a leading '.' stays (".net"). Hyphens split:
    // "full-stack" vs "full stack" then meet, and "fullstack" is a miss either way.
    private static readonly char[] Separators =
        [' ', ',', '(', ')', '[', ']', '{', '}', '/', '\\', '|', '&', ':', ';', '"', '\'', '!', '?', '–', '—', '-'];

    private static readonly HashSet<string> SeniorityTokens = new(StringComparer.Ordinal)
    {
        "junior", "graduate", "student", "studerende", "studentermedhjælper", "intern",
        "trainee", "praktikant", "senior", "lead", "principal", "staff", "chief", "head",
    };

    // A named stack is a role-defining claim: a ".Net udvikler" and a "Java udvikler" are two
    // jobs however similar the rest of the title reads. Tokens map to a *family* before the
    // comparison — C#/.NET/ASP.NET are near-synonyms in ads, and "dotnet" is ".NET" spelled for
    // a URL — so only a cross-family divergence counts. Ambiguous English words ("go", "c")
    // stay out of the list: a false stack conflict silently unmerges a real duplicate.
    private static readonly Dictionary<string, string> StackTokenFamily = new(StringComparer.Ordinal)
    {
        ["c#"] = "dotnet", ["csharp"] = "dotnet", [".net"] = "dotnet", ["dotnet"] = "dotnet",
        ["asp.net"] = "dotnet", ["vb.net"] = "dotnet",
        ["java"] = "java",
        ["kotlin"] = "kotlin",
        ["python"] = "python", ["django"] = "python",
        ["golang"] = "golang",
        ["rust"] = "rust",
        ["php"] = "php", ["laravel"] = "php",
        ["ruby"] = "ruby", ["rails"] = "ruby",
        ["c++"] = "cpp", ["cpp"] = "cpp",
        ["node"] = "node", ["nodejs"] = "node", ["node.js"] = "node",
        ["react"] = "react", ["reactjs"] = "react",
        ["angular"] = "angular", ["angularjs"] = "angular",
        ["vue"] = "vue", ["vuejs"] = "vue",
        ["typescript"] = "typescript",
        ["azure"] = "azure",
        ["aws"] = "aws",
    };

    /// <summary>Distinct tokens of an already-normalised (lowercased, entity-decoded) title.</summary>
    internal static HashSet<string> Tokenise(string normalisedTitle)
    {
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in normalisedTitle.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            var token = raw.TrimEnd('.');
            if (token.Length > 0) tokens.Add(token);
        }
        return tokens;
    }

    internal static double Jaccard(IReadOnlySet<string> a, IReadOnlySet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        var intersection = a.Count(b.Contains);
        var union = a.Count + b.Count - intersection;
        return (double)intersection / union;
    }

    /// <summary>
    /// True when the two titles claim conflicting seniorities. A subset relation is not a
    /// conflict: "Senior/Lead X" spans "Senior X" (one portal's rendering of the same ad),
    /// and an unstated seniority spans everything.
    /// </summary>
    internal static bool SeniorityConflicts(IReadOnlySet<string> a, IReadOnlySet<string> b)
    {
        var sigA = a.Where(SeniorityTokens.Contains).ToHashSet(StringComparer.Ordinal);
        var sigB = b.Where(SeniorityTokens.Contains).ToHashSet(StringComparer.Ordinal);
        return !sigA.IsSubsetOf(sigB) && !sigB.IsSubsetOf(sigA);
    }

    /// <summary>
    /// True when the two titles name diverging stack families (".Net udvikler" vs "Java
    /// udvikler"). Subset spans, same as seniority: naming no stack — or a subset of the
    /// other title's stacks — is compatible with being the same ad.
    /// </summary>
    internal static bool StackConflicts(IReadOnlySet<string> a, IReadOnlySet<string> b)
    {
        var sigA = Families(a);
        var sigB = Families(b);
        return !sigA.IsSubsetOf(sigB) && !sigB.IsSubsetOf(sigA);
    }

    private static HashSet<string> Families(IReadOnlySet<string> tokens)
    {
        var families = new HashSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (StackTokenFamily.TryGetValue(token, out var family)) families.Add(family);
        }
        return families;
    }
}
