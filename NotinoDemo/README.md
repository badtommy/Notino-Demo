# Notino Demo API

Minimal ASP.NET Core Web API for hackathon demos. It runs inside the existing Aspire AppHost, provisions MSSQL through Aspire, seeds demo data on startup, writes unhandled exceptions to `logs/notino-errors.json`, and generates automatic traffic every 4 seconds.

## Run

Start the Aspire AppHost from the repository root:

```bash
cd /Users/erik.baca/RiderProjects/hackaton
dotnet run --project hackaton/hackaton.csproj
```

The API project is orchestrated by Aspire together with SQL Server. The API itself listens on port `5000` inside the app process, while Aspire may proxy or expose it on a dashboard-managed URL.

## OpenAPI

- `GET /openapi/v1.json`

## Endpoints

- `GET /`
- `GET /health`
- `GET /api/products`
- `GET /api/products/{id}`
- `GET /api/products/category/{category}?page=1&pageSize=10`
- `GET /api/products/{id}/discount?percent=10`
- `POST /api/orders/checkout`

## Automatic traffic

`TrafficSimulator` starts automatically and sends a mix of happy-path and buggy requests every 4 seconds.

## Logs

Unhandled exceptions are appended as CLEF JSON lines to:

- `logs/notino-errors.json`

## Intentional bugs

This demo intentionally contains the requested error scenarios in `ProductService` and `OrderService` so the log file gets populated for the agent demo.

## Notes

Running `NotinoDemo` directly without Aspire requires a valid SQL Server connection string, typically via `ConnectionStrings__sqldb` (or `ConnectionStrings__sql` as a fallback). When launched through the Aspire AppHost, that connection is injected automatically.
