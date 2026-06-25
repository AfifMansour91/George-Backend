using System.Text.Json;
using George.DB;

namespace George.Services;

/// <summary>Builds IntegrationLog rows for the customer activity timeline (EntityType="customer").
/// The human title/subtitle + acting user are packed into RequestJson (the log table has no message/actor column).</summary>
public static class CustomerActivityLog
{
    public const string EntityTypeCustomer = "customer";
    public const string OpCreated = "customer_created";
    public const string OpOrderPlaced = "order_placed";
    public const string OpCharged = "charged";
    public const string OpNote = "note";

    public sealed class Payload
    {
        public string? Title { get; set; }
        public string? Subtitle { get; set; }
        public int? ActorUserId { get; set; }
    }

    public static IntegrationLog Build(int siteId, int customerId, string operation, string title, string? subtitle, int? actorUserId)
    {
        var json = JsonSerializer.Serialize(new Payload
        {
            Title = title,
            Subtitle = subtitle,
            ActorUserId = actorUserId is > 0 ? actorUserId : null,
        });
        return new IntegrationLog
        {
            SiteId = siteId,
            EntityType = EntityTypeCustomer,
            EntityId = customerId,
            Direction = "internal",
            Operation = operation,
            Level = "info",
            Success = true,
            RequestJson = json,
            CreatedAtUtc = DateTime.UtcNow,
        };
    }

    public static Payload? ParsePayload(string? requestJson)
    {
        if (string.IsNullOrWhiteSpace(requestJson)) return null;
        try { return JsonSerializer.Deserialize<Payload>(requestJson); }
        catch { return null; }
    }
}
