# Order line display parity (TS ↔ C#)

The shop-manager voucher and order cards use `src/lib/orderItemLineDisplay.ts`.

The backend auto-voucher (`OrderService.BuildAutoVoucherHtml`) uses the C# port:

- `Core/George.Services/OrderItemLineDisplay.cs`

## Tests

From repo root `george-backend/`:

```bash
dotnet test Core/George.Services.Tests/George.Services.Tests.csproj
```

Golden cases (e.g. kiosk salmon `weight` + `saleTotalWeight`) live in `OrderItemLineDisplayTests.cs`.

## Not ported (by design)

TypeScript helpers that depend on **live product catalog** (`Product`, `getCatalogPricePerKgForPickingDisplay`) are **not** duplicated in C#:

- `resolveOrderItemPickingRatePerKg`, picking-column pricing, etc.

Voucher HTML uses quantity badge, product name, attribute summary, picked display, and legacy unit-weight hint — all covered by the port.

## When you change TS

1. Update `OrderItemLineDisplay.cs` to match.
2. Add or adjust tests in `George.Services.Tests`.
3. Run `dotnet test` as above.
