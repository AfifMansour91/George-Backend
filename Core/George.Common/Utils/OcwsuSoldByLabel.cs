using System.Collections.Frozen;

namespace George.Common.Utils;

/// <summary>Maps <see cref="OcwsuSoldByLabelKey"/> to WooCommerce API wire values.</summary>
public static class OcwsuSoldByLabel
{
    public const string Piece = "piece";
    public const string Tray = "tray";
    public const string Pack = "pack";
    public const string Bunch = "bunch";
    public const string Bag = "bag";
    public const string Box = "box";
    public const string BoxAlt = "box_alt";

    public static OcwsuSoldByLabelKey DefaultKey => OcwsuSoldByLabelKey.Piece;

    private static readonly FrozenDictionary<OcwsuSoldByLabelKey, string> ApiByKey =
        new Dictionary<OcwsuSoldByLabelKey, string>
        {
            [OcwsuSoldByLabelKey.Piece] = Piece,
            [OcwsuSoldByLabelKey.Tray] = Tray,
            [OcwsuSoldByLabelKey.Pack] = Pack,
            [OcwsuSoldByLabelKey.Bunch] = Bunch,
            [OcwsuSoldByLabelKey.Bag] = Bag,
            [OcwsuSoldByLabelKey.Box] = Box,
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, OcwsuSoldByLabelKey> KeyByApi =
        new Dictionary<string, OcwsuSoldByLabelKey>(StringComparer.OrdinalIgnoreCase)
        {
            [Piece] = OcwsuSoldByLabelKey.Piece,
            [Tray] = OcwsuSoldByLabelKey.Tray,
            [Pack] = OcwsuSoldByLabelKey.Pack,
            [Bunch] = OcwsuSoldByLabelKey.Bunch,
            [Bag] = OcwsuSoldByLabelKey.Bag,
            [Box] = OcwsuSoldByLabelKey.Box,
            [BoxAlt] = OcwsuSoldByLabelKey.Box,
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public static string ToApiValue(OcwsuSoldByLabelKey key) => ApiByKey[key];

    public static OcwsuSoldByLabelKey Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return DefaultKey;

        return KeyByApi.TryGetValue(value.Trim(), out var key) ? key : DefaultKey;
    }

    public static OcwsuSoldByLabelKey? ParseNullable(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return KeyByApi.TryGetValue(value.Trim(), out var key) ? key : DefaultKey;
    }
}
