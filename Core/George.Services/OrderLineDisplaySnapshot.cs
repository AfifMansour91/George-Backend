using System.Text.Json;
using System.Text.Json.Serialization;

namespace George.Services;

/// <summary>
/// Typed order-line display snapshot persisted as <c>OrderItem.LineDisplayJson</c> (camelCase).
/// Written ONCE at line creation by <see cref="OrderLineDisplayFieldsBuilder"/>; surfaces render from it
/// (numbers + clean names, no Hebrew-label parsing). Keep in sync with shop-manager
/// <c>orderItemLineDisplay.ts</c> <c>OrderLineDisplaySnapshot</c>.
/// </summary>
public sealed class OrderLineDisplaySnapshot
{
    /// <summary>Schema version for forward evolution.</summary>
    [JsonPropertyName("v")]
    public int V { get; set; } = 1;

    /// <summary>standard | by_weight | by_unit_average | by_unit_variable | by_unit_by_variant | by_unit_and_weight</summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = OrderLineDisplayKinds.Standard;

    /// <summary>Clean size name (e.g. "בין 5-6 ק״ג") — no approx-weight suffix.</summary>
    [JsonPropertyName("sizeName")]
    public string? SizeName { get; set; }

    /// <summary>Informational per-unit weight (grams) — average / by-variant portion.</summary>
    [JsonPropertyName("approxUnitWeightGrams")]
    public int? ApproxUnitWeightGrams { get; set; }

    /// <summary>Customer-chosen per-unit weight (grams) — בחירת משקל ליחידה; always displayed.</summary>
    [JsonPropertyName("chosenUnitWeightGrams")]
    public int? ChosenUnitWeightGrams { get; set; }

    /// <summary>Cutting option value (e.g. "פילה פרוס בלי עור").</summary>
    [JsonPropertyName("cuttingName")]
    public string? CuttingName { get; set; }

    /// <summary>Ordered unit count when the line is sold by units.</summary>
    [JsonPropertyName("unitCount")]
    public decimal? UnitCount { get; set; }

    /// <summary>Total ordered weight (grams) when derivable (kg-channel lines, or units × per-unit).</summary>
    [JsonPropertyName("totalWeightGrams")]
    public int? TotalWeightGrams { get; set; }

    private static readonly JsonSerializerOptions SerializeOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public string ToJson() => JsonSerializer.Serialize(this, SerializeOptions);

    /// <summary>Null on missing/invalid JSON or unknown kind — caller falls back to legacy rendering.</summary>
    public static OrderLineDisplaySnapshot? TryParse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var snap = JsonSerializer.Deserialize<OrderLineDisplaySnapshot>(json, DeserializeOptions);
            if (snap == null || !OrderLineDisplayKinds.IsKnown(snap.Kind)) return null;
            return snap;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

public static class OrderLineDisplayKinds
{
    public const string Standard = "standard";
    public const string ByWeight = "by_weight";
    public const string ByUnitAverage = "by_unit_average";
    public const string ByUnitVariable = "by_unit_variable";
    public const string ByUnitByVariant = "by_unit_by_variant";
    public const string ByUnitAndWeight = "by_unit_and_weight";

    public static bool IsKnown(string? kind) =>
        kind is Standard or ByWeight or ByUnitAverage or ByUnitVariable or ByUnitByVariant or ByUnitAndWeight;
}
