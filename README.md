# POS Backend

Multi-tenant POS & Inventory backend — ASP.NET Core (.NET 9), EF Core, PostgreSQL.

See `ARCHITECTURE.md` for the full design. This README is for running the backend.

## Layout

```
src/
  Pos.BuildingBlocks/    Money, Result, Ulid, base interfaces
  Pos.Domain/            Entities + domain interfaces (no dependencies)
  Pos.Application/       Use cases (MediatR), DTOs, validators, behaviors
  Pos.Infrastructure/    EF Core DbContext, JWT, tenant context, repositories
  Pos.Api/               ASP.NET host: minimal-API endpoints, SignalR, middleware
tests/
  Pos.UnitTests/
  Pos.IntegrationTests/  Testcontainers + Postgres
deploy/
  Dockerfile, docker-compose.yml
```

## Quick start

```bash
# Bring up postgres + redis + api
docker compose -f deploy/docker-compose.yml up --build

# Migrations run on container start (see entrypoint).
# API listens on http://localhost:8080
# OpenAPI: http://localhost:8080/scalar/v1
```

## Local dev

```bash
dotnet restore Pos.sln
dotnet ef database update -p src/Pos.Infrastructure -s src/Pos.Api
dotnet run --project src/Pos.Api
```

## Seed credentials (Development env only)

- Tenant slug: `demo`
- Admin: `admin@demo.local` / `Passw0rd!`
- Cashier: `cashier@demo.local` / `Passw0rd!`

## Smoke test

```bash
# Login
curl -s -X POST http://localhost:8080/api/v1/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"email":"admin@demo.local","password":"Passw0rd!","tenantSlug":"demo"}'

# Use the access token for subsequent requests.
TOKEN=...
curl -s http://localhost:8080/api/v1/products -H "Authorization: Bearer $TOKEN"
```

## Endpoints (v1)

| Method | Path | Description |
|---|---|---|
| POST | `/api/v1/auth/login` | Login with tenant slug |
| POST | `/api/v1/auth/refresh` | Rotate refresh token |
| POST | `/api/v1/auth/logout` | Revoke refresh token |
| GET  | `/api/v1/shops` | List shops in tenant |
| GET  | `/api/v1/products` | Search / list products |
| POST | `/api/v1/products` | Create product |
| GET  | `/api/v1/inventory/{productId}/balance` | On-hand per warehouse |
| POST | `/api/v1/inventory/adjustments` | Manual stock adjustment |
| POST | `/api/v1/pos/sales` | Commit a sale (idempotent) |
| POST | `/api/v1/pos/shifts/open` | Open shift |
| POST | `/api/v1/pos/shifts/{id}/close` | Close shift |
| POST | `/api/v1/sync/batch` | Offline batch sync |
| GET  | `/health/live` `/health/ready` | Health |
