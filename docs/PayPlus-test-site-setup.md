# PayPlus test-site setup (phase 1 - backend only)

PayPlus is a second payment gateway alongside Cardcom, built to the same "Giorgio owns capture" model
(see `WooCommerce-giorgio-owns-cardcom-capture.md`): the checkout places an authorization only
(`charge_method=2`, PayPlus's own "Approval/J5" terminology), and Giorgio captures the charge at picking -
same code path as phone orders. A site may have Cardcom **or** PayPlus configured, never both
(`Site.PaymentGatewayProvider` is a single column).

This phase does **not** include a settings UI or the WooCommerce plugin's "giorgio owns capture" mode - it
only makes the backend ready for both. To exercise the new gateway end-to-end today, configure a real site
via the existing `PUT Payment/Site/{id}/Settings` endpoint (the same one the future settings UI will call).

## Configuring a test site

```
PUT /Payment/Site/{siteId}/Settings
Authorization: Bearer <staff JWT>
Content-Type: application/json

{
  "paymentGatewayProvider": "payplus",
  "payPlusPaymentPageUid": "<payment page uid from PayPlus dashboard>",
  "payPlusApiKey": "<api_key>",
  "payPlusSecretKey": "<secret_key>",
  "payPlusTestMode": true
}
```

`payPlusSecretKey` is encrypted at rest via `PaymentTokenProtector` (same as Cardcom's API password) - it is
never stored or logged in plaintext, and the response never echoes it back (only `hasPayPlusSecretKey` /
`payPlusSecretKeyNeedsResave` booleans, mirroring the Cardcom settings response shape).

**Never commit real PayPlus credentials to source control.** Configure them against a real site via this API
call (Postman/curl/a one-off script), not by hardcoding them anywhere in the repo.

`payPlusTestMode: true` selects PayPlus's sandbox base URL (`restapidev.payplus.co.il`) instead of production.
Use PayPlus's own sandbox test card for verification (kept out of this file - see the PayPlus dashboard /
the credentials the account owner already has); keep test transactions to a few ILS each, per PayPlus's own
guidance, to avoid the sandbox account being flagged.

## Verifying the flow

1. `POST Payment/Site/{siteId}/TestConnection` - confirms `api-key`/`secret-key` are accepted.
2. `POST Payment/Order/{orderId}/Session` on an order for that site - confirms a hosted-page authorization
   (not an immediate charge) is created; the response's `paymentUrl` should open PayPlus's hosted page.
3. Complete the test payment on the hosted page, then confirm the webhook (`POST /Webhooks/PayPlus`) and/or
   the return flow independently re-verify the transaction via `Transactions/View` before marking anything
   authorized - this is the same non-negotiable "never trust the echo alone" discipline Cardcom has, applied
   to PayPlus from day one (see `PaymentService.PayPlus.cs` / `GatewayChargeVerification`).
4. `POST Payment/Order/{orderId}/Finalize` (picking-time capture) - confirms `Transactions/ChargeByTransactionUID`
   fires and the order moves to `Captured`.
5. `POST Payment/Order/{orderId}/Refund` - confirms `Transactions/RefundByTransactionUID`.
6. Cancel an order with an open (uncaptured) hold - confirms `Transactions/Cancel` voids it.
7. Confirm a Cardcom-configured site is unaffected - this phase changes zero lines of Cardcom's own
   capture/refund/void logic (PayPlus is implemented as sibling code paths, not a shared rewrite).

## Known gaps in this phase (see PR/commit description for the full list)

- Phone/manual "SavedCard" order reuse (`TryPlaceAuthorizationHoldIfNeededAsync`, saved-card checkout) is
  still Cardcom-only; PayPlus sites hitting those paths get a clear "not configured" style response, not a
  crash or silent wrong behavior.
- The manual "sync from gateway" recovery button (`SyncWooGatewayPaymentFromCardcomAsync`) and the
  admin invoice issue/resend endpoints remain Cardcom-only for now.
- Webhook signature verification is implemented and confirmed against official docs
  (`docs.payplus.co.il/reference/validate-requests-received-from-payplus`): `hash` header =
  `base64(HMAC-SHA256(raw body, secret_key))`, checked in `PaymentService.ProcessPayPlusWebhookAsync` with a
  constant-time comparison before any state change. A mismatch aborts processing (logged); every path also
  still independently re-confirms via `Transactions/View` regardless of signature outcome.
- Invoice+ (`books/docs/*`) field names are taken from `docs.payplus.co.il` but not yet exercised against a
  live sandbox - verify document creation end-to-end before relying on it in production.
- `payplus-payment-gateway` WooCommerce plugin and the `giorgio` plugin's multi-gateway support are a
  separate, later phase.
