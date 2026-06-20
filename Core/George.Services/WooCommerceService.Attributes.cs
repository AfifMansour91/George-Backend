using George.DB;
using George.Services.Response;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Attribute = George.DB.Attribute;

namespace George.Services
{
    /// <summary>WooCommerce global product attributes: import linking and name/slug resolution.</summary>
    public partial class WooCommerceService
    {
        private sealed class WooImportAttributeCatalog
        {
            public Dictionary<int, string> DisplayNameByWooId { get; } = new();
            public Dictionary<string, string> DisplayNameBySlug { get; } = new(StringComparer.OrdinalIgnoreCase);
        }

        private static string NormalizeAttributeName(string? name) => (name ?? string.Empty).Trim();

        private static string NormalizeWooAttributeSlugKey(string? slugOrName)
        {
            var key = NormalizeAttributeName(slugOrName);
            if (key.StartsWith("pa_", StringComparison.OrdinalIgnoreCase))
                key = key[3..];
            return key;
        }

        private async Task<WooImportAttributeCatalog> UpsertAttributesFromWooAsync(
            GeorgeDBContext db,
            Site siteForImport,
            HttpClient httpClient,
            string baseUrl,
            WooCommerceImportFromWooRes stats,
            CancellationToken cancelToken)
        {
            var catalog = new WooImportAttributeCatalog();
            var wooAttributes = await FetchWooPagedAsync<WooCommerceAttributeResponse>(
                httpClient,
                $"{baseUrl}/products/attributes",
                cancelToken);
            if (wooAttributes.Count == 0)
                return catalog;

            var siteId = siteForImport.Id;
            var existingAttributes = await db.Attribute
                .Include(a => a.AttributeValue)
                .Where(a => !a.IsDeleted && a.SiteId == siteId)
                .ToListAsync(cancelToken);

            var existingByWooId = existingAttributes
                .Where(a => a.WooCommerceId is > 0)
                .GroupBy(a => a.WooCommerceId!.Value)
                .ToDictionary(g => g.Key, g => g.First());

            var existingByName = existingAttributes
                .GroupBy(a => NormalizeAttributeName(a.Name).ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.First());

            foreach (var wooAttr in wooAttributes)
            {
                var displayName = NormalizeAttributeName(wooAttr.name);
                if (string.IsNullOrEmpty(displayName))
                    continue;

                catalog.DisplayNameByWooId[wooAttr.id] = displayName;
                if (!string.IsNullOrWhiteSpace(wooAttr.slug))
                    catalog.DisplayNameBySlug[NormalizeWooAttributeSlugKey(wooAttr.slug)] = displayName;
                catalog.DisplayNameBySlug[NormalizeWooAttributeSlugKey(displayName)] = displayName;

                Attribute? local = null;
                if (existingByWooId.TryGetValue(wooAttr.id, out var byWooId))
                    local = byWooId;
                else if (existingByName.TryGetValue(displayName.ToLowerInvariant(), out var byName))
                    local = byName;

                var termNames = await GetWooCommerceAttributeTermNamesAsync(
                    baseUrl,
                    wooAttr.id,
                    httpClient,
                    cancelToken);

                if (local == null)
                {
                    local = new Attribute
                    {
                        Name = displayName,
                        SiteId = siteId,
                        WooCommerceId = wooAttr.id,
                        CreationTime = DateTime.UtcNow,
                        GuidId = Guid.NewGuid(),
                        IsDeleted = false,
                    };
                    db.Attribute.Add(local);
                    await db.SaveChangesAsync(cancelToken);
                    stats.Attributes.Created++;

                    foreach (var term in termNames)
                    {
                        db.AttributeValue.Add(new AttributeValue
                        {
                            AttributeId = local.Id,
                            Value = term,
                        });
                    }
                    if (termNames.Count > 0)
                        await db.SaveChangesAsync(cancelToken);

                    existingByWooId[wooAttr.id] = local;
                    existingByName[displayName.ToLowerInvariant()] = local;
                    continue;
                }

                var changed = false;
                if (local.WooCommerceId != wooAttr.id)
                {
                    local.WooCommerceId = wooAttr.id;
                    changed = true;
                }
                if (!string.Equals(NormalizeAttributeName(local.Name), displayName, StringComparison.OrdinalIgnoreCase))
                {
                    local.Name = displayName;
                    changed = true;
                }

                var existingValues = local.AttributeValue
                    .Select(v => v.Value?.Trim() ?? string.Empty)
                    .Where(v => v.Length > 0)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                foreach (var term in termNames)
                {
                    if (existingValues.Contains(term))
                        continue;
                    db.AttributeValue.Add(new AttributeValue
                    {
                        AttributeId = local.Id,
                        Value = term,
                    });
                    changed = true;
                }

                if (changed)
                {
                    local.UpdatedDate = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancelToken);
                    stats.Attributes.Updated++;
                }
            }

            return catalog;
        }

        private static string ResolveWooImportAttributeDisplayName(
            WooImportProductAttributeItem attr,
            WooImportAttributeCatalog? attributeCatalog)
        {
            var rawName = NormalizeAttributeName(attr.name);
            if (attributeCatalog == null || string.IsNullOrEmpty(rawName))
                return rawName;

            if (attr.id is > 0
                && attributeCatalog.DisplayNameByWooId.TryGetValue(attr.id.Value, out var byId))
                return byId;

            if (attributeCatalog.DisplayNameBySlug.TryGetValue(NormalizeWooAttributeSlugKey(rawName), out var bySlug))
                return bySlug;

            return rawName;
        }
    }
}
