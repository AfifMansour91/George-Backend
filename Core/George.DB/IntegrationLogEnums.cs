namespace George.DB;

/// <summary>Entity kind an <see cref="IntegrationLog"/> row concerns.</summary>
public enum IntegrationLogEntityType
{
    Order,
    Product,
    Category,
    Promotion,
    Customer,
}

/// <summary>Direction of the logged call relative to George.</summary>
public enum IntegrationLogDirection
{
    /// <summary>In-app operation (no external store call).</summary>
    Internal,
    /// <summary>Store → George.</summary>
    Inbound,
    /// <summary>George → store.</summary>
    Outbound,
}

/// <summary>Severity for the logs screen filter.</summary>
public enum IntegrationLogLevel
{
    Info,
    Warning,
    Error,
}

/// <summary>The logged operation. Order-lifecycle actions plus outbound sync endpoints.</summary>
public enum IntegrationLogOperation
{
    Create,
    Update,
    Cancel,
    AddItems,
    RemoveItem,
    Picking,
    Payment,
    Reminder,
    /// <summary>George → store order sync (<c>oc-storeos/v1/orders</c>).</summary>
    OutboundOrderSync,
}

/// <summary>
/// Maps the log enums to their stable persisted string (kept lowercase / snake_case so the
/// <see cref="IntegrationLog"/> columns stay human-readable and query-friendly). Centralizing the
/// wire values here removes the magic strings that were scattered across the logging call sites.
/// </summary>
public static class IntegrationLogWire
{
    public static string ToWire(this IntegrationLogEntityType v) => v switch
    {
        IntegrationLogEntityType.Order => "order",
        IntegrationLogEntityType.Product => "product",
        IntegrationLogEntityType.Category => "category",
        IntegrationLogEntityType.Promotion => "promotion",
        IntegrationLogEntityType.Customer => "customer",
        _ => v.ToString().ToLowerInvariant(),
    };

    public static string ToWire(this IntegrationLogDirection v) => v switch
    {
        IntegrationLogDirection.Internal => "internal",
        IntegrationLogDirection.Inbound => "inbound",
        IntegrationLogDirection.Outbound => "outbound",
        _ => v.ToString().ToLowerInvariant(),
    };

    public static string ToWire(this IntegrationLogLevel v) => v switch
    {
        IntegrationLogLevel.Info => "info",
        IntegrationLogLevel.Warning => "warning",
        IntegrationLogLevel.Error => "error",
        _ => "info",
    };

    public static string ToWire(this IntegrationLogOperation v) => v switch
    {
        IntegrationLogOperation.Create => "create",
        IntegrationLogOperation.Update => "update",
        IntegrationLogOperation.Cancel => "cancel",
        IntegrationLogOperation.AddItems => "add_items",
        IntegrationLogOperation.RemoveItem => "remove_item",
        IntegrationLogOperation.Picking => "picking",
        IntegrationLogOperation.Payment => "payment",
        IntegrationLogOperation.Reminder => "reminder",
        IntegrationLogOperation.OutboundOrderSync => "oc-storeos/orders",
        _ => v.ToString().ToLowerInvariant(),
    };
}
