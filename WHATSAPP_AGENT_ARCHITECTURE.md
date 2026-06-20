# WhatsApp Ordering Agent ΓÇö Architecture Proposal

**Project:** ShopManager / George Backend
**Feature:** A multi-tenant conversational-ordering platform ΓÇö per-account agents, subscription billing, multiple channels (WhatsApp + Web)
**Date:** 2026-06-15 (revised)
**Status:** Proposal (for review)

---

## ╫¬╫º╫ª╫Ö╫¿ ╫₧╫á╫ö╫£╫Ö╫¥ (Hebrew Executive Summary)

╫ö╫£╫º╫ò╫ù ╫₧╫ó╫ò╫á╫Ö╫Ö╫ƒ ╫⌐╫£╫¢╫£ ╫₧╫á╫ö╫£ ╫ù╫á╫ò╫¬ ╫Ö╫ö╫Ö╫ö **AGENT (╫í╫ò╫¢╫ƒ ╫ù╫¢╫¥) ╫æ╫ò╫ò╫ÿ╫í╫É╫ñ ╫ö╫ó╫í╫º╫Ö ╫⌐╫£╫ò**, ╫¢╫Ü ╫⌐╫£╫º╫ò╫ù╫ò╫¬ ╫ö╫º╫ª╫ö ╫Ö╫ò╫¢╫£╫ò ╫£╫æ╫ª╫ó ╫ö╫û╫₧╫á╫ò╫¬ ╫Ö╫⌐╫Ö╫¿╫ò╫¬ ╫æ╫⌐╫Ö╫ù╫¬ ╫ò╫ò╫ÿ╫í╫É╫ñ ΓÇö ╫ò╫æ╫ö╫₧╫⌐╫Ü ╫£╫₧╫¢╫ò╫¿ ╫û╫É╫¬ ╫¢╫₧╫ò╫ª╫¿ ╫æ╫¬╫⌐╫£╫ò╫¥, ╫¿╫æ-╫ó╫¿╫ò╫ª╫Ö ╫ò╫¿╫æ-╫£╫º╫ò╫ù╫Ö.

**╫ö╫É╫¥ ╫û╫ö ╫É╫ñ╫⌐╫¿╫Ö? ╫¢╫ƒ, ╫æ╫ö╫ù╫£╫ÿ.** ╫ö╫ö╫û╫₧╫á╫ö ╫⌐╫á╫ò╫ª╫¿╫¬ ╫æ╫ò╫ò╫ÿ╫í╫É╫ñ ╫ó╫ò╫æ╫¿╫¬ ╫ô╫¿╫Ü ╫É╫ò╫¬╫ò `OrderService.CreateOrderAsync` ╫ö╫º╫Ö╫Ö╫¥, ╫ò╫£╫¢╫ƒ ╫₧╫º╫æ╫£╫¬ ╫É╫ò╫ÿ╫ò╫₧╫ÿ╫Ö╫¬ ╫ö╫¬╫¿╫É╫ò╫¬ SignalR, ╫ö╫ô╫ñ╫í╫¬ ╫⌐╫ò╫æ╫¿ ╫æ-PrintAgent, ╫ò╫û╫¿╫Ö╫₧╫¬ ╫ö╫í╫ÿ╫ÿ╫ò╫í╫Ö╫¥.

**╫ö╫ù╫£╫ÿ╫ò╫¬ ╫₧╫ñ╫¬╫ù:**

