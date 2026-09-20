using George.Common;
using George.Common.Request;
using George.Data.Models;
using George.DB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Attribute = George.DB.Attribute;

namespace George.Data
{
    public class AttributeStorage : StorageBase
    {
        public AttributeStorage(GeorgeDBContext dbContext, ILogger<AttributeStorage> logger)
            : base(dbContext, logger)
        {
        }

        public async Task<DataListResult<Attribute>> GetAttributesAsync(
            AttributeFilter? filter,
            PagingExDto paging,
            CancellationToken cancelToken = default)
        {
            DataListResult<Attribute> res = new DataListResult<Attribute>();

            // Build the query.
            var query = _dbContext.Attribute
                .Include(a => a.Site)
                .Include(a => a.AttributeValue)
                .AsNoTracking();

            // Filter.
            if (filter != null)
            {
                if (filter.Name.HasValue())
                    query = query.Where(a => a.Name.Contains(filter.Name));

                if (filter.SiteIds != null && filter.SiteIds.Any())
                {
                    query = query.Where(a => filter.SiteIds.Contains(a.SiteId));
                }

                if (filter.Search != null && filter.Search.SearchTerm.HasValue())
                {
                    var term = filter.Search.SearchTerm!;
                    query = query.Where(a => a.Name.Contains(term));
                }
            }

            if (paging.IncludeTotal)
                res.Total = await query.CountAsync(cancelToken).ConfigureAwait(false);

            // Add sorting.
            query = query.OrderBy(a => a.Name);

            // Add paging.
            //query = query.Skip(paging.Skip).Take(paging.Take);

            // Get the data from the DB.
            res.Items = await query.ToListAsync(cancelToken).ConfigureAwait(false);

            return res;
        }

        public async Task<Attribute?> GetAttributeAsync(int attributeId, CancellationToken cancelToken)
        {
            return await _dbContext.Attribute
                .Include(a => a.Site)
                .Include(a => a.AttributeValue)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == attributeId, cancelToken);
        }

        /// <param name="preserveValueOrder">
        /// True when the values list order is the user's own (attributes screen) and becomes the manual display order.
        /// False for system-created attributes (Woo sync / product save), which stay "never ordered" (alphabetical).
        /// </param>
        public async Task<Attribute> CreateAttributeAsync(Attribute attribute, List<string>? values, CancellationToken cancelToken, bool preserveValueOrder = false)
        {
            _dbContext.Attribute.Add(attribute);
            await _dbContext.SaveChangesAsync(cancelToken);

            // Add attribute values if provided
            var orderedValues = NormalizeOrderedValues(values);
            if (orderedValues.Count > 0)
            {
                for (var i = 0; i < orderedValues.Count; i++)
                {
                    _dbContext.AttributeValue.Add(new AttributeValue
                    {
                        AttributeId = attribute.Id,
                        Value = orderedValues[i],
                        DisplayOrder = preserveValueOrder ? i : null
                    });
                }
                await _dbContext.SaveChangesAsync(cancelToken);
            }

            return attribute;
        }

        public async Task<Attribute?> UpdateAttributeAsync(Attribute updated, List<string>? values, CancellationToken cancelToken)
        {
            var dbAttr = await _dbContext.Attribute
                .Include(a => a.AttributeValue)
                .FirstOrDefaultAsync(a => a.Id == updated.Id, cancelToken);

            if (dbAttr == null) return null;

            dbAttr.Name = updated.Name;
            dbAttr.SiteId = updated.SiteId != 0 ? updated.SiteId : dbAttr.SiteId;
            dbAttr.UpdatedDate = DateTime.UtcNow;
            dbAttr.UpdateUserId = updated.UpdateUserId;

            // Update attribute values: upsert by value, the request list order becomes the display order.
            if (values != null)
            {
                var orderedValues = NormalizeOrderedValues(values);
                // Matched the way the DB key compares (case/trailing-space insensitive) so a kept value is never delete+insert of the same key.
                var existingByValue = new Dictionary<string, AttributeValue>(StringComparer.OrdinalIgnoreCase);
                foreach (var av in dbAttr.AttributeValue)
                    existingByValue.TryAdd(av.Value.Trim(), av);

                var existingRows = dbAttr.AttributeValue.ToList(); // snapshot: Add() below fixes new rows up into the navigation

                // The list order is persisted only when it carries a manual order: the attribute is already ordered, or
                // this request moved existing values. Otherwise (product editor appending a value, a plain rename...) the
                // attribute stays "never ordered" - alphabetical, and clients keep their numeric-aware sort ("10" after "9").
                var keptInRequestOrder = new List<AttributeValue>();
                foreach (var value in orderedValues)
                {
                    if (existingByValue.TryGetValue(value, out var row) && !keptInRequestOrder.Contains(row))
                        keptInRequestOrder.Add(row);
                }
                var keptInCurrentOrder = existingRows
                    .Where(keptInRequestOrder.Contains)
                    .OrderBy(av => av.DisplayOrder ?? int.MaxValue)
                    .ThenBy(av => av.Value, StringComparer.OrdinalIgnoreCase);
                var persistOrder = existingRows.Any(av => av.DisplayOrder != null)
                    || !keptInRequestOrder.SequenceEqual(keptInCurrentOrder);

                for (var i = 0; i < orderedValues.Count; i++)
                {
                    if (existingByValue.TryGetValue(orderedValues[i], out var existing))
                    {
                        if (persistOrder) existing.DisplayOrder = i;
                    }
                    else
                    {
                        _dbContext.AttributeValue.Add(new AttributeValue
                        {
                            AttributeId = dbAttr.Id,
                            Value = orderedValues[i],
                            DisplayOrder = persistOrder ? i : null
                        });
                    }
                }

                _dbContext.AttributeValue.RemoveRange(existingRows.Where(av => !keptInRequestOrder.Contains(av)));
            }

            await _dbContext.SaveChangesAsync(cancelToken);
            return dbAttr;
        }

