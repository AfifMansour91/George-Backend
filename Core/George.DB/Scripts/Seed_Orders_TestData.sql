-- =============================================================================
-- Seed script: Insert test orders and order items for Kanban/testing.
-- Run after Migration_AddOrderAndOrderItemTables.sql.
-- Uses the first Account and first Site in the DB; change variables if needed.
-- =============================================================================

SET NOCOUNT ON;

-- Use first account and first site (edit if you have multiple and want a specific one)
DECLARE @AccountId INT = (SELECT TOP 1 Id FROM [dbo].[Account] ORDER BY Id);
DECLARE @SiteId  INT = (SELECT TOP 1 Id FROM [dbo].[Site] WHERE AccountId = @AccountId ORDER BY Id);

IF @AccountId IS NULL OR @SiteId IS NULL
BEGIN
    RAISERROR('No Account or Site found. Create an account and site first.', 16, 1);
    RETURN;
END

PRINT 'Using AccountId=' + CAST(@AccountId AS VARCHAR(10)) + ', SiteId=' + CAST(@SiteId AS VARCHAR(10));

DECLARE @OrderId INT;
DECLARE @Today DATE = CAST(SYSDATETIME() AS DATE);

-- -----------------------------------------------------------------------------
-- Order 1: New, Website, Shipping, Unpaid (cash) – for "חדשה" column
-- -----------------------------------------------------------------------------
INSERT INTO [dbo].[Order] (
    IsDeleted, CreationTime, AccountId, SiteId, OrderNumber, Source, Status,
    DeliveryType, PaymentStatus, CustomerName, CustomerPhone,
    DeliveryAddress, DeliveryDate, DeliveryTime, ManagerNote, CustomerNote, SubTotal, ShippingCost, Total
)
VALUES (
    0, SYSDATETIME(), @AccountId, @SiteId, '1001', N'Website', N'New',
    N'Shipping', N'Unpaid', N'ישראל ישראלי', N'054-4570027',
    N'רחוב הרצל 123, תל אביב', @Today, N'18:00-20:00', NULL, N'בבקשה להשאיר בדלת', 185.00, 14.00, 199.00
);
SET @OrderId = SCOPE_IDENTITY();

INSERT INTO [dbo].[OrderItem] (OrderId, Title, VariantTitle, Quantity, UnitWeightGrams, PricePerUnit, TotalPrice, Notes, SortOrder)
VALUES
    (@OrderId, N'בשר טחון', N'500g', 1, 500, 45.00, 45.00, NULL, 0),
    (@OrderId, N'פיצה', N'משפחתית', 2, NULL, 60.00, 120.00, NULL, 1),
    (@OrderId, N'שתיה', N'1.5L', 1, NULL, 12.00, 12.00, NULL, 2),
    (@OrderId, N'קטשופ', NULL, 1, NULL, 8.00, 8.00, NULL, 3);

-- -----------------------------------------------------------------------------
-- Order 2: New, Kiosk, Pickup, Paid – for "חדשה" column
-- -----------------------------------------------------------------------------
INSERT INTO [dbo].[Order] (
    IsDeleted, CreationTime, AccountId, SiteId, OrderNumber, Source, Status,
    DeliveryType, PaymentStatus, CustomerName, CustomerPhone,
    PickupDate, PickupTime, DeliveryNote, ManagerNote, CustomerNote, Total
)
VALUES (
    0, SYSDATETIME(), @AccountId, @SiteId, '1002', N'Kiosk', N'New',
    N'Pickup', N'Paid', N'משה כהן', N'050-1234567',
    @Today, N'12:30', N'איסוף בחלון', NULL, N'אני אבוא בערך ב-12:30', 87.50
);
SET @OrderId = SCOPE_IDENTITY();

INSERT INTO [dbo].[OrderItem] (OrderId, Title, VariantTitle, Quantity, UnitWeightGrams, PricePerUnit, TotalPrice, Notes, SortOrder)
VALUES
    (@OrderId, N'המבורגר', N'כפול', 2, NULL, 35.00, 70.00, NULL, 0),
    (@OrderId, N'צ\'יפס', N'בינוני', 1, NULL, 17.50, 17.50, NULL, 1);

-- -----------------------------------------------------------------------------
-- Order 3: New, Phone, Shipping, Unpaid (cash) + manager note – for "חדשה"
-- -----------------------------------------------------------------------------
INSERT INTO [dbo].[Order] (
    IsDeleted, CreationTime, AccountId, SiteId, OrderNumber, Source, Status,
    DeliveryType, PaymentStatus, CustomerName, CustomerPhone,
    DeliveryAddress, DeliveryDate, DeliveryTime, ManagerNote, CustomerNote, Total
)
VALUES (
    0, SYSDATETIME(), @AccountId, @SiteId, '1003', N'Phone', N'New',
    N'Shipping', N'Unpaid', N'רחל לוי', N'052-9876543',
    N'כיכר העצמאות 60, דירה 4', @Today, N'19:00', N'לקוח VIP – להקדים אם אפשר', N'תזמינו לפני 18:00', 312.00
);
SET @OrderId = SCOPE_IDENTITY();

INSERT INTO [dbo].[OrderItem] (OrderId, Title, VariantTitle, Quantity, UnitWeightGrams, PricePerUnit, TotalPrice, Notes, SortOrder)
VALUES
    (@OrderId, N'סטייק', N'מבחר', 0.8, 800, 120.00, 96.00, N'חיתוך עבה', 0),
    (@OrderId, N'סלט', N'בית', 2, NULL, 28.00, 56.00, NULL, 1),
    (@OrderId, N'משקאות', NULL, 4, NULL, 15.00, 60.00, NULL, 2),
    (@OrderId, N'קינוח', N'עוגת שוקולד', 1, NULL, 35.00, 35.00, NULL, 3);

