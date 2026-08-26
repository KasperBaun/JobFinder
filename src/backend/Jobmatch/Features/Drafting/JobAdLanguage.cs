namespace Jobmatch.Features.Drafting;

/// <summary>What an application is written in. The GUI ships English and Danish, and so does this.</summary>
public enum DraftLanguage
{
    English,
    Danish,
}

// Which language the employer advertised in — the language they expect an application back in. Asking
// the model to infer this from the ad does not work: a Danish ad carries an English company boilerplate,
// an English ad carries a Danish job title, and a small model follows whichever it saw most of. Deciding
// it here and stating it in the prompt is the difference between a sendable letter and an unusable one.
public static class JobAdLanguage
{
    // Function words, not vocabulary: they appear at a rate that barely moves between ads, and none of
    // them are words an English ad picks up from a Danish company name or address.
    private static readonly HashSet<string> DanishFunctionWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "og", "til", "med", "som", "der", "ikke", "vi", "du", "har", "er", "af", "en", "et", "på",
        "kan", "skal", "ved", "eller", "hvor", "vores", "dig", "at", "det", "den", "for", "være",
        "bliver", "både", "samt", "hos", "vil", "man", "sig", "men", "om", "de", "din", "dine",
    };

    /// <summary>
    /// Measured across the shipped portals, a Danish ad runs 0.10-0.34 and an English one 0.008-0.017,
    /// so this sits an order of magnitude clear of both.
    /// </summary>
    private const double DanishShareThreshold = 0.05;

    public static DraftLanguage Of(string? adText)
    {
        if (string.IsNullOrWhiteSpace(adText)) return DraftLanguage.English;

        var words = 0;
        var danish = 0;
        foreach (var token in adText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var word = token.Trim('.', ',', ':', ';', '!', '?', '(', ')', '"', '\'', '-', '–', '/');
            if (word.Length == 0 || !word.All(char.IsLetter)) continue;

            words++;
            if (DanishFunctionWords.Contains(word)) danish++;
        }

        return words > 0 && (double)danish / words >= DanishShareThreshold
            ? DraftLanguage.Danish
            : DraftLanguage.English;
    }
}