        /// <summary>Trimmed, non-empty, de-duplicated (case-insensitive, first wins) values in their original order.</summary>
        private static List<string> NormalizeOrderedValues(List<string>? values)
        {
            var result = new List<string>();
            if (values == null) return result;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in values)
            {
                var value = raw?.Trim();
                if (string.IsNullOrEmpty(value)) continue;
                if (seen.Add(value)) result.Add(value);
            }
            return result;
        }

        /// <summary>
        /// Manual value order per attribute name, for sorting product option values / variants on read.
        /// Only attributes that were actually ordered (any non-null DisplayOrder) are returned, so shops that never
        /// reordered see no change. Scope: one site, or every site of an account (lowest site id wins per attribute name).
        /// </summary>
        public async Task<Dictionary<string, Dictionary<string, int>>> GetAttributeValueOrderMapAsync(int? siteId, int? accountId, CancellationToken cancelToken)
        {
            var map = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase);
            if (!(siteId > 0) && !(accountId > 0)) return map;

            var query = _dbContext.AttributeValue.AsNoTracking()
                .Where(av => !av.Attribute.IsDeleted && av.Attribute.AttributeValue.Any(x => x.DisplayOrder != null));
            query = siteId > 0
                ? query.Where(av => av.Attribute.SiteId == siteId)
                : query.Where(av => av.Attribute.Site.AccountId == accountId);

            var rows = await query
                .Select(av => new { av.AttributeId, av.Attribute.SiteId, av.Attribute.Name, av.Value, av.DisplayOrder })
                .ToListAsync(cancelToken).ConfigureAwait(false);

            foreach (var attr in rows.GroupBy(r => r.AttributeId).OrderBy(g => g.First().SiteId).ThenBy(g => g.Key))
            {
                var name = attr.First().Name?.Trim();
                if (string.IsNullOrEmpty(name) || map.ContainsKey(name)) continue;
                var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                var index = 0;
                foreach (var row in attr.OrderBy(r => r.DisplayOrder ?? int.MaxValue).ThenBy(r => r.Value, StringComparer.OrdinalIgnoreCase))
                {
                    var value = row.Value?.Trim();
                    if (!string.IsNullOrEmpty(value) && !order.ContainsKey(value)) order[value] = index++;
                }
                map[name] = order;
            }
            return map;
        }

        /// <summary>
        /// Distinct (option name, value, product) links for a site's live products - the raw rows behind the per-value
        /// product count. Unions ProductOptionValue with the variants' option values (same sources the Woo sync uses).
        /// </summary>
        public async Task<List<(string OptionName, string Value, int ProductId)>> GetSiteProductOptionValueLinksAsync(int siteId, CancellationToken cancelToken)
        {
            var fromOptions = await _dbContext.ProductOptionValue.AsNoTracking()
                .Where(pov => !pov.ProductOption.IsDeleted
                    && !pov.ProductOption.Product.IsDeleted
                    && pov.ProductOption.Product.Site.Any(s => s.Id == siteId))
                .Select(pov => new { OptionName = pov.ProductOption.Name, pov.Value, pov.ProductOption.ProductId })
                .ToListAsync(cancelToken).ConfigureAwait(false);

            var fromVariants = await _dbContext.ProductVariantOptionValue.AsNoTracking()
                .Where(v => !v.ProductVariant.IsDeleted
                    && !v.ProductVariant.Product.IsDeleted
                    && v.ProductVariant.Product.Site.Any(s => s.Id == siteId))
                .Select(v => new { v.OptionName, Value = v.OptionValue, v.ProductVariant.ProductId })
                .ToListAsync(cancelToken).ConfigureAwait(false);

            return fromOptions.Concat(fromVariants)
                .Where(r => !string.IsNullOrWhiteSpace(r.OptionName) && !string.IsNullOrWhiteSpace(r.Value))
                .Select(r => (OptionName: r.OptionName.Trim(), Value: r.Value.Trim(), r.ProductId))
                .Distinct()
                .ToList();
        }

        public async Task<bool> UpdateAttributeWooCommerceIdAsync(int attributeId, int? wooCommerceId, CancellationToken cancelToken)
        {
            var attribute = await _dbContext.Attribute
                .FirstOrDefaultAsync(a => a.Id == attributeId, cancelToken);

            if (attribute == null) return false;

            attribute.WooCommerceId = wooCommerceId;
            attribute.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

        public async Task<bool> DeleteAttributeAsync(int id, CancellationToken cancelToken = default)
        {
            var dbModel = await _dbContext.Attribute
                .FirstOrDefaultAsync(a => a.Id == id, cancelToken);

            if (dbModel == null) return false;

            dbModel.IsDeleted = true;
            dbModel.UpdatedDate = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancelToken);
            return true;
        }

    }
}