-- -----------------------------------------------------------------------------
-- Order 4: InTreatment, Website, Pickup, Paid – for "בטיפול" column
-- -----------------------------------------------------------------------------
INSERT INTO [dbo].[Order] (
    IsDeleted, CreationTime, AccountId, SiteId, OrderNumber, Source, Status,
    DeliveryType, PaymentStatus, CustomerName, CustomerPhone,
    PickupDate, PickupTime, ManagerNote, Total
)
VALUES (
    0, SYSDATETIME(), @AccountId, @SiteId, '1004', N'Website', N'InTreatment',
    N'Pickup', N'Paid', N'דוד גולדמן', N'053-5551234',
    @Today, N'14:00', N'להכין עד 13:45', 156.00
);
SET @OrderId = SCOPE_IDENTITY();

INSERT INTO [dbo].[OrderItem] (OrderId, Title, VariantTitle, Quantity, UnitWeightGrams, PricePerUnit, TotalPrice, SortOrder)
VALUES
    (@OrderId, N'סושי', N'מגוון 20 חתיכות', 1, NULL, 65.00, 65.00, 0),
    (@OrderId, N'מרק', N'מיסו', 2, NULL, 18.00, 36.00, 1),
    (@OrderId, N'תה', NULL, 2, NULL, 12.50, 25.00, 2),
    (@OrderId, N'עוגיות', NULL, 1, NULL, 30.00, 30.00, 3);

-- -----------------------------------------------------------------------------
-- Order 5: InTreatment, Kiosk, Shipping, Unpaid – for "בטיפול"
-- -----------------------------------------------------------------------------
INSERT INTO [dbo].[Order] (
    IsDeleted, CreationTime, AccountId, SiteId, OrderNumber, Source, Status,
    DeliveryType, PaymentStatus, CustomerName, CustomerPhone,
    DeliveryAddress, DeliveryDate, DeliveryTime, Total
)
VALUES (
    0, SYSDATETIME(), @AccountId, @SiteId, '1005', N'Kiosk', N'InTreatment',
    N'Shipping', N'Unpaid', N'שרה אברהם', N'054-1112233',
    N'דרך בן גוריון 45', @Today, N'17:30', 74.00
);
SET @OrderId = SCOPE_IDENTITY();

INSERT INTO [dbo].[OrderItem] (OrderId, Title, VariantTitle, Quantity, UnitWeightGrams, PricePerUnit, TotalPrice, SortOrder)
VALUES
    (@OrderId, N'פסטה', N'פנה עם רוטב עגבניות', 1, NULL, 42.00, 42.00, 0),
    (@OrderId, N'לחם', NULL, 1, NULL, 12.00, 12.00, 1),
    (@OrderId, N'מים', N'1.5L', 2, NULL, 10.00, 20.00, 2);

-- -----------------------------------------------------------------------------
-- Order 6: Ready, Website, Shipping, Paid – for "מוכן" column
-- -----------------------------------------------------------------------------
INSERT INTO [dbo].[Order] (
    IsDeleted, CreationTime, AccountId, SiteId, OrderNumber, Source, Status,
    DeliveryType, PaymentStatus, CustomerName, CustomerPhone,
    DeliveryAddress, DeliveryDate, DeliveryTime, Total
)
VALUES (
    0, SYSDATETIME(), @AccountId, @SiteId, '1006', N'Website', N'Ready',
    N'Shipping', N'Paid', N'יעקב רוזן', N'050-4445566',
    N'שדרות רוטשילד 88', @Today, N'20:00', 198.00
);
SET @OrderId = SCOPE_IDENTITY();

INSERT INTO [dbo].[OrderItem] (OrderId, Title, VariantTitle, Quantity, UnitWeightGrams, PricePerUnit, TotalPrice, SortOrder)
VALUES
    (@OrderId, N'סלמון', N'ממולא', 1, NULL, 78.00, 78.00, 0),
    (@OrderId, N'אורז', N'יסמין', 2, NULL, 22.00, 44.00, 1),
    (@OrderId, N'יין', N'אדום', 1, NULL, 76.00, 76.00, 2);

-- -----------------------------------------------------------------------------
-- Order 7: Ready, Phone, Pickup, Unpaid (cash) – for "מוכן"
-- -----------------------------------------------------------------------------
INSERT INTO [dbo].[Order] (
    IsDeleted, CreationTime, AccountId, SiteId, OrderNumber, Source, Status,
    DeliveryType, PaymentStatus, CustomerName, CustomerPhone,
    PickupDate, PickupTime, Total
)
VALUES (
    0, SYSDATETIME(), @AccountId, @SiteId, '1007', N'Phone', N'Ready',
    N'Pickup', N'Unpaid', N'מיכל ברק', N'052-7778899',
    @Today, N'13:00', 45.00
);
SET @OrderId = SCOPE_IDENTITY();

INSERT INTO [dbo].[OrderItem] (OrderId, Title, VariantTitle, Quantity, UnitWeightGrams, PricePerUnit, TotalPrice, SortOrder)
VALUES
    (@OrderId, N'קפה', N'אספרסו כפול', 2, NULL, 15.00, 30.00, 0),
    (@OrderId, N'מאפה', N'קרואסון', 1, NULL, 15.00, 15.00, 1);

PRINT 'Done. Inserted 7 test orders with items (2 New, 2 InTreatment, 2 Ready + 1 extra Ready).';
PRINT 'Order numbers: 1001-1007 for SiteId=' + CAST(@SiteId AS VARCHAR(10)) + '.';
