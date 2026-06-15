# WhatsApp Ordering Agent — Architecture Proposal

**Project:** ShopManager / George Backend
**Feature:** Per-store WhatsApp AI ordering agent
**Date:** 2026-06-15
**Status:** Proposal (for review)

---

## תקציר מנהלים (Hebrew Executive Summary)

הלקוח מעוניין שלכל מנהל חנות יהיה **AGENT (סוכן חכם) בווטסאף העסקי שלו**, כך שלקוחות הקצה יוכלו לבצע הזמנות ישירות בשיחת ווטסאף.

**האם זה אפשרי? כן, בהחלט.** זו יכולת בשלה ונפוצה, ומתממשת דרך ה־WhatsApp Business Platform (Cloud API) של Meta. הארכיטקטורה המוצעת משתלבת באופן טבעי עם ShopManager הקיים: ההזמנה שנוצרת בווטסאף עוברת דרך אותו `OrderService.CreateOrderAsync` הקיים, ולכן מקבלת אוטומטית את כל מה שכבר בנוי — התראות SignalR, הדפסת שובר ב־PrintAgent, וזרימת הסטטוסים.

**שלוש החלטות מפתח שגובשו:**

1. **חיבור המספר** — מומלץ דרך ספק מורשה (BSP) עם תהליך הרשמה מובנה (Embedded Signup). כל בעל חנות מחבר את המספר שלו בעצמו תוך דקות. **אילוץ חשוב:** מספר שמחובר ל־API לא יכול לשמש במקביל באפליקציית ווטסאף הרגילה — נדרשת החלטה לכל חנות (מספר ייעודי להזמנות, או מעבר מלא).
2. **חוכמת הסוכן** — גישה **היברידית**: מודל שפה (LLM) להבנת טקסט חופשי ("שתי פיצות וקולה") יחד עם כפתורים ורשימות מובנים לאישור ותשלום. זה מאזן בין חוויה טבעית לאמינות.
3. **רב־לקוחיות (multi-tenant)** — כל הגדרות הסוכן (פרסונה, שפה, תפריט, שעות פעילות, אמצעי תשלום) נשמרות **לכל חנות בנפרד** וניתנות להתאמה ללא שינוי קוד.

**מסקנה:** הפיצ'ר ישים, מודרני, ומתבסס ברובו על תשתית קיימת. ההערכה: MVP לחנות בודדת תוך כ־4–6 שבועות פיתוח, ומוצר רב־לקוחי מלא תוך כ־3 חודשים. עלות תפעולית עיקרית: תמחור הודעות של Meta + עלות ה־LLM (אגורות בודדות לשיחה).

---

## 1. Feasibility & Goal

**Goal:** Give every store manager an AI agent living on their business WhatsApp number, so the merchant's end-customers can place orders conversationally.

**Verdict: Feasible and well-supported.** This is built on Meta's **WhatsApp Business Platform (Cloud API)** — the same infrastructure behind most commercial WhatsApp commerce. The key insight for ShopManager: an order created by the agent is just another order. By routing it through the existing `OrderService.CreateOrderAsync`, the agent inherits the entire downstream pipeline (realtime notifications, voucher printing, status lifecycle) for free.

This document recommends a connection model, an agent design, and a multi-tenant architecture that is efficient, modern, and configurable per client.

---

## 2. Critical Constraint (read this first)

WhatsApp's official API has one rule that dictates the whole onboarding UX:

> **A phone number connected to the Cloud API cannot also be used in the WhatsApp / WhatsApp Business mobile app at the same time.** Migrating a number to the API removes it from the app.

Implications for each store owner — they choose one of:

- **A dedicated ordering number** (recommended default): the owner keeps their personal/manual WhatsApp on their existing number, and uses a *second* number purely for the agent. Cleanest separation.
- **Full migration**: the owner moves their existing business number to the API. They then handle all manual conversations through ShopManager's built-in **team inbox** (see §6.7 Human Handoff) instead of the WhatsApp app.

This is a product decision per merchant, surfaced during onboarding. The architecture supports both — only the number provisioning step differs.

---

## 3. Recommended Connection Model

