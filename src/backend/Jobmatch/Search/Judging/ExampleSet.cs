using Jobmatch.Features.Applications;
using Jobmatch.Features.History;
using Jobmatch.Infrastructure.Paths;

namespace Jobmatch.Search.Judging;

/// <summary>
/// The few-shot examples the judge learns the user's taste from: the curated <c>examples/</c> files
/// plus listings the user marked in previous runs, with their reasons. Curated wins on a
/// (title, company) collision — a hand-written archetype is a deliberate statement, a mark is one
/// data point.
/// </summary>
public sealed class ExampleSet(UserContext ctx, IRunHistoryStore runs, IMarksService marks)
{
    public IReadOnlyList<ExampleListing> Load()
    {
        var curated = ExamplesLoader.Load(ctx.ExamplesDir);
        var marked = MarkedExamplesLoader.Load(runs, marks.LoadAll());
        if (marked.Count == 0) return curated;

        var seen = new HashSet<string>(
            curated.Select(e => $"{e.Title}|{e.Company}"), StringComparer.OrdinalIgnoreCase);
        var merged = new List<ExampleListing>(curated);
        merged.AddRange(marked.Where(m => seen.Add($"{m.Title}|{m.Company}")));
        return merged;
    }
}
