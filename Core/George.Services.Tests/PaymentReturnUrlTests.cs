using George.Services.Payments;
using Xunit;

namespace George.Services.Tests;

public class PaymentReturnUrlTests
{
    // PEPE 9/15: staff on giorgio.co.il, API configured with another app host - the PayPlus return page landed
    // on the other origin, its sessionStorage breadcrumb was invisible, and the tab stayed on "thank you".
    [Fact]
    public void ResolveReturnAppBase_UsesRequestOrigin_WhenAbsoluteHttps()
    {
        Assert.Equal("https://giorgio.co.il",
            PaymentService.ResolveReturnAppBase("https://storeos.co.il", "https://giorgio.co.il"));
        Assert.Equal("https://giorgio.co.il",
            PaymentService.ResolveReturnAppBase("https://storeos.co.il", "https://giorgio.co.il/"));
    }

    [Fact]
    public void ResolveReturnAppBase_FallsBackToConfiguredBase_WhenOriginMissingOrInvalid()
    {
        Assert.Equal("https://storeos.co.il", PaymentService.ResolveReturnAppBase("https://storeos.co.il/", null));
        Assert.Equal("https://storeos.co.il", PaymentService.ResolveReturnAppBase("https://storeos.co.il", ""));
        Assert.Equal("https://storeos.co.il", PaymentService.ResolveReturnAppBase("https://storeos.co.il", "null"));
        Assert.Equal("https://storeos.co.il", PaymentService.ResolveReturnAppBase("https://storeos.co.il", "javascript:alert(1)"));
        Assert.Equal("https://storeos.co.il", PaymentService.ResolveReturnAppBase("https://storeos.co.il", "giorgio.co.il"));
    }

    [Fact]
    public void ResolveReturnAppBase_KeepsOnlyTheAuthority()
    {
        Assert.Equal("http://localhost:5173",
            PaymentService.ResolveReturnAppBase("https://storeos.co.il", "http://localhost:5173/orderscompleted/5?x=1"));
    }
}
