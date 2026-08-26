using Jobmatch.Infrastructure.IO;
using Jobmatch.Infrastructure.Paths;

namespace Jobmatch.Features.Cv;

public sealed class CvDocumentStore(UserContext ctx) : ICvDocumentStore
{
    public string? Find()
    {
        if (!File.Exists(ctx.CvPath)) return null;
        var text = File.ReadAllText(ctx.CvPath);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    public void Save(string text)
    {
        var normalized = CvTextNormalizer.Normalize(text);
        if (normalized.Length == 0)
            throw new InvalidRequestException("The CV text is empty.");

        AtomicFile.WriteAllText(ctx.CvPath, normalized);
    }
}
