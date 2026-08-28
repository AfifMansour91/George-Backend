using George.Common;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Unlinked (WP-local) promotion stamps must follow the picked line gross proportionally -
/// the client scenario: 5% discount stamped on 500g (₪124.50 → ₪6.23), picked at 0.55kg.
/// </summary>
public class OrderDiscountScalingTests
{
    [Fact]
    public void UnlinkedDiscount_ScalesUp_WhenPickedWeightAboveOrdered()
    {
        // נתח סינטה: 249 ₪/ק"ג, הוזמן 500 גרם, נלקט 0.55 ק"ג.
        var scaled = OrderDiscountTotals.ScaleStampedLineDiscount(
            discountAmount: 6.23m, promotionId: null,
            orderedGross: 0.5m * 249m, currentGross: 0.55m * 249m);
        Assert.Equal(6.85m, scaled);
        Assert.Equal(130.10m, 136.95m - scaled);
    }

    [Fact]
    public void UnlinkedDiscount_ScalesDown_WhenPickedWeightBelowOrdered()
    {
        var scaled = OrderDiscountTotals.ScaleStampedLineDiscount(
            discountAmount: 6.23m, promotionId: null,
            orderedGross: 124.50m, currentGross: 0.45m * 249m);
        Assert.Equal(5.61m, scaled);
    }

    [Fact]
    public void LinkedDiscount_IsNeverScaled_EvaluatorOwnsIt()
    {
        var scaled = OrderDiscountTotals.ScaleStampedLineDiscount(
            discountAmount: 6.23m, promotionId: 12,
            orderedGross: 124.50m, currentGross: 136.95m);
        Assert.Equal(6.23m, scaled);
    }

    [Fact]
    public void PairScaling_ComposesAcrossRepicks_UsingTotalPriceRatio()
    {
        // הזוג (TotalPrice ↔ DiscountAmount) נשמר בכל שמירת ליקוט: הבסיס הוא ה-TotalPrice הקודם,
        // בלי שום גזירה מ-Quantity/PricePerUnit (שסמנטיקת המשקל שלהם לא אמינה).
        // ליקוט ראשון: 124.50 → 136.95.
        var first = OrderDiscountTotals.ScaleStampedLineDiscount(6.23m, null, 124.50m, 136.95m);
        Assert.Equal(6.85m, first);
        // ליקוט חוזר: 136.95 → 149.40 - היחס מצטבר נכון (בקירוב עיגול אגורה).
        var second = OrderDiscountTotals.ScaleStampedLineDiscount(first, null, 136.95m, 149.40m);
        Assert.Equal(7.47m, second);
    }

    [Fact]
    public void UnlinkedDiscount_Unchanged_WhenGrossEqualOrUnknown()
    {
        Assert.Equal(6.23m, OrderDiscountTotals.ScaleStampedLineDiscount(6.23m, null, 124.50m, 124.50m));
        Assert.Equal(6.23m, OrderDiscountTotals.ScaleStampedLineDiscount(6.23m, null, 0m, 136.95m));
        Assert.Equal(6.23m, OrderDiscountTotals.ScaleStampedLineDiscount(6.23m, null, 124.50m, 0m));
        Assert.Equal(0m, OrderDiscountTotals.ScaleStampedLineDiscount(null, null, 124.50m, 136.95m));
    }
}
