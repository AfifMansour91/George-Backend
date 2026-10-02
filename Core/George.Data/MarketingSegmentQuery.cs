using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using George.DB;
using Microsoft.EntityFrameworkCore;

namespace George.Data;

/// <summary>
/// One segment condition (spec §3.1). Conditions are AND-ed; no OR, no nesting - that is what lets the UI
/// show a filter as a plain Hebrew sentence.
/// </summary>
public class SegmentCondition
{
    [JsonPropertyName("axis")]
    public string Axis { get; set; } = string.Empty;

    [JsonPropertyName("operator")]
    public string Operator { get; set; } = string.Empty;

    [JsonPropertyName("value")]
    public string? Value { get; set; }

    /// <summary>Upper bound for <c>between</c>.</summary>
    [JsonPropertyName("value2")]
    public string? Value2 { get; set; }

    /// <summary>Look-back window in days for the <c>product</c> axis.</summary>
    [JsonPropertyName("days")]
    public int? Days { get; set; }

    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    /// <summary>Display text of a picked entity (category / product name). Never evaluated - it only lets the UI
    /// render "bought from בשר בקר" later without looking the id up again.</summary>
    [JsonPropertyName("label")]
    public string? Label { get; set; }
}

public static class SegmentAxis
{
    public const string Recency = "recency";
    public const string Frequency = "frequency";
    public const string Monetary = "monetary";
    public const string Product = "product";
    public const string Channel = "channel";
    public const string Geo = "geo";
    public const string Dates = "dates";
    /// <summary>Personal pace (system segment "at risk" and, later, the personal-pace automation).</summary>
    public const string Risk = "risk";
    /// <summary>"שלח שוב למי שלא הזמין": recipients of an earlier send (Value = its id) without an attributed order. System-only.</summary>
    public const string Resend = "resend";
}

public static class SegmentOperator
{
    public const string NotOrderedIn = "not_ordered_in";
    public const string OrderedIn = "ordered_in";
    public const string FirstOrderIn = "first_order_in";
    public const string Gte = "gte";
    public const string Lte = "lte";
    public const string Between = "between";
    public const string TopPercent = "top_percent";
    public const string BoughtCategory = "bought_category";
    public const string BoughtProduct = "bought_product";
    public const string Is = "is";
    public const string CityIs = "city_is";
    public const string BirthdayThisMonth = "birthday_this_month";
    public const string BirthdayToday = "birthday_today";
    public const string PaceExceeded = "pace_exceeded";
    public const string NonBuyers = "non_buyers";
}

/// <summary>Order-source buckets of the <c>channel</c> axis. <c>Order.Source</c> holds magic strings; anything that is not Kiosk/Phone came from the website.</summary>
public static class SegmentChannel
{
    public const string Web = "web";
    public const string Kiosk = "kiosk";
    public const string Phone = "phone";
}

/// <summary>The ready-made segments (spec §3.2) - computed, never stored, cannot be deleted. "Club members" is out until a club module exists.</summary>
public static class MarketingSystemSegments
{
    public const string NewCustomers = "new_customers";
    public const string Regulars = "regulars";
    public const string Vip = "vip";
    public const string AtRisk = "at_risk";
    public const string Dormant = "dormant";
    public const string NoOrders = "no_orders";
    public const string BirthdayMonth = "birthday_month";

    public static readonly IReadOnlyList<(string Key, SegmentCondition[] Conditions)> All = new List<(string, SegmentCondition[])>
    {
        (NewCustomers, new[] { new SegmentCondition { Axis = SegmentAxis.Recency, Operator = SegmentOperator.FirstOrderIn, Value = "30", Unit = "days" } }),
        (Regulars, new[] { new SegmentCondition { Axis = SegmentAxis.Frequency, Operator = SegmentOperator.Gte, Value = "4" } }),
        (Vip, new[] { new SegmentCondition { Axis = SegmentAxis.Monetary, Operator = SegmentOperator.TopPercent, Value = "20" } }),
        (AtRisk, new[] { new SegmentCondition { Axis = SegmentAxis.Risk, Operator = SegmentOperator.PaceExceeded, Value = "2" } }),
        (Dormant, new[] { new SegmentCondition { Axis = SegmentAxis.Recency, Operator = SegmentOperator.NotOrderedIn, Value = "90", Unit = "days" } }),
        (NoOrders, new[] { new SegmentCondition { Axis = SegmentAxis.Frequency, Operator = SegmentOperator.Lte, Value = "0" } }),
        (BirthdayMonth, new[] { new SegmentCondition { Axis = SegmentAxis.Dates, Operator = SegmentOperator.BirthdayThisMonth } }),
    };

