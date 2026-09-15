using George.Common;
using George.Data;
using George.DB;
using George.Services.Bundles;
using George.Services.Request;
using George.Services.Response;
using Microsoft.Extensions.Logging;

namespace George.Services
{
    /// <summary>
    /// Bundles (מארזים) half of the order flows - BUNDLES_SYNC_SPEC.md §2 (order-line semantics), §3.3 (manual
    /// orders, picking, swap, delete), §6 (Woo intake) and §8 (order screens). A bundle order line is a PARENT
    /// (the bundle product, the money) followed by CHILD lines (its components, the quantities); see
    /// <see cref="BundleOrderLines"/> and <see cref="BundleOrderLineBuilder"/>.
    /// </summary>
    public partial class OrderService
    {
        private readonly BundleStorage _bundleStorage;
        private readonly BundleService _bundleService;

        // ───────────────────────────── manual orders (§3.3) ─────────────────────────────

        /// <summary>
        /// Builds the order lines of a manual request (phone / kiosk / API): plain lines exactly as before, and a
        /// line whose product is a bundle expanded into parent + children (client prices ignored, pricing engine
        /// with the site's effective prices). Returns a Hebrew error for an invalid bundle selection.
        /// </summary>
        private async Task<(List<OrderItem> Lines, string? Error)> BuildManualOrderLinesAsync(
            int siteId,
            IReadOnlyList<CreateOrderItemReq> reqs,
            Dictionary<int, Product?> productCache,
            int firstSortOrder,
            CancellationToken cancelToken)
        {
            var lines = new List<OrderItem>();
            bool? allowFreeSwap = null;
            var sort = firstSortOrder;
            foreach (var lineReq in reqs)
            {
                Product? product = null;
                if (lineReq.ProductId is > 0)
                    product = await GetCachedProductAsync(productCache, lineReq.ProductId.Value, cancelToken).ConfigureAwait(false);

                if (product != null && BundleProducts.IsBundle(product))
                {
                    if (!allowFreeSwap.HasValue)
                    {
                        var site = await _siteStorage.GetSiteAsync(siteId, cancelToken).ConfigureAwait(false);
                        allowFreeSwap = site?.BundleAllowFreeSwap == true;
                    }
                    var (expansion, error) = await ExpandManualBundleLineAsync(
                        siteId, lineReq, product, allowFreeSwap.Value, productCache, sort, cancelToken).ConfigureAwait(false);
                    if (error != null || expansion == null)
                        return (lines, error ?? BundleOrderLineBuilder.ErrNoDefinition);
                    lines.AddRange(expansion.All);
                    sort += 1 + expansion.Children.Count;
                    continue;
                }

                var oi = _mapper.Map<OrderItem>(lineReq);
                oi.SortOrder = sort++;
                if (lineReq.ProductId is > 0)
                    OrderLineDisplayFieldsBuilder.MergeComputedDisplayFields(oi, lineReq, product);
                lines.Add(oi);
            }
            return (lines, null);
        }

