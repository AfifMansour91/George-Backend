using George.Common;
using George.Common.Utils;
using Newtonsoft.Json;

namespace George.Services.Response;

public class OcwsuFixedUnitPriceDisplayRes
{
    public int ProductId { get; set; }
    public bool DisplayPricePerFixedUnit { get; set; }

    [JsonConverter(typeof(OcwsuSoldByLabelKeyJsonConverter))]
    public OcwsuSoldByLabelKey DisplayPricePerFixedUnitLabel { get; set; } = OcwsuSoldByLabel.DefaultKey;

    public Dictionary<string, string>? LabelOptions { get; set; }
    public List<string>? LabelOptionKeys { get; set; }
    public bool Success { get; set; }
}