    public static SegmentCondition[]? Find(string? key) =>
        All.FirstOrDefault(s => string.Equals(s.Key, key, StringComparison.OrdinalIgnoreCase)).Conditions;
}

/// <summary>A customer with the order aggregates every segment axis is built from. Composed in SQL - never materialised as a whole.</summary>
public class CustomerAggRow
{
    public Customer Customer { get; set; } = null!;
    public int OrderCount { get; set; }
    public decimal TotalRevenue { get; set; }
    public DateTime? FirstOrderAt { get; set; }
    public DateTime? LastOrderAt { get; set; }
}

/// <summary>
/// Translates a segment definition into an <see cref="IQueryable{T}"/> over customers. Everything runs in SQL:
/// the customers list loads a whole site into memory, which a segment spanning an account cannot afford.
/// </summary>
public static class MarketingSegmentQuery
{
    /// <summary>Spec open decision 4: beyond 3 conditions the sentence stops reading as a sentence.</summary>
    public const int MaxConditions = 3;

    private const int DefaultProductDays = 90;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static List<SegmentCondition> Parse(string? definitionJson)
    {
        if (string.IsNullOrWhiteSpace(definitionJson))
            return new List<SegmentCondition>();
        try
        {
            return JsonSerializer.Deserialize<List<SegmentCondition>>(definitionJson, JsonOptions) ?? new List<SegmentCondition>();
        }
        catch (JsonException)
        {
            return new List<SegmentCondition>();
        }
    }

    public static string Serialize(IEnumerable<SegmentCondition>? conditions) =>
        JsonSerializer.Serialize(conditions ?? Enumerable.Empty<SegmentCondition>());

    /// <summary>User-facing (Hebrew) error for an invalid definition, or null when valid.</summary>
    public static string? Validate(IReadOnlyCollection<SegmentCondition>? conditions, bool allowSystemAxes = false)
    {
        if (conditions == null || conditions.Count == 0)
            return null; // no conditions = all customers
        if (conditions.Count > MaxConditions)
            return $"אפשר לשלב עד {MaxConditions} תנאים בסגמנט";

        foreach (var c in conditions)
        {
            var axis = (c.Axis ?? string.Empty).Trim().ToLowerInvariant();
            var op = (c.Operator ?? string.Empty).Trim().ToLowerInvariant();
            switch (axis)
            {
                case SegmentAxis.Recency:
                    if (op is not (SegmentOperator.NotOrderedIn or SegmentOperator.OrderedIn or SegmentOperator.FirstOrderIn))
                        return "תנאי 'מתי הזמין' לא חוקי";
                    if (!TryInt(c.Value, out var days) || days < 1 || days > 3650)
                        return "מספר הימים חייב להיות בין 1 ל-3650";
                    break;
                case SegmentAxis.Frequency:
                    if (op is not (SegmentOperator.Gte or SegmentOperator.Lte or SegmentOperator.Between))
                        return "תנאי 'כמה פעמים' לא חוקי";
                    if (!TryInt(c.Value, out var n) || n < 0)
                        return "מספר ההזמנות חייב להיות 0 ומעלה";
                    if (op == SegmentOperator.Between && (!TryInt(c.Value2, out var n2) || n2 < n))
                        return "טווח ההזמנות לא חוקי";
                    break;
                case SegmentAxis.Monetary:
                    if (op is not (SegmentOperator.Gte or SegmentOperator.Lte or SegmentOperator.TopPercent))
                        return "תנאי 'כמה כסף' לא חוקי";
                    if (!TryDecimal(c.Value, out var amount) || amount < 0)
                        return "הסכום חייב להיות 0 ומעלה";
                    if (op == SegmentOperator.TopPercent && (amount < 1 || amount > 100))
                        return "האחוזון חייב להיות בין 1 ל-100";
                    break;
                case SegmentAxis.Product:
                    if (op is not (SegmentOperator.BoughtCategory or SegmentOperator.BoughtProduct))
                        return "תנאי 'מה קנה' לא חוקי";
                    if (!TryInt(c.Value, out var id) || id <= 0)
                        return "יש לבחור קטגוריה או מוצר";
                    if (c.Days is < 1 or > 3650)
                        return "טווח הזמן חייב להיות בין 1 ל-3650 ימים";
                    break;
                case SegmentAxis.Channel:
                    if (op != SegmentOperator.Is || NormalizeChannel(c.Value) == null)
                        return "תנאי 'מאיפה' לא חוקי";
                    break;
                case SegmentAxis.Geo:
                    if (op != SegmentOperator.CityIs || string.IsNullOrWhiteSpace(c.Value))
                        return "יש לבחור עיר";
                    break;
                case SegmentAxis.Dates:
                    if (op is not (SegmentOperator.BirthdayThisMonth or SegmentOperator.BirthdayToday))
                        return "תנאי 'תאריכים' לא חוקי";
                    break;
                case SegmentAxis.Risk when allowSystemAxes:
                    if (op != SegmentOperator.PaceExceeded || !TryDecimal(c.Value, out var mult) || mult < 1)
                        return "תנאי 'קצב אישי' לא חוקי";
                    break;
                case SegmentAxis.Resend when allowSystemAxes:
                    if (op != SegmentOperator.NonBuyers || !int.TryParse(c.Value, out var sourceSend) || sourceSend <= 0)
                        return "תנאי 'שליחה חוזרת' לא חוקי";
                    break;
                default:
                    return $"ציר לא מוכר: {c.Axis}";
            }
        }
        return null;
    }