        private async Task<(BundleLineExpansion? Expansion, string? Error)> ExpandManualBundleLineAsync(
            int siteId,
            CreateOrderItemReq lineReq,
            Product bundleProduct,
            bool allowFreeSwap,
            Dictionary<int, Product?> productCache,
            int firstSortOrder,
            CancellationToken cancelToken)
        {
            var def = await _bundleStorage.GetDefinitionAsync(bundleProduct.Id, cancelToken).ConfigureAwait(false);
            if (def == null || def.Components.Count == 0)
                return (null, $"{BundleOrderLineBuilder.ErrNoDefinition} ({bundleProduct.Name})");
            if (lineReq.Quantity <= 0m)
                return (null, $"כמות המארז '{bundleProduct.Name}' חייבת להיות גדולה מ-0");
            // Bundles are sold in whole units (the store's line quantity counts bundles); 1.5 bundles would
            // silently scale every component (0.75 kg, 3 units) and can never be mirrored back to Woo.
            if (lineReq.Quantity != decimal.Truncate(lineReq.Quantity))
                return (null, $"כמות המארז '{bundleProduct.Name}' חייבת להיות מספר שלם");

            var bundleQty = lineReq.Quantity;
            var slots = BundleOrderLineBuilder.OrderedSlots(def);
            var selections = lineReq.BundleComponents ?? new List<CreateOrderBundleComponentReq>();
            foreach (var sel in selections)
            {
                if (slots.All(s => s.Id != sel.ComponentId))
                    return (null, $"{BundleOrderLineBuilder.ErrSlotUnknown} (componentId {sel.ComponentId}, {bundleProduct.Name})");
            }

            var swaps = new Dictionary<int, BundleService.BundleSlotSwap>();
            var specs = new List<(ProductBundleComponent Slot, int Index, int ProductId, int? VariantId, decimal Surcharge, bool IsSwap, CreateOrderBundleComponentReq? Sel)>();
            for (var i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                var sel = selections.FirstOrDefault(s => s.ComponentId == slot.Id);
                var productId = sel is { ProductId: > 0 } ? sel.ProductId : slot.ComponentProductId;
                var variantId = sel is { ProductId: > 0 }
                    ? (sel.ProductVariantId is > 0 ? sel.ProductVariantId : null)
                    : slot.ComponentVariantId;
                var err = BundleOrderLineBuilder.ResolveSlotSelection(
                    slot, productId, variantId, allowFreeSwap, sel?.SwapSurcharge,
                    out var surcharge, out var isSwap, out _, out var resolvedVariantId);
                if (err != null)
                    return (null, $"{err} (רכיב: {slot.ComponentProduct?.Name ?? "#" + slot.ComponentProductId})");
                // The configured product sent without a variant keeps the slot's configured variant.
                variantId = resolvedVariantId;
                if (isSwap)
                    swaps[slot.Id] = new BundleService.BundleSlotSwap { ProductId = productId, VariantId = variantId, Surcharge = surcharge };
                specs.Add((slot, i, productId, variantId, surcharge, isSwap, sel));
            }

            // Site-effective prices of the configured components + swaps, plus any free-swapped product.
            var prices = await _bundleService.ResolveComponentPricesAsync(def, siteId, cancelToken).ConfigureAwait(false);
            var extraKeys = specs
                .Select(s => (ProductId: s.ProductId, VariantId: s.VariantId))
                .Where(k => !prices.ContainsKey(k))
                .Distinct()
                .ToList();
            if (extraKeys.Count > 0)
            {
                var extra = await _bundleService.ResolvePricesAsync(extraKeys, siteId, cancelToken).ConfigureAwait(false);
                foreach (var kv in extra) prices[kv.Key] = kv.Value;
            }
            var pricing = BundlePricingEngine.Price(BundleService.BuildPricingInput(def, prices, bundleQty, swaps));

            // Parent line: the bundle product carries the money; client prices / labels are ignored.
            var parent = _mapper.Map<OrderItem>(lineReq);
            parent.ProductId = bundleProduct.Id;
            parent.ProductVariantId = null;
            parent.Title = string.IsNullOrWhiteSpace(lineReq.Title) ? bundleProduct.Name : lineReq.Title.Trim();
            parent.VariantTitle = null;
            parent.PricePerUnit = pricing.UnitPrice;
            parent.TotalPrice = pricing.LineTotal;
            parent.SaleUnits = null;
            parent.SaleTotalWeight = null;
            parent.OrderLinePerUnitWeightLabel = null;
            parent.OrderLineSizeLabel = null;
            parent.OrderLineCuttingLabel = null;
            parent.LineDisplayJson = null;
            parent.PickedQuantity = null;
            parent.PickingUserConfirmed = false;

            var lineSlots = new List<BundleLineSlot>(specs.Count);
            foreach (var (slot, index, productId, variantId, surcharge, isSwap, sel) in specs)
            {
                var slotProduct = await GetCachedProductAsync(productCache, productId, cancelToken).ConfigureAwait(false);
                // The product in the slot must exist and belong to the order's account (a free swap may name any
                // product id, and a configured component may have been deleted after the bundle was defined).
                if (slotProduct == null || slotProduct.IsDeleted)
                    return (null, BundleService.DeletedComponentError(slot.ComponentProduct?.Name ?? $"#{productId}"));
                if (slotProduct.AccountId.HasValue && slotProduct.AccountId != bundleProduct.AccountId)
                    return (null, $"{BundleOrderLineBuilder.ErrProductOtherAccount} ({slotProduct.Name})");
                var variant = variantId is > 0
                    ? slotProduct?.ProductVariant?.FirstOrDefault(v => v.Id == variantId.Value && !v.IsDeleted)
                    : null;
                prices.TryGetValue((productId, variantId), out var unitPrice);
                lineSlots.Add(new BundleLineSlot
                {
                    ComponentId = slot.Id,
                    SlotIndex = index,
                    ProductId = productId,
                    ProductVariantId = variant?.Id,
                    Product = slotProduct,
                    Title = !string.IsNullOrWhiteSpace(sel?.Title) ? sel!.Title!.Trim() : (slotProduct?.Name ?? slot.ComponentProduct?.Name),
                    VariantTitle = VariantDisplayTitle(variant),
                    QtyPerBundle = slot.Qty,
                    IsWeight = BundleOrderLineBuilder.IsWeightSlot(slot, slotProduct),
                    LineQuantityOverride = sel?.Quantity is > 0m ? sel.Quantity : null,
                    UnitPrice = unitPrice,
                    SwappedFromProductId = isSwap ? slot.ComponentProductId : null,
                    Surcharge = surcharge,
                    Sku = variant?.Sku ?? slotProduct?.Sku,
                });
            }

            var expansion = BundleOrderLineBuilder.Expand(
                parent, bundleQty, lineSlots,
                def.Config.PricingMode, def.Config.DiscountType, def.Config.DiscountValue,
                firstSortOrder, basePrice: pricing.BasePrice);
            return (expansion, null);
        }

        private async Task<Product?> GetCachedProductAsync(Dictionary<int, Product?> cache, int productId, CancellationToken cancelToken)
        {
            if (cache.TryGetValue(productId, out var cached)) return cached;
            var product = await _productStorage.GetProductAsync(productId, cancelToken).ConfigureAwait(false);
            cache[productId] = product;
            return product;
        }

        /// <summary>Variant option values joined with " | " (same form as the Woo variant title), decoded.</summary>
        private static string? VariantDisplayTitle(ProductVariant? variant)
        {
            if (variant?.ProductVariantOptionValue == null) return null;
            var values = variant.ProductVariantOptionValue
                .OrderBy(ov => ov.OptionName)
                .Select(ov => WooPercentEncodedText.Decode(ov.OptionValue?.Trim()))
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            return values.Count > 0 ? string.Join(" | ", values) : null;
        }

        // ───────────────────────────── Woo intake (§6) ─────────────────────────────

        /// <summary>
        /// Builds the order lines of a WooCommerce payload (create and update paths share it). Plain lines are built
        /// exactly as before; a line carrying <c>bundle</c> - or whose product resolves to a George bundle - becomes a
        /// parent followed by one child per component (ids mapped per site). A bundle George does not know stays ONE
        /// unlinked line. Every line stores the WC item id (<c>itemId</c>).
        /// </summary>
        private async Task<List<OrderItem>> BuildWooCommerceOrderLinesAsync(
            int siteId,
            int accountId,
            int orderId,
            WooCommerceOrderPayload payload,
            Dictionary<int, Product?> productCache,
            CancellationToken cancelToken)
        {
            var lines = new List<OrderItem>();
            if (payload.Items == null) return lines;
            var sort = 0;
            foreach (var it in payload.Items)
            {
                var ourProductId = await ResolveWooCommerceItemProductIdAsync(
                    siteId, accountId, it.ProductId, it.Sku, GetEffectiveVariationId(it), cancelToken).ConfigureAwait(false);
                if (it.Bundle != null)
                {
                    // The external id George stamped on the Woo bundle is authoritative when it names a George bundle.
                    var georgeId = it.Bundle.TryGetGeorgeProductId();
                    if (georgeId.HasValue)
                    {
                        var byExternal = await GetCachedProductAsync(productCache, georgeId.Value, cancelToken).ConfigureAwait(false);
                        if (byExternal != null && BundleProducts.IsBundle(byExternal))
                            ourProductId = georgeId;
                    }
                    if (!ourProductId.HasValue && it.Bundle.WooProductId is > 0 && it.Bundle.WooProductId != it.ProductId)
                        ourProductId = await ResolveWooCommerceItemProductIdAsync(
                            siteId, accountId, it.Bundle.WooProductId, null, null, cancelToken).ConfigureAwait(false);
                }
                var product = ourProductId.HasValue
                    ? await GetCachedProductAsync(productCache, ourProductId.Value, cancelToken).ConfigureAwait(false)
                    : null;

                var oi = await BuildWooCommerceLineAsync(siteId, orderId, it, ourProductId, product, cancelToken).ConfigureAwait(false);

                if (product != null && BundleProducts.IsBundle(product))
                {
                    BundleLineExpansion? expansion = null;
                    try
                    {
                        expansion = await ExpandWooBundleLineAsync(siteId, accountId, it, oi, product, productCache, sort, cancelToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancelToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Woo intake: bundle expansion failed for product {ProductId} (site {SiteId}); keeping a single line", product.Id, siteId);
                    }
                    if (expansion != null)
                    {
                        lines.AddRange(expansion.All);
                        sort += 1 + expansion.Children.Count;
                        continue;
                    }
                }

                oi.SortOrder = sort++;
                lines.Add(oi);
            }
            return lines;
        }

        /// <summary>One Woo payload item → one order line (the pre-bundles logic, shared by create + update).</summary>
        private async Task<OrderItem> BuildWooCommerceLineAsync(
            int siteId,
            int orderId,
            WooCommerceOrderItemPayload it,
            int? ourProductId,
            Product? product,
            CancellationToken cancelToken)
        {
            var siteVariantWooIds = product != null
                ? await _productStorage.GetSiteVariantWooIdMapForProductAsync(product.Id, siteId, cancelToken).ConfigureAwait(false)
                : null;
            var matchedVariant = GetVariantFromPayloadItem(it, product, siteVariantWooIds);
            var (qty, unitWeightGrams, variantTitle) = GetWooCommerceItemQuantityAndUnitWeight(it, product);
            // Unresolved lines keep ProductId null - the raw Woo id is NOT a local Product.Id, and
            // storing it links the line to whatever product happens to own that id (wrong image/price
            // on the order screen, stock deducted from the wrong product). Raw id stays in WooCommerceProductId.
            var oi = new OrderItem
            {
                OrderId = orderId,
                ProductId = ourProductId,
                ProductVariantId = matchedVariant?.Id,
                Title = it.Name,
                VariantTitle = GetVariantTitleFromPayload(it) ?? variantTitle,
                Quantity = qty,
                UnitWeightGrams = unitWeightGrams,
                PricePerUnit = it.UnitPrice,
                TotalPrice = it.LineTotal,
                Notes = !string.IsNullOrWhiteSpace(it.Note) ? it.Note : it.ProductNote,
                SaleUnits = it.SaleUnits,
                SaleTotalWeight = it.SaleTotalWeight,
                WooCommerceProductId = it.ProductId,
                WooCommerceVariationId = GetEffectiveVariationId(it),
                WooLineItemId = it.ItemId is > 0 ? it.ItemId : (it.Bundle?.ItemId is > 0 ? it.Bundle.ItemId : null),
            };
            PopulateWooCommerceOrderItemPayloadColumns(oi, it);
            var mergeReq = new CreateOrderItemReq
            {
                ProductId = ourProductId,
                ProductVariantId = matchedVariant?.Id,
                Quantity = qty,
                UnitWeightGrams = unitWeightGrams,
                SaleUnits = it.SaleUnits,
                SaleTotalWeight = it.SaleTotalWeight,
            };
            OrderLineDisplayFieldsBuilder.MergeComputedDisplayFields(oi, mergeReq, product);
            ApplyWooCommerceQuantityTypeToLineDisplay(oi, it);
            return oi;
        }

        /// <summary>
        /// Parent + children for a Woo bundle line whose product is a George bundle. Components come from the payload
        /// (ids mapped per site, slots matched by key / index against the definition); when the payload carries none,
        /// the definition's configured components are used. Returns null when nothing can be expanded.
        /// </summary>
        private async Task<BundleLineExpansion?> ExpandWooBundleLineAsync(
            int siteId,
            int accountId,
            WooCommerceOrderItemPayload it,
            OrderItem parent,
            Product bundleProduct,
            Dictionary<int, Product?> productCache,
            int firstSortOrder,
            CancellationToken cancelToken)
        {
            var def = await _bundleStorage.GetDefinitionAsync(bundleProduct.Id, cancelToken).ConfigureAwait(false);
            var orderedSlots = def != null ? BundleOrderLineBuilder.OrderedSlots(def) : new List<ProductBundleComponent>();
            var comps = it.Bundle?.Components ?? new List<WooCommerceOrderItemBundleComponentPayload>();
            if (comps.Count == 0 && orderedSlots.Count == 0)
                return null;

            var bundleQty = parent.Quantity > 0m ? parent.Quantity : 1m;
            var lineSlots = new List<BundleLineSlot>();
            if (comps.Count > 0)
            {
                for (var idx = 0; idx < comps.Count; idx++)
                {
                    var comp = comps[idx];
                    var slotIndex = comp.Index is >= 0 ? comp.Index.Value : idx;
                    var slot = def != null ? BundleOrderLineBuilder.MatchSlot(orderedSlots, comp.Key, slotIndex) : null;

                    var compProductId = await ResolveWooCommerceItemProductIdAsync(
                        siteId, accountId, comp.ProductId, comp.Sku, comp.VariationId, cancelToken).ConfigureAwait(false);
                    // Unmapped Woo product in a slot that was not swapped: the configured component is what is in it.
                    if (!compProductId.HasValue && slot != null && comp.SwappedFromProductId is not > 0)
                        compProductId = slot.ComponentProductId;
                    var compProduct = compProductId.HasValue
                        ? await GetCachedProductAsync(productCache, compProductId.Value, cancelToken).ConfigureAwait(false)
                        : null;

                    ProductVariant? variant = null;
                    if (compProduct != null && comp.VariationId is > 0)
                    {
                        var vmap = await _productStorage.GetSiteVariantWooIdMapForProductAsync(compProduct.Id, siteId, cancelToken).ConfigureAwait(false);
                        variant = GetVariantFromPayloadItem(new WooCommerceOrderItemPayload { VariationId = comp.VariationId }, compProduct, vmap);
                    }
                    if (variant == null && compProduct != null && slot != null
                        && compProduct.Id == slot.ComponentProductId && slot.ComponentVariantId.HasValue)
                    {
                        variant = compProduct.ProductVariant?.FirstOrDefault(v => v.Id == slot.ComponentVariantId.Value && !v.IsDeleted);
                    }

                    var hasUnit = !string.IsNullOrWhiteSpace(comp.Unit) || !string.IsNullOrWhiteSpace(comp.Mode);
                    var (qtyPer, isWeight) = BundleOrderLineBuilder.NormalizeWooComponentQty(
                        comp.Qty ?? slot?.Qty, comp.Unit ?? slot?.Unit, comp.Mode ?? slot?.Mode);
                    if (!hasUnit && slot != null)
                        isWeight = BundleOrderLineBuilder.IsWeightSlot(slot, compProduct);
                    else if (!hasUnit && slot == null)
                        isWeight = string.Equals(compProduct?.SetupType?.Name, "by_weight", StringComparison.OrdinalIgnoreCase);

                    int? swappedFrom = null;
                    if (comp.SwappedFromProductId is > 0)
                    {
                        swappedFrom = await ResolveWooCommerceItemProductIdAsync(
                            siteId, accountId, comp.SwappedFromProductId, null, comp.SwappedFromVariationId, cancelToken).ConfigureAwait(false)
                            ?? slot?.ComponentProductId;
                    }
                    else if (slot != null && compProductId.HasValue && compProductId.Value != slot.ComponentProductId)
                    {
                        swappedFrom = slot.ComponentProductId;
                    }

                    lineSlots.Add(new BundleLineSlot
                    {
                        ComponentId = slot?.Id,
                        SlotIndex = slotIndex,
                        ProductId = compProductId,
                        ProductVariantId = variant?.Id,
                        Product = compProduct,
                        Title = !string.IsNullOrWhiteSpace(comp.Name) ? comp.Name.Trim() : (compProduct?.Name ?? slot?.ComponentProduct?.Name),
                        VariantTitle = VariantDisplayTitle(variant),
                        QtyPerBundle = qtyPer,
                        IsWeight = isWeight,
                        SwappedFromProductId = swappedFrom,
                        Surcharge = BundlePricingEngine.Round2(comp.Surcharge ?? 0m),
                        // actualQty arrives in the component's own unit (grams possible) - store kg like Quantity.
                        PickedQuantity = comp.ActualQty is > 0m
                            ? BundleOrderLineBuilder.NormalizeWooComponentQty(comp.ActualQty, comp.Unit ?? slot?.Unit, comp.Mode ?? slot?.Mode).Qty
                            : null,
                        WooProductId = comp.ProductId,
                        WooVariationId = comp.VariationId is > 0 ? comp.VariationId : null,
                        Sku = !string.IsNullOrWhiteSpace(comp.Sku) ? comp.Sku : (variant?.Sku ?? compProduct?.Sku),
                    });
                }
            }
            else
            {
                for (var i = 0; i < orderedSlots.Count; i++)
                {
                    var slot = orderedSlots[i];
                    var compProduct = await GetCachedProductAsync(productCache, slot.ComponentProductId, cancelToken).ConfigureAwait(false);
                    var variant = slot.ComponentVariantId.HasValue
                        ? compProduct?.ProductVariant?.FirstOrDefault(v => v.Id == slot.ComponentVariantId.Value && !v.IsDeleted)
                        : null;
                    lineSlots.Add(new BundleLineSlot
                    {
                        ComponentId = slot.Id,
                        SlotIndex = i,
                        ProductId = slot.ComponentProductId,
                        ProductVariantId = variant?.Id,
                        Product = compProduct,
                        Title = compProduct?.Name ?? slot.ComponentProduct?.Name,
                        VariantTitle = VariantDisplayTitle(variant),
                        QtyPerBundle = slot.Qty,
                        IsWeight = BundleOrderLineBuilder.IsWeightSlot(slot, compProduct),
                        Sku = variant?.Sku ?? compProduct?.Sku,
                    });
                }
            }

            // Informational catalog (site-effective) price per child - never summed into order totals.
            try
            {
                var keys = lineSlots
                    .Where(s => s.ProductId is > 0)
                    .Select(s => (ProductId: s.ProductId!.Value, VariantId: s.ProductVariantId))
                    .Distinct()
                    .ToList();
                if (keys.Count > 0)
                {
                    var prices = await _bundleService.ResolvePricesAsync(keys, siteId, cancelToken).ConfigureAwait(false);
                    foreach (var s in lineSlots)
                    {
                        if (s.ProductId is > 0 && prices.TryGetValue((s.ProductId.Value, s.ProductVariantId), out var p))
                            s.UnitPrice = p;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Woo intake: failed to resolve component prices for bundle {ProductId} (site {SiteId})", bundleProduct.Id, siteId);
            }

            var pricingMode = !string.IsNullOrWhiteSpace(it.Bundle?.PricingMode) ? it.Bundle!.PricingMode : def?.Config.PricingMode;
            return BundleOrderLineBuilder.Expand(
                parent, bundleQty, lineSlots,
                pricingMode, def?.Config.DiscountType, def?.Config.DiscountValue ?? 0m,
                firstSortOrder, basePrice: it.Bundle?.BasePrice is > 0m ? it.Bundle.BasePrice : null);
        }

        /// <summary>
        /// Picking-state merge for a Woo webhook rebuild with bundles: top-level lines (plain + parents) are matched
        /// positionally by Woo identity as before; a child is matched inside its parent's children by slot index and
        /// product, so George's weighing survives the store's re-send.
        /// </summary>
        private static void MergePickingStateIntoBundleChildren(List<OrderItem> newItems, List<OrderItem> previousItemsOrdered)
        {
            var newParents = newItems.Where(BundleOrderLines.IsBundleParent).ToList();
            if (newParents.Count == 0) return;
            var oldTop = BundleOrderLines.WithoutChildren(previousItemsOrdered).OrderBy(i => i.SortOrder).ToList();
            var newTop = BundleOrderLines.WithoutChildren(newItems).ToList();
            var n = Math.Min(newTop.Count, oldTop.Count);
            for (var i = 0; i < n; i++)
            {
                var parent = newTop[i];
                var prevParent = oldTop[i];
                if (!BundleOrderLines.IsBundleParent(parent) || !BundleOrderLines.IsBundleParent(prevParent)) continue;
                if (!SameWooCommerceLineIdentityForPickingMerge(parent, prevParent)) continue;
                // The parent's picked state is derived from its children in George (spec §3.3) - carry it over,
                // including a re-weighed total, so a picked bundle stays billable after the store's re-send.
                parent.PickedQuantity = prevParent.PickedQuantity is > 0m ? prevParent.PickedQuantity : parent.Quantity;
                parent.PickingUserConfirmed = prevParent.PickingUserConfirmed;
                if (prevParent.PickingUserConfirmed && prevParent.TotalPrice.HasValue)
                    parent.TotalPrice = prevParent.TotalPrice;

                var newChildren = newItems.Where(c => BundleOrderLines.IsChildOf(c, parent)).ToList();
                var oldChildren = BundleOrderLines.ChildrenOf(previousItemsOrdered, prevParent);
                foreach (var child in newChildren)
                {
                    var prev = oldChildren.FirstOrDefault(o =>
                            (o.BundleComponentIndex ?? -1) == (child.BundleComponentIndex ?? -2)
                            && (o.ProductId ?? 0) == (child.ProductId ?? 0)
                            && (o.WooCommerceProductId ?? 0) == (child.WooCommerceProductId ?? 0))
                        ?? oldChildren.FirstOrDefault(o =>
                            (o.BundleComponentIndex ?? -1) == (child.BundleComponentIndex ?? -2)
                            && (o.ProductId ?? 0) == (child.ProductId ?? 0));
                    if (prev == null) continue;
                    // George's own picking wins only once a picker confirmed the line. Before that the previous
                    // row holds just the ingest baseline (= ordered qty), so a weight the store sent now
                    // (`actualQty` → PickedQuantity on the new child) must not be overwritten by it.
                    if (!prev.PickingUserConfirmed) continue;
                    if (!prev.PickedQuantity.HasValue || prev.PickedQuantity.Value <= 0m) continue;
                    child.PickedQuantity = prev.PickedQuantity;
                    child.PickingUserConfirmed = true;
                    child.TotalPrice = prev.TotalPrice;
                }
            }
        }

        // ───────────────────────────── picking (§3.3 / §4) ─────────────────────────────

        /// <summary>
        /// After a picking save: every bundle parent is confirmed when all its children are, its picked quantity
        /// mirrors the ordered bundles, פחת never lands on bundle lines, and in <c>sum</c> + <c>ReweighPrice</c> the
        /// parent total follows the weighed components. Recalculates the header totals. No-op without bundles.
        /// </summary>
        private async Task ApplyBundlePickingRulesAsync(int orderId, CancellationToken cancelToken)
        {
            var order = await _orderStorage.GetOrderByIdTrackedAsync(orderId, cancelToken).ConfigureAwait(false);
            if (order == null) return;
            var active = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
            var parents = active.Where(BundleOrderLines.IsBundleParent).ToList();
            if (parents.Count == 0) return;

            var defs = await _bundleStorage
                .GetDefinitionsAsync(parents.Select(p => p.BundleProductId!.Value).Distinct().ToList(), cancelToken)
                .ConfigureAwait(false);
            foreach (var parent in parents)
            {
                var children = BundleOrderLines.ChildrenOf(active, parent);
                if (children.Count == 0) continue;
                defs.TryGetValue(parent.BundleProductId!.Value, out var def);
                var cfg = def?.Config;
                BundleOrderLineBuilder.ApplyPickingRules(
                    parent, children,
                    cfg?.PricingMode ?? BundlePricingEngine.PricingModeFixed,
                    cfg?.ReweighPrice ?? false,
                    cfg?.DiscountType,
                    cfg?.DiscountValue ?? 0m);
            }
            await _orderStorage.PersistTrackedOrderTotalsAsync(order, cancelToken).ConfigureAwait(false);
        }

        /// <summary>
        /// POST /Order/{orderId}/Items/{orderItemId}/swap - replaces the product of a bundle child line during picking.
        /// A product outside the slot's configured swaps requires <c>Site.BundleAllowFreeSwap</c>. Recomputes the parent
        /// price, re-baselines the child's picking, adjusts catalog stock and mirrors the order to the store.
        /// </summary>
        public async Task<IApiResponse<OrderRes>> SwapBundleComponentAsync(int orderId, int orderItemId, SwapOrderItemReq? req, CancellationToken cancelToken = default)
        {
            var response = new ApiResponse<OrderRes>();
            if (req == null || req.ProductId <= 0)
                return CreateResponse(response, StatusCode.InvalidRequest, "productId is required.");

            var order = await _orderStorage.GetOrderByIdTrackedAsync(orderId, cancelToken).ConfigureAwait(false);
            if (order == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Order not found.");
            if (string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase)
                || string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                return CreateResponse(response, StatusCode.InvalidRequest, "לא ניתן להחליף רכיבים בהזמנה שהסתיימה או בוטלה.");

            var active = order.OrderItem?.Where(i => !i.IsDeleted).ToList() ?? new List<OrderItem>();
            var line = active.FirstOrDefault(i => i.Id == orderItemId);
            if (line == null)
                return CreateResponse(response, StatusCode.ItemNotFound, "Order item not found.");
            if (!BundleOrderLines.IsBundleChild(line))
                return CreateResponse(response, StatusCode.InvalidRequest, BundleOrderLineBuilder.ErrChildNotSwappable);
            var parent = active.FirstOrDefault(i => i.Id == line.ParentOrderItemId);
            if (parent == null)
                return CreateResponse(response, StatusCode.InvalidRequest, "שורת המארז של הרכיב לא נמצאה.");

            var site = await _siteStorage.GetSiteAsync(order.SiteId, cancelToken).ConfigureAwait(false);
            var allowFreeSwap = site?.BundleAllowFreeSwap == true;
            var def = parent.BundleProductId.HasValue
                ? await _bundleStorage.GetDefinitionAsync(parent.BundleProductId.Value, cancelToken).ConfigureAwait(false)
                : null;
            var slot = def?.Components.FirstOrDefault(c => c.Id == line.BundleComponentId);

            var product = await _productStorage.GetProductAsync(req.ProductId, cancelToken).ConfigureAwait(false);
            if (product == null || product.IsDeleted)
                return CreateResponse(response, StatusCode.ItemNotFound, "המוצר החלופי לא נמצא.");
            if (BundleProducts.IsBundle(product))
                return CreateResponse(response, StatusCode.InvalidRequest, "לא ניתן להכניס מארז לתוך מארז.");
            // Never let a swap pull in another account's catalog (the product id is client-supplied).
            if (product.AccountId.HasValue && product.AccountId.Value != order.AccountId)
                return CreateResponse(response, StatusCode.InvalidRequest, BundleOrderLineBuilder.ErrProductOtherAccount);
            if (product.Site != null && product.Site.Count > 0 && product.Site.All(s => s.Id != order.SiteId))
                return CreateResponse(response, StatusCode.InvalidRequest, BundleOrderLineBuilder.ErrProductNotOnSite);
            ProductVariant? variant = null;
            if (req.ProductVariantId is > 0)
            {
                variant = product.ProductVariant?.FirstOrDefault(v => v.Id == req.ProductVariantId.Value && !v.IsDeleted);
                if (variant == null)
                    return CreateResponse(response, StatusCode.InvalidRequest, "הווריאציה שנבחרה אינה שייכת למוצר החלופי.");
            }

            decimal surcharge;
            int? swappedFrom;
            if (slot != null)
            {
                var err = BundleOrderLineBuilder.ResolveSlotSelection(
                    slot, product.Id, variant?.Id, allowFreeSwap, req.Surcharge,
                    out surcharge, out var isSwap, out _, out var resolvedVariantId);
                if (err != null)
                    return CreateResponse(response, err == BundleOrderLineBuilder.ErrFreeSwapNotAllowed ? StatusCode.UnauthorizedData : StatusCode.InvalidRequest, err);
                swappedFrom = isSwap ? slot.ComponentProductId : null;
                // Back to the configured product without a variant = the configured variant.
                if (variant == null && resolvedVariantId is > 0)
                    variant = product.ProductVariant?.FirstOrDefault(v => v.Id == resolvedVariantId.Value && !v.IsDeleted);
            }
            else
            {
                // A bundle George has no definition for: only a free swap can change it.
                if (!allowFreeSwap)
                    return CreateResponse(response, StatusCode.UnauthorizedData, BundleOrderLineBuilder.ErrFreeSwapNotAllowed);
                var original = line.SwappedFromProductId ?? line.ProductId;
                surcharge = BundlePricingEngine.Round2(Math.Max(0m, req.Surcharge ?? 0m));
                swappedFrom = original.HasValue && original.Value != product.Id ? original : null;
            }

            // Stock: give back what was consumed for the previous product, consume the baseline of the new one.
            var oldProductId = line.ProductId;
            var oldVariantId = line.ProductVariantId;
            var oldPicked = line.PickedQuantity ?? 0m;
            var oldSurcharge = line.SwapSurcharge ?? 0m;

            var isWeight = string.Equals(line.OrderLineQuantityMode, "weight", StringComparison.OrdinalIgnoreCase);
            line.ProductId = product.Id;
            line.ProductVariantId = variant?.Id;
            line.Title = !string.IsNullOrWhiteSpace(req.Title) ? req.Title.Trim() : product.Name;
            line.VariantTitle = VariantDisplayTitle(variant);
            line.SwappedFromProductId = swappedFrom;
            line.SwapSurcharge = swappedFrom.HasValue || surcharge > 0m ? surcharge : null;
            line.OrderLinePerUnitWeightLabel = null;
            line.OrderLineSizeLabel = null;
            line.OrderLineCuttingLabel = null;
            line.LineDisplayJson = null;
            line.SaleUnits = null;
            line.SaleTotalWeight = null;
            line.UnitWeightGrams = isWeight ? 1000m : null;
            OrderLineDisplayFieldsBuilder.MergeComputedDisplayFields(line, new CreateOrderItemReq
            {
                ProductId = product.Id,
                ProductVariantId = variant?.Id,
                Quantity = line.Quantity,
                UnitWeightGrams = isWeight ? 1000m : null,
            }, product);
            line.OrderLineQuantityMode = isWeight ? "weight" : "units";
            if (isWeight) line.UnitWeightGrams = 1000m;
            line.LineSku = variant?.Sku ?? product.Sku;
            line.DepreciationPercent = null;
            line.TotalPrice = null;
            line.PickedQuantity = OrderItemStockConsumption.ResolveOrderedCatalogConsumption(line);
            line.PickingUserConfirmed = false;
            line.UpdatedDate = DateTime.UtcNow;

            try
            {
                var priceMap = await _bundleService.ResolvePricesAsync(
                    new[] { (ProductId: product.Id, VariantId: variant?.Id) }, order.SiteId, cancelToken).ConfigureAwait(false);
                priceMap.TryGetValue((product.Id, variant?.Id), out var unitPrice);
                line.PricePerUnit = unitPrice;
                var wooMap = await _bundleStorage.GetSiteWooProductIdMapAsync(new[] { product.Id }, order.SiteId, cancelToken).ConfigureAwait(false);
                line.WooCommerceProductId = wooMap.TryGetValue(product.Id, out var wooPid) ? wooPid : null;
                line.WooCommerceVariationId = null;
                if (variant != null)
                {
                    var vmap = await _bundleStorage.GetSiteVariantWooIdMapAsync(new[] { variant.Id }, order.SiteId, cancelToken).ConfigureAwait(false);
                    line.WooCommerceVariationId = vmap.TryGetValue(variant.Id, out var wooVid) ? wooVid : null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bundle swap: failed to resolve price / Woo ids for product {ProductId} (order {OrderId})", product.Id, orderId);
            }

            // Parent: unit price from the engine (definition known), else adjust by the surcharge delta.
            var children = BundleOrderLines.ChildrenOf(active, parent);
            if (def != null)
            {
                var swaps = BundleOrderLineBuilder.SwapsFromChildren(children);
                var prices = await _bundleService.ResolveComponentPricesAsync(def, order.SiteId, cancelToken).ConfigureAwait(false);
                var extraKeys = swaps.Values
                    .Select(s => (ProductId: s.ProductId, VariantId: s.VariantId))
                    .Where(k => !prices.ContainsKey(k))
                    .Distinct()
                    .ToList();
                if (extraKeys.Count > 0)
                {
                    var extra = await _bundleService.ResolvePricesAsync(extraKeys, order.SiteId, cancelToken).ConfigureAwait(false);
                    foreach (var kv in extra) prices[kv.Key] = kv.Value;
                }
                var pricing = BundlePricingEngine.Price(BundleService.BuildPricingInput(def, prices, parent.Quantity, swaps));
                parent.PricePerUnit = pricing.UnitPrice;
                parent.TotalPrice = pricing.LineTotal;
                BundleOrderLineBuilder.ApplyPickingRules(
                    parent, children, def.Config.PricingMode, def.Config.ReweighPrice, def.Config.DiscountType, def.Config.DiscountValue);
            }
            else
            {
                var unit = Math.Max(0m, (parent.PricePerUnit ?? 0m) - oldSurcharge + surcharge);
                parent.PricePerUnit = BundlePricingEngine.Round2(unit);
                parent.TotalPrice = BundlePricingEngine.Round2(unit * parent.Quantity);
                BundleOrderLineBuilder.ApplyPickingRules(parent, children, BundlePricingEngine.PricingModeFixed, false, null, 0m);
            }
            parent.UpdatedDate = DateTime.UtcNow;
            await _orderStorage.PersistTrackedOrderTotalsAsync(order, cancelToken).ConfigureAwait(false);

            var stockPushIds = new List<int>();
            if (oldProductId is > 0 && oldPicked > 0m)
            {
                await _productStorage.ApplyPickingConsumptionDeltaAsync(oldProductId.Value, oldVariantId, -oldPicked, cancelToken).ConfigureAwait(false);
                stockPushIds.Add(oldProductId.Value);
            }
            if (line.PickedQuantity is > 0m)
            {
                await _productStorage.ApplyPickingConsumptionDeltaAsync(product.Id, variant?.Id, line.PickedQuantity.Value, cancelToken).ConfigureAwait(false);
                stockPushIds.Add(product.Id);
            }
            if (stockPushIds.Count > 0)
                await ScheduleWooCommerceCatalogStockPushForProductsAsync(order.SiteId, stockPushIds, "bundle component swap", cancelToken).ConfigureAwait(false);

            await TryReapplyOrderPromotionsAfterPickingAsync(orderId, cancelToken).ConfigureAwait(false);

            var loaded = await _orderStorage.GetOrderByIdAsync(orderId, cancelToken).ConfigureAwait(false);
            if (loaded == null)
                return CreateResponse(response, StatusCode.ItemNotFound);
            await ScheduleWooCommerceStoreSyncIfApplicableAsync(orderId, loaded, "bundle component swap", statusOverrideForWcRest: null, cancelToken).ConfigureAwait(false);
            response.Data = _mapper.Map<OrderRes>(loaded);
            await EnrichOrderResAsync(response.Data, loaded, cancelToken).ConfigureAwait(false);
            await LogOrderEventAsync(IntegrationLogOperation.Picking, IntegrationLogDirection.Internal, loaded.SiteId, loaded.Id, loaded.ExternalOrderId, true,
                requestJson: OrderLogSnapshot(loaded), cancelToken: cancelToken).ConfigureAwait(false);
            return response;
        }

        // ───────────────────────────── order screens (§8) ─────────────────────────────

        private static bool IsOrderPickable(Order order) =>
            !string.Equals(order.Status, "Completed", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(order.Status, "Cancelled", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Fills the bundle fields of order responses: <c>siteAllowsFreeSwap</c>, the display name of a swapped-out
        /// component, and (single-order reads of a still pickable order) each child's configured swap options.
        /// </summary>
        private async Task ApplyBundleOrderResEnrichmentAsync(
            IReadOnlyList<OrderRes> list,
            IReadOnlyList<Order> orders,
            bool includeSwapOptions,
            CancellationToken cancelToken)
        {
            if (list.Count == 0) return;
            var orderById = orders.ToDictionary(o => o.Id);
            var children = new List<(OrderRes Res, OrderItemRes Item, Order Order)>();
            foreach (var res in list)
            {
                if (!orderById.TryGetValue(res.Id, out var order)) continue;
                res.SiteAllowsFreeSwap = order.Site?.BundleAllowFreeSwap == true;
                foreach (var item in res.Items)
                {
                    if (item.IsBundleChild)
                        children.Add((res, item, order));
                }
            }
            if (children.Count == 0) return;

            try
            {
                var swappedIds = children
                    .Where(c => c.Item.SwappedFromProductId is > 0)
                    .Select(c => c.Item.SwappedFromProductId!.Value)
                    .Distinct()
                    .ToList();
                if (swappedIds.Count > 0)
                {
                    var infos = await _bundleStorage.GetCatalogProductInfosAsync(swappedIds, cancelToken).ConfigureAwait(false);
                    foreach (var c in children)
                    {
                        if (c.Item.SwappedFromProductId is > 0 && infos.TryGetValue(c.Item.SwappedFromProductId.Value, out var info))
                            c.Item.SwappedFromProductName = info.Name;
                    }
                }

                if (!includeSwapOptions) return;
                var pickable = children.Where(c => IsOrderPickable(c.Order)).ToList();
                if (pickable.Count == 0) return;

                int? ParentBundleProductId((OrderRes Res, OrderItemRes Item, Order Order) c) =>
                    c.Res.Items.FirstOrDefault(p => p.Id == c.Item.ParentOrderItemId)?.BundleProductId;

                var bundleIds = pickable.Select(ParentBundleProductId).Where(id => id is > 0).Select(id => id!.Value).Distinct().ToList();
                var defs = bundleIds.Count > 0
                    ? await _bundleStorage.GetDefinitionsAsync(bundleIds, cancelToken).ConfigureAwait(false)
                    : new Dictionary<int, BundleDefinition>();
                foreach (var c in pickable)
                {
                    var options = new List<OrderItemBundleSwapOptionRes>();
                    var bundleId = ParentBundleProductId(c);
                    if (bundleId.HasValue && defs.TryGetValue(bundleId.Value, out var def))
                    {
                        var slot = def.Components.FirstOrDefault(s => s.Id == c.Item.BundleComponentId);
                        // Configured swaps are offered only for a swappable slot.
                        if (slot is { Swappable: true })
                        {
                            options.AddRange(slot.Swaps
                                .Where(s => !s.IsDeleted)
                                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                                .Select(s => new OrderItemBundleSwapOptionRes
                                {
                                    ProductId = s.SwapProductId,
                                    ProductVariantId = s.SwapVariantId,
                                    Name = s.SwapProduct?.Name,
                                    Surcharge = s.Surcharge,
                                }));
                        }
                    }
                    c.Item.BundleSwapOptions = options;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Bundle order enrichment failed (names / swap options); response returned without them.");
            }
        }

        // ───────────────────────────── prints (§8) ─────────────────────────────

        /// <summary>
        /// Order-entry vouchers (NewImmediate / FutureImmediate / MovedToTreatment / FutureAtTime) list a bundle's
        /// components under the parent; the after-picking voucher shows the parent only.
        /// </summary>
        private static bool VoucherShowsBundleComponents(string trigger) =>
            !string.Equals(trigger, "AfterPicking", StringComparison.OrdinalIgnoreCase);

        /// <summary>Component row text for vouchers: quantity badge + name (no price). "(במקום X)" is not known here.</summary>
        private static string VoucherBundleComponentText(OrderItem child)
        {
            var qty = OrderItemLineDisplay.FormatOrderItemQuantityBadge(child);
            var name = OrderItemLineDisplay.GetOrderItemProductName(child);
            return string.IsNullOrWhiteSpace(qty) ? name : $"{qty} {name}";
        }
    }
}
