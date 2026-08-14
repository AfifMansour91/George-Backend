namespace George.Services.Payments;

/// <summary>Verdict of comparing a Cardcom transaction inquiry against what George expects.</summary>
public enum GatewayVerifyOutcome
{
    /// <summary>Inquiry failed / no usable data — leave the order's verification state untouched.</summary>
    Inconclusive,
    /// <summary>Transaction is an authorization hold only — final charge hasn't happened yet, nothing to compare.</summary>
    HoldOnly,
    /// <summary>
    /// The order claims a captured charge, but the transaction George knows about is a hold only.
    /// This is the false-success signature (Delinka #18326): the plugin reported "charged" while the
    /// capture never happened. Surface loudly — the order looks paid with no money behind it.
    /// </summary>
    HoldButMarkedCaptured,
    /// <summary>Cardcom's transaction is consistent with what George expects (within tolerance).</summary>
    Match,
    /// <summary>Cardcom's transaction diverges from what George expects — surface loudly, never auto-correct.</summary>
    Mismatch,
}

/// <summary>
/// Pure comparison of a Cardcom <c>GetTransactionInfoById</c> result against the order. Kept free of I/O
/// so the money rules are unit-testable.
///
/// A final charge is compared against <c>Order.Total</c> — the amount owed when the charge was made.
/// Refunds are separate Cardcom transactions and do not alter the original charge, so they must NOT be
/// subtracted here (a routine partial refund would otherwise false-flag every refunded order).
/// <c>RefundedAmount</c> matters only when the original deal itself shows as refunded/voided: then George
/// must know about a refund covering it, or the order looks paid while the money went back.
///
/// Known limits (documented product decisions): a corrective refund keeps the historical mismatch flag
/// (the charge really was wrong); a partial refund executed entirely outside George is a separate Cardcom
/// transaction invisible on the original deal, so it cannot be reconciled here.
/// </summary>
public static class GatewayChargeVerification
{
    /// <summary>Amounts within this delta are the same charge (2-decimal currency on both sides).</summary>
    public const decimal AmountTolerance = 0.01m;

    public static GatewayVerifyOutcome Evaluate(
        CardcomTransactionInfoResult info,
        decimal? orderTotal,
        decimal? refundedAmount,
        bool orderMarkedCaptured = false)
    {
        if (info is not { Success: true })
            return GatewayVerifyOutcome.Inconclusive;

        if (info.IsRefund == true)
        {
            if (info.Amount is not > 0m)
                return GatewayVerifyOutcome.Inconclusive;
            var refunded = refundedAmount is > 0m ? refundedAmount.Value : 0m;
            return refunded + AmountTolerance >= info.Amount.Value
                ? GatewayVerifyOutcome.Match
                : GatewayVerifyOutcome.Mismatch;
        }

        if (info.IsAuthorizationHold && !info.IsFinalCharge)
        {
            // "Information" rows are parsed as holds too, but they are inquiry echoes — ambiguous
            // evidence, not proof the capture is missing. Only a genuine J5 hold on an order that
            // claims to be captured is the false-success signature.
            var isGenuineHold = !string.Equals(info.DealType, "Information", StringComparison.OrdinalIgnoreCase);
            return orderMarkedCaptured && isGenuineHold
                ? GatewayVerifyOutcome.HoldButMarkedCaptured
                : GatewayVerifyOutcome.HoldOnly;
        }

        // "Information" rows are inquiries/echoes, not charges.
        if (!info.IsFinalCharge && string.Equals(info.DealType, "Information", StringComparison.OrdinalIgnoreCase))
            return GatewayVerifyOutcome.Inconclusive;

        if (info.Amount is not > 0m || orderTotal is not > 0m)
            return GatewayVerifyOutcome.Inconclusive;

        return Math.Abs(info.Amount.Value - orderTotal.Value) <= AmountTolerance
            ? GatewayVerifyOutcome.Match
            : GatewayVerifyOutcome.Mismatch;
    }
}