- **╫ù╫Ö╫æ╫ò╫¿ ╫ö╫₧╫í╫ñ╫¿** ΓÇö ╫ô╫¿╫Ü ╫í╫ñ╫º ╫₧╫ò╫¿╫⌐╫ö (BSP). ╫É╫Ö╫£╫ò╫Ñ: ╫₧╫í╫ñ╫¿ ╫ö╫₧╫ù╫ò╫æ╫¿ ╫£-API ╫£╫É ╫Ö╫¢╫ò╫£ ╫£╫⌐╫₧╫⌐ ╫æ╫₧╫º╫æ╫Ö╫£ ╫æ╫É╫ñ╫£╫Ö╫º╫ª╫Ö╫Ö╫¬ ╫ò╫ò╫ÿ╫í╫É╫ñ ╫ö╫¿╫Æ╫Ö╫£╫ö.
- **╫í╫ò╫¢╫ƒ ╫ö╫Ö╫æ╫¿╫Ö╫ô╫Ö** ΓÇö ╫₧╫ò╫ô╫£ ╫⌐╫ñ╫ö (LLM) ╫£╫ö╫æ╫á╫¬ ╫ÿ╫º╫í╫ÿ ╫ù╫ò╫ñ╫⌐╫Ö + ╫¢╫ñ╫¬╫ò╫¿╫Ö╫¥ ╫₧╫ò╫æ╫á╫Ö╫¥ ╫£╫É╫Ö╫⌐╫ò╫¿ ╫ò╫¬╫⌐╫£╫ò╫¥.
- **╫¿╫æ-╫ó╫¿╫ò╫ª╫Ö** ΓÇö ╫É╫ò╫¬╫ò ╫í╫ò╫¢╫ƒ ╫Ö╫¿╫ò╫Ñ ╫Æ╫¥ ╫æ╫É╫¬╫¿ (╫ª'╫É╫ÿ) ╫ò╫Æ╫¥ ╫æ╫ò╫ò╫ÿ╫í╫É╫ñ; ╫ö╫₧╫ò╫ù ╫û╫ö╫ö, ╫¿╫º ╫ö╫ó╫¿╫ò╫Ñ ╫₧╫¬╫ù╫£╫ú.
- **╫₧╫ò╫ô╫£ ╫₧╫á╫ò╫Ö ╫ù╫ò╫ô╫⌐╫Ö** ΓÇö ╫¢╫£ ╫ù╫⌐╫æ╫ò╫ƒ (Account) ╫₧╫º╫æ╫£ ╫í╫ò╫¢╫ƒ ╫₧╫⌐╫£╫ò ╫ò╫₧╫⌐╫£╫¥ ╫ù╫ò╫ô╫⌐╫Ö╫¬. ╫ö╫¬╫⌐╫¬╫Ö╫¬ ╫¢╫æ╫¿ ╫¿╫æ-╫£╫º╫ò╫ù╫Ö╫¬.
- **╫ó╫¬╫Ö╫ô╫Ö (╫⌐╫Ö╫₧╫ò╫⌐ ╫ù╫Ö╫ª╫ò╫á╫Ö)** ΓÇö ╫æ╫É╫₧╫ª╫ó╫ò╫¬ ╫Æ╫æ╫ò╫£ ╫₧╫₧╫⌐╫º (`IOrderSink`) ╫ö╫í╫ò╫¢╫ƒ ╫ö╫ò╫ñ╫Ü ╫£╫ñ╫£╫ÿ╫ñ╫ò╫¿╫₧╫ö ╫ó╫ª╫₧╫É╫Ö╫¬ ╫⌐╫á╫Ö╫¬╫ƒ ╫£╫₧╫¢╫ò╫¿ ╫Æ╫¥ ╫£╫£╫º╫ò╫ù╫ò╫¬ ╫ù╫Ö╫ª╫ò╫á╫Ö╫Ö╫¥.

**╫₧╫í╫º╫á╫ö:** ╫ö╫ñ╫Ö╫ª'╫¿ ╫Ö╫⌐╫Ö╫¥ ╫ò╫₧╫¬╫æ╫í╫í ╫æ╫¿╫ò╫æ╫ò ╫ó╫£ ╫¬╫⌐╫¬╫Ö╫¬ ╫º╫Ö╫Ö╫₧╫¬. ╫æ╫á╫Ö╫Ö╫ö ╫á╫¢╫ò╫á╫ö ╫₧╫ö╫Ö╫ò╫¥ ╫ö╫ò╫ñ╫¢╫¬ ╫É╫ò╫¬╫ò ╫₧╫ñ╫Ö╫ª'╫¿ ╫£╫₧╫ò╫ª╫¿ SaaS ╫¿╫æ-╫£╫º╫ò╫ù╫Ö ╫ö╫á╫₧╫¢╫¿ ╫æ╫₧╫á╫ò╫Ö.

---

## 1. Feasibility & Goal

**Goal:** Give every store manager an AI agent on their business WhatsApp so end-customers can order conversationally ΓÇö and grow it into a paid, multi-channel, multi-tenant product.

**Verdict: Feasible and well-supported.** Built on Meta's WhatsApp Business Platform (Cloud API). The key insight: an order created by the agent is just another order. Routing it through the existing `OrderService.CreateOrderAsync` inherits the whole downstream pipeline (realtime notifications, voucher printing, status lifecycle) for free.

## 2. Critical Constraint (read this first)

> **A phone number connected to the Cloud API cannot also be used in the WhatsApp / WhatsApp Business mobile app at the same time.** Migrating a number to the API removes it from the app.

Each store owner chooses: a **dedicated ordering number** (recommended default), or **full migration** (handle manual chats via ShopManager's team inbox, ┬º6.7). The **web channel (┬º7) has no such constraint.**

## 3. Recommended Connection Model

**Recommendation: Official Cloud API via a BSP with Embedded Signup.**

| Option | Pros | Cons | Verdict |
|---|---|---|---|
| Direct Cloud API | No markup; full control | You build/maintain signup, billing, WABA mgmt | Long-term, high volume |
| **BSP ΓÇö 360dialog** | Flat per-number, no per-message markup, reseller-friendly | Less all-in-one than Twilio | **Recommended** |
| BSP ΓÇö Twilio | Already a dependency; great SDK | Per-message markup | Good for fast pilot |
| Shared platform number | Simplest | No per-store branding | Rejected |

A BSP acting as a **Tech Provider/reseller** is what lets each external business (┬º10) onboard its own verified number. The provider sits behind `IWhatsAppGateway`, so moving to direct Cloud API later is a config change.

## 4. Agent Design ΓÇö Hybrid

- **LLM layer (NLU):** interprets free text, maps to catalog items via tool/function-calling ΓÇö never invents prices or orders.
- **Structured UI:** interactive buttons/lists (WhatsApp) or web components for the exact steps ΓÇö selection, quantity, confirmation, payment.
- **Deterministic FSM:** `Greeting ΓåÆ Browsing ΓåÆ Cart ΓåÆ Checkout ΓåÆ Payment ΓåÆ Confirmed`. The LLM proposes; the state machine disposes.

Tools (the only way the agent can act): `search_catalog` ΓåÆ `ICatalogSource`; cart ops ΓåÆ Redis; `lookup_customer` ΓåÆ `GetCustomerProfileByPhoneAsync`; `create_order` / `get_order_status` ΓåÆ **`IOrderSink`** (George: `OrderService.CreateOrderAsync` / `GetOrdersAsync`). Writes go through interfaces (┬º10), not directly to George.

## 5. High-Level Architecture

```
CHANNELS:  WhatsApp Gateway (Cloud API/BSP)      Web Chat Adapter (SignalR widget)
        Γöé  normalized InboundMessage (+ channelType, tenantId)
        Γû╝
Entitlement check (subscription active?)  ΓåÆ  Message Queue + Redis (session/cart)
        Γöé consume
        Γû╝
AGENT CORE (.NET):  FSM + Claude (tool-calling) + per-tenant AgentConfig   [channel-agnostic]
        Γöé tools call interfaces, not concrete services
        Γû╝
IOrderSink / ICatalogSource  ΓåÆ  George.Services impl (ShopManager)  |  External impl (future)
        Γû╝
George: OrderService.CreateOrderAsync ΓåÆ SignalR + PrintJob ΓåÆ PrintAgent   (unchanged)
```

**Principles:** reuse over rebuild; decouple ingestion from thinking; channel-agnostic core (┬º7); backend-agnostic writes (┬º10).

## 6. Components in Detail

**6.1 Channel Adapters** ΓÇö WhatsApp Gateway (webhook + `X-Hub-Signature-256` verify + `phone_number_id`ΓåÆtenant) and a Web Chat Adapter (SignalR + embeddable widget). Both normalize inbound to a common shape with `channelType` and `tenantId`.

**6.2 Message Queue** ΓÇö Azure Service Bus / RabbitMQ via MassTransit: retries, dead-lettering, de-dup.

**6.3 Agent Core (Conversation Worker)** ΓÇö loads per-tenant `AgentConfig` + Redis session, runs the FSM, calls Claude, dispatches tools through `IOrderSink`/`ICatalogSource`.

**6.4 Session & Cart Store** ΓÇö Redis, key `conv:{tenantId}:{channel}:{userKey}`, TTL per channel window.

**6.5 Order Creation** ΓÇö `create_order` ΓåÆ `IOrderSink`; ShopManager impl calls existing `OrderService.CreateOrderAsync` with `Source="WhatsApp"` or `"Website"`.

**6.6 Outbound Providers** ΓÇö `IWhatsAppGateway` (SendText/Interactive/Template); web adapter pushes over SignalR.

**6.7 Human Handoff (team inbox)** ΓÇö worker pauses the bot and raises a SignalR event; a team-inbox view in shop-manager lets staff take over (both channels).

**6.8 Entitlement & Metering** ΓÇö gate that checks subscription before the agent runs; counters per account (┬º9).

## 7. Multi-Channel: Web + WhatsApp

The agent brain is **channel-agnostic**. WhatsApp is one adapter; the website is another. Worker, FSM, Redis sessions and tools are identical ΓÇö only the edges differ.

| Aspect | WhatsApp | Web chat |
|---|---|---|
| Transport | Cloud API webhook + outbound API | SignalR (already in stack) + widget |
| Order Source | `Source="WhatsApp"` | `Source="Website"` (already recognized) |
| Messaging rules | 24h window + approved templates | None ΓÇö free-form anytime |
| Structured choices | WhatsApp list/button messages | Native web UI components |
| Identity | Phone number (E.164) | Web session / JWT (can be logged-in) |
| Onboarding & cost | Meta verification + per-conversation fee | None ΓÇö cheapest to run |

The web channel is the **easiest and cheapest first pilot** (no Meta onboarding, no per-message cost, no number/app conflict). A logged-in web user lets the agent pre-fill saved details and last orders.

## 8. Multi-Tenancy & Per-Client Adaptability

Everything tenant-specific is **data, not code.**

- **`SiteWhatsAppChannel`** (per store): SiteId, WabaId, PhoneNumberId, DisplayPhone, `AccessTokenEncrypted`, Provider, Status, WebhookVerifyToken.
- **`AgentConfig`** (per tenant): Persona/SystemPromptOverride, DefaultLanguage, Tone, Greeting, BusinessHours, MenuMode, PaymentMethods, DeliveryTypes, EnabledChannels, HandoffKeywords, feature flags.

A new tenant goes live by accepting sensible defaults ΓÇö **no deployment, no code change.**

## 9. Productization & Billing

Selling the agent as a **paid monthly add-on, one agent per Account**, is a commercial layer on top of the existing multi-tenancy ΓÇö it does not touch agent logic.

New data:

| Table | Purpose / key fields |
|---|---|
| `AccountSubscription` | AccountId, Plan, Status (active/trial/past_due/cancelled), StartDate, RenewalDate, ExternalBillingId |
| `ConversationUsage` | AccountId, Period, ChannelType, ConversationCount, LlmTokens, OverageUnits |

- **Entitlement gate** ΓÇö check subscription is active before processing a message; inactive accounts get a polite fallback.
- **Usage metering** ΓÇö meter WhatsApp conversations + LLM calls per account ΓåÆ cost control + tiered/overage pricing.
- **Billing** ΓÇö drive monthly charges through the existing Payment integration, or plug in Stripe Billing for subscriptions/invoicing/dunning.
- **Admin control** ΓÇö a toggle in shop-manager to enable the agent and pick a plan flips the entitlement record.

## 10. Designing for External Reuse (the `IOrderSink` boundary)

This single decision determines whether future resale is easy or painful ΓÇö make it **deliberately, now.** Put the write/read tools behind thin interfaces:

`IOrderSink` (CreateOrder, GetOrderStatus) + `ICatalogSource` (SearchCatalog, GetProduct) + `ICustomerSource` (LookupCustomer)

```
Channel Adapters (WhatsApp / Web)  +  Billing & Entitlement  +  AgentConfig
        Γû╝
AGENT CORE (FSM + Claude + tools)   ΓöÇΓöÇ the reusable product ΓöÇΓöÇ
        Γöé writes via interfaces only
        Γû╝
IOrderSink / ICatalogSource / ICustomerSource
   Γû╝                                              Γû╝
George impl (ShopManager = 1st customer)      External impl (REST/webhook contract) = future clients
```

- **ShopManager accounts** ΓÇö the implementation wraps existing George services. Zero extra work.
- **External customers (future)** ΓÇö write a new implementation, or publish a REST/webhook contract. The entire agent ΓÇö FSM, Claude, channels, billing ΓÇö is reused unchanged.

This turns "a WhatsApp feature inside ShopManager" into **a standalone, multi-tenant conversational-ordering platform that ShopManager is simply the first customer of.** Cheap now; expensive to retrofit.

**Honest caveats:** each external business needs its own Meta Business verification and WABA (a BSP as Tech Provider/reseller handles this, but it's a real per-client onboarding step). Third-party resale also raises data-isolation and compliance expectations ΓÇö strict per-tenant scoping, encrypted credentials, and a clear data-processing posture must be first-class.

## 11. Technology Choices

| Concern | Choice | Why |
|---|---|---|
| Runtime | .NET 8 | Matches team & codebase |
| Channel transport | Cloud API/BSP (WhatsApp) + SignalR (web) | SignalR already in stack |
| Messaging | MassTransit + Service Bus / RabbitMQ | Resilient, idempotent, scalable |
| Session state | Redis | Fast, TTL-native, scalable |
| LLM | Anthropic Claude (tool-calling) | Strong Hebrew, reliable tool use |
| Billing | Existing Payment integration or Stripe Billing | Subscriptions, invoicing, dunning |
| Secrets | Key Vault / envelope encryption | Per-tenant tokens never plaintext |
| Observability | OpenTelemetry + existing NLog | Trace end-to-end |

## 12. Phased Rollout

1. **Phase 0 ΓÇö Spike (1 wk):** define `IOrderSink`/`ICatalogSource`; echo bot on one channel; tenant routing.
2. **Phase 1 ΓÇö Web MVP (3ΓÇô5 wks):** hybrid agent in a web chat widget for one account; `create_order` via `IOrderSink`. Cheapest path to a working product.
3. **Phase 2 ΓÇö WhatsApp channel (3ΓÇô4 wks):** WhatsApp adapter, BSP Embedded Signup, templates, 24h-window handling.
4. **Phase 3 ΓÇö Productization (3ΓÇô4 wks):** `AccountSubscription` + `ConversationUsage`, entitlement gate, metering, billing, admin toggle. Now sellable monthly.
5. **Phase 4 ΓÇö External platform:** publish the `IOrderSink` REST/webhook contract, reseller WABA onboarding, tenant-isolation hardening, analytics.

## 13. Risks & Mitigations

| Risk | Mitigation |
|---|---|
| Number/app conflict (┬º2) | Default to a dedicated number; web channel avoids it entirely |
| LLM hallucinating prices/orders | Tool-only writes + FSM + structured confirmation |
| Cost runaway | Entitlement gate + per-account metering; buttons over free-text; cache catalog |
| Billing edge cases | Use a proven billing engine (Stripe) for proration/dunning |
| External-client compliance | First-class per-tenant isolation, encrypted credentials, clear DPA |
| Meta verification delays | Start early; BSP/Tech Provider smooths multi-client onboarding |

## 14. Net Code Impact

| Change | Type |
|---|---|
| `IOrderSink` / `ICatalogSource` / `ICustomerSource` + George impls | New |
| Agent Core (FSM + LLM + tools), channel-agnostic | New |
| WhatsApp Gateway + Web Chat adapter | New |
| `AccountSubscription`, `ConversationUsage`, `SiteWhatsAppChannel`, `AgentConfig` + migrations | New |
| Entitlement gate + metering + billing integration | New |
| Redis + queue infrastructure | New (infra) |
| shop-manager: web chat widget, team inbox, agent + plan admin UI | New (frontend) |
| `OrderService.CreateOrderAsync`, SignalR, PrintAgent core | Unchanged Γ£à |

The core order domain stays **untouched** ΓÇö the agent is an additive, reusable layer above it.

---

*Prepared as a technical proposal for review. Meta policy, pricing, and BSP/billing terms should be re-verified at implementation time.*
