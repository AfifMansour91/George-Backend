using George.Common;
using George.Common.Utils;
using Newtonsoft.Json;

namespace George.Services.Request;

public class SyncOcwsuFixedUnitPriceDisplayReq
{
    public int SiteId { get; set; }
    public int ProductId { get; set; }
    public bool? DisplayPricePerFixedUnit { get; set; }

    [JsonConverter(typeof(OcwsuSoldByLabelKeyJsonConverter))]
    public OcwsuSoldByLabelKey? DisplayPricePerFixedUnitLabel { get; set; }
}
