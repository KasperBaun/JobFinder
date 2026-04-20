using System.Text;
using Jobmatch.Models;

namespace Jobmatch.Output;

public static class MarkdownReportWriter
{
    public static void WriteListings(IReadOnlyList<Listing> listings, string path, string title = "Job listings")
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(title).Append('\n').Append('\n');
        sb.Append($"_Generated {DateTimeOffset.Now:O} — {listings.Count} listing(s)_").Append('\n').Append('\n');

        if (listings.Count == 0)
        {
            sb.Append("_No listings to show._\n");
        }
        else
        {
            foreach (var (listing, index) in listings.Select((l, i) => (l, i + 1)))
            {
                sb.Append($"## {index}. {listing.Title}").Append('\n');
                var meta = new List<string>();
                if (!string.IsNullOrWhiteSpace(listing.Company)) meta.Add($"**{listing.Company}**");
                if (!string.IsNullOrWhiteSpace(listing.Location)) meta.Add(listing.Location!);
                if (listing.RemoteMode != RemoteMode.Unknown) meta.Add(listing.RemoteMode.ToString().ToLowerInvariant());
                if (listing.PostedAt.HasValue) meta.Add($"posted {listing.PostedAt.Value:yyyy-MM-dd}");
                meta.Add($"via {listing.Portal}");
                sb.Append(string.Join(" — ", meta)).Append('\n').Append('\n');
                sb.Append($"[{listing.Url}]({listing.Url})").Append('\n').Append('\n');
            }
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, sb.ToString());
    }

    public static void WriteMatches(IReadOnlyList<Match> matches, string path, string title = "Top matches")
    {
        var sb = new StringBuilder();
        sb.Append("# ").Append(title).Append('\n').Append('\n');
        sb.Append($"_Generated {DateTimeOffset.Now:O} — {matches.Count} match(es)_").Append('\n').Append('\n');

        if (matches.Count == 0)
        {
            sb.Append("_No matches above the minimum score threshold._\n");
        }
        else
        {
            foreach (var (match, index) in matches.Select((m, i) => (m, i + 1)))
            {
                var l = match.Listing;
                sb.Append($"## {index}. {l.Title} — score {match.Score:0.00}").Append('\n');
                var meta = new List<string>();
                if (!string.IsNullOrWhiteSpace(l.Company)) meta.Add($"**{l.Company}**");
                if (!string.IsNullOrWhiteSpace(l.Location)) meta.Add(l.Location!);
                if (l.RemoteMode != RemoteMode.Unknown) meta.Add(l.RemoteMode.ToString().ToLowerInvariant());
                if (l.PostedAt.HasValue) meta.Add($"posted {l.PostedAt.Value:yyyy-MM-dd}");
                meta.Add($"via {l.Portal}");
                sb.Append(string.Join(" — ", meta)).Append('\n').Append('\n');

                sb.Append($"[{l.Url}]({l.Url})").Append('\n').Append('\n');

                sb.Append("| signal | score |\n| --- | --- |\n");
                foreach (var kvp in match.Breakdown)
                {
                    sb.Append($"| {kvp.Key} | {kvp.Value:0.00} |\n");
                }
                sb.Append('\n');

                sb.Append("**Reasoning:** ").Append(match.Reasoning.Notes).Append("\n\n");
                if (match.Reasoning.PrimaryStackHits.Count > 0)
                {
                    sb.Append($"- primary stack hits: {string.Join(", ", match.Reasoning.PrimaryStackHits)}\n");
                }
                if (match.Reasoning.SecondaryStackHits.Count > 0)
                {
                    sb.Append($"- secondary stack hits: {string.Join(", ", match.Reasoning.SecondaryStackHits)}\n");
                }
                if (match.Reasoning.DomainHits.Count > 0)
                {
                    sb.Append($"- domain hits: {string.Join(", ", match.Reasoning.DomainHits)}\n");
                }
                if (match.Reasoning.DisqualifierHits.Count > 0)
                {
                    sb.Append($"- disqualifier hits: {string.Join(", ", match.Reasoning.DisqualifierHits)}\n");
                }
                sb.Append('\n');
            }
        }

        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, sb.ToString());
    }
}
