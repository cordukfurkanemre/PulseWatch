# PulseWatch

PulseWatch is a lightweight website monitoring application built with ASP.NET Core, Entity Framework Core, and Azure SQL. It checks configured websites on a schedule, records availability and response-time history, tracks incidents, and presents the results in a responsive monitoring dashboard.

The project intentionally keeps the frontend simple: the dashboard is built with HTML, CSS, vanilla JavaScript, and dependency-free SVG charts.

## Features

- Automatic website checks every 30 seconds
- Manual on-demand health checks
- HTTP status and response-time tracking
- Per-site uptime calculation
- Automatic incident opening and resolution
- Historical health-check and incident data
- Response-time chart for the latest 30 checks
- Multi-site dashboard with site selection
- 30-second dashboard auto-refresh and manual refresh
- Responsive dark monitoring interface
- Azure SQL transient-failure retries
- Graceful worker recovery from website and database failures
- UTC storage with browser-local date and time display

## Tech Stack

| Area | Technology |
| --- | --- |
| Backend | ASP.NET Core Web API (.NET 10) |
| Data access | Entity Framework Core 10 |
| Database | SQL Server / Azure SQL |
| Background processing | ASP.NET Core `BackgroundService` |
| Frontend | HTML, CSS, vanilla JavaScript |
| Charts | Native SVG |
| API description | OpenAPI |

## How It Works

1. `WebsiteMonitoringWorker` loads active websites from the database.
2. Each website is checked using `HttpClient` with a 15-second timeout.
3. The status code, response time, timestamp, and success state are stored as a `HealthCheck`.
4. Failed checks open an incident when one is not already active.
5. A successful check resolves the active incident.
6. The dashboard APIs aggregate current status, uptime, and response-time history.
7. The browser refreshes dashboard data every 30 seconds without reloading the page.

## Project Structure

```text
PulseWatch/
├── Controllers/             # Website, dashboard, and health APIs
├── Data/                    # EF Core DbContext
├── Migrations/              # Database migrations
├── Models/                  # Website, HealthCheck, and Incident entities
├── Services/                # Monitoring service and background worker
├── wwwroot/
│   ├── index.html           # Dashboard page
│   ├── css/dashboard.css    # Responsive dashboard styles
│   └── js/dashboard.js      # API integration and SVG chart rendering
├── Program.cs               # Application and dependency configuration
└── PulseWatch.csproj
```

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- SQL Server or an Azure SQL database
- EF Core CLI tools if you need to apply migrations

### 1. Clone the repository

```bash
git clone https://github.com/cordukfurkanemre/PulseWatch.git
cd PulseWatch
```

### 2. Restore dependencies

```bash
dotnet restore
```

### 3. Configure the database securely

The repository does not contain database credentials. For local development, store the complete connection string with .NET User Secrets:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<your complete SQL Server or Azure SQL connection string>"
```

Do not commit real connection strings, passwords, tokens, private keys, or local secret files. The included `.gitignore` blocks common credential and certificate formats.

### 4. Apply database migrations

```bash
dotnet ef database update
```

### 5. Run the application

```bash
dotnet run --launch-profile https
```

Open the dashboard at:

```text
https://localhost:7282/
```

The configured port can also be found in `Properties/launchSettings.json` or in the application startup output.

## Dashboard

The dashboard displays:

- Total, online, and offline website counts
- Average uptime across monitored websites
- Latest HTTP status and response time
- Latest check time in the browser's local timezone
- A chronological response-time graph for the selected website

All values come from the API and Azure SQL; no monitoring data is hard-coded in the frontend.

## API Reference

### Dashboard

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/api/dashboard/summary` | Returns total sites, online/offline counts, and average uptime |
| `GET` | `/api/dashboard/sites` | Returns the latest status and uptime for every website |
| `GET` | `/api/dashboard/sites/{id}/response-times?limit=30` | Returns recent response-time measurements in chronological order |

The response-time `limit` is clamped between `1` and `100`.

### Websites

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/api/websites` | Lists all websites |
| `GET` | `/api/websites/{id}` | Returns one website |
| `POST` | `/api/websites` | Creates a website |
| `PUT` | `/api/websites/{id}` | Updates a website |
| `DELETE` | `/api/websites/{id}` | Deletes a website |
| `POST` | `/api/websites/{id}/check` | Runs an immediate website check |
| `GET` | `/api/websites/{id}/checks` | Returns health-check history |
| `GET` | `/api/websites/{id}/uptime` | Returns calculated uptime |
| `GET` | `/api/websites/{id}/incidents` | Returns incident history |

### Application Health

| Method | Endpoint | Description |
| --- | --- | --- |
| `GET` | `/api/health` | Returns the API health status |

## Example: Add a Website

```http
POST /api/websites
Content-Type: application/json
```

```json
{
  "name": "Example Website",
  "url": "https://example.com",
  "ipAddress": null,
  "isActive": true,
  "createdUtc": "2026-01-01T00:00:00Z"
}
```

Once active, the website is picked up automatically by the monitoring worker.

## Reliability and Security

- EF Core uses SQL Server transient-failure retries.
- A temporary database failure is logged and retried during the next monitoring cycle instead of stopping the API.
- Each website check uses its own dependency-injection scope and `DbContext`.
- Cancellation tokens are propagated through HTTP and database operations.
- Secrets are loaded from .NET User Secrets during local development.
- Frontend values are inserted using safe DOM APIs rather than raw HTML.
- External website links are restricted to HTTP and HTTPS URLs.

## Production Considerations

This repository is currently a monitoring MVP. Before exposing the application itself to the public internet, consider adding:

- Authentication and authorization for website-management endpoints
- URL validation and SSRF protection for user-supplied monitoring targets
- Rate limiting
- Retention or aggregation policies for historical health checks
- Centralized logging and alert notifications
- A production secret provider such as Azure Key Vault or App Service configuration

Publishing the source repository is separate from publicly deploying the running API. Do not expose the write endpoints publicly without appropriate access controls.

## Build Verification

```bash
dotnet build
```

The current project builds with zero errors and zero warnings.