**Recommendation: Official Cloud API via a BSP (Business Solution Provider) with Embedded Signup.**

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| **Direct Cloud API (self-managed)** | No middleman markup; full control | You build/maintain Embedded Signup, billing, WABA management, support | Viable long-term once volume is high |
| **BSP — 360dialog** | Flat monthly price per number, **no per-message markup**, ISV/partner model built for multi-tenant resellers, hosted or on-prem API | Less "all-in-one" than Twilio | **Recommended** for a multi-store reseller model |
| **BSP — Twilio** | Already a project dependency; excellent SDK & docs | Per-message markup on top of Meta fees; cost scales with volume | Good for fast pilot |
| **Shared platform number** | Simplest; one WABA | No per-store branding; not what the client asked for | Rejected |

**Why a BSP first:** it removes the hardest parts (Meta Business verification orchestration, Embedded Signup hosting, per-number billing) so you ship faster, while still giving each store its **own branded number**. Start on a BSP for the pilot; the provider is abstracted behind a `IWhatsAppGateway` interface (§6.6) so you can move to direct Cloud API later without touching agent logic.

**Cost shape (for client expectation-setting):** Meta charges per 24-hour *conversation* (cheaper for user-initiated/service, more for business-initiated/marketing; free tier for service conversations exists but terms change — verify current pricing at onboarding). LLM cost is a few cents per completed order at most. Plus the BSP's flat per-number fee.

---

## 4. Agent Design — Hybrid

The agent combines free-text understanding with structured, deterministic UI:

- **LLM layer (NLU + dialog):** interprets natural messages ("תוסיף עוד קולה", "כמה זה יוצא?"), maps them to catalog items, fills the cart, answers questions. Implemented with **tool/function-calling** so the model never invents prices or orders — it can only act through validated tools.
- **Structured WhatsApp UI:** for anything that must be exact — category navigation, item selection, quantity, the final order confirmation, delivery/pickup choice, payment — the agent sends **interactive list & button messages**. This eliminates ambiguity at the steps that matter (money, address) and reduces token cost.
- **Deterministic state machine:** a per-conversation FSM (`Greeting → Browsing → Cart → Checkout → Payment → Confirmed`) governs allowed transitions. The LLM proposes; the state machine disposes. This prevents hallucinated orders and keeps the flow auditable.

The LLM is given **tools**, not free rein:

| Tool | Backs onto (existing service) | Returns |
|---|---|---|
| `search_catalog(siteId, query)` | Product/Catalog query | matching products + prices |
| `get_cart(conversationId)` | session store | current cart |
| `add_to_cart / update_cart / remove` | session store | updated cart |
| `lookup_customer(siteId, phone)` | `GetCustomerProfileByPhoneAsync` | known customer, last order |
| `get_last_order(siteId, phone)` | `GetLastOrderItemsAsync` | quick reorder |
| `create_order(...)` | **`OrderService.CreateOrderAsync`** (`Source="WhatsApp"`) | order number |
| `get_order_status(orderId)` | `GetOrdersAsync` | live status |

Because `create_order` is the *only* path to writing data and it reuses the existing validated service, the agent cannot bypass business rules, pricing, or promotions.

---

## 5. High-Level Architecture

```
                          Meta WhatsApp Cloud API (per-store WABA number)
                                   ▲                       │
                          outbound │ (templates /          │ inbound webhook
                           session messages)               ▼
                          ┌────────┴───────────────────────────────────┐
                          │   George.WhatsApp.Gateway  (ASP.NET 8)      │
                          │   • webhook endpoint + signature verify     │
                          │   • phone_number_id → SiteId routing        │
                          │   • IWhatsAppGateway (BSP-abstracted send)  │
                          └───────────────┬─────────────────────────────┘
                                          │ publish InboundMessage
                                          ▼
                               ┌──────────────────────┐      ┌───────────────┐
                               │  Message Queue        │      │  Redis        │
                               │  (Azure Service Bus / │      │  session +    │
                               │   RabbitMQ, MassTransit)│    │  cart state   │
                               └──────────┬────────────┘      └──────┬────────┘
                                          │ consume                  │
                                          ▼                          │
                          ┌───────────────────────────────────────┐ │
                          │  Conversation Worker  (.NET hosted svc)│◄┘
                          │  • FSM + Anthropic Claude (tool-calling)│
                          │  • per-Site AgentConfig (prompt/persona)│
                          │  • tool dispatch → internal API facade  │
                          └───────────────┬─────────────────────────┘
                                          │ tools call existing services
                                          ▼
              ┌────────────────────────────────────────────────────────────┐
              │   EXISTING George.Services  (unchanged core)                │
              │   OrderService.CreateOrderAsync ─┬─► SignalR NewOrderCreated │
              │   Catalog / Customer / Promotions │   (staff UI alert)       │
              │                                   └─► PrintJob ─► PrintAgent  │
              └────────────────────────────────────────────────────────────┘
```

