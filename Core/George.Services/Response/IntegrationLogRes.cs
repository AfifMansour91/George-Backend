using System;

namespace George.Services.Response;

/// <summary>
/// One integration-log row for the admin "sync logs" screen - a single external sync call between
/// George and a store (WooCommerce), or an in-app order-lifecycle event. Mirrors
/// <see cref="George.DB.IntegrationLog"/> with the full request/response bodies so support can see
/// exactly what was sent.
/// </summary>
public class IntegrationLogRes
{
    public int Id { get; set; }
    public int SiteId { get; set; }

    /// <summary><c>order</c> | <c>product</c> | <c>category</c> | <c>promotion</c> | <c>customer</c>.</summary>
    public string EntityType { get; set; } = "";

    /// <summary>George id of the entity (orderId / productId / …) when applicable.</summary>
    public int? EntityId { get; set; }

    /// <summary>Store-side id (e.g. WooCommerce order/product id) for cross-referencing.</summary>
    public string? ExternalId { get; set; }

    /// <summary><c>internal</c> | <c>inbound</c> | <c>outbound</c>.</summary>
    public string Direction { get; set; } = "";

    /// <summary>Logical operation, e.g. <c>create</c>, <c>update</c>, <c>oc-storeos/orders</c>.</summary>
    public string Operation { get; set; } = "";

    /// <summary>Severity for filtering: <c>info</c> | <c>warning</c> | <c>error</c>.</summary>
    public string Level { get; set; } = "info";

    public string? Url { get; set; }
    public int? HttpStatus { get; set; }
    public bool Success { get; set; }
    public string? RequestJson { get; set; }
    public string? ResponseBody { get; set; }
    public int? DurationMs { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