    /// <summary>All (non-deleted) customers of the account inside the branch scope, with their order aggregates.</summary>
    public static IQueryable<CustomerAggRow> BaseQuery(GeorgeDBContext db, int accountId, IReadOnlyCollection<int> siteIds)
    {
        return db.Set<Customer>()
            .AsNoTracking()
            .Where(c => c.AccountId == accountId && !c.IsDeleted && siteIds.Contains(c.SiteId))
            .Select(c => new CustomerAggRow
            {
                Customer = c,
                OrderCount = c.Order.Count(o => !o.IsDeleted && o.Status != "Cancelled"),
                TotalRevenue = c.Order.Where(o => !o.IsDeleted && o.Status != "Cancelled").Sum(o => o.Total ?? 0m),
                FirstOrderAt = c.Order.Where(o => !o.IsDeleted && o.Status != "Cancelled").Min(o => (DateTime?)o.CreationTime),
                LastOrderAt = c.Order.Where(o => !o.IsDeleted && o.Status != "Cancelled").Max(o => (DateTime?)o.CreationTime),
            });
    }

    /// <summary>Consent is NOT a segment condition (spec §3.4) - a segment says who, the dispatcher says who may be sent to. This is that second predicate.</summary>
    public static IQueryable<CustomerAggRow> WhereSmsConsent(IQueryable<CustomerAggRow> query) =>
        query.Where(r => r.Customer.MarketingSms && r.Customer.OptedOutAt == null);

