using George.DB;
using George.Services;
using Xunit;

namespace George.Services.Tests;

public class WooCommerceProductLabelTests
{
    [Fact]
    public void BuildEdV1ProductLabels_maps_all_storefront_flags()
    {
        var product = new Product
        {
            LabelFrozen = true,
            LabelGlutenFree = true,
            LabelNotKosher = false,
            LabelKosherForPassover = true,
            LabelBestseller = true,
            LabelLowAvailability = true,
            LabelReadyToCook = true,
            LabelNatural = true,
            LabelSugarFree = true,
            LabelLactoseFree = true,
            LabelNew = true,
        };

        var labels = WooCommerceService.BuildEdV1ProductLabels(product);

        Assert.Equal(11, labels.Count);
        Assert.True(labels["frozen"]);
        Assert.True(labels["gluten_free"]);
        Assert.False(labels["not_kosher"]);
        Assert.True(labels["kosher_for_passover"]);
        Assert.True(labels["bestseller"]);
        Assert.True(labels["low_availability"]);
        Assert.True(labels["readytocook"]);
        Assert.True(labels["natural"]);
        Assert.True(labels["sugarfree"]);
        Assert.True(labels["lactosefree"]);
        Assert.True(labels["new"]);
    }

    [Fact]
    public void BuildEdV1ProductLabels_expires_passover_and_new_by_end_date()
    {
        var product = new Product
        {
            LabelKosherForPassover = true,
            LabelKosherForPassoverEndDate = DateTime.UtcNow.AddDays(-1),
            LabelNew = true,
            LabelNewEndDate = DateTime.UtcNow.AddDays(-1),
        };

        var labels = WooCommerceService.BuildEdV1ProductLabels(product);

        Assert.False(labels["kosher_for_passover"]);
        Assert.False(labels["new"]);
    }
}
