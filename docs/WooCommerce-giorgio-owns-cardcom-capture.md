# Giorgio owns Cardcom capture (website orders)

**Mode (giorgio option `giorgio_owns_cardcom_capture`):** the store's Cardcom gateway plugin only places the
checkout J5 hold and saves the token; giorgio hands the token to Giorgio; Giorgio charges at picking
(same code path as phone orders) and pushes the payment result back to the store. The gateway's capture
hooks are detached, so nothing on the site ever charges.

Why: every website-charge incident of Aug 2026 (Zano-Dagim 967/1074/1130/1135, Delinka 18326) came from the
money moving on the site and Giorgio only hearing about it afterwards - through a fragile chain
(Giorgio → Woo REST → gateway plugin (7 s timeout) → Cardcom → webhook → Giorgio). In this mode the chain
is only used to *display* the result.

## Rollout per site

1. Deploy Giorgio (`Order_AddPaymentCaptureOwner.sql`, backend, shop-manager) and giorgio.
2. Run a staging checkout, finish picking, confirm: Giorgio `ChargeToken` event + invoice, Woo order note
   "Giorgio: החיוב בקארדקום בוצע", **no** "Capture Charge" note from the Cardcom gateway.
3. Turn on *חיוב קארדקום מתבצע ב־Giorgio* in giorgio → הזמנות.

## Orders that already exist when the option is switched on

The gateway hooks are detached for the whole site, so the plugin would never charge them either.
Switching the option on therefore schedules `run_giorgio_handover_backfill` (WP-Cron, batches of 25,
60 days back): every Cardcom order that still holds a token and was never captured re-sends its
OrderPayment v2 - now with the token - and Giorgio takes ownership (`PaymentCaptureOwner = Giorgio`).
Orders the gateway already charged are skipped; orders Giorgio already settled ignore the handover.

* Still in picking → charged at finish like any Giorgio order.
* Already Ready/Completed and unpaid (e.g. 1074/1130/1135 on 23/08) → the kanban popover / archive show
  **"חייב כרטיס שמור עכשיו"** (`RetryCardcomChargeButton` via `orderCanChargeStoredCardNow`), which runs the
  same POST /Finalize.
* Orders with no token at all (very old / failed checkout) stay plugin-flow with no capture path - use the
  payment link / phone charge from the same popover.

## Contract - store → Giorgio (`payment` block of the order payload and OrderPayment v2)

| field | source (gateway meta, newest row) | Giorgio |
|---|---|---|
| `captureOwner` | `"giorgio"` (only when the option is on) | marks `Order.PaymentCaptureOwner = Giorgio` |
| `token` | `cardcom_token_val` (UUID) | `CardcomPaymentJson` (encrypted) |
| `tokenExpiry` | `cardcom_Tokef` MMYY | card expiry for ChargeToken |
| `approvalNumber` | `cardcom_Approval_Num` | `CardcomApprovalNumber` → void of the J5 before charge |
| `numOfPayments` | `cardcom_NumOfPayments` | `CardcomSelectedInstallments` → installments on the charge |
| `transactionId` | hold deal number | `GatewayPaymentTransactionId` until Giorgio charges |
| `authorizedAmount` | order total at checkout | `PaymentAuthorizedAmount` (void amount) |

In this mode the store never reports a final capture; `isFinished` is always `false` and the
"completed" echo webhook is skipped. Giorgio ignores any "captured" claim on a Giorgio-owned order.

## Contract - Giorgio → store (`payment` block of `oc-storeos/v1/orders`, Giorgio-owned orders only)

```json
"payment": {
  "captureOwner": "giorgio",
  "status": "paid | refunded | partiallyRefunded | failed | authorized | unpaid",
  "transactionId": "259565971", "invoiceNumber": "526", "documentUrl": "https://…",
  "paidAt": "2026-08-20T09:14:17Z", "amount": 627.40,
  "refundedAmount": 0, "refundedAt": null, "refundInvoiceNumber": null, "refundDocumentUrl": null,
  "cardLast4": "1010", "installments": null
}
```

giorgio (`apply_giorgio_payment_result_to_order`): sets transaction id, paid date, `Cardcom Payment ID` /
`CardcomInternalDealNumber` / `initial_document_no` / `cardcom_charge_captured=yes`, one order note per state
change, and mirrors refunds as WooCommerce refund records (`refund_payment: false`). Order status itself
still comes from the payload's `status`. Pushed after capture, after refund, after a hosted-page charge,
and with every status sync; the push retries on 502/503/504.

## Giorgio side

* `Order.PaymentCaptureOwner` (`George.Common.Payment.PaymentCaptureOwner`), exposed on `OrderRes`.
* Intake: `PaymentService.ApplyWooCommerceGatewayPaymentFields` → `ApplyGiorgioCaptureHandover`.
* Charge: `FinalizePickingPaymentAsync` (unchanged) - shop-manager routes Giorgio-owned website orders to
  it (`isGiorgioOwnedCapture` in `orderPaymentDisplay.ts`) instead of polling for a webhook.
* Push: `PaymentService.ScheduleStorePaymentPush` → `WooCommerceService.SyncOrderToOcStoreosAsync` →
  `BuildOcStoreosGiorgioPaymentBlock`.
* Plugin-world features are switched off for these orders: "בדוק תשלום מול Cardcom", charge verification
  against plugin ids, gateway-rejection banner.
