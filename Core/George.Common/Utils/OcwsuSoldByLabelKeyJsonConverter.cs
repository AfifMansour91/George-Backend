using Newtonsoft.Json;

namespace George.Common.Utils;

/// <summary>Serializes <see cref="OcwsuSoldByLabelKey"/> as WooCommerce API strings (piece, tray, …).</summary>
public sealed class OcwsuSoldByLabelKeyJsonConverter : JsonConverter<OcwsuSoldByLabelKey?>
{
    public override OcwsuSoldByLabelKey? ReadJson(
        JsonReader reader,
        Type objectType,
        OcwsuSoldByLabelKey? existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return null;

        if (reader.TokenType != JsonToken.String)
            throw new JsonSerializationException($"Expected string for {nameof(OcwsuSoldByLabelKey)}, got {reader.TokenType}.");

        return OcwsuSoldByLabel.ParseNullable((string?)reader.Value);
    }

    public override void WriteJson(JsonWriter writer, OcwsuSoldByLabelKey? value, JsonSerializer serializer)
    {
        if (!value.HasValue)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteValue(OcwsuSoldByLabel.ToApiValue(value.Value));
    }
}