**Design principles applied:**

- **Reuse over rebuild.** Order creation, printing, notifications, promotions, customer CRM already exist and are multi-tenant via `SiteId`. The agent is a new *channel*, not a new order system.
- **Decouple ingestion from thinking.** The webhook must answer Meta in milliseconds. It only validates, routes, and enqueues. The LLM/FSM work happens in a separate worker consuming the queue — so a slow model call or a traffic spike never drops a webhook or blocks Meta's retries.
- **Stateless gateway, externalized session.** Conversation/cart state lives in Redis keyed by `(siteId, customerPhone)`, so the gateway and worker scale horizontally.
- **Provider abstraction.** `IWhatsAppGateway` hides the BSP. Swapping 360dialog ↔ Twilio ↔ direct Cloud API is a config change.

---

## 6. Components in Detail

### 6.1 Gateway (`George.WhatsApp.Gateway`)
New ASP.NET 8 service (or a module in `George.Api` for the pilot, split out later).
- `GET /whatsapp/webhook` — Meta verification challenge.
- `POST /whatsapp/webhook` — receives messages/status callbacks. **Verifies the X-Hub-Signature-256 HMAC** before processing.
- Resolves `phone_number_id` → `SiteId` via the new `SiteWhatsAppChannel` table, attaches tenant context, and publishes an `InboundWhatsAppMessage` to the queue. Returns `200 OK` immediately.

### 6.2 Message Queue
**Azure Service Bus** (if hosting on Azure) or **RabbitMQ**, via **MassTransit**. Gives retries, dead-lettering, and back-pressure. De-duplicates Meta's at-least-once webhook redeliveries (idempotency key = WhatsApp message id).

### 6.3 Conversation Worker
.NET hosted worker (`IHostedService`) or separate deployable.
- Loads per-`Site` `AgentConfig`, loads/creates Redis session.
- Runs the **FSM**; within a state, calls **Anthropic Claude** with the allowed tools.
- Dispatches tool calls to the internal facade over `George.Services`.
- Composes the reply (text + interactive components) and sends via `IWhatsAppGateway`.

### 6.4 Session & Cart Store
**Redis**, key `wa:session:{siteId}:{phoneE164}`, TTL aligned to WhatsApp's 24h service window. Holds FSM state, cart, language, and recent message context for the LLM.

### 6.5 Order Creation
The `create_order` tool maps the cart to `CreateOrderReq` with `Source="WhatsApp"` and calls the **existing** `OrderService.CreateOrderAsync`. No change to the order domain. Downstream SignalR + PrintAgent fire automatically. (Add `"WhatsApp"` to the recognized `Source` values and to the staff UI filter.)

### 6.6 Outbound Provider (`IWhatsAppGateway`)
New interface in `George.Providers/WhatsApp/`, mirroring the existing `SmsProvider` pattern:
```csharp
public interface IWhatsAppGateway
{
    Task<bool> SendTextAsync(int siteId, string toPhone, string text, CancellationToken ct = default);
    Task<bool> SendInteractiveAsync(int siteId, string toPhone, InteractiveMessage msg, CancellationToken ct = default);
    Task<bool> SendTemplateAsync(int siteId, string toPhone, string templateName, object[] args, CancellationToken ct = default);
}
```
Per-`Site` credentials are loaded from `SiteWhatsAppChannel`. Concrete impls: `ThreeSixtyDialogGateway`, `TwilioWhatsAppGateway`. **Template messages** (pre-approved by Meta) are required to *initiate* contact outside the 24h window; free-form/interactive messages are allowed *inside* it.

### 6.7 Human Handoff (team inbox)
When the customer asks for a human, or the FSM hits low confidence, the worker flips the conversation to `HumanRequested`, pauses the bot, and raises a SignalR event to the store's staff UI. Add a lightweight **team-inbox view** in the `shop-manager` React app so staff can reply through ShopManager (essential if a store fully migrated its number off the app — see §2).

### 6.8 Payments
For the pilot: **cash / pay-on-pickup** and a **hosted payment link** generated from the existing Payment integration, sent in chat. Avoid handling card data in the conversation.

---

## 7. Multi-Tenancy & Per-Client Adaptability

Everything tenant-specific is **data, not code.** Two new tables:

