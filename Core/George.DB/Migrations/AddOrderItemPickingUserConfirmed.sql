-- Migration: distinguish ליקוט confirmed in UI vs catalog baseline (PickedQuantity = Quantity on ingest for piece lines).

IF COL_LENGTH(N'dbo.OrderItem', N'PickingUserConfirmed') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrderItem] ADD [PickingUserConfirmed] bit NOT NULL
        CONSTRAINT [DF_OrderItem_PickingUserConfirmed] DEFAULT (0);
END
GO

-- Rows that are clearly not "baseline only": weight lines, adjusted qty, or adjusted line total.
UPDATE [dbo].[OrderItem]
SET [PickingUserConfirmed] = 1
WHERE [IsDeleted] = 0
  AND [PickedQuantity] IS NOT NULL AND [PickedQuantity] > 0
  AND (
      [OrderLineQuantityMode] = N'weight'
      OR ([UnitWeightGrams] IS NOT NULL AND [UnitWeightGrams] > 0)
      OR [PickedQuantity] <> [Quantity]
      OR (
          [TotalPrice] IS NOT NULL AND [PricePerUnit] IS NOT NULL
          AND ABS([TotalPrice] - [Quantity] * [PricePerUnit]) > 0.05
      )
  );
GO
