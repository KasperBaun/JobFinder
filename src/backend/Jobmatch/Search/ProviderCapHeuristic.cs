using System.Globalization;
using Jobmatch.Models;

namespace Jobmatch.Search;

// Some sources cap results with a server-side `limit` query param instead of pagination (SmartRecruiters
// limit=100, Oracle limit=200). Those never enter a pagination loop, so HitPageCap can't see them. Heuristic:
// if a non-paginating source declares a numeric limit and returned exactly that many, it was probably capped.
public static class ProviderCapHeuristic
{
    public static bool LimitReached(PortalConfig portal, int fetchedCount)
    {
        if (portal.Pagination is not null) return false;          // pagination cap is detected properly elsewhere
        if (portal.QueryParams is null || fetchedCount <= 0) return false;
        if (!portal.QueryParams.TryGetValue("limit", out var raw) || raw is null) return false;

        var s = Convert.ToString(raw, CultureInfo.InvariantCulture);
        return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit)
            && limit > 0
            && fetchedCount == limit;
    }
}
