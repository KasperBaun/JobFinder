using System.Globalization;

namespace Jobmatch.Jobs;

/// <summary>Filename-safe run identifier: <c>yyyyMMdd-HHmmss-&lt;6 hex&gt;</c> (UTC). Shared by the job and the search pipeline so the JobSearch record and the history file share one id.</summary>
public static class RunId
{
    public static string New(DateTimeOffset startedAt)
    {
        var stamp = startedAt.UtcDateTime.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        var suffix = Guid.NewGuid().ToString("N")[..6];
        return $"{stamp}-{suffix}";
    }
}
