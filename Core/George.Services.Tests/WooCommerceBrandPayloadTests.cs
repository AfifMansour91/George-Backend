using System.Text.Json;
using George.Services;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Locks down the on-the-wire JSON shape used to assign brands to a product in WooCommerce REST v3.
/// Write mode requires <c>brands</c> as an array of objects with <c>id</c> (official Products API docs).
/// </summary>
public class WooCommerceBrandPayloadTests
{
    [Fact]
    public void Body_serializes_as_array_of_id_objects()
    {
        var body = new WooCommerceService.WooProductBrandsAssignmentBody
        {
            Brands = new[]
            {
                new WooCommerceService.WooProductBrandIdRef { Id = 16 },
                new WooCommerceService.WooProductBrandIdRef { Id = 21 },
            },
        };

        var json = JsonSerializer.Serialize(body);

        Assert.Equal("{\"brands\":[{\"id\":16},{\"id\":21}]}", json);
    }

    [Fact]
    public void Empty_array_serializes_as_brackets_not_null()
    {
        // Never send `null` to clear; always send `[]`.
        var body = new WooCommerceService.WooProductBrandsAssignmentBody
        {
            Brands = System.Array.Empty<WooCommerceService.WooProductBrandIdRef>(),
        };

        var json = JsonSerializer.Serialize(body);

        Assert.Equal("{\"brands\":[]}", json);
    }

    [Fact]
    public void Body_includes_id_property_required_by_Woo_write_schema()
    {
        var body = new WooCommerceService.WooProductBrandsAssignmentBody
        {
            Brands = new[] { new WooCommerceService.WooProductBrandIdRef { Id = 16 } },
        };

        var json = JsonSerializer.Serialize(body);

        Assert.Contains("\"id\":16", json);
        Assert.DoesNotContain("\"slug\":", json);
    }
}
