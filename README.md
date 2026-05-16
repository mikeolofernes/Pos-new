# POS — Multi-Tenant POS & Inventory Platform

End-to-end .NET 9 stack: ASP.NET Core API + Blazor Web App (Interactive WebAssembly) + PostgreSQL + Redis. See `ARCHITECTURE.md` for the full design.

## Repository layout

```
src/
  Pos.BuildingBlocks/    Money, Result, Ulid, Pagination
  Pos.Domain/            Entities + ITenantContext
  Pos.Application/       MediatR CQRS handlers, validators, behaviors
  Pos.Infrastructure/    EF Core DbContext, JWT, Argon2, seed
  Pos.Api/               ASP.NET host: minimal-API endpoints + SignalR
  Pos.Shared.Contracts/  DTOs shared between API and Blazor client
  Pos.Web.Client/        Blazor WebAssembly — POS, Products, Inventory, Shifts
  Pos.Web/               Razor Components host + YARP reverse proxy to API
tests/
  Pos.UnitTests/
deploy/
  Dockerfile             API image
  Dockerfile.web         Web image (with wasm-tools)
  docker-compose.yml     postgres + redis + api + web
```

## Run with Docker

```bash
docker compose -f deploy/docker-compose.yml up --build
```

- Web (Blazor POS):     http://localhost:8081
- API (Scalar OpenAPI): http://localhost:8080/scalar/v1
- Postgres:             localhost:5432  (pos / pos / pos)

The `Pos.Web` host reverse-proxies `/api/*` and `/hubs/*` to the API container, so the Blazor client can use same-origin URLs (no CORS).

## Local dev (no Docker)

```bash
# Terminal 1 — API
dotnet run --project src/Pos.Api               # http://localhost:8080

# Terminal 2 — Web (Blazor)
dotnet run --project src/Pos.Web                # http://localhost:5173
```

You will need `dotnet workload install wasm-tools` once for WebAssembly AOT builds.

## Seeded credentials (Development)

| Tenant slug | Email                 | Password   | Role    |
|-------------|-----------------------|------------|---------|
| `demo`      | `admin@demo.local`    | `Passw0rd!` | Admin   |
| `demo`      | `cashier@demo.local`  | `Passw0rd!` | Cashier |

## Frontend pages

- `/login`     — Sign in (tenant slug + email + password)
- `/pos`       — POS sale screen (catalog grid + cart + tender modal, offline-capable)
- `/products`  — Product list + create
- `/inventory` — On-hand per product + manual adjustment (idempotent)
- `/shifts`    — Open / close shift with cash variance

## Offline-first

The POS route runs in the browser via **Interactive WebAssembly**. On launch it:
1. Fetches the product catalog from `/api/v1/products` and snapshots it to **IndexedDB** (store `catalog`).
2. If offline, sales are written to a local **outbox** (store `outbox`) with a client-generated `Idempotency-Key`.
3. On reconnect, the outbox drains via `POST /api/v1/sync/batch`. Idempotency dedupes on the server.

A PWA `manifest.webmanifest` and service worker (`service-worker.published.js`) are wired in — installable on tablets/phones.

## Key endpoints

| Method | Path                                  | Description                |
|--------|---------------------------------------|----------------------------|
| POST   | `/api/v1/auth/login`                  | Login + issue token pair   |
| POST   | `/api/v1/auth/refresh`                | Rotate refresh token       |
| GET    | `/api/v1/shops`                       | List shops in tenant       |
| GET    | `/api/v1/products?q=&page=&pageSize=` | Search / list products     |
| POST   | `/api/v1/products`                    | Create product             |
| GET    | `/api/v1/inventory/{productId}/balance` | On-hand per warehouse    |
| POST   | `/api/v1/inventory/adjustments`       | Manual stock adjustment    |
| POST   | `/api/v1/pos/sales`                   | Commit a sale (idempotent) |
| POST   | `/api/v1/pos/shifts/open`             | Open shift                 |
| POST   | `/api/v1/pos/shifts/{id}/close`       | Close shift                |
| POST   | `/api/v1/sync/batch`                  | Offline batch sync         |
| GET    | `/health/live`, `/health/ready`       | Liveness / readiness       |

All write endpoints accept (and the sale + adjustment endpoints **require**) an `Idempotency-Key` header. The Blazor client generates one per action using `Guid.NewGuid().ToString("N")`.

## Smoke test

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"tenantSlug":"demo","email":"admin@demo.local","password":"Passw0rd!","deviceName":"curl"}' \
  | jq -r .accessToken)

curl -s http://localhost:8080/api/v1/products -H "Authorization: Bearer $TOKEN" | jq
```

## First-time migrations

The API auto-bootstraps the schema:
- If EF migrations exist → `Migrate()`.
- Otherwise, in Development → `EnsureCreated()` + seed.

For production, generate migrations once:
```bash
dotnet ef migrations add Initial -p src/Pos.Infrastructure -s src/Pos.Api
```
