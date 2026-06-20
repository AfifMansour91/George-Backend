using George.DB;

namespace George.Services;

/// <summary>
/// Maps OCWSU WooCommerce weight-unit meta to George <see cref="Unit"/> rows during catalog import.
/// </summary>
public static class WooCommerceImportUnitMapping
{
    /// <summary>
    /// OCWSU plugin meta values: <c>kg</c> or <c>grams</c> (see WooCommerce-ocwsu-meta.md).
    /// George <see cref="Unit.Name"/> is typically <c>kg</c> or <c>g</c>.
    /// </summary>
    public static string? MapOcwsuWeightUnitsMetaToGeorgeUnitName(string? ocwsuUnitsFromMeta)
    {
        if (string.IsNullOrWhiteSpace(ocwsuUnitsFromMeta))
            return null;

        var t = ocwsuUnitsFromMeta.Trim().ToLowerInvariant();
        return t switch
        {
            "grams" or "gram" or "g" => "g",
            "kg" or "kilogram" or "kilograms" => "kg",
            _ when string.Equals(ocwsuUnitsFromMeta.Trim(), "גרם", StringComparison.Ordinal) => "g",
            _ when string.Equals(ocwsuUnitsFromMeta.Trim(), "ק\"ג", StringComparison.Ordinal) => "kg",
            _ => null
        };
    }

    /// <summary>
    /// Resolve George <see cref="Unit.Id"/> from OCWSU <c>_ocwsu_product_weight_units</c> meta.
    /// </summary>
    public static int? ResolveUnitIdFromOcwsu(IReadOnlyList<Unit> units, string? ocwsuUnitsFromMeta)
    {
        if (string.IsNullOrWhiteSpace(ocwsuUnitsFromMeta) || units.Count == 0)
            return null;

        var georgeName = MapOcwsuWeightUnitsMetaToGeorgeUnitName(ocwsuUnitsFromMeta);
        if (georgeName != null)
        {
            var direct = units.FirstOrDefault(u =>
                string.Equals(u.Name, georgeName, StringComparison.OrdinalIgnoreCase));
            if (direct != null)
                return direct.Id;
        }

        var target = ocwsuUnitsFromMeta.Trim().ToLowerInvariant();
        foreach (var u in units)
        {
            var mapped = MapGeorgeUnitNameToOcwsuWeightUnits(u.Name);
            if (string.Equals(mapped, target, StringComparison.OrdinalIgnoreCase))
                return u.Id;
        }

        return null;
    }

    /// <summary>
    /// George unit name → OCWSU plugin radio value (<c>kg</c> / <c>grams</c>).
    /// </summary>
    public static string MapGeorgeUnitNameToOcwsuWeightUnits(string? unitName)
    {
        if (string.IsNullOrWhiteSpace(unitName))
            return "";

        var u = unitName.Trim();
        var lower = u.ToLowerInvariant();
        if (lower is "kg" or "kilogram" or "kilograms")
            return "kg";
        if (string.Equals(u, "ק\"ג", StringComparison.Ordinal))
            return "kg";
        if (string.Equals(u, "גרם", StringComparison.Ordinal))
            return "grams";
        if (lower is "g" or "gram" or "grams")
            return "grams";

        return u;
    }
}
