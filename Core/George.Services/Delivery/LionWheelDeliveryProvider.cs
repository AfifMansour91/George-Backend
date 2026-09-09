using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using George.DB;
using Microsoft.Extensions.Logging;

namespace George.Services.Delivery;

/// <summary>
/// LionWheel courier (https://members.lionwheel.com/api/v1, auth via <c>?key=</c>).
/// Create = POST tasks/create; cancel = PUT tasks/{id}/update with status 4 (no delete endpoint).
/// </summary>
public class LionWheelDeliveryProvider : IDeliveryProvider
{
    public const string Key = "lionwheel";

    private const string ApiBaseUrl = "https://members.lionwheel.com/api/v1";
    private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Verified against the live API (2026-09-08): the update endpoint expects the status NAME
    /// ("CANCELED", their single-L spelling) - numeric "4" (docs) returns a Rails 500 page.
    /// </summary>
    private const string LionWheelStatusCancelled = "CANCELED";

    /// <summary>LionWheel numeric task statuses (tasks/show + webhook payloads).</summary>
    private static readonly Dictionary<int, string> StatusNames = new()
    {
        [0] = "unassigned",
        [1] = "assigned",
        [2] = "active",
        [3] = "completed",
        [4] = "cancelled",
        [5] = "roundtrip_delivered",
        [6] = "in_inventory",
        [7] = "out_inventory",
        [8] = "failed",
        [9] = "final_failed",
        [10] = "in_transfer",
    };

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<LionWheelDeliveryProvider> _logger;

    public LionWheelDeliveryProvider(IHttpClientFactory httpClientFactory, ILogger<LionWheelDeliveryProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public string ProviderKey => Key;

    public async Task<DeliveryCreateResult> CreateTaskAsync(Order order, DeliveryProviderConfig config, CancellationToken cancelToken)
    {
        var apiKey = config.ApiKey?.Trim();
        if (string.IsNullOrEmpty(apiKey))
            return new DeliveryCreateResult(false, null, null, "LionWheel API key is not configured.");

        try
        {
            var payload = BuildCreateTaskPayload(order, config);
            var url = $"{ApiBaseUrl}/tasks/create?key={Uri.EscapeDataString(apiKey)}";
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = HttpTimeout;

            var httpResponse = await httpClient.PostAsync(url, content, cancelToken).ConfigureAwait(false);
            var responseText = await httpResponse.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "LionWheel task create rejected for order {OrderId}: HTTP {Status} {Body}",
                    order.Id, (int)httpResponse.StatusCode, Truncate(responseText));
                return new DeliveryCreateResult(false, null, null,
                    $"HTTP {(int)httpResponse.StatusCode}: {Truncate(responseText, 300)}");
            }

            using var doc = JsonDocument.Parse(responseText);
            var root = doc.RootElement;
            string? taskId = null;
            string? trackingLink = null;
            if (root.TryGetProperty("task_id", out var taskEl))
                taskId = taskEl.ValueKind == JsonValueKind.Number
                    ? taskEl.GetInt64().ToString(CultureInfo.InvariantCulture)
                    : taskEl.GetString();
            if (root.TryGetProperty("tracking_link", out var trackEl))
                trackingLink = trackEl.GetString();

            if (string.IsNullOrWhiteSpace(taskId))
                return new DeliveryCreateResult(false, null, null, "Response missing task_id.");

            return new DeliveryCreateResult(true, taskId.Trim(), trackingLink?.Trim(), null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LionWheel task create failed for order {OrderId}", order.Id);
            return new DeliveryCreateResult(false, null, null, ex.Message);
        }
    }

