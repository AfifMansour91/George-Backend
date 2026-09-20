using George.Data;
using George.DB;
using George.Services.Bundles;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    /// <summary>
    /// Bundles (מארזים) in the outgoing order push (oc-storeos <c>POST /orders</c>) - BUNDLES_SYNC_SPEC.md §6.
    /// A bundle parent line is sent as one item carrying a <c>bundle</c> object (definition + component slots with
    /// per-site Woo ids, swaps, surcharges and weighed quantities); child lines are never sent as items.
    /// </summary>
    public partial class WooCommerceService
    {
        /// <summary>
        /// The §6 <c>bundle</c> object of one parent line. Pure: ids are already resolved per site
        /// (<paramref name="wooProductIds"/> / <paramref name="wooVariationIds"/> keyed by George ids).
        /// </summary>
        public static Dictionary<string, object?> BuildOcStoreosBundleObject(
            OrderItem parent,
            IReadOnlyList<OrderItem> children,
            BundleDefinition? definition,
            IReadOnlyDictionary<int, int> wooProductIds,
            IReadOnlyDictionary<int, int> wooVariationIds)
        {
            var cfg = definition?.Config;
            var slots = definition != null ? BundleOrderLineBuilder.OrderedSlots(definition) : new List<ProductBundleComponent>();
            var bundles = parent.Quantity > 0m ? parent.Quantity : 1m;

            var components = new List<Dictionary<string, object?>>();
            var position = 0;
            foreach (var child in children.OrderBy(c => c.BundleComponentIndex ?? int.MaxValue).ThenBy(c => c.SortOrder).ThenBy(c => c.Id))
            {
                var slot = child.BundleComponentId.HasValue ? slots.FirstOrDefault(s => s.Id == child.BundleComponentId.Value) : null;
                var index = child.BundleComponentIndex ?? (slot != null ? slots.IndexOf(slot) : position);
                var key = slot != null ? (string.IsNullOrEmpty(slot.ComponentKey) ? "c" + slot.Id : slot.ComponentKey) : null;

                int? productWooId = child.ProductId.HasValue && wooProductIds.TryGetValue(child.ProductId.Value, out var pw)
                    ? pw
                    : child.WooCommerceProductId;
                var variationWooId = child.ProductVariantId.HasValue && wooVariationIds.TryGetValue(child.ProductVariantId.Value, out var vw)
                    ? vw
                    : (child.WooCommerceVariationId ?? 0);
                int? swappedFromWooId = child.SwappedFromProductId.HasValue && wooProductIds.TryGetValue(child.SwappedFromProductId.Value, out var sw)
                    ? sw
                    : null;

                // The configured original variation (Woo id) when the slot holds a swap; null otherwise.
                int? swappedFromVariationWooId = swappedFromWooId.HasValue && slot?.ComponentVariantId is > 0
                    && wooVariationIds.TryGetValue(slot.ComponentVariantId.Value, out var sfv)
                    ? sfv
                    : null;

                var isWeight = string.Equals(child.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase);
                // A swap to a product measured differently (portions ↔ kg) changed the LINE's unit and quantity in
                // George; the definition's unit / qty no longer describe it, so the line's own values are sent.
                var slotIsWeight = slot != null && BundleOrderLineBuilder.IsWeightSlot(slot, slot.ComponentProduct);
                var followsSlot = slot != null && !(child.SwappedFromProductId.HasValue && slotIsWeight != isWeight);
                // A swapped child may also carry the alternative's OWN quantity - the line is what ships.
                var qtyPerBundle = followsSlot && !child.SwappedFromProductId.HasValue
                    ? slot!.Qty
                    : BundlePricingEngine.Round4(child.Quantity / bundles);
                var unit = followsSlot && !string.IsNullOrWhiteSpace(slot!.Unit) ? slot.Unit : (isWeight ? "kg" : "unit");
                // Plugin vocabulary: units | units_weight | weight.
                var mode = followsSlot && !string.IsNullOrWhiteSpace(slot!.Mode) ? slot.Mode : (isWeight ? "weight" : "units");
                // Line total across all bundles once weighed in George (confirmed picks only). George stores kg;
                // the plugin reads actualQty in the emitted unit, so a grams slot gets grams.
                // In the slot unit: a weighed-piece line (200 g portions) stores kg, the slot counts pieces.
                decimal? actualQty = child.PickingUserConfirmed && child.PickedQuantity.HasValue ? BundleOrderLineBuilder.PickedQuantityInSlotUnit(child) : null;
                if (actualQty.HasValue && IsGramsUnit(unit))
                    actualQty = actualQty.Value * 1000m;

                components.Add(new Dictionary<string, object?>
                {
                    ["index"] = index,
                    ["key"] = key,
                    ["productId"] = productWooId,
                    ["variationId"] = variationWooId,
                    ["sku"] = child.LineSku,
                    ["name"] = child.Title,
                    ["qty"] = qtyPerBundle,
                    ["unit"] = unit,
                    ["mode"] = mode,
                    ["unitWeight"] = followsSlot ? slot?.UnitWeightKg : null,
                    ["swappedFromProductId"] = swappedFromWooId,
                    ["swappedFromVariationId"] = swappedFromVariationWooId,
                    ["surcharge"] = child.SwapSurcharge ?? 0m,
                    ["actualQty"] = actualQty,
                    ["description"] = slot?.Description ?? string.Empty,
                });
                position++;
            }

            int? bundleWooId = parent.BundleProductId.HasValue && wooProductIds.TryGetValue(parent.BundleProductId.Value, out var bw)
                ? bw
                : parent.WooCommerceProductId;

            var bundle = new Dictionary<string, object?>
            {
                ["externalId"] = parent.BundleProductId.HasValue ? $"george-{parent.BundleProductId.Value}" : null,
                ["wooProductId"] = bundleWooId,
                ["pricingMode"] = cfg?.PricingMode ?? BundlePricingEngine.PricingModeFixed,
                ["reweighPrice"] = cfg?.ReweighPrice ?? false,
                ["invoiceDisplay"] = cfg?.InvoiceDisplay ?? "bundle",
                // Per bundle, after the bundle discount, before surcharges - as the order line was priced.
                ["basePrice"] = BundleOrderLineBuilder.BaseFromParent(parent, children),
                ["components"] = components,
            };
            // Existing WC order item to update in place; omitted = create (dictionary nulls are serialized, so drop the key).
            if (parent.WooLineItemId is > 0)
                bundle["itemId"] = parent.WooLineItemId.Value;
            return bundle;
        }

        /// <summary>True for the plugin's grams unit spellings (<c>grams</c> / <c>gram</c> / <c>g</c>).</summary>
        private static bool IsGramsUnit(string? unit)
        {
            var u = (unit ?? "").Trim().ToLowerInvariant();
            return u == "grams" || u == "gram" || u == "g";
        }

        /// <summary>
        /// <c>bundle</c> objects for every bundle parent of an order, keyed by the parent line id. Resolves the
        /// definitions and the per-site Woo ids of components / swaps. Never throws (an empty map on failure).
        /// </summary>
        private async Task<Dictionary<int, Dictionary<string, object?>>> BuildOcStoreosBundleObjectsAsync(
            int siteId,
            IReadOnlyList<OrderItem> activeLines,
            CancellationToken cancelToken)
        {
            var result = new Dictionary<int, Dictionary<string, object?>>();
            var parents = activeLines.Where(BundleOrderLines.IsBundleParent).ToList();
            if (parents.Count == 0) return result;
            try
            {
                var childrenByParent = parents.ToDictionary(p => p.Id, p => BundleOrderLines.ChildrenOf(activeLines, p));
                var allChildren = childrenByParent.Values.SelectMany(c => c).ToList();

                var defs = await _bundleStorage
                    .GetDefinitionsAsync(parents.Select(p => p.BundleProductId!.Value).Distinct().ToList(), cancelToken)
                    .ConfigureAwait(false);

                var productIds = parents.Select(p => p.BundleProductId!.Value)
                    .Concat(allChildren.Where(c => c.ProductId is > 0).Select(c => c.ProductId!.Value))
                    .Concat(allChildren.Where(c => c.SwappedFromProductId is > 0).Select(c => c.SwappedFromProductId!.Value))
                    .Distinct()
                    .ToList();
                // Children's variants plus the configured slot variants (swappedFromVariationId of a swapped slot).
                var variantIds = allChildren.Where(c => c.ProductVariantId is > 0).Select(c => c.ProductVariantId!.Value)
                    .Concat(defs.Values.SelectMany(d => d.Components).Where(c => c.ComponentVariantId is > 0).Select(c => c.ComponentVariantId!.Value))
                    .Distinct()
                    .ToList();
                var wooProductIds = await _bundleStorage.GetSiteWooProductIdMapAsync(productIds, siteId, cancelToken).ConfigureAwait(false);
                var wooVariationIds = await _bundleStorage.GetSiteVariantWooIdMapAsync(variantIds, siteId, cancelToken).ConfigureAwait(false);

                // A swapped-in product with no Woo id on this site cannot be expressed to the store: the payload
                // falls back to the line's WooCommerceProductId (null for a George-side swap) and the store keeps
                // the original product in the slot.
                foreach (var child in allChildren)
                {
                    if (child.SwappedFromProductId is > 0 && child.ProductId is > 0 && !wooProductIds.ContainsKey(child.ProductId.Value))
                        _logger.LogWarning(
                            "oc-storeos sync: order {OrderId} bundle child line {LineId} was swapped to product {ProductId}, which has no Woo id on site {SiteId}; the store will keep the original component (product {SwappedFromProductId})",
                            child.OrderId, child.Id, child.ProductId.Value, siteId, child.SwappedFromProductId.Value);
                }

                foreach (var parent in parents)
                {
                    defs.TryGetValue(parent.BundleProductId!.Value, out var def);
                    result[parent.Id] = BuildOcStoreosBundleObject(parent, childrenByParent[parent.Id], def, wooProductIds, wooVariationIds);
                }
            }
            catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "oc-storeos sync: failed to build bundle objects for site {SiteId}; bundle lines go out without the bundle block", siteId);
            }
            return result;
        }
    }
}
