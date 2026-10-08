namespace George.Services.Tests;

/// <summary>
/// Zano 2026-10-07: "hide delivery time on printouts" must be able to hide the time on delivery orders only,
/// leaving the pickup time printed. Scope "all" / NULL keeps the original behavior (hide on every order).
/// </summary>
public class VoucherDeliveryTimeVisibilityTests
{
    [Theory]
    [InlineData(null, "all", true)]
    [InlineData(false, "all", true)]
    [InlineData(false, "shipping", true)]
    [InlineData(false, "shipping", false)]
    public void FlagOff_NeverHides(bool? hide, string scope, bool isShipping)
    {
        Assert.False(VoucherDeliveryTimeVisibility.HidesTime(hide, scope, isShipping));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("all")]
    [InlineData("ALL")]
    [InlineData("garbage")]
    public void ScopeAllOrUnknown_HidesOnDeliveryAndPickup(string? scope)
    {
        Assert.True(VoucherDeliveryTimeVisibility.HidesTime(true, scope, isShipping: true));
        Assert.True(VoucherDeliveryTimeVisibility.HidesTime(true, scope, isShipping: false));
    }

    [Theory]
    [InlineData("shipping")]
    [InlineData("Shipping ")]
    public void ScopeShipping_HidesOnDeliveryOnly(string scope)
    {
        Assert.True(VoucherDeliveryTimeVisibility.HidesTime(true, scope, isShipping: true));
        Assert.False(VoucherDeliveryTimeVisibility.HidesTime(true, scope, isShipping: false));
    }
}
