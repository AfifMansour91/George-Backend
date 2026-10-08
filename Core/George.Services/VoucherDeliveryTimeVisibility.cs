namespace George.Services
{
    /// <summary>
    /// Site.VoucherHideDeliveryTime + Site.VoucherHideDeliveryTimeScope on order printouts (voucher / A4, auto-print).
    /// Scope "all" (NULL/default) hides the time on every order; "shipping" hides it on delivery orders only, so
    /// pickup orders keep printing their time (Zano 2026-10-07: the manual-order form forces a slot on deliveries
    /// and the store read it as a commitment). Mirrors src/lib/voucherDeliveryTimeVisibility.ts in the frontend.
    /// </summary>
    public static class VoucherDeliveryTimeVisibility
    {
        public const string ScopeAll = "all";
        public const string ScopeShipping = "shipping";

        public static string NormalizeScope(string? scope) =>
            string.Equals(scope?.Trim(), ScopeShipping, StringComparison.OrdinalIgnoreCase) ? ScopeShipping : ScopeAll;

        /// <summary>Should the printed time be omitted for an order of the given delivery kind?</summary>
        public static bool HidesTime(bool? hideDeliveryTime, string? scope, bool isShipping)
        {
            if (hideDeliveryTime != true) return false;
            return NormalizeScope(scope) == ScopeAll || isShipping;
        }
    }
}
