-- Enable/disable SignalR new-order push (shop-manager reads via GET /General/Configuration).
IF NOT EXISTS (SELECT 1 FROM dbo.SystemConfiguration WHERE [Key] = N'OrdersRealtimeEnabled')
BEGIN
    INSERT INTO dbo.SystemConfiguration ([Key], [Value], [Description])
    VALUES (
        N'OrdersRealtimeEnabled',
        N'false',
        N'When true, backend pushes NewOrderCreated via SignalR (/hubs/orders) after order create.'
    );
END
GO
