using George.DB;

namespace George.Services;

/// <summary>
/// Multi-site notification settings resolution.
/// A row with SiteId == null is the account default; a row with SiteId set is a FULL per-site
/// override (whole-row copy) — when it exists it wins entirely, there is no field-level fallback.
/// </summary>
public static class NotificationSettingsResolver
{
    /// <summary>Effective settings for a site: the site's override row if present, else the account default row.</summary>
    public static AccountNotificationSettings? Resolve(Account? account, int? siteId) =>
        Resolve(account?.AccountNotificationSettings, siteId);

    public static AccountNotificationSettings? Resolve(IEnumerable<AccountNotificationSettings>? rows, int? siteId)
    {
        if (rows == null)
            return null;

        AccountNotificationSettings? accountDefault = null;
        foreach (var row in rows)
        {
            if (row.IsDeleted)
                continue;
            if (siteId.HasValue && row.SiteId == siteId.Value)
                return row;
            if (row.SiteId == null)
                accountDefault = row;
        }
        return accountDefault;
    }

    /// <summary>True when the site has its own override row (as opposed to inheriting the account default).</summary>
    public static bool HasSiteOverride(IEnumerable<AccountNotificationSettings>? rows, int siteId) =>
        rows != null && rows.Any(r => !r.IsDeleted && r.SiteId == siteId);
}