    public async Task<bool> CancelTaskAsync(string externalTaskId, DeliveryProviderConfig config, CancellationToken cancelToken)
    {
        var apiKey = config.ApiKey?.Trim();
        if (string.IsNullOrEmpty(apiKey))
            return false;

        try
        {
            var url = $"{ApiBaseUrl}/tasks/{Uri.EscapeDataString(externalTaskId)}/update?key={Uri.EscapeDataString(apiKey)}";
            var body = JsonSerializer.Serialize(new { status = LionWheelStatusCancelled });
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var httpClient = _httpClientFactory.CreateClient();
            httpClient.Timeout = HttpTimeout;

            var httpResponse = await httpClient.PutAsync(url, content, cancelToken).ConfigureAwait(false);
            if (!httpResponse.IsSuccessStatusCode)
            {
                var responseText = await httpResponse.Content.ReadAsStringAsync(cancelToken).ConfigureAwait(false);
                _logger.LogWarning(
                    "LionWheel task cancel rejected for task {TaskId}: HTTP {Status} {Body}",
                    externalTaskId, (int)httpResponse.StatusCode, Truncate(responseText));
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LionWheel task cancel failed for task {TaskId}", externalTaskId);
            return false;
        }
    }

    public (string TaskId, string CourierStatus)? ParseWebhookStatus(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            // Webhook payload mirrors the task structure; be tolerant to a wrapping "task" object.
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("task", out var taskObj)
                && taskObj.ValueKind == JsonValueKind.Object)
                root = taskObj;

            // Live payloads (tasks/show shape) carry "id"; the docs' create response uses "task_id".
            string? taskId = null;
            if (root.TryGetProperty("task_id", out var idEl) || root.TryGetProperty("id", out idEl))
                taskId = idEl.ValueKind == JsonValueKind.Number
                    ? idEl.GetInt64().ToString(CultureInfo.InvariantCulture)
                    : idEl.GetString();
            if (string.IsNullOrWhiteSpace(taskId))
                return null;

            if (!root.TryGetProperty("status", out var statusEl))
                return null;
            string courierStatus;
            if (statusEl.ValueKind == JsonValueKind.Number)
            {
                var code = statusEl.GetInt32();
                courierStatus = StatusNames.TryGetValue(code, out var name) ? name : code.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                courierStatus = statusEl.GetString()?.Trim().ToLowerInvariant() ?? "";
                // The API also serializes statuses as numeric strings ("1") - map those to names too.
                if (int.TryParse(courierStatus, NumberStyles.Integer, CultureInfo.InvariantCulture, out var codeFromString)
                    && StatusNames.TryGetValue(codeFromString, out var nameFromString))
                    courierStatus = nameFromString;
                // Live API spells it CANCELED (single L) - canonicalize to our "cancelled".
                if (courierStatus == "canceled")
                    courierStatus = "cancelled";
            }
            if (courierStatus.Length == 0)
                return null;

            return (taskId.Trim(), courierStatus);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// LionWheel cod_type: 0=cash, 1=cheque, 2=card, 3=bank transfer. George payment methods map to card for
    /// every credit variant (SavedCard / CreditSms / CreditPhone / CreditCard / ExternalCredit) and to bank
    /// transfer for on-account customers (settled by transfer); unknown methods send no type.
    /// </summary>
    public static int? MapPaymentMethodToCodType(string? paymentMethod)
    {
        var m = (paymentMethod ?? "").Trim().ToLowerInvariant();
        return m switch
        {
            "cash" => 0,
            "cheque" or "check" => 1,
            "creditcard" or "creditsms" or "creditphone" or "savedcard" or "externalcredit" or "credit" => 2,
            "banktransfer" or "onaccount" => 3,
            _ => null,
        };
    }

    /// <summary>Public for tests - pure payload construction.</summary>
    public static Dictionary<string, object?> BuildCreateTaskPayload(Order order, DeliveryProviderConfig config)
    {
        var (street, number) = SplitStreetAndNumber(order.DeliveryStreet ?? order.DeliveryAddress);
        var supplyDate = (order.DeliveryDate ?? order.PickupDate ?? DateTime.UtcNow).Date;

        var payload = new Dictionary<string, object?>
        {
            // Official Postman collection: pickup_at is dd/MM/yyyy ("13/05/2022").
            ["pickup_at"] = supplyDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ["original_order_id"] = string.IsNullOrWhiteSpace(order.OrderNumber) ? order.Id.ToString(CultureInfo.InvariantCulture) : order.OrderNumber.Trim(),
            ["destination_recipient_name"] = FirstNonEmpty(order.DeliveryRecipientName, order.CustomerName) ?? "",
            ["destination_phone"] = FirstNonEmpty(order.DeliveryRecipientPhone, order.CustomerPhone) ?? "",
            ["destination_city"] = order.DeliveryCity?.Trim() ?? "",
            ["destination_street"] = street,
            ["destination_number"] = number,
        };

        AddIfNotEmpty(payload, "destination_floor", order.DeliveryFloor);
        AddIfNotEmpty(payload, "destination_apartment", order.DeliveryApartment);
        // entrance_code is in the GitHub docs but not the official Postman body - send the dedicated
        // field and mirror it into destination_notes so the courier sees it either way.
        AddIfNotEmpty(payload, "destination_entrance_code", order.DeliveryEntranceCode);
        AddIfNotEmpty(payload, "destination_notes",
            string.IsNullOrWhiteSpace(order.DeliveryEntranceCode) ? null : $"קוד כניסה: {order.DeliveryEntranceCode.Trim()}");
        AddIfNotEmpty(payload, "destination_email", order.CustomerEmail);
        AddIfNotEmpty(payload, "notes", FirstNonEmpty(order.DeliveryNote, order.CustomerNote));
        if (order.BagsCount is > 0)
            payload["packages_quantity"] = order.BagsCount.Value;

        // Order value + payment method for the courier sheet ("גובינה" / "גובינה סוג", 2026-09-09):
        // money_collect is the amount in agorot (docs: "amount in cents, $3 should be 300"), cod_type is
        // 0=cash, 1=cheque, 2=card, 3=bank transfer.
        var total = order.Total ?? order.SubTotal;
        if (total is > 0m)
            payload["money_collect"] = (long)Math.Round(total.Value * 100m, 0, MidpointRounding.AwayFromZero);
        var codType = MapPaymentMethodToCodType(order.PaymentMethod);
        if (codType.HasValue)
            payload["cod_type"] = codType.Value;

        // Shipping-company tokens (not c_key customer tokens) require company_id on every task -
        // "This account requires every delivery to belong to a company." Configured per site in Settings.
        var settings = DeliveryDispatchService.ParseSettings(config.SettingsJson);
        if (settings.TryGetValue("company_id", out var companyId) && !string.IsNullOrWhiteSpace(companyId))
            payload["company_id"] = companyId.Trim();

        // Package pickup address from provider config (source_*).
        AddIfNotEmpty(payload, "source_city", config.PickupCity);
        AddIfNotEmpty(payload, "source_street", config.PickupStreet);
        AddIfNotEmpty(payload, "source_number", config.PickupNumber);
        AddIfNotEmpty(payload, "source_recipient_name", config.PickupName);
        AddIfNotEmpty(payload, "source_phone", config.PickupPhone);

        return payload;
    }

    /// <summary>
    /// George stores street+house as one line ("הרצל 12"); LionWheel wants them split
    /// (destination_street / destination_number, number is required). Trailing house-number
    /// token (optionally with a Hebrew letter suffix, e.g. "12א") is peeled off; when the
    /// line has no number the full text stays in street and number is sent empty.
    /// </summary>
    public static (string street, string number) SplitStreetAndNumber(string? addressLine)
    {
        var line = addressLine?.Trim() ?? "";
        if (line.Length == 0) return ("", "");
        var m = Regex.Match(line, @"^(?<street>.*?)[\s,]+(?<num>\d+[א-ת']?)\s*$");
        if (m.Success && m.Groups["street"].Value.Trim().Length > 0)
            return (m.Groups["street"].Value.Trim().TrimEnd(','), m.Groups["num"].Value.Trim());
        return (line, "");
    }

    private static void AddIfNotEmpty(Dictionary<string, object?> payload, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            payload[key] = value.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();

    private static string Truncate(string? text, int max = 800) =>
        string.IsNullOrEmpty(text) ? "" : text.Length <= max ? text : text[..max];
}