    /// <summary>Applies the conditions. Async because <c>top_percent</c> needs a threshold query first.</summary>
    public static async Task<IQueryable<CustomerAggRow>> ApplyAsync(
        GeorgeDBContext db,
        IQueryable<CustomerAggRow> query,
        IEnumerable<SegmentCondition>? conditions,
        DateTime utcNow,
        DateTime israelToday,
        CancellationToken cancelToken)
    {
        if (conditions == null)
            return query;

        // The percentile is taken over the whole scope, not over what the other conditions left.
        var scope = query;

        foreach (var c in conditions)
        {
            var axis = (c.Axis ?? string.Empty).Trim().ToLowerInvariant();
            var op = (c.Operator ?? string.Empty).Trim().ToLowerInvariant();

            switch (axis)
            {
                case SegmentAxis.Recency:
                {
                    TryInt(c.Value, out var days);
                    var cutoff = utcNow.AddDays(-days);
                    query = op switch
                    {
                        SegmentOperator.OrderedIn => query.Where(r => r.LastOrderAt != null && r.LastOrderAt >= cutoff),
                        SegmentOperator.FirstOrderIn => query.Where(r => r.FirstOrderAt != null && r.FirstOrderAt >= cutoff),
                        // "Did not order in N days" speaks about people who DID order once; never-ordered is its own segment.
                        _ => query.Where(r => r.LastOrderAt != null && r.LastOrderAt < cutoff),
                    };
                    break;
                }
                case SegmentAxis.Frequency:
                {
                    TryInt(c.Value, out var n);
                    if (op == SegmentOperator.Between)
                    {
                        TryInt(c.Value2, out var n2);
                        query = query.Where(r => r.OrderCount >= n && r.OrderCount <= n2);
                    }
                    else if (op == SegmentOperator.Lte)
                        query = query.Where(r => r.OrderCount <= n);
                    else
                        query = query.Where(r => r.OrderCount >= n);
                    break;
                }
                case SegmentAxis.Monetary:
                {
                    TryDecimal(c.Value, out var amount);
                    if (op == SegmentOperator.TopPercent)
                    {
                        var buyers = scope.Where(r => r.OrderCount > 0);
                        var buyersCount = await buyers.CountAsync(cancelToken).ConfigureAwait(false);
                        if (buyersCount == 0)
                        {
                            query = query.Where(r => false);
                            break;
                        }
                        var take = Math.Max(1, (int)Math.Ceiling(buyersCount * (double)amount / 100d));
                        var threshold = await buyers
                            .OrderByDescending(r => r.TotalRevenue)
                            .Skip(take - 1)
                            .Select(r => r.TotalRevenue)
                            .FirstOrDefaultAsync(cancelToken)
                            .ConfigureAwait(false);
                        query = query.Where(r => r.OrderCount > 0 && r.TotalRevenue >= threshold);
                    }
                    else if (op == SegmentOperator.Lte)
                        query = query.Where(r => r.TotalRevenue <= amount);
                    else
                        query = query.Where(r => r.TotalRevenue >= amount);
                    break;
                }
                case SegmentAxis.Product:
                {
                    TryInt(c.Value, out var id);
                    var cutoff = utcNow.AddDays(-(c.Days ?? DefaultProductDays));
                    if (op == SegmentOperator.BoughtProduct)
                    {
                        query = query.Where(r => r.Customer.Order.Any(o =>
                            !o.IsDeleted && o.Status != "Cancelled" && o.CreationTime >= cutoff &&
                            o.OrderItem.Any(i => i.ProductId == id)));
                    }
                    else
                    {
                        query = query.Where(r => r.Customer.Order.Any(o =>
                            !o.IsDeleted && o.Status != "Cancelled" && o.CreationTime >= cutoff &&
                            o.OrderItem.Any(i => db.Set<ProductCategory>().Any(pc => pc.ProductId == i.ProductId && pc.CategoryId == id))));
                    }
                    break;
                }
                case SegmentAxis.Channel:
                {
                    var channel = NormalizeChannel(c.Value);
                    query = channel switch
                    {
                        SegmentChannel.Kiosk => query.Where(r => r.Customer.Order.Any(o => !o.IsDeleted && o.Status != "Cancelled" && o.Source == "Kiosk")),
                        SegmentChannel.Phone => query.Where(r => r.Customer.Order.Any(o => !o.IsDeleted && o.Status != "Cancelled" && o.Source == "Phone")),
                        _ => query.Where(r => r.Customer.Order.Any(o => !o.IsDeleted && o.Status != "Cancelled" && o.Source != "Kiosk" && o.Source != "Phone")),
                    };
                    break;
                }
                case SegmentAxis.Geo:
                {
                    var city = (c.Value ?? string.Empty).Trim();
                    query = query.Where(r => r.Customer.City != null && r.Customer.City.Trim() == city);
                    break;
                }
                case SegmentAxis.Dates:
                {
                    var month = israelToday.Month;
                    var day = israelToday.Day;
                    query = op == SegmentOperator.BirthdayToday
                        ? query.Where(r => r.Customer.BirthDate != null && r.Customer.BirthDate.Value.Month == month && r.Customer.BirthDate.Value.Day == day)
                        : query.Where(r => r.Customer.BirthDate != null && r.Customer.BirthDate.Value.Month == month);
                    break;
                }
                case SegmentAxis.Resend:
                {
                    // Who actually got the earlier message (sent/delivered), minus whoever it was credited with an order.
                    int.TryParse(c.Value, out var sourceSendId);
                    var recipients = db.MarketingDelivery
                        .Where(d => d.SendId == sourceSendId && (d.Status == MarketingStorage.DeliveryStatus.Sent || d.Status == MarketingStorage.DeliveryStatus.Delivered))
                        .Select(d => d.NormalizedPhone);
                    var buyers = db.MarketingAttribution
                        .Where(a => a.SendId == sourceSendId)
                        .Join(db.MarketingDelivery, a => a.DeliveryId, d => d.Id, (a, d) => d.NormalizedPhone);
                    query = query.Where(r => recipients.Contains(r.Customer.NormalizedPhone) && !buyers.Contains(r.Customer.NormalizedPhone));
                    break;
                }
                case SegmentAxis.Risk:
                {
                    // days since last order > multiplier × average gap, where the average gap =
                    // (last - first) / (orders - 1). Needs 3+ orders - below that there is no personal pace.
                    TryDecimal(c.Value, out var mult);
                    var m = (int)Math.Round(mult <= 0 ? 2 : mult);
                    query = query.Where(r => r.OrderCount >= 3
                        && EF.Functions.DateDiffDay(r.LastOrderAt, utcNow) * (r.OrderCount - 1)
                            > m * EF.Functions.DateDiffDay(r.FirstOrderAt, r.LastOrderAt));
                    break;
                }
            }
        }

        return query;
    }

    public static string? NormalizeChannel(string? value)
    {
        var v = (value ?? string.Empty).Trim().ToLowerInvariant();
        return v is SegmentChannel.Web or SegmentChannel.Kiosk or SegmentChannel.Phone ? v : null;
    }

    private static bool TryInt(string? s, out int value) =>
        int.TryParse((s ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool TryDecimal(string? s, out decimal value) =>
        decimal.TryParse((s ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
