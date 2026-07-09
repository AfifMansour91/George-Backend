namespace George.Common;

/// <summary>Shared names for SignalR hubs, features, and client event methods (logging + routing).</summary>
public static class RealtimeHubNames
{
    public const string Orders = "Orders";
    public const string Scale = "Scale";
}

public static class RealtimeFeatures
{
    public const string NewOrder = "NewOrder";
    public const string ScaleWeight = "ScaleWeight";
}

public static class RealtimeEventNames
{
    public const string NewOrderCreated = "NewOrderCreated";
    public const string ScaleWeightChanged = "ScaleWeightChanged";
}

public static class RealtimeEntityTypes
{
    public const string Order = "Order";
}
