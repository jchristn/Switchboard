# Change Log

## Current Version

v4.0.2

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