**`SiteWhatsAppChannel`** (1 per store)
- `SiteId`, `WabaId`, `PhoneNumberId`, `DisplayPhone`
- `AccessTokenEncrypted` (envelope-encrypted; never plaintext), `Provider` (`360dialog`/`twilio`/`cloud`)
- `Status` (`PendingSignup`/`Connected`/`Suspended`), `WebhookVerifyToken`

**`SiteAgentConfig`** (1 per store) — the "personality & rules" knob:
- `Persona` / `SystemPromptOverride`, `DefaultLanguage` (he/en/ar/ru), `Tone`
- `Greeting`, `BusinessHours`, `OutOfHoursMessage`
- `MenuMode` (catalog source / categories to expose), `PaymentMethods`, `DeliveryTypes`
- `HandoffKeywords`, `MaxItemsPerOrder`, feature flags

A new store goes live by completing Embedded Signup (fills `SiteWhatsAppChannel`) and accepting sensible `SiteAgentConfig` defaults — **no deployment, no code change.** Per-client customization = editing config rows, exposed later as an admin screen in `shop-manager`.

---

## 8. Technology Choices (modern & efficient)

| Concern | Choice | Why |
|---|---|---|
| Runtime | **.NET 8** | Matches the existing team & codebase |
| Webhook API | ASP.NET 8 Minimal API | Lightweight, fast cold path |
| Messaging | **MassTransit** + Azure Service Bus / RabbitMQ | Resilient, idempotent, scalable |
| Session state | **Redis** | Fast, TTL-native, horizontally scalable |
| LLM | **Anthropic Claude** (tool-calling) | Strong Hebrew, reliable structured tool use |
| WhatsApp | **BSP (360dialog) → Cloud API** behind `IWhatsAppGateway` | Fast onboarding now, portable later |
| Secrets | Azure Key Vault / envelope encryption | Per-tenant tokens must never sit in plaintext |
| Observability | OpenTelemetry + existing NLog | Trace a message end-to-end across services |

---

## 9. Phased Rollout

**Phase 0 — Spike (1 wk):** One sandbox WABA number; webhook → echo bot; confirm signature verification and `phone_number_id → SiteId` routing.

**Phase 1 — Single-store MVP (4–6 wks):** Hybrid agent for one store. Catalog browse, cart, `create_order` into the real pipeline, order confirmation, basic handoff. Cash/pickup only. Hard-coded single config.

**Phase 2 — Multi-tenant (3–4 wks):** `SiteWhatsAppChannel` + `SiteAgentConfig`, Embedded Signup onboarding, Redis sessions, queue/worker split, payment links.

**Phase 3 — Polish & scale:** Admin config UI in `shop-manager`, team inbox, analytics, template-message library, A/B of prompts, multi-language, load testing.

---

## 10. Risks & Mitigations

- **Number/app conflict (§2):** surface clearly in onboarding; default to a dedicated ordering number.
- **Meta verification delays:** start business verification early; BSP smooths this.
- **LLM hallucinating prices/orders:** mitigated by tool-only writes + FSM + structured confirmation. Never trust free text for money/quantities.
- **24h window & template approval:** design conversations to stay inside the window; pre-approve the few templates needed (order confirmation, ready-for-pickup).
- **Cost runaway:** cap context length, prefer interactive buttons over free-text round-trips, cache catalog per site.
- **Privacy/PII:** customer phone & address flow through; encrypt tokens, scope all queries by `SiteId`, honor existing `MarketingApproval` flags.

---

## 11. Net Code Impact

| Change | Type |
|---|---|
| `George.WhatsApp.Gateway` (webhook + routing) | **New** |
| `Conversation Worker` (FSM + LLM + tools) | **New** |
| `George.Providers/WhatsApp/` (`IWhatsAppGateway` + impls) | **New** |
| `SiteWhatsAppChannel`, `SiteAgentConfig` entities + migrations | **New** |
| Redis + queue infrastructure | **New (infra)** |
| `shop-manager`: team inbox + agent config UI | **New (frontend)** |
| Add `"WhatsApp"` to `Source` enum + staff filters | **Minor edit** |
| `OrderService.CreateOrderAsync`, SignalR, PrintAgent | **Unchanged ✅** |

The core order domain is untouched — the agent is purely an additive channel.

---

*Prepared as a technical proposal for review. Pricing, Meta policy details, and BSP terms should be re-verified at implementation time, as they change frequently.*
