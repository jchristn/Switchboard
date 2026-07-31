# Change Log

## Unreleased

- Exposed origin server health checks through the management API and dashboard. Two new read-only,
  bearer-authenticated endpoints — `GET /origins/health` (all origins) and `GET /origins/{guid}/health`
  (single origin) — return live health with uptime percentage, a rolling 24-hour window of individual
  check results, first/last-check and last-healthy/unhealthy timestamps, consecutive success/failure
  counts, and the most recent error (`OriginServerHealthStatus` / `HealthCheckRecord` models)
- The dashboard Origins view now renders a health status badge with a bar histogram of recent checks in
  the Health column, and a detail modal with uptime, consecutive counts, last error, the full histogram,
  and check timestamps; health refreshes every 15 seconds. Health telemetry is in-memory only and never
  persisted
- Documented the new endpoints and models in `REST_API.md`, the Postman collection, and the generated
  OpenAPI/Swagger document; added positive and negative integration tests for both endpoints
- Fixed a bug where editing an existing origin's hostname, port, SSL, or health-check URL/method did not
  take effect: the background health check now re-reads the target on every iteration, so in-place
  configuration changes are picked up without a restart. When the target changes, the origin's health
  telemetry is reset so uptime and history reflect the new target (previously an edited origin kept probing
  its old address and stayed unhealthy, while a newly created origin worked)

## Current Version

v4.1.0

### Changes in v4.1.0

- Reworked the management dashboard end to end: grouped navigation, an operator overview with KPI cards and a request-activity chart, a request-history inspector, form-based settings editing with restart-required annotations, a first-run setup wizard, an OpenAPI-driven API Explorer, kebab action menus with consistent View / Edit / View JSON / Delete, an icon topbar with a GitHub link, and full internationalization
- Database-managed configuration (origins, endpoints, routes, origin mappings, URL rewrites, and blocked headers created via the dashboard or management API) now projects into the running gateway automatically, so it takes effect without a restart; file/programmatic configuration remains an untouched baseline
- Fixed origin and endpoint retrieval, update, and delete by GUID, which previously returned HTTP `500` (`no such column: guid`); GUIDs are now derived deterministically from the identifier and resolved without a persisted column
- Expanded the dashboard to nine languages — English, Spanish, German, French, Portuguese, Mandarin, Cantonese, Japanese, and Farsi (Farsi with right-to-left layout); moved the login language selector into the login card
- The API Endpoints table now shows each endpoint's HTTP method and URL pattern (its routes) directly in the list
- Added management API endpoints backing the dashboard: `GET /history/timeseries` (bucketed request activity), `GET`/`PUT /settings` (global configuration with masked secrets and restart-required/runtime-editable metadata), `POST /system/restart` (graceful restart for supervised deployments), and `POST /config/validate` (configuration validation)
- The default administrator credential is now writable, so the out-of-box admin can actually create, update, and delete resources through the dashboard and management API
- Permission failures on management writes now return HTTP `403` instead of `401`, so a read-only credential attempting a write is no longer treated as a logged-out session
- The dashboard client only ends the session on an authentication `401`, not on an authorization failure
- Setup wizard: toggling SSL/TLS on an origin now sets the port to the conventional value (443 with SSL, 80 without)
- Request Activity chart: the Y-axis now uses whole-number ticks, so an empty chart shows `0` and `1` instead of duplicated labels
- Aligned the version to `v4.1.0` across the NuGet package, Docker image tags, compose files, `sb.json`, the dashboard, build scripts, and documentation
- Added a Postman collection covering the full management API, and a `DOCKERHUB_README.md`

### Changes in v4.0.10

- Enforce configured blocked headers (global and per-endpoint) when forwarding requests to origins; previously only a fixed hop-by-hop set was stripped
- Return HTTP `413` (instead of `400`) when a request exceeds `MaxRequestBodySize`, matching the `TooLarge` error model
- Add human-readable error messages for the `SlowDown` (429) and `TokenExpired` (401) error codes
- Re-architected the test suite onto the [Touchstone](https://github.com/jchristn/touchstone) framework (Test.Shared / Test.Automated / Test.Xunit / Test.Nunit) with substantially expanded coverage

### Changes in v4.0.9

- Upgraded `Switchboard.Core` to `Watson` `7.0.14`
- Added explicit OpenAPI / Swagger route metadata for documentation endpoints
- Added documented CORS preflight coverage for OpenAPI and Swagger surfaces
- Expanded integration coverage to validate documentation preflight behavior and metadata

### New Features

- **Database Backend** - Store configuration in SQLite, MySQL, PostgreSQL, or SQL Server
  - Runtime configuration changes without restart
  - Multi-instance configuration sharing (with external databases)
  - Automatic schema creation and migration

- **Management API** - Full RESTful API for configuration management
  - CRUD operations for origins, endpoints, routes, and mappings
  - URL rewrite rule management
  - Blocked headers management
  - User and credential management
  - Bearer token authentication
  - OpenAPI 3.0.3 specification at `/openapi.json`
  - Interactive Swagger UI at `/swagger`

- **Web Dashboard** - React-based management interface
  - Visual configuration of origins and endpoints
  - Real-time health monitoring
  - Request history viewer with filtering
  - Settings management

- **Request History** - Track and analyze proxied requests
  - Searchable request/response history
  - Configurable body capture
  - Automatic cleanup with retention policies
  - Statistics and metrics

- **Docker Improvements**
  - Separate Docker images: `jchristn77/switchboard` (server) and `jchristn77/switchboard-ui` (dashboard)
  - Dashboard container with nginx serving React SPA
  - Database-specific compose files (SQLite, MySQL, PostgreSQL, SQL Server)
  - Network troubleshooting tools included (curl, wget, dig, ping, vim, jq)
  - Improved health checks for all services

### Configuration Changes

New settings sections:

```json
{
  "Database": {
    "Enable": true,
    "Type": "Sqlite",
    "Filename": "switchboard.db"
  },
  "Management": {
    "Enable": true,
    "BasePath": "/_sb/v1.0/",
    "AdminToken": "your-token",
    "RequireAuthentication": true
  },
  "RequestHistory": {
    "Enable": true,
    "CaptureRequestBody": false,
    "CaptureResponseBody": false,
    "RetentionDays": 7,
    "MaxRecords": 10000
  }
}
```

### Documentation

- Added `docs/REST_API.md` - Complete REST API reference
- Added `docs/DASHBOARD-GUIDE.md` - Dashboard user guide
- Added `docs/MIGRATION.md` - Migration guide from JSON-only configuration

---

## Previous Versions

v3.0.x

- Added origin server healthchecks and ratelimiting
- Added OpenAPI/Swagger documentation support

v2.0.x

- Added authentication support
- Reorganized API endpoints into groups (`ApiEndpointGroup`) for authenticated `Authenticated` and unauthenticated `Unauthenticated`

v1.0.x

- Initial release
