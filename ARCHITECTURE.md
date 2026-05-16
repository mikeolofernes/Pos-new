# Multi-Tenant, Multi-Shop POS & Inventory Platform — Architecture & Implementation Plan

> Stack: Blazor Web App (Interactive Auto + PWA) · ASP.NET Core (.NET 9) · EF Core · PostgreSQL · SignalR · Redis · Docker
> Style: Clean Architecture + Modular Monolith (microservice-ready)
> Scale target: thousands of tenants, tens of thousands of daily transactions, offline-capable, mobile-heavy.

---

## Table of Contents

1.  [System Architecture](#1-system-architecture)
2.  [Multi-Tenant Design](#2-multi-tenant-design)
3.  [Database Design](#3-database-design)
4.  [POS Features](#4-pos-features)
5.  [Inventory System](#5-inventory-system)
6.  [Financial & Reporting](#6-financial--reporting)
7.  [Security](#7-security)
8.  [Offline-First POS Design](#8-offline-first-pos-design)
9.  [Mobile & Responsive Design](#9-mobile--responsive-design)
10. [Recommended Project Structure](#10-recommended-project-structure)
11. [Suggested Modules](#11-suggested-modules)
12. [API Design](#12-api-design)
13. [Performance & Scalability](#13-performance--scalability)
14. [DevOps & Deployment](#14-devops--deployment)
15. [Recommended Third-Party Integrations](#15-recommended-third-party-integrations)
16. [AI Features Roadmap](#16-ai-features-roadmap)
17. [Step-by-Step Development Roadmap](#17-step-by-step-development-roadmap)
18. [Sample Entity Models](#18-sample-entity-models)
19. [Recommended NuGet Packages](#19-recommended-nuget-packages)
20. [Common Mistakes to Avoid](#20-common-mistakes-to-avoid)

---

## 1. System Architecture

### 1.1 High-Level Diagram (textual)

```
                                +----------------------------+
                                |  Edge / CDN (CloudFront)   |
                                +-------------+--------------+
                                              |
          +-----------------------------------+--------------------------------+
          |                                                                     |
   +------v------+        +---------------+      +-----------------+   +-------v-------+
   |  Blazor Web | <----> |  ASP.NET Core | <--> |  PostgreSQL 16  |   |  Object Store |
   |  App (PWA)  |  HTTPS |  API Gateway  |  EF  | (Primary +RR)   |   |  (S3/MinIO)   |
   |  Interactive|        |  + SignalR Hub|      +-----------------+   +---------------+
   |  Auto       |        +-------+-------+
   |  IndexedDB  |                |
   +------+------+        +-------+-------+      +-----------------+
          |               |  Redis (cache,|      |  Hangfire/Quartz|
          | WebUSB/Serial |  pub/sub, RL) |      |  (background)   |
          v               +-------+-------+      +-----------------+
   +-------------+                |
   | Hardware:   |        +-------+-------+      +-----------------+
   | scanner,    |        |  OpenSearch / |      |  OpenTelemetry  |
   | printer,    |        |  Elastic      |      |  Collector ->   |
   | drawer      |        |  (search/log) |      |  Tempo/Loki/    |
   +-------------+        +---------------+      |  Prometheus     |
                                                 +-----------------+
```

### 1.2 Monolith vs Modular Monolith vs Microservices

| Style | Pros | Cons | Verdict |
|---|---|---|---|
| Monolith | Simple, fast | Couples teams, hard to scale parts | ❌ |
| **Modular Monolith** | Strong module boundaries, single deploy, ACID across modules, easy to extract later | Single process | ✅ **Start here** |
| Microservices | Independent scale/deploy | Distributed transactions, ops complexity, latency, cost | Phase 3+ only |

**Recommendation:** Start with a Modular Monolith. POS, Inventory, Sales, Payments share a *transactional boundary* (oversell prevention, stock decrement on sale). Distributing them prematurely turns ACID into Saga complexity. Plan boundaries as if extracting later — only extract Reporting/Notifications/AI first because they are async by nature.

### 1.3 Tenant Isolation Strategies

| Strategy | Cost | Isolation | Operational | Recommendation |
|---|---|---|---|---|
| Shared DB / shared schema (`TenantId` discriminator) | 💲 Cheapest | Logical only | Easy migrations | ✅ **Default** |
| Shared DB / separate schema (per tenant) | 💲💲 | Better | Migrations × N schemas | Mid-tier customers |
| Separate DB per tenant | 💲💲💲 | Strongest | Migrations × N DBs, connection sprawl | Enterprise tier only |

**Recommended hybrid:**
- **Pooled** (shared schema + `TenantId` + RLS) for the SMB tier (default, scales to thousands).
- **Siloed** (DB-per-tenant) for Enterprise customers needing residency/compliance.
- Tenant-resolution layer abstracts which mode a tenant uses.

### 1.4 CQRS

Use **lightweight CQRS** via MediatR:
- **Commands** (write) → domain services + EF Core write context.
- **Queries** (read) → Dapper or EF AsNoTracking against read replicas / materialized views.
- Avoid full Event Sourcing globally; **apply Event Sourcing only where it pays off**: `InventoryLedger`, `JournalEntry`, `AuditTrail`. These are append-only by domain need.

### 1.5 Repository & Unit of Work

- Don’t wrap EF in generic repositories — `DbContext` *is* a UoW. ✋
- Use **specific aggregate repositories** only when you need to hide query complexity (e.g., `IInventoryLedgerRepository.PostMovement(...)`). Otherwise inject `DbContext` into application services.
- Use `IUnitOfWork.SaveChangesAsync()` interface only as a seam for testing.

### 1.6 Event-Driven Possibilities

- In-process: **MediatR `INotification`** for synchronous cross-module events (e.g., `SaleCommitted` → loyalty points).
- Cross-process: **Outbox pattern** (table in PostgreSQL) → background dispatcher → Redis Streams or NATS / RabbitMQ when extracted.
- Domain events recorded inside the same DB transaction as the entity → guaranteed delivery.

### 1.7 Caching Strategy

| Tier | Tool | Use |
|---|---|---|
| L1 (per-process) | `IMemoryCache` | Hot reference data: tax rates, settings |
| L2 (shared) | Redis | Tenant config, product catalog, sessions, rate-limit counters, SignalR backplane |
| L3 (DB) | Materialized views | Reporting aggregations |
| Client | IndexedDB | Catalog snapshot for offline |

Cache invalidation via outbox events (`ProductUpdated` → bust `tenant:{id}:product:{sku}`).

### 1.8 Offline Sync Strategy (overview)

- **Client** keeps a *local replica* of catalog + open shift data in IndexedDB.
- All writes go to a **local outbox** with an `idempotencyKey` (ULID).
- Background sync drains outbox → server `/sync/batch` endpoint when online.
- Server replies with **server-assigned IDs** + canonical state; client merges (LWW for catalog, **server-wins for inventory**, **client-wins for closed receipts**).
- **Inventory oversell**: server validates and may issue **compensating adjustments** (stock pulled from another bin or flagged as exception).

### 1.9 Real-Time Sync

- **SignalR** hub per tenant group (`tenant-{id}`) and per shop (`shop-{id}`).
- Backplane: **Redis** (or Azure SignalR Service for scale-out).
- Push: stock changes, new orders, kitchen display updates, shift events.

### 1.10 File Storage

- S3-compatible (AWS S3, Cloudflare R2, MinIO for self-host).
- Tenant-scoped bucket prefix `tenants/{tenantId}/...`.
- Pre-signed URLs for upload/download — never proxy through API.
- Receipt PDFs cached with content-hash for dedupe.

### 1.11 Logging & Monitoring

- **Serilog** → OpenTelemetry exporter → Loki (logs) / Tempo (traces) / Prometheus (metrics).
- Always enrich with `TenantId`, `ShopId`, `UserId`, `CorrelationId`, `DeviceId`.
- Health checks at `/health/live` and `/health/ready` (DB, Redis, Storage).

### 1.12 Background Jobs

- **Hangfire** (PostgreSQL storage) for delayed/recurring jobs (subscription billing, reorder suggestions, EOD closing).
- **Channels** + `IHostedService` for in-process queues (outbox dispatcher, SignalR fan-out).

### 1.13 API Versioning

- URL-based: `/api/v1/...`, `/api/v2/...`.
- `Asp.Versioning` package, OpenAPI per version.
- Deprecation header `Sunset:` + 6-month policy.

---

## 2. Multi-Tenant Design

### 2.1 Tenant Hierarchy

```
Tenant (SaaS customer)
  └── Subscription (plan, limits)
  └── Shop / Branch (1..N)
        └── Register / Terminal (1..N)
        └── Cash Drawer
        └── Warehouse (often 1:1 with shop, but can be shared)
  └── User (1..N)
        └── UserShopRole (many-to-many with role per shop)
  └── FeatureFlag (per-tenant overrides)
```

### 2.2 Core Tenant Entity (sketch)

```csharp
public class Tenant : IEntity
{
    public Guid Id { get; set; }
    public string Slug { get; set; }                  // for subdomain: acme.pos.app
    public string Name { get; set; }
    public string CountryCode { get; set; }           // ISO 3166-1 alpha-2
    public string DefaultCurrency { get; set; }       // ISO 4217
    public string TimeZoneId { get; set; }            // IANA
    public TenantStatus Status { get; set; }          // Trial, Active, Suspended, Cancelled
    public TenantIsolationMode IsolationMode { get; set; } // Pooled | Siloed
    public string? DedicatedConnectionString { get; set; } // siloed only
    public DateTimeOffset CreatedAt { get; set; }
    public Guid SubscriptionId { get; set; }
}

public class Shop : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Code { get; set; }                  // unique within tenant
    public string Name { get; set; }
    public Address Address { get; set; }
    public string TimeZoneId { get; set; }
    public string Currency { get; set; }
    public bool IsActive { get; set; }
}

public class UserShopRole : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid UserId { get; set; }
    public Guid ShopId { get; set; }
    public Guid RoleId { get; set; }
}
```

### 2.3 Tenant-Aware DbContext

```csharp
public class AppDbContext : DbContext
{
    private readonly ITenantContext _tenant;

    protected override void OnModelCreating(ModelBuilder mb)
    {
        foreach (var et in mb.Model.GetEntityTypes()
                 .Where(t => typeof(ITenantEntity).IsAssignableFrom(t.ClrType)))
        {
            var p = Expression.Parameter(et.ClrType, "e");
            var body = Expression.Equal(
                Expression.Property(p, nameof(ITenantEntity.TenantId)),
                Expression.Property(Expression.Constant(_tenant), nameof(ITenantContext.TenantId)));
            mb.Entity(et.ClrType).HasQueryFilter(Expression.Lambda(body, p));
        }
    }

    public override int SaveChanges()
    {
        foreach (var e in ChangeTracker.Entries<ITenantEntity>()
                          .Where(e => e.State == EntityState.Added))
            e.Entity.TenantId = _tenant.TenantId;
        return base.SaveChanges();
    }
}
```

### 2.4 Tenant Resolution Middleware

Resolution order (first match wins):
1. **JWT claim** `tid` (preferred — set at login).
2. **Subdomain** `{slug}.pos.app`.
3. **Header** `X-Tenant-Id` (machine-to-machine only, signed).
4. **Path prefix** `/t/{slug}/...` (admin tools).

```csharp
public class TenantResolutionMiddleware
{
    public async Task Invoke(HttpContext ctx, ITenantStore store, ITenantContext tenantCtx)
    {
        var tid = ctx.User.FindFirst("tid")?.Value
                 ?? await store.ResolveBySubdomainAsync(ctx.Request.Host.Host);
        if (tid is null) { ctx.Response.StatusCode = 400; return; }
        tenantCtx.SetTenant(Guid.Parse(tid));
        await _next(ctx);
    }
}
```

### 2.5 Super Admin

- Separate **`/admin`** area authenticated against a *system* tenant (`TenantId == Guid.Empty`).
- `IgnoreQueryFilters()` only available behind `[Authorize(Policy="SystemAdmin")]`.
- All access logged to immutable `system_audit` table.

### 2.6 Subscription & Feature Flags

```csharp
public class Subscription { public Guid TenantId; public PlanId PlanId;
    public DateOnly RenewsAt; public BillingStatus Status; public int MaxShops;
    public int MaxUsers; public long MaxMonthlyTx; }

public class FeatureFlag { public Guid TenantId; public string Key; public bool Enabled; public string? Json; }
```

Feature checks: `IFeatureGate.IsEnabledAsync("pos.kitchen-display")` → backed by Redis with 60s TTL.

### 2.7 Billing-Ready

- **Stripe Billing** integration: `customer_id`, `subscription_id`, webhooks → `Subscription`.
- Metered usage events (transactions/month, devices) → push to Stripe.

---

## 3. Database Design

### 3.1 Naming Conventions

- Tables: `snake_case`, plural: `sales`, `sale_items`, `inventory_movements`.
- PKs: `id` (UUID v7 for time-ordered, indexable).
- FKs: `<entity>_id`.
- Tenant column: `tenant_id` on every tenant-owned table.
- Soft delete: `deleted_at TIMESTAMPTZ NULL` (NOT a `is_deleted bool`).
- Timestamps: `created_at`, `updated_at` `TIMESTAMPTZ` UTC.
- Concurrency: `xmin` system column or `row_version BYTEA`.
- Money: `NUMERIC(19,4)`. Never `float`.
- Avoid `ENUM` types in PG → prefer `SMALLINT` with code-side enum for migration agility.

### 3.2 Core Tables

```
tenants, subscriptions, plans, feature_flags
shops, registers, warehouses
users, roles, permissions, user_shop_roles, refresh_tokens, devices
products, product_variants, product_modifiers, categories, brands
units_of_measure, product_units, barcodes
price_lists, price_list_items, discounts, coupons, taxes, tax_groups
suppliers, purchase_orders, purchase_order_items, goods_receipts
inventory_movements, stock_balances (denormalised cache), batches, serials
sales, sale_items, sale_payments, payment_methods
customers, loyalty_accounts, loyalty_transactions, gift_cards, store_credits
shifts, cash_movements, cash_drawer_counts
returns, return_items, refunds
journal_entries, journal_lines, accounts (chart of accounts)
audit_events, outbox_messages, integration_jobs
notifications, notification_templates
```

### 3.3 Inventory Movement Ledger (the heart of the system)

**Append-only.** All stock changes — purchases, sales, transfers, adjustments — are insertions; corrections are *new rows* (negative qty), never updates.

```sql
CREATE TABLE inventory_movements (
  id              UUID PRIMARY KEY DEFAULT uuidv7(),
  tenant_id       UUID NOT NULL,
  shop_id         UUID NOT NULL,
  warehouse_id    UUID NOT NULL,
  product_id      UUID NOT NULL,
  variant_id      UUID NULL,
  batch_id        UUID NULL,
  serial_id       UUID NULL,
  movement_type   SMALLINT NOT NULL,   -- 1=PurchaseIn,2=SaleOut,3=TransferIn,4=TransferOut,
                                       -- 5=AdjustIn,6=AdjustOut,7=ReturnIn,8=ReturnOut,
                                       -- 9=ProductionIn,10=ProductionOut,11=Damage,12=Expiry
  quantity        NUMERIC(19,4) NOT NULL,  -- signed
  unit_cost       NUMERIC(19,4) NOT NULL,  -- moving avg snapshot
  reference_type  SMALLINT NOT NULL,        -- 1=Sale,2=PO,3=Transfer,4=Adjustment,5=Return
  reference_id    UUID NOT NULL,
  occurred_at     TIMESTAMPTZ NOT NULL,
  posted_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
  user_id         UUID NOT NULL,
  device_id       UUID NULL,
  idempotency_key TEXT NOT NULL,
  CONSTRAINT uq_movement_idem UNIQUE (tenant_id, idempotency_key)
) PARTITION BY RANGE (occurred_at);

CREATE INDEX ix_mov_stock     ON inventory_movements (tenant_id, warehouse_id, product_id, variant_id, occurred_at DESC);
CREATE INDEX ix_mov_reference ON inventory_movements (tenant_id, reference_type, reference_id);
```

**`stock_balances`** is a derived cache (UPSERTed on each movement inside the same TX) for O(1) on-hand reads:

```sql
CREATE TABLE stock_balances (
  tenant_id UUID, warehouse_id UUID, product_id UUID, variant_id UUID,
  on_hand NUMERIC(19,4) NOT NULL,
  reserved NUMERIC(19,4) NOT NULL DEFAULT 0,
  avg_cost NUMERIC(19,4) NOT NULL,
  updated_at TIMESTAMPTZ NOT NULL,
  PRIMARY KEY (tenant_id, warehouse_id, product_id, variant_id)
);
```

### 3.4 Sales Ledger

`sales` and `sale_items` are immutable after `status = Completed`. Any change = `return` or `void` row.

### 3.5 Accounting-Ready (double-entry)

```sql
CREATE TABLE journal_entries (
  id UUID PRIMARY KEY, tenant_id UUID NOT NULL, occurred_at TIMESTAMPTZ NOT NULL,
  reference_type SMALLINT, reference_id UUID, memo TEXT, created_at TIMESTAMPTZ );

CREATE TABLE journal_lines (
  id UUID PRIMARY KEY, journal_entry_id UUID NOT NULL,
  account_id UUID NOT NULL,             -- chart of accounts
  debit NUMERIC(19,4) NOT NULL DEFAULT 0,
  credit NUMERIC(19,4) NOT NULL DEFAULT 0,
  CHECK (debit >= 0 AND credit >= 0 AND (debit = 0 OR credit = 0))
);
-- Trigger or app-level invariant: SUM(debit) == SUM(credit) per entry.
```

### 3.6 Indexing Strategy

- **Composite indexes always start with `tenant_id`** (selectivity, RLS).
- B-tree for FK + status, partial indexes for hot filters (`WHERE deleted_at IS NULL`).
- BRIN index on `inventory_movements(occurred_at)` (time-series).
- GIN/`pg_trgm` on `products(name)` and barcodes for search.
- Use **covering indexes** (`INCLUDE (...)`) for dashboard queries.

### 3.7 Partitioning

- `inventory_movements`, `sales`, `audit_events`, `journal_lines` → **range-partition by month** (`occurred_at`).
- Detach old partitions for archival (parquet → cold storage).
- Hash-partition by `tenant_id` only when one tenant becomes a hotspot (rare).

### 3.8 Concurrency

- Optimistic concurrency on aggregates (`xmin`).
- For stock decrement: `INSERT` movement + `UPDATE stock_balances ... WHERE on_hand >= :qty RETURNING ...` inside one TX. Row-lock on `stock_balances` PK.
- `FOR UPDATE SKIP LOCKED` on outbox/job tables.

### 3.9 Soft Delete

- `deleted_at` column + global query filter.
- Hard-purge job per tenant data-retention policy.

---

## 4. POS Features

### 4.1 Feature Matrix

| Area | Feature | Notes |
|---|---|---|
| Sale | Barcode, search, variants, modifiers, combos, multi-unit | Local + remote search |
| Pricing | Multi price-list, discounts (line/cart), coupons, BOGO, loyalty | Stackable rules engine |
| Payments | Cash, card (terminal), wallet, gift card, store credit, split, partial, layaway | Multi-currency |
| Receipts | Email, SMS, print (thermal 58/80mm), reprint, digital | ESC/POS via WebUSB or print agent |
| Operations | Hold/park/resume, void, return, exchange, refund, tip, commission | Manager PIN required |
| Hardware | Cash drawer, kitchen printer, customer display, scale | Hardware Abstraction Layer |
| Shift | Open/close, blind/declared count, cash movements | Reconciliation report |
| Tax | Inclusive/exclusive, multi-tax, exemptions, fiscal compliance | Country tax engines |

### 4.2 Sale Workflow (Online)

```
ScanItem → AddLine → ApplyPriceRules → CalcTaxes → CalcLoyalty
     → AddPayment* → Tender → ServerCommit (TX: sale + items + payments + inventory + journal + outbox)
     → ReceiptRender → PrintQueue → ShiftLog → SignalRPushDashboard
```

### 4.3 Sale Workflow (Offline)

```
ScanItem (local catalog) → Local cart → Tender (cash only by default; cards require online auth)
     → Generate idempotency_key (ULID) + local receipt no. (`shop-register-yyyymmdd-seq`)
     → Persist to IndexedDB outbox → Print local receipt
     → On reconnect → /sync/batch → server validates & responds with canonical IDs
```

### 4.4 Hold / Park

`sales.status = Parked` with TTL; index on `(tenant_id, shop_id, status, parked_at)`.

### 4.5 Returns & Exchanges

Always linked to original sale (`return.original_sale_id`), validated within return-window per tenant policy. Generates negative inventory movement + reverse journal entry.

### 4.6 Hardware Abstraction Layer

```csharp
public interface IPosDevice<T> { Task<bool> ConnectAsync(); Task DisconnectAsync(); }
public interface IBarcodeScanner : IPosDevice<string> { event Action<string> OnScan; }
public interface IReceiptPrinter : IPosDevice<ReceiptDoc> { Task PrintAsync(ReceiptDoc d); }
public interface ICashDrawer    { Task OpenAsync(); }
public interface IPaymentTerminal { Task<TenderResult> ChargeAsync(Money m, string idem); }
```

Implementations:
- `WebUsbScanner`, `WebSerialPrinter`, `WebHidDrawer` for browser.
- `MauiBridgeScanner` etc. for MAUI Hybrid app.
- `LocalAgentPrinter` (HTTP `localhost:9100`) fallback for legacy printers.

---

## 5. Inventory System

### 5.1 Costing Methods (per tenant config)

- **Moving Average** (default — simple, sufficient for retail).
- **FIFO** — track cost layers per movement; complex but accurate.
- **LIFO** — discouraged (illegal in IFRS countries; offer only where allowed).
- Costing engine is pluggable: `ICostingStrategy.Apply(Movement m, Balance b)`.

### 5.2 Reorder Logic

```
ROP = (avgDailyDemand × leadTimeDays) + safetyStock
SuggestedQty = max(ROP × multiplier - onHand - onOrder, MOQ)
```

Recurring Hangfire job recomputes per-product per-warehouse; suggestions surface in Purchasing UI.

### 5.3 Stock Transfers

Two movements within one TX: `TransferOut` at source + `InTransit` virtual warehouse + `TransferIn` at destination on receipt.

### 5.4 Cycle Counting / Physical

- Generate count sheet with current `on_hand`.
- Counters enter actual; differences post `AdjustIn/AdjustOut` movements with cost impact.
- Manager approval required (RBAC).

### 5.5 Batch / Lot / Expiry

`batches(id, product_id, batch_no, manufactured_at, expires_at, supplier_id)` — required for FEFO (First Expired First Out) picking. Background job alerts on near-expiry.

### 5.6 Serial Numbers

`serials(id, product_id, serial_no, status: InStock|Sold|Returned|RMA, batch_id)` — unique per tenant. Required scan on sale for serialised SKUs.

### 5.7 Bundles / Kits / Simple Assembly

`product_bundle_components(bundle_id, component_id, qty)` → on sale, decrement components. Assembly = explicit `Production` movement: out components, in finished good.

### 5.8 Multi-Warehouse

Stock balances and movements are warehouse-scoped. Sale picks the warehouse from the register's default; configurable fallback chain.

### 5.9 Forecasting (later)

Time-series (Prophet / ML.NET / Azure Forecasting) on aggregated daily sales → reorder hints.

### 5.10 Performance

- Read on-hand from `stock_balances` (1 row), never re-aggregate movements at request time.
- Bulk receive PO via `COPY` into staging table → set-based UPSERT.
- Reporting via materialized views refreshed concurrently.

---

## 6. Financial & Reporting

### 6.1 Reports

- **Operational:** EOD sales by shop / cashier / payment method, Shift reconciliation, Voids & discounts, Hourly heatmap.
- **Inventory:** Stock-on-hand, Valuation (Avg/FIFO), ABC analysis, Slow movers, Near-expiry, Stock-out cost.
- **Financial:** P&L (cash + accrual), COGS, Gross margin, Tax summary by jurisdiction, Cash flow, AR/AP aging.
- **Marketing:** Loyalty engagement, Coupon ROI, Customer cohorts, RFM segmentation.

### 6.2 Materialized Views

```sql
CREATE MATERIALIZED VIEW mv_daily_sales AS
SELECT tenant_id, shop_id, DATE_TRUNC('day', completed_at) d,
       COUNT(*) tx_count, SUM(total) gross, SUM(tax_total) tax, SUM(discount_total) disc
FROM sales WHERE status = 2 GROUP BY 1,2,3;
CREATE UNIQUE INDEX ON mv_daily_sales(tenant_id, shop_id, d);
-- Refresh hourly; CONCURRENTLY to avoid locks.
```

### 6.3 BI / Export

- Read-only replica + role grants for tenant BI tools (Metabase, Power BI).
- CSV / XLSX / PDF exports queued via Hangfire → S3 → signed URL email.
- Webhooks for `report.generated`.

### 6.4 Accounting Integration Readiness

`journal_entries` mirror every business event. Connector modules map to QuickBooks / Xero / Zoho / Tally CoA via `account_mapping` table.

---

## 7. Security

### 7.1 AuthN

- **JWT access tokens** (15 min) + **rotating refresh tokens** (30 d, single-use, family-revoke on reuse).
- Refresh stored hashed (SHA-256) in `refresh_tokens` with `device_id`.
- **MFA**: TOTP (RFC 6238), WebAuthn for admins.
- Manager PIN (4-6 digit) stored as `argon2id` for in-shop overrides — short-lived **device-bound** session token.

### 7.2 AuthZ

- **Permission-based**, not role-based, in code: `[Authorize(Policy="sales.refund")]`.
- Roles are *bundles* of permissions, configurable per tenant.
- Scope every check by `ShopId` (user might have refund rights at one shop only).

### 7.3 Tenant Isolation

- EF global query filters + **PostgreSQL Row-Level Security** as defense-in-depth:

```sql
ALTER TABLE sales ENABLE ROW LEVEL SECURITY;
CREATE POLICY sales_tenant_iso ON sales
  USING (tenant_id = current_setting('app.tenant_id')::uuid);
```

Set `SET LOCAL app.tenant_id = '...';` per request (via EF interceptor).

### 7.4 API Security

- HTTPS only, HSTS, secure cookies.
- CORS allow-list per tenant slug.
- Rate-limiting via `AspNetCoreRateLimit` or built-in `RateLimiter` middleware (per IP + per tenant + per user).
- Anti-CSRF on cookie-auth endpoints.
- Input validation via FluentValidation + `[FromBody]` model binding only.
- Audit every mutating call: who, what, when, where, result.

### 7.5 Secrets

- Azure Key Vault / AWS Secrets Manager / HashiCorp Vault.
- No secrets in `appsettings.json`. Use `dotnet user-secrets` for dev only.

### 7.6 PCI

- **Never store PAN.** All card capture via tokenising terminal (Stripe Terminal, Adyen, etc.). Scope = SAQ-C-VT or P2PE.
- Audit logs encrypted at rest, retained 1 year.

### 7.7 OWASP

Run OWASP ZAP in CI. Mitigate Top 10 by default — parameterised SQL (EF), output encoding (Razor), auth on every endpoint, dependency scans (Dependabot).

### 7.8 Device & Session Tracking

`devices(id, tenant_id, shop_id, type, fingerprint, last_seen_at, status)`. Refresh token bound to device; revoke device → cascading session kill via SignalR.

---

## 8. Offline-First POS Design

### 8.1 Local Storage Strategy (Blazor)

Recommended: **Blazor Web App with Interactive Auto** + **PWA**. The POS route runs in **WASM mode** to enable offline; admin runs Server-rendered.

- **IndexedDB** via `Blazored.LocalStorage` is insufficient — use `TG.Blazor.IndexedDB` or `BlazorDexie` (Dexie.js wrapper).
- Stores:
  - `catalog` (products, variants, barcodes, prices, taxes) — keyed by `productId`.
  - `customers` — top N + recently used.
  - `cart_sessions` — parked sales.
  - `outbox` — pending `SyncEnvelope` rows.
  - `receipts_local` — printed copies.
  - `meta` — last-sync cursors per resource.

### 8.2 Sync Engine

```csharp
public record SyncEnvelope(
  Guid Id, string IdempotencyKey, string Resource,  // "sale","return","stock_count"
  string Op,                                        // "create","update"
  string PayloadJson, DateTimeOffset CreatedAt,
  Guid TenantId, Guid ShopId, Guid DeviceId,
  long ClientSeq);                                  // monotonically increasing per device
```

- **Drain loop**: every 15s when online; manual trigger button.
- **Batch endpoint** `POST /api/v1/sync/batch` accepts up to N envelopes, returns per-envelope `accepted | conflict | rejected` with canonical state.
- Server uses `idempotency_key` to dedupe (UNIQUE constraint).

### 8.3 Conflict Resolution Rules

| Domain | Strategy |
|---|---|
| Catalog | Server wins (read-only on client) |
| Sales | Append-only — can never conflict; idempotency dedupes |
| Inventory | Server-authoritative; oversells flagged as exceptions, manager review |
| Customer profile | Last-write-wins with `version`; or merge fields |
| Settings | Server wins |

### 8.4 Retry Policy

Exponential backoff (2s, 4s, 8s, 30s, 2m, 10m) + jitter; max attempts 50; on permanent failure (`422`) → quarantine queue + UI notification.

### 8.5 Offline Inventory

Client maintains an *optimistic on-hand counter* per product; deducts on sale. On sync, server returns canonical balance; UI reconciles. If oversell detected server-side → automatic refund or hold.

### 8.6 Clock Drift

Server returns `serverTime` on every response; client computes offset; receipts are stamped with both `clientTime` and `serverTime` (server stamps on accept).

### 8.7 Device Identity

UUID generated on first launch, stored in IndexedDB + bound to refresh token. All envelopes include `deviceId`.

---

## 9. Mobile & Responsive Design

### 9.1 Layouts

- **Mobile (≤ 640px):** Single-column, bottom action bar, full-width tender modal, large 56px touch targets.
- **Tablet (641-1280px):** Two-pane (catalog left, cart right), the canonical POS layout.
- **Desktop (≥ 1281px):** Three-pane (categories | catalog | cart) + keyboard shortcuts.
- Bootstrap 5 grid + custom CSS variables per breakpoint.

### 9.2 Touch UX

- Min 44×44 tap targets (Apple HIG).
- Long-press = context menu.
- Swipe-left on cart line = remove.
- Numeric keypad component for tender / quantity.

### 9.3 Hardware Access

- **Camera scanning**: `getUserMedia` + `BarcodeDetector` API (fallback to `zxing-js`).
- **Barcode scanner (USB)**: behaves as keyboard; capture via global keydown w/ debounce.
- **WebUSB / WebSerial / WebBluetooth** for advanced devices (Chromium browsers).

### 9.4 PWA

- `manifest.json`, service worker (Workbox), offline shell, install prompt.
- Push notifications via Web Push (VAPID).

### 9.5 Accessibility

- WCAG 2.1 AA. ARIA on all interactive components. Keyboard navigation. High-contrast theme. Screen-reader-friendly receipts.

---

## 10. Recommended Project Structure

```
/src
  /Pos.Domain                  # Entities, value objects, domain events, interfaces
    /Common
    /Tenancy
    /Catalog
    /Sales
    /Inventory
    ...
  /Pos.Application             # Use cases (CQRS handlers), DTOs, validators
    /Tenancy
    /Sales
    ...
    /Common (Behaviors: Validation, Logging, Transaction, Tenant)
  /Pos.Infrastructure          # EF Core, Repositories, Identity, Outbox, Email, FileStore
    /Persistence
      /Configurations
      /Migrations
    /Identity
    /Caching
    /Messaging
    /Files
  /Pos.Modules.<Name>          # Modular monolith feature modules
    /Pos.Modules.Pos
    /Pos.Modules.Inventory
    /Pos.Modules.Purchasing
    /Pos.Modules.Loyalty
    /Pos.Modules.Accounting
    /Pos.Modules.Reporting
    /Pos.Modules.Notifications
    /Pos.Modules.Billing
    /Pos.Modules.AI
  /Pos.Api                     # ASP.NET Core host: minimal APIs + SignalR + middleware
    /Endpoints/v1
    /Hubs
    /Middleware
    /Composition (DI)
  /Pos.Web                     # Blazor Web App (Interactive Auto)
    /Client                    # WASM project
      /Features/Pos
      /Features/Inventory
      /Sync
      /Hardware
      /Storage
    /Server                    # Server-rendered + prerender
    /Shared                    # Razor components reused across server/client
  /Pos.Shared.Contracts        # DTOs / API contracts shared with Web client
  /Pos.BuildingBlocks          # Result, Error, Pagination, Money, Ulid helpers
/tests
  /Pos.UnitTests
  /Pos.IntegrationTests        # Testcontainers-postgres
  /Pos.E2E.Playwright
/deploy
  /docker
  /k8s
  /helm
/docs
```

**Module boundaries:** each `Pos.Modules.X` exposes a public `IModule` registration + integration events; *no module references another module's internals* — only `Pos.Shared.Contracts` and `Pos.BuildingBlocks`.

---

## 11. Suggested Modules

| Module | Responsibilities | Key Entities | APIs (samples) | Depends On |
|---|---|---|---|---|
| **Identity** | AuthN, JWT, MFA, refresh, devices, password reset | `User, RefreshToken, Device` | `/auth/login, /auth/refresh, /auth/mfa/enroll` | – |
| **Tenant Mgmt** | Tenant CRUD, isolation mode, slug routing | `Tenant, Subscription, Plan` | `/admin/tenants` | Identity |
| **Shop Mgmt** | Shops, registers, warehouses, settings | `Shop, Register, Warehouse` | `/shops, /shops/{id}/registers` | Tenant, Identity |
| **POS** | Sales, payments, shifts, returns, void, hold | `Sale, SaleItem, SalePayment, Shift` | `/pos/sales, /pos/shifts/open` | Catalog, Inventory, Loyalty, Accounting |
| **Catalog** | Products, variants, modifiers, categories, barcodes | `Product, Variant, Modifier, Category` | `/catalog/products` | – |
| **Pricing** | Price lists, discounts, coupons, taxes | `PriceList, Discount, Coupon, Tax` | `/pricing/discounts` | Catalog |
| **Inventory** | Movements, balances, transfers, adjustments, batches/serials | `Movement, Balance, Batch, Serial` | `/inventory/movements, /inventory/transfers` | Catalog |
| **Purchasing** | POs, receiving, supplier mgmt | `PurchaseOrder, GoodsReceipt` | `/purchasing/pos` | Inventory, Suppliers |
| **Suppliers** | Supplier CRUD, terms | `Supplier` | `/suppliers` | – |
| **CRM** | Customers, segments | `Customer` | `/crm/customers` | – |
| **Loyalty** | Points, tiers, rewards, gift cards, store credit | `LoyaltyAccount, GiftCard` | `/loyalty/points, /giftcards` | CRM, POS |
| **Accounting** | Journal entries, CoA, AR/AP | `JournalEntry, Account` | `/accounting/journal` | POS, Inventory, Purchasing |
| **Reporting** | Materialised views, exports, scheduled reports | `ReportJob` | `/reports/daily-sales` | All |
| **Notifications** | Email/SMS/Push templates, dispatch | `NotificationTemplate` | `/notifications/send` | – |
| **Audit** | Append-only audit log | `AuditEvent` | `/audit/events` | – |
| **Billing** | Plan, subscription, Stripe webhooks, usage metering | `Subscription, Invoice, UsageEvent` | `/billing/portal` | Tenant |
| **Settings** | Per-tenant + per-shop settings, feature flags | `Setting, FeatureFlag` | `/settings` | Tenant |
| **Integrations** | Webhooks, OAuth apps, API keys | `Webhook, ApiKey` | `/integrations/webhooks` | Identity |
| **AI** | Forecasting, anomaly, suggestions (async) | `AiJob, AiPrediction` | `/ai/predict` | Reporting, Inventory |

---

## 12. API Design

### 12.1 Conventions

- REST, JSON, UTF-8.
- Resource URLs plural: `/api/v1/sales`, `/api/v1/sales/{id}`.
- HTTP verbs respected. `PATCH` JSON Merge Patch for partial updates.
- **Idempotency-Key** header **required** on all `POST` for write-once resources (sales, payments, transfers).
- **ETag / If-Match** on `PUT/PATCH/DELETE`.
- **Problem+JSON** (RFC 7807) for errors.

### 12.2 Minimal APIs vs Controllers

- **Minimal APIs** for new endpoints (faster, less ceremony, OpenAPI-friendly in .NET 9).
- Group via `MapGroup("/api/v1/sales").RequireAuthorization()`.
- Controllers only if you have inheritance-based filters you cannot replace.

### 12.3 Sample Endpoint

```csharp
group.MapPost("/", async (
        CreateSaleRequest req,
        [FromHeader(Name="Idempotency-Key")] string idem,
        ISender sender, CancellationToken ct) =>
    {
        var result = await sender.Send(new CreateSaleCommand(req, idem), ct);
        return result.Match(
            ok  => Results.Created($"/api/v1/sales/{ok.Id}", ok),
            err => Results.Problem(err.ToProblemDetails()));
    })
    .WithName("CreateSale")
    .RequireAuthorization("sales.create")
    .Produces<SaleDto>(201)
    .ProducesProblem(409)
    .WithOpenApi();
```

### 12.4 Pagination / Filtering / Sorting

- **Cursor pagination** for high-volume lists (sales, movements): `?cursor=<opaque>&limit=50`.
- Offset only for small admin lists.
- Filtering: explicit query params; for advanced search use `?q=...` (OpenSearch).
- Sorting: `?sort=-createdAt,name`.

### 12.5 Real-Time Endpoints (SignalR)

- `/hubs/pos` — join groups: `tenant-{id}`, `shop-{id}`, `register-{id}`.
- Server pushes: `stockChanged`, `saleCommitted`, `kdsTicketAdded`, `shiftClosed`.

### 12.6 Webhooks

- Outgoing webhooks per tenant (`webhook_endpoints`). HMAC-SHA256 signature with rotated secret.
- Delivery via outbox + Hangfire with retries; dead-letter UI for failed deliveries.

---

## 13. Performance & Scalability

### 13.1 PostgreSQL

- `shared_buffers=25%`, `effective_cache_size=70%`, `work_mem` per workload.
- `pg_stat_statements`, `auto_explain`.
- Use `pgbouncer` (transaction pooling) — never let .NET connect directly at scale.
- Replicas for read traffic (reports, BI). EF Core supports separate read/write contexts via interceptor.

### 13.2 EF Core

- `AsNoTracking()` for queries; **compiled queries** for hot paths.
- **Bulk** ops via `EFCore.BulkExtensions` for receiving / imports.
- Project to DTO in queries (avoid hydrating entities).
- Keep aggregates small; lazy-load disabled.
- `SplitQuery` for complex includes; otherwise risk N+1 explosions.

### 13.3 Caching

- **Redis**: tenant config, product catalog snapshots, session/JWT-revoke list, rate-limit counters.
- Cache-aside; TTL + outbox-driven invalidation.

### 13.4 Background Processing

- **Hangfire** dashboard secured to system-admin.
- Critical jobs idempotent + isolated by `tenant_id` queue.

### 13.5 Horizontal Scaling

- Stateless API → run N replicas behind ALB.
- Sticky-session **only** for SignalR fallback; prefer Redis backplane.
- DB scale: vertical first (CPU/IO), then read replicas, then partitioning by `tenant_id` for hottest tenants.

### 13.6 SignalR Scaling

- Redis backplane for ≤ 50k concurrent. Beyond — Azure SignalR Service.

### 13.7 Large Inventory Optimisation

- Don't query movements at request time; read from `stock_balances`.
- Movements partitioned monthly + BRIN indexes.
- Reporting on materialised views.

---

## 14. DevOps & Deployment

### 14.1 Docker

- Multi-stage Dockerfiles (`sdk` → `aspnet`).
- Non-root user, read-only FS where possible.
- `docker-compose` for dev: api, web, postgres, redis, mailpit, minio, opensearch.

### 14.2 CI/CD (GitHub Actions sample stages)

```
on: [push, pull_request]
jobs:
  build-test:
    - dotnet restore / build / test (unit + integration with Testcontainers)
    - npm ci / playwright (E2E)
    - dotnet ef migrations script (DDL diff)
    - trivy / dependency scan
    - publish container -> ghcr.io/.../pos-api:${SHA}
  deploy-staging:    # auto on main
  deploy-prod:       # manual approval, blue/green
```

### 14.3 Environments

`Local → Dev → Staging → Production` with config via env vars (12-factor). Secrets in vault. Per-env tenant fixtures via seed scripts.

### 14.4 Migrations

- EF Core migrations checked in.
- Run migrations as a **separate Job** (not on app startup) — prevents race conditions across replicas.
- Backwards-compatible (expand → migrate code → contract) for zero-downtime deploys.

### 14.5 Backups

- PG continuous WAL archiving (pgBackRest or Barman).
- Nightly base + point-in-time recovery (PITR) tested **monthly**.
- Per-tenant logical exports for offboarding.

### 14.6 Observability

- OpenTelemetry .NET auto-instrumentation.
- Dashboards: tenant-scoped (latency, error rate, sale throughput, sync lag).
- Alerts: SLO-based (e.g., p95 sale commit < 500ms, error rate < 0.5%).

### 14.7 Health Checks

`/health/live` (process up), `/health/ready` (DB + Redis + Storage reachable). K8s probes wired.

### 14.8 Kubernetes Readiness

- Helm chart per service; HPA on CPU + RPS.
- PDBs; rolling updates; init container runs migrations.
- ConfigMap + sealed-secrets.

---

## 15. Recommended Third-Party Integrations

| Area | Options |
|---|---|
| Card payments | Stripe Terminal, Adyen, Square, SumUp, local acquirers |
| Online payments | Stripe, PayPal, Mollie |
| Barcode | ZXing, BarcodeDetector, Dynamsoft |
| Receipt printers | ESC/POS (Epson/Star), QZ Tray, PrintNode (cloud agent) |
| SMS | Twilio, MessageBird, Vonage |
| Email | SendGrid, Postmark, Amazon SES |
| Push | Firebase Cloud Messaging, Web Push (VAPID) |
| Accounting | QuickBooks Online, Xero, Zoho Books, Tally, MYOB |
| Shipping | EasyPost, Shippo, Sendcloud, local carriers |
| AI | OpenAI / Anthropic / Azure OpenAI, ML.NET, AWS Forecast |
| Analytics | Metabase, Power BI, PostHog, GA4 |
| Search | OpenSearch / Elastic / Meilisearch |
| Maps/Geo | Mapbox, Google Maps |
| Identity (optional) | Keycloak, Auth0, Azure AD B2C |

---

## 16. AI Features Roadmap

| Feature | Phase | Notes |
|---|---|---|
| Smart reorder suggestions | 4 | Time-series on demand + lead time |
| Sales forecasting | 4 | Per SKU per shop daily/weekly |
| Anomaly / fraud detection | 4 | Outliers in voids, refunds, discounts per cashier |
| Receipt / invoice OCR | 5 | Goods receiving from supplier invoice |
| Customer insights / segmentation | 4 | RFM, churn risk |
| Dynamic pricing suggestions | 5 | Margin + elasticity hints |
| Voice-assisted POS | 5 | "Add 2 large pizzas" via Whisper + intent model |
| AI chatbot (support + admin Q&A) | 5 | RAG on tenant data + docs |
| Image-based product recognition | 6 | Computer vision for produce/bakery |

All AI features are **async** and **opt-in per tenant** (data residency + cost).

---

## 17. Step-by-Step Development Roadmap

### Phase 1 — MVP (Weeks 1-12)

- Identity + Tenant + Shop + RBAC.
- Catalog (products, variants, barcodes).
- Basic POS (cash sale, single tax, single price-list, receipt print).
- Inventory ledger + balances + adjustments.
- Shift open/close + EOD report.
- PWA shell + offline catalog + offline cash sales.
- Docker dev environment, CI, basic auth, single-tenant happy-path.

### Phase 2 — Core Inventory & Multi-Shop (Weeks 13-24)

- Multi-shop, transfers, purchasing, suppliers.
- Discounts, coupons, multi-payment, returns/refunds.
- Customer + loyalty basic.
- Background jobs (Hangfire), webhooks, audit.
- Stripe billing.

### Phase 3 — Enterprise (Weeks 25-40)

- Accounting journals, advanced taxes (multi-jurisdiction), gift cards, store credit.
- Batch/lot/serial, FEFO, multi-warehouse, manufacturing-lite.
- KDS / customer display / kitchen printer.
- SiloedTenant (DB-per-tenant) option.
- SignalR scale-out, Redis hardened, OpenTelemetry full.

### Phase 4 — Analytics & AI (Weeks 41-52)

- Reporting MVs, dashboards, exports, BI replicas.
- Forecasting, reorder AI, anomaly detection.
- Mobile companion (MAUI Hybrid optional).
- Marketplace / app store for integrations.

### Sprint Plan (2-week sprints)

| Sprint | Deliverable | Risk |
|---|---|---|
| 1 | Repo, CI, DB, Identity skeleton | Low |
| 2 | Tenant resolution + RLS | **High** — get this right |
| 3 | Catalog CRUD + import | Med |
| 4 | POS sale (online) + ledger | **High** — TX correctness |
| 5 | PWA + offline outbox | High |
| 6 | Sync engine + idempotency | **High** |
| ... | ... | ... |

### Priority Matrix (Impact × Effort)

- **Now:** Tenancy correctness, transactional sale + inventory, offline.
- **Next:** Returns, payments, shifts, reporting basics.
- **Later:** AI, BI, accounting deep integrations, microservice extraction.
- **Avoid:** Microservices, CQRS-everywhere, generic repository pattern.

### Risk Analysis

| Risk | Likelihood | Impact | Mitigation |
|---|---|---|---|
| Tenant data leak | Low | Catastrophic | RLS + filters + integration tests per tenant |
| Offline sync conflicts | High | Med | Idempotency + server-wins inventory |
| EF Core perf surprises | Med | Med | AsNoTracking, compiled queries, load tests |
| Hardware fragmentation | High | Med | HAL + agent fallback |
| Money rounding | Med | High | NUMERIC + central `Money` value object |

### Tech Debt Strategy

- 20% of every sprint reserved for refactoring.
- ADRs (Architecture Decision Records) in `/docs/adr`.
- Quarterly "boy scout" sprint.

---

## 18. Sample Entity Models

```csharp
public abstract class TenantEntity : ITenantEntity
{
    public Guid Id { get; set; } = UlidGuid.NewUlidGuid();
    public Guid TenantId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public byte[]? RowVersion { get; set; }
}

public class Product : TenantEntity
{
    public string Sku { get; set; } = default!;
    public string Name { get; set; } = default!;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public Guid? BrandId { get; set; }
    public ProductType Type { get; set; }      // Standard, Variant, Bundle, Service
    public bool TrackInventory { get; set; } = true;
    public bool TrackBatches { get; set; }
    public bool TrackSerials { get; set; }
    public Money DefaultPrice { get; set; } = Money.Zero("USD");
    public decimal? DefaultCost { get; set; }
    public Guid TaxGroupId { get; set; }
    public Guid UnitOfMeasureId { get; set; }
    public List<ProductVariant> Variants { get; set; } = new();
    public List<Barcode> Barcodes { get; set; } = new();
}

public class ProductVariant : TenantEntity
{
    public Guid ProductId { get; set; }
    public string Sku { get; set; } = default!;
    public Dictionary<string,string> Attributes { get; set; } = new(); // {"color":"red","size":"M"}
    public Money? PriceOverride { get; set; }
    public decimal? CostOverride { get; set; }
}

public class InventoryMovement : TenantEntity
{
    public Guid ShopId { get; set; }
    public Guid WarehouseId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public Guid? BatchId { get; set; }
    public Guid? SerialId { get; set; }
    public MovementType Type { get; set; }
    public decimal Quantity { get; set; }     // signed
    public decimal UnitCost { get; set; }
    public ReferenceType ReferenceType { get; set; }
    public Guid ReferenceId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public Guid UserId { get; set; }
    public Guid? DeviceId { get; set; }
    public string IdempotencyKey { get; set; } = default!;
}

public class Sale : TenantEntity
{
    public Guid ShopId { get; set; }
    public Guid RegisterId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid? CustomerId { get; set; }
    public string Number { get; set; } = default!;     // shop-register-yyyymmdd-seq
    public SaleStatus Status { get; set; }              // Draft, Parked, Completed, Voided, Refunded
    public Money Subtotal { get; set; }
    public Money DiscountTotal { get; set; }
    public Money TaxTotal { get; set; }
    public Money Total { get; set; }
    public Money TenderedTotal { get; set; }
    public Money ChangeDue { get; set; }
    public DateTimeOffset CompletedAt { get; set; }
    public Guid CashierId { get; set; }
    public string IdempotencyKey { get; set; } = default!;
    public List<SaleItem> Items { get; set; } = new();
    public List<SalePayment> Payments { get; set; } = new();
}

public class SaleItem : TenantEntity
{
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public Guid? VariantId { get; set; }
    public string NameSnapshot { get; set; } = default!;
    public decimal Quantity { get; set; }
    public Money UnitPrice { get; set; }
    public Money LineDiscount { get; set; }
    public Money LineTax { get; set; }
    public Money LineTotal { get; set; }
    public Guid TaxGroupIdSnapshot { get; set; }
    public Guid? SerialIdSnapshot { get; set; }
    public Guid? BatchIdSnapshot { get; set; }
}

public class SalePayment : TenantEntity
{
    public Guid SaleId { get; set; }
    public Guid PaymentMethodId { get; set; }
    public Money Amount { get; set; }
    public string Currency { get; set; } = default!;
    public decimal FxRate { get; set; }
    public string? ExternalReference { get; set; }   // terminal txn id
    public PaymentStatus Status { get; set; }
}

public class Customer : TenantEntity
{
    public string DisplayName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public Address? Address { get; set; }
    public Guid? LoyaltyAccountId { get; set; }
}

public class Supplier : TenantEntity
{
    public string Name { get; set; } = default!;
    public string? TaxId { get; set; }
    public Address? Address { get; set; }
    public string? PaymentTerms { get; set; }   // Net30, COD
    public string Currency { get; set; } = "USD";
}

public sealed record Money(decimal Amount, string Currency)
{
    public static Money Zero(string c) => new(0m, c);
    public Money Add(Money o) { Ensure(o); return new(Amount + o.Amount, Currency); }
    public Money Subtract(Money o) { Ensure(o); return new(Amount - o.Amount, Currency); }
    private void Ensure(Money o){ if (o.Currency != Currency) throw new InvalidOperationException(); }
}
```

---

## 19. Recommended NuGet Packages

| Concern | Package(s) |
|---|---|
| Auth | `Microsoft.AspNetCore.Authentication.JwtBearer`, `OpenIddict` (if you want full IdP) |
| MFA | `Otp.NET`, `Fido2.AspNetCore` |
| Validation | `FluentValidation.AspNetCore` |
| Mapping | `Mapster` (faster, less ceremony than AutoMapper) |
| CQRS | `MediatR` |
| Result/Errors | `ErrorOr` or `OneOf` |
| Logging | `Serilog.AspNetCore`, `Serilog.Sinks.OpenTelemetry` |
| Telemetry | `OpenTelemetry.Extensions.Hosting`, `OpenTelemetry.Instrumentation.AspNetCore`, `...Http`, `...EntityFrameworkCore`, `...Npgsql` |
| Background | `Hangfire`, `Hangfire.PostgreSql` |
| Caching | `Microsoft.Extensions.Caching.StackExchangeRedis` |
| PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL`, `EFCore.NamingConventions` (snake_case), `EFCore.BulkExtensions` |
| EF utilities | `EntityFrameworkCore.Triggered`, `Microsoft.EntityFrameworkCore.Design` |
| Reporting | `QuestPDF`, `ClosedXML`, `CsvHelper` |
| Barcode | `ZXing.Net`, JS interop with `@zxing/browser` |
| Offline (Blazor) | `BlazorDexie` or `TG.Blazor.IndexedDB`, `Blazored.LocalStorage` |
| HTTP resilience | `Microsoft.Extensions.Http.Resilience` (Polly v8) |
| Rate limiting | built-in `Microsoft.AspNetCore.RateLimiting` |
| Versioning | `Asp.Versioning.Mvc`, `Asp.Versioning.Mvc.ApiExplorer` |
| OpenAPI | `Microsoft.AspNetCore.OpenApi`, `Scalar.AspNetCore` (modern UI) |
| SignalR scale | `Microsoft.AspNetCore.SignalR.StackExchangeRedis` |
| Testing | `xUnit`, `FluentAssertions`, `Testcontainers.PostgreSql`, `Testcontainers.Redis`, `Microsoft.Playwright` |
| Multitenancy helper (optional) | `Finbuckle.MultiTenant` |
| Stripe | `Stripe.net` |
| ULID | `NUlid` |

---

## 20. Common Mistakes to Avoid

### Multi-Tenant
- ❌ Forgetting `tenant_id` on a single table → cross-tenant leak.
- ❌ Relying only on EF query filters (skip them with one `IgnoreQueryFilters()`). **Always pair with PG RLS.**
- ❌ Tenant resolved from header without auth — trivial to spoof.
- ❌ One DbContext shared across requests / no scoped tenant context.
- ❌ Migrations that backfill without batching → table-locks for hours.

### POS
- ❌ Decrementing stock in the application layer outside a TX → oversells.
- ❌ Using `float` / `double` for money. Always `decimal` / `NUMERIC(19,4)`.
- ❌ Auto-generating receipt numbers on the server only — breaks offline; generate per-device with `shop-register-yyyymmdd-seq` and reconcile.
- ❌ Mutating completed sales — always issue void/return.
- ❌ Storing card data — outsource to PCI-DSS terminals.
- ❌ Single global cash drawer for all registers.

### Inventory
- ❌ Recomputing on-hand from movements on every read.
- ❌ FIFO without cost layers — produces wrong COGS.
- ❌ No `idempotency_key` on movements → dup writes on retry.
- ❌ Allowing negative stock without explicit policy flag.
- ❌ Mixing cost & valuation methods between tenants without per-tenant config.

### Blazor
- ❌ Mixing rendering modes carelessly — interactivity boundary causes confusing bugs. Pick **Interactive Auto** and design routes intentionally.
- ❌ Calling `HttpClient` inside server-rendered components — use server-side services.
- ❌ Storing sensitive state in WASM — it's all client-side.
- ❌ Not preloading IndexedDB before going offline.
- ❌ Using `StateHasChanged()` everywhere — prefer event-driven re-renders.

### EF Core
- ❌ N+1 from lazy loading — disable it.
- ❌ Tracking on read-only queries.
- ❌ Long-running DbContexts — keep them request-scoped.
- ❌ Mass `Include` chains causing cartesian explosions; use `AsSplitQuery()`.
- ❌ Running migrations on app startup with multiple replicas.

### PostgreSQL
- ❌ Connecting directly without PgBouncer at scale.
- ❌ Boolean `is_deleted` columns instead of `deleted_at`.
- ❌ `TIMESTAMP WITHOUT TIME ZONE` — always use `TIMESTAMPTZ`.
- ❌ Forgetting `CONCURRENTLY` on index/MV refresh in production.
- ❌ Single huge `audit_events` table without partitioning.
- ❌ Using PG `ENUM` types — locks during migration.

---

## Appendix A — Recommended First Commit Order

1. Solution skeleton (`dotnet new sln` + projects per §10).
2. `Pos.BuildingBlocks` (Money, Result, Ulid, Errors).
3. `Pos.Domain` (Tenant, Shop, User, Product, Sale, Movement).
4. `Pos.Infrastructure.Persistence` (DbContext, configurations, snake_case naming, migrations).
5. `Pos.Infrastructure.Identity` (JWT + refresh + device).
6. Tenant middleware + RLS policies + integration tests.
7. POS sale command (online) + inventory ledger TX.
8. Blazor shell + login + product list.
9. PWA + IndexedDB catalog snapshot.
10. Sync engine (outbox + batch endpoint + idempotency).
11. Returns / shifts / reports.
12. Hangfire + outbox dispatcher + webhooks.

## Appendix B — Frontend Rendering Mode Recommendation

Use **Blazor Web App** with **Interactive Auto** + **PWA** as primary.
- Admin / reporting routes → **Interactive Server** (low-latency, no WASM payload).
- POS routes → **Interactive WebAssembly** (offline + hardware access).
- Public marketing pages → **Static SSR**.
- Optional **MAUI Hybrid** wrapper later for native printer/scanner reliability on Windows POS hardware.

## Appendix C — Key SLOs (production targets)

| Metric | Target |
|---|---|
| Sale commit p95 (online) | < 400 ms |
| Sale commit p95 (offline → on reconnect) | < 5 s for batch of 100 |
| Catalog search p95 | < 150 ms |
| API availability | 99.9 % monthly |
| Sync lag p95 | < 30 s when online |
| Inventory consistency | 0 oversells per 1M sales (post-reconciliation) |

---

*End of plan.* Review, adjust priorities to your team’s capacity, and start at Appendix A.
