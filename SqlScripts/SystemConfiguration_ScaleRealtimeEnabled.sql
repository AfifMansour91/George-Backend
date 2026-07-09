-- Enable/disable SignalR live-scale-weight relay (shop-manager reads via GET /General/Configuration).
IF NOT EXISTS (SELECT 1 FROM dbo.SystemConfiguration WHERE [Key] = N'ScaleRealtimeEnabled')
BEGIN
    INSERT INTO dbo.SystemConfiguration ([Key], [Value], [Description])
    VALUES (
        N'ScaleRealtimeEnabled',
        N'false',
        N'When true, backend relays live scale weight via SignalR (/hubs/scale). Branch ScaleAgent POSTs to /Scale/Reading.'
    );
END
GO
