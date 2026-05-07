using System.Text.Json;
using George.Services;
using Xunit;

namespace George.Services.Tests;

/// <summary>
/// Locks down the on-the-wire JSON shape used to assign brands to a product in WooCommerce.
///
/// Per <c>brands/brands-feature-spec.md §5.7</c>:
///
///   ✅ correct:   {"brands":[16,21]}     (flat array of IDs)
///   ❌ WRONG:     {"brands":[{"id":16}, {"id":21}]}   (categories shape — won't work for brands)
///
/// This is called out as the most common mistake when integrating with WooCommerce Brands.
/// If serialization ever drifts from the flat-ID shape, this test fails fast and loudly
/// before any production traffic is sent.
/// </summary>
public class WooCommerceBrandPayloadTests
{
    [Fact]
    public void Body_serializes_as_flat_id_array()
    {
        var body = new WooCommerceService.WooProductBrandsAssignmentBody
        {
            Brands = new[] { 16, 21 },
        };

        var json = JsonSerializer.Serialize(body);

        Assert.Equal("{\"brands\":[16,21]}", json);
    }

    [Fact]
    public void Empty_array_serializes_as_brackets_not_null()
    {
        // Per spec note about the 2016 bug: never send `null` to clear; always send `[]`.
        var body = new WooCommerceService.WooProductBrandsAssignmentBody
        {
            Brands = System.Array.Empty<int>(),
        };

        var json = JsonSerializer.Serialize(body);

        Assert.Equal("{\"brands\":[]}", json);
    }

    [Fact]
    public void Body_does_NOT_serialize_as_array_of_objects()
    {
        // Defensive: confirm the shape isn't what categories use.
        var body = new WooCommerceService.WooProductBrandsAssignmentBody
        {
            Brands = new[] { 16 },
        };

        var json = JsonSerializer.Serialize(body);

        Assert.DoesNotContain("\"id\":", json);
        Assert.DoesNotContain("\"slug\":", json);
    }
}
