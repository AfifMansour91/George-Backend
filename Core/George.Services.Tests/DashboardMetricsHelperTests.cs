using George.Services;

namespace George.Services.Tests;

public class DashboardMetricsHelperTests
{
    [Fact]
    public void PctChange_ReturnsNullWhenBaselineZeroAndCurrentNonZero()
    {
        var result = DashboardMetricsHelper.PctChange(100m, 0m);
        Assert.Null(result);
    }

    [Fact]
    public void PctChange_ReturnsZeroWhenBothZero()
    {
        var result = DashboardMetricsHelper.PctChange(0m, 0m);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void PctChange_ComputesIncrease()
    {
        var result = DashboardMetricsHelper.PctChange(110m, 100m);
        Assert.Equal(10m, result);
    }

    [Fact]
    public void IsUnsettledPayment_TreatsPaidAsSettled()
    {
        Assert.False(DashboardMetricsHelper.IsUnsettledPayment("Paid"));
        Assert.False(DashboardMetricsHelper.IsUnsettledPayment("Captured"));
        Assert.True(DashboardMetricsHelper.IsUnsettledPayment("Unpaid"));
        Assert.True(DashboardMetricsHelper.IsUnsettledPayment("Pending"));
    }
}
