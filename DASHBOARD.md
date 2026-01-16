# Switchboard Dashboard Implementation Plan

This document outlines the complete implementation plan for migrating Switchboard configuration to a database backend and building a management dashboard. The end state is a fully RESTful management platform with a web dashboard for configuration, monitoring, and request history.

---

## Table of Contents

1. [Overview](#overview)
2. [Phase 1: Database Infrastructure](#phase-1-database-infrastructure)
3. [Phase 2: Data Models and Migration](#phase-2-data-models-and-migration)
4. [Phase 3: Management API](#phase-3-management-api)
5. [Phase 4: Request History System](#phase-4-request-history-system)
6. [Phase 5: Dashboard Frontend](#phase-5-dashboard-frontend)
7. [Phase 6: Integration and Testing](#phase-6-integration-and-testing)
8. [Phase 7: Documentation and Deployment](#phase-7-documentation-and-deployment)
9. [Appendix: Data Models](#appendix-data-models)
10. [Appendix: API Reference](#appendix-api-reference)

---

## Overview

### Current State

- Configuration is loaded from `switchboard.json` at startup
- No runtime configuration changes possible without restart
- No request history or monitoring capabilities
- No management interface

### Target State

- Configuration stored in database (SQLite, MySQL, PostgreSQL, SQL Server)
- Full CRUD operations via REST API for all configuration entities
- Real-time configuration changes without restart
- Request history with searchable/filterable storage
- Web dashboard for management and monitoring

### Key Principles

1. **Backwards Compatibility**: JSON import/export for migration and backup
2. **Multi-Database Support**: Interface/implementation pattern per BACKEND_ARCHITECTURE.md
3. **Thread Safety**: Maintain existing locking patterns for runtime state
4. **Separation of Concerns**: Database config vs runtime state vs request history

---

## Phase 1: Database Infrastructure

### 1.1 Database Driver Interface

- [ ] Create `src/Switchboard.Core/Database/` directory structure
- [ ] Create `DatabaseTypeEnum.cs` enumeration
  ```
  Sqlite, Mysql, Postgres, SqlServer
  ```
- [ ] Create `IDatabaseDriver.cs` interface
  - [ ] `bool IsOpen { get; }`
  - [ ] `DatabaseTypeEnum DatabaseType { get; }`
  - [ ] `Task OpenAsync(CancellationToken token = default)`
  - [ ] `Task CloseAsync(CancellationToken token = default)`
  - [ ] `Task InitializeSchemaAsync(CancellationToken token = default)`
  - [ ] `Task<T> InsertAsync<T>(T record, CancellationToken token = default)`
  - [ ] `Task<List<T>> SelectAsync<T>(Func<T, bool>? query = null, CancellationToken token = default)`
  - [ ] `Task<T?> SelectByIdAsync<T>(string id, CancellationToken token = default)`
  - [ ] `Task<T> UpdateAsync<T>(T record, CancellationToken token = default)`
  - [ ] `Task DeleteAsync<T>(T record, CancellationToken token = default)`
  - [ ] `Task<bool> ExistsAsync<T>(string id, CancellationToken token = default)`
  - [ ] Implement `IDisposable` and `IAsyncDisposable`
- [ ] Create `DatabaseDriverBase.cs` abstract base class
  - [ ] Common dispose pattern implementation
  - [ ] Connection string management
  - [ ] Schema version tracking

### 1.2 SQLite Implementation

- [ ] Create `src/Switchboard.Core/Database/Sqlite/` directory
- [ ] Create `SqliteDatabaseDriver.cs`
  - [ ] Implement all `IDatabaseDriver` methods
  - [ ] Schema creation for all tables (see Appendix)
  - [ ] Connection pooling configuration
  - [ ] WAL mode for concurrency
- [ ] Add NuGet reference: `Microsoft.Data.Sqlite`

### 1.3 MySQL Implementation

- [ ] Create `src/Switchboard.Core/Database/Mysql/` directory
- [ ] Create `MysqlDatabaseDriver.cs`
  - [ ] Implement all `IDatabaseDriver` methods
  - [ ] MySQL-specific schema syntax
  - [ ] Connection string builder
  - [ ] SSL/TLS support
- [ ] Add NuGet reference: `MySqlConnector`

### 1.4 PostgreSQL Implementation

- [ ] Create `src/Switchboard.Core/Database/Postgres/` directory
- [ ] Create `PostgresDatabaseDriver.cs`
  - [ ] Implement all `IDatabaseDriver` methods
  - [ ] PostgreSQL-specific schema syntax (SERIAL, TEXT, etc.)
  - [ ] Connection string builder
  - [ ] SSL/TLS support
- [ ] Add NuGet reference: `Npgsql`

### 1.5 SQL Server Implementation

- [ ] Create `src/Switchboard.Core/Database/SqlServer/` directory
- [ ] Create `SqlServerDatabaseDriver.cs`
  - [ ] Implement all `IDatabaseDriver` methods
  - [ ] SQL Server-specific schema syntax (NVARCHAR, IDENTITY, etc.)
  - [ ] Connection string builder
  - [ ] Windows/SQL authentication support
- [ ] Add NuGet reference: `Microsoft.Data.SqlClient`

### 1.6 Database Factory

- [ ] Create `DatabaseDriverFactory.cs`
  - [ ] `static IDatabaseDriver Create(DatabaseSettings settings)`
  - [ ] Support for connection string or individual parameters
  - [ ] Validation of required parameters per database type

---

## Phase 2: Data Models and Migration

### 2.1 Database Entity Models

Create models in `src/Switchboard.Core/Models/`:

- [ ] `OriginServer.cs` - Origin server configuration
  - [ ] `string Identifier` (Primary Key)
  - [ ] `string Hostname`
  - [ ] `int Port`
  - [ ] `bool Ssl`
  - [ ] `int HealthCheckIntervalMs`
  - [ ] `string HealthCheckMethod`
  - [ ] `string HealthCheckUrl`
  - [ ] `int UnhealthyThreshold`
  - [ ] `int HealthyThreshold`
  - [ ] `int MaxParallelRequests`
  - [ ] `int RateLimitRequestsThreshold`
  - [ ] `bool LogRequest`
  - [ ] `bool LogRequestBody`
  - [ ] `bool LogResponse`
  - [ ] `bool LogResponseBody`
  - [ ] `DateTime CreatedUtc`
  - [ ] `DateTime? ModifiedUtc`

- [ ] `ApiEndpoint.cs` - API endpoint configuration
  - [ ] `string Identifier` (Primary Key)
  - [ ] `string Name`
  - [ ] `int TimeoutMs`
  - [ ] `string LoadBalancingMode` (RoundRobin, Random)
  - [ ] `bool BlockHttp10`
  - [ ] `int MaxRequestBodySize`
  - [ ] `bool LogRequestFull`
  - [ ] `bool LogRequestBody`
  - [ ] `bool LogResponseBody`
  - [ ] `bool IncludeAuthContextHeader`
  - [ ] `string? AuthContextHeader`
  - [ ] `DateTime CreatedUtc`
  - [ ] `DateTime? ModifiedUtc`

- [ ] `EndpointOriginMapping.cs` - Many-to-many relationship
  - [ ] `int Id` (Primary Key, Auto-increment)
  - [ ] `string EndpointIdentifier` (Foreign Key)
  - [ ] `string OriginIdentifier` (Foreign Key)
  - [ ] `int SortOrder`

- [ ] `EndpointRoute.cs` - URL routes for endpoints
  - [ ] `int Id` (Primary Key, Auto-increment)
  - [ ] `string EndpointIdentifier` (Foreign Key)
  - [ ] `string HttpMethod` (GET, POST, PUT, DELETE, PATCH, HEAD, OPTIONS)
  - [ ] `string UrlPattern` (parameterized URL)
  - [ ] `bool RequiresAuthentication`
  - [ ] `int SortOrder`

- [ ] `UrlRewrite.cs` - URL rewrite rules
  - [ ] `int Id` (Primary Key, Auto-increment)
  - [ ] `string EndpointIdentifier` (Foreign Key)
  - [ ] `string HttpMethod`
  - [ ] `string SourcePattern`
  - [ ] `string TargetPattern`

- [ ] `BlockedHeader.cs` - Globally blocked headers
  - [ ] `int Id` (Primary Key, Auto-increment)
  - [ ] `string HeaderName`

- [ ] `RequestHistory.cs` - Request history records
  - [ ] `Guid RequestId` (Primary Key)
  - [ ] `DateTime TimestampUtc`
  - [ ] `string HttpMethod`
  - [ ] `string RequestPath`
  - [ ] `string? QueryString`
  - [ ] `string? EndpointIdentifier`
  - [ ] `string? OriginIdentifier`
  - [ ] `string? ClientIp`
  - [ ] `long RequestBodySize`
  - [ ] `string? RequestBody` (nullable, configurable)
  - [ ] `string? RequestHeaders` (JSON)
  - [ ] `int StatusCode`
  - [ ] `long ResponseBodySize`
  - [ ] `string? ResponseBody` (nullable, configurable)
  - [ ] `string? ResponseHeaders` (JSON)
  - [ ] `long DurationMs`
  - [ ] `bool WasAuthenticated`
  - [ ] `string? ErrorMessage`

### 2.2 Runtime State Models

These models hold runtime state (not persisted):

- [ ] Create/update `OriginServerState.cs`
  - [ ] `bool Healthy`
  - [ ] `int ActiveRequests`
  - [ ] `int PendingRequests`
  - [ ] `int HealthCheckSuccess`
  - [ ] `int HealthCheckFailure`
  - [ ] `SemaphoreSlim Semaphore`
  - [ ] `object Lock`

- [ ] Create/update `ApiEndpointState.cs`
  - [ ] `int LastIndex` (round-robin state)
  - [ ] `object Lock`

### 2.3 Configuration to Database Migration Service

- [ ] Create `src/Switchboard.Core/Services/ConfigurationMigrationService.cs`
  - [ ] `Task ImportFromJsonAsync(string jsonPath, bool overwrite = false)`
  - [ ] `Task<string> ExportToJsonAsync()` (returns JSON string)
  - [ ] `Task ExportToJsonFileAsync(string outputPath)`
  - [ ] Validation of imported configuration
  - [ ] Handling of duplicate identifiers
  - [ ] Transaction support for atomic import

### 2.4 Schema Management

- [ ] Create database schema scripts for each database type
  - [ ] `Scripts/schema-sqlite.sql`
  - [ ] `Scripts/schema-mysql.sql`
  - [ ] `Scripts/schema-postgres.sql`
  - [ ] `Scripts/schema-sqlserver.sql`
- [ ] Schema version table for migrations
- [ ] Incremental migration support

---

## Phase 3: Management API

### 3.1 API Route Structure

Create routes in `src/Switchboard.Server/API/`:

**Note:** The base path is configurable via `Management.BasePath` in settings (default: `/_sb/v1.0/`). All routes below are relative to this configurable prefix. This structure allows for future API versioning (e.g., `/_sb/v2.0/`).

```
{BasePath}/   (default: /_sb/v1.0/)
├── origins/                    # Origin server CRUD
├── endpoints/                  # API endpoint CRUD
├── endpoints/{id}/routes/      # Endpoint routes CRUD
├── endpoints/{id}/origins/     # Endpoint-origin mappings
├── endpoints/{id}/rewrites/    # URL rewrite rules
├── headers/blocked/            # Blocked headers CRUD
├── history/                    # Request history queries
├── config/                     # Import/export operations
└── health/                     # System health status
```

### 3.2 Origin Server Endpoints

- [ ] Create `OriginRoutes.cs`
- [ ] `PUT /v1.0/origins` - Create origin server
  - [ ] Request body: `OriginServer` (without Id, CreatedUtc)
  - [ ] Response: Created `OriginServer` with all fields
  - [ ] Validation: Unique identifier, valid hostname, valid port
- [ ] `GET /v1.0/origins` - List all origin servers
  - [ ] Query params: `skip`, `take`, `searchTerm`
  - [ ] Response: Array of `OriginServer` with runtime health status
- [ ] `GET /v1.0/origins/{identifier}` - Get origin by identifier
  - [ ] Include runtime state (healthy, activeRequests, etc.)
- [ ] `HEAD /v1.0/origins/{identifier}` - Check origin exists
- [ ] `POST /v1.0/origins/{identifier}` - Update origin server
  - [ ] Partial update support
  - [ ] Apply changes to runtime state
- [ ] `DELETE /v1.0/origins/{identifier}` - Delete origin server
  - [ ] Validate no endpoints reference this origin
  - [ ] Or cascade delete with confirmation flag

### 3.3 API Endpoint Endpoints

- [ ] Create `EndpointRoutes.cs`
- [ ] `PUT /v1.0/endpoints` - Create API endpoint
- [ ] `GET /v1.0/endpoints` - List all endpoints
  - [ ] Include route count, origin count
- [ ] `GET /v1.0/endpoints/{identifier}` - Get endpoint with full details
  - [ ] Include routes, origin mappings, rewrite rules
- [ ] `HEAD /v1.0/endpoints/{identifier}` - Check endpoint exists
- [ ] `POST /v1.0/endpoints/{identifier}` - Update endpoint
- [ ] `DELETE /v1.0/endpoints/{identifier}` - Delete endpoint
  - [ ] Cascade delete routes, mappings, rewrites

### 3.4 Endpoint Routes (URL Patterns)

- [ ] Create `EndpointRouteRoutes.cs`
- [ ] `PUT /v1.0/endpoints/{endpointId}/routes` - Add route
- [ ] `GET /v1.0/endpoints/{endpointId}/routes` - List routes
- [ ] `GET /v1.0/endpoints/{endpointId}/routes/{routeId}` - Get route
- [ ] `POST /v1.0/endpoints/{endpointId}/routes/{routeId}` - Update route
- [ ] `DELETE /v1.0/endpoints/{endpointId}/routes/{routeId}` - Delete route

### 3.5 Endpoint-Origin Mappings

- [ ] Create `EndpointOriginRoutes.cs`
- [ ] `PUT /v1.0/endpoints/{endpointId}/origins` - Add origin mapping
- [ ] `GET /v1.0/endpoints/{endpointId}/origins` - List mapped origins
- [ ] `DELETE /v1.0/endpoints/{endpointId}/origins/{originId}` - Remove mapping
- [ ] `POST /v1.0/endpoints/{endpointId}/origins/reorder` - Reorder origins

### 3.6 URL Rewrite Rules

- [ ] Create `RewriteRoutes.cs`
- [ ] `PUT /v1.0/endpoints/{endpointId}/rewrites` - Add rewrite rule
- [ ] `GET /v1.0/endpoints/{endpointId}/rewrites` - List rewrite rules
- [ ] `POST /v1.0/endpoints/{endpointId}/rewrites/{rewriteId}` - Update rule
- [ ] `DELETE /v1.0/endpoints/{endpointId}/rewrites/{rewriteId}` - Delete rule

### 3.7 Blocked Headers

- [ ] Create `HeaderRoutes.cs`
- [ ] `PUT /v1.0/headers/blocked` - Add blocked header
- [ ] `GET /v1.0/headers/blocked` - List blocked headers
- [ ] `DELETE /v1.0/headers/blocked/{headerName}` - Remove blocked header

### 3.8 Configuration Import/Export

- [ ] Create `ConfigRoutes.cs`
- [ ] `GET /v1.0/config/export` - Export configuration as JSON
  - [ ] Response: Complete configuration in legacy JSON format
- [ ] `POST /v1.0/config/import` - Import configuration from JSON
  - [ ] Request body: JSON configuration
  - [ ] Query param: `overwrite=true/false`
  - [ ] Validation and error reporting
- [ ] `POST /v1.0/config/validate` - Validate configuration JSON
  - [ ] Returns validation errors without importing

### 3.9 System Health and Status

- [ ] Create `HealthRoutes.cs`
- [ ] `GET /v1.0/health` - Overall system health
  - [ ] Database connection status
  - [ ] Origin server health summary
  - [ ] Request rate metrics
- [ ] `GET /v1.0/health/origins` - Origin health details
  - [ ] Per-origin health status, active requests, etc.
- [ ] `GET /v1.0/stats` - System statistics
  - [ ] Total requests processed
  - [ ] Requests per endpoint
  - [ ] Error rates

### 3.10 Management API Authentication

- [ ] Implement management API authentication
  - [ ] Option 1: Separate admin bearer token (`AdminBearerToken` in settings)
  - [ ] Option 2: Basic authentication
  - [ ] Option 3: Reuse existing callback system
- [ ] Create `ManagementAuthenticationMiddleware.cs`
- [ ] Configure authentication in settings file

---

## Phase 4: Request History System

### 4.1 History Capture Service

- [ ] Create `src/Switchboard.Core/Services/HistoryService.cs`
  - [ ] `Task RecordAsync(RequestHistory record, CancellationToken token = default)`
  - [ ] Configurable body capture (none, truncated, full)
  - [ ] Configurable body size limit
  - [ ] Async/fire-and-forget recording (don't block request)
  - [ ] In-memory buffer with background flush to database

### 4.2 History Integration in GatewayService

- [ ] Modify `GatewayService.cs` to capture request history
  - [ ] Capture at start of `DefaultRoute()`:
    - [ ] Timestamp
    - [ ] HTTP method
    - [ ] Request path
    - [ ] Query string
    - [ ] Client IP (x-forwarded-for or remote address)
    - [ ] Request headers
    - [ ] Request body (if configured)
  - [ ] Capture at end of `DefaultRoute()`:
    - [ ] Selected endpoint identifier
    - [ ] Selected origin identifier
    - [ ] Status code
    - [ ] Response headers
    - [ ] Response body (if configured)
    - [ ] Duration (already tracked with Stopwatch)
    - [ ] Authentication status
    - [ ] Error message (if any)
  - [ ] Handle streaming/chunked responses appropriately

### 4.3 History Query API

- [ ] Create `HistoryRoutes.cs`
- [ ] `GET /v1.0/history` - Query request history
  - [ ] Query params:
    - [ ] `startTime` / `endTime` (ISO 8601)
    - [ ] `endpointIdentifier`
    - [ ] `originIdentifier`
    - [ ] `statusCode` (exact or range: `4xx`, `5xx`)
    - [ ] `httpMethod`
    - [ ] `pathContains`
    - [ ] `minDurationMs`
    - [ ] `skip` / `take`
    - [ ] `orderBy` (timestamp, duration, statusCode)
    - [ ] `orderDirection` (asc, desc)
  - [ ] Response: Paginated list with total count
- [ ] `GET /v1.0/history/{requestId}` - Get full request detail
  - [ ] Include request/response bodies if captured
- [ ] `DELETE /v1.0/history` - Purge history
  - [ ] Query params: `olderThan` (ISO 8601 date)
  - [ ] Requires confirmation flag

### 4.4 History Retention Policy

- [ ] Create `HistoryRetentionService.cs`
  - [ ] Background task for automatic cleanup
  - [ ] Configurable retention period (hours/days)
  - [ ] Configurable max record count
  - [ ] Run interval configuration

### 4.5 History Settings

Add to settings file:

- [ ] `History.Enabled` (bool)
- [ ] `History.CaptureRequestBody` (bool)
- [ ] `History.CaptureResponseBody` (bool)
- [ ] `History.MaxBodySizeBytes` (int)
- [ ] `History.RetentionHours` (int)
- [ ] `History.MaxRecords` (int)
- [ ] `History.FlushIntervalMs` (int)

---

## Phase 5: Dashboard Frontend

### 5.1 Project Setup

- [ ] Create `dashboard/` directory in project root
- [ ] Initialize Vite + React project
  ```bash
  npm create vite@latest dashboard -- --template react
  ```
- [ ] Install dependencies:
  - [ ] `react-router-dom` (routing)
  - [ ] `axios` (HTTP client)
  - [ ] `prop-types` (type checking)
- [ ] Configure project structure per FRONTEND_ARCHITECTURE.md
- [ ] Set up CSS variables and theming (light/dark)
- [ ] Configure Vite proxy for development

### 5.2 Core Components

- [ ] Create `src/components/common/`
  - [ ] `Header.jsx` - Top navigation bar
  - [ ] `Sidebar.jsx` - Side navigation menu
  - [ ] `DataTable.jsx` - Sortable, paginated table
  - [ ] `Modal.jsx` - Modal dialog
  - [ ] `Toast.jsx` - Toast notifications
  - [ ] `LoadingSpinner.jsx` - Loading indicator
  - [ ] `ErrorMessage.jsx` - Error display
  - [ ] `ConfirmDialog.jsx` - Confirmation modal
  - [ ] `Badge.jsx` - Status badges
  - [ ] `HealthIndicator.jsx` - Health status indicator

### 5.3 Context Providers

- [ ] Create `src/context/`
  - [ ] `AuthContext.jsx` - Authentication state, server URL, token
  - [ ] `AppContext.jsx` - Sidebar state, notifications, theme

### 5.4 API Client

- [ ] Create `src/utils/api.js`
  - [ ] `ApiClient` class with methods:
    - [ ] `validateToken()`
    - [ ] **Origins**
      - [ ] `getOrigins(options)`
      - [ ] `getOrigin(identifier)`
      - [ ] `createOrigin(data)`
      - [ ] `updateOrigin(identifier, data)`
      - [ ] `deleteOrigin(identifier)`
    - [ ] **Endpoints**
      - [ ] `getEndpoints(options)`
      - [ ] `getEndpoint(identifier)`
      - [ ] `createEndpoint(data)`
      - [ ] `updateEndpoint(identifier, data)`
      - [ ] `deleteEndpoint(identifier)`
    - [ ] **Routes**
      - [ ] `getEndpointRoutes(endpointId)`
      - [ ] `createEndpointRoute(endpointId, data)`
      - [ ] `updateEndpointRoute(endpointId, routeId, data)`
      - [ ] `deleteEndpointRoute(endpointId, routeId)`
    - [ ] **Origin Mappings**
      - [ ] `getEndpointOrigins(endpointId)`
      - [ ] `addEndpointOrigin(endpointId, originId)`
      - [ ] `removeEndpointOrigin(endpointId, originId)`
    - [ ] **History**
      - [ ] `getHistory(filters)`
      - [ ] `getHistoryDetail(requestId)`
      - [ ] `purgeHistory(olderThan)`
    - [ ] **Config**
      - [ ] `exportConfig()`
      - [ ] `importConfig(json, overwrite)`
      - [ ] `validateConfig(json)`
    - [ ] **Health**
      - [ ] `getHealth()`
      - [ ] `getOriginHealth()`
      - [ ] `getStats()`

### 5.5 Login Page

- [ ] Create `src/components/Login.jsx`
  - [ ] Server URL input field
  - [ ] API token input field
  - [ ] Connect button
  - [ ] Error message display
  - [ ] Remember server URL in localStorage
  - [ ] Responsive layout

### 5.6 Dashboard Layout

- [ ] Create `src/components/Dashboard.jsx`
  - [ ] Header with logo, theme toggle, logout
  - [ ] Collapsible sidebar navigation
  - [ ] Main content area with routing
  - [ ] Section-based navigation:
    - [ ] Overview
    - [ ] Origin Servers
    - [ ] API Endpoints
    - [ ] Request History
    - [ ] Configuration
    - [ ] Settings

### 5.7 Overview View

- [ ] Create `src/components/views/OverviewView.jsx`
  - [ ] System health status card
  - [ ] Origin server health summary (healthy/unhealthy counts)
  - [ ] Endpoint count
  - [ ] Recent request statistics (last hour)
  - [ ] Quick links to common actions
  - [ ] Auto-refresh every 30 seconds

### 5.8 Origin Servers View

- [ ] Create `src/components/views/OriginsView.jsx`
  - [ ] Data table with columns:
    - [ ] Health status indicator
    - [ ] Identifier
    - [ ] Hostname:Port
    - [ ] SSL status
    - [ ] Active/Pending requests
    - [ ] Actions (Edit, Delete)
  - [ ] Search/filter functionality
  - [ ] "Add Origin" button
  - [ ] Sorting by column

- [ ] Create `src/components/editors/OriginEditor.jsx`
  - [ ] Form fields for all origin properties
  - [ ] Health check configuration section
  - [ ] Rate limiting configuration section
  - [ ] Logging configuration section
  - [ ] Validation feedback
  - [ ] Save/Cancel buttons

### 5.9 API Endpoints View

- [ ] Create `src/components/views/EndpointsView.jsx`
  - [ ] Data table with columns:
    - [ ] Identifier
    - [ ] Name
    - [ ] Route count
    - [ ] Origin count
    - [ ] Load balancing mode
    - [ ] Actions (Edit, Delete)
  - [ ] Search/filter functionality
  - [ ] "Add Endpoint" button

- [ ] Create `src/components/editors/EndpointEditor.jsx`
  - [ ] Basic info section (identifier, name, timeout)
  - [ ] Load balancing configuration
  - [ ] Routes management sub-section
    - [ ] List of routes (method + URL pattern)
    - [ ] Add/Edit/Delete routes inline
    - [ ] Auth required toggle per route
  - [ ] Origin servers sub-section
    - [ ] Assigned origins with drag-to-reorder
    - [ ] Add/Remove origin assignment
  - [ ] URL rewrite rules sub-section
    - [ ] List of rewrite rules
    - [ ] Add/Edit/Delete rules
  - [ ] Logging configuration
  - [ ] Authentication header configuration

- [ ] Create `src/components/editors/RouteEditor.jsx`
  - [ ] HTTP method dropdown
  - [ ] URL pattern input with placeholder hints
  - [ ] Authentication required toggle
  - [ ] Validation for URL pattern syntax

### 5.10 Request History View

- [ ] Create `src/components/views/HistoryView.jsx`
  - [ ] Filter panel:
    - [ ] Date/time range picker
    - [ ] Endpoint dropdown
    - [ ] Origin dropdown
    - [ ] Status code filter (200, 4xx, 5xx, custom)
    - [ ] HTTP method filter
    - [ ] Path contains search
    - [ ] Min duration filter
  - [ ] Data table with columns:
    - [ ] Timestamp
    - [ ] Method
    - [ ] Path (truncated)
    - [ ] Endpoint
    - [ ] Origin
    - [ ] Status code (color-coded)
    - [ ] Duration (ms)
  - [ ] Click row to view details
  - [ ] Pagination controls
  - [ ] "Purge History" button with confirmation

- [ ] Create `src/components/history/RequestDetail.jsx`
  - [ ] Modal or slide-out panel
  - [ ] Request info section:
    - [ ] Full URL
    - [ ] Client IP
    - [ ] Headers (collapsible)
    - [ ] Body (collapsible, syntax highlighted if JSON)
  - [ ] Response info section:
    - [ ] Status code with text
    - [ ] Headers (collapsible)
    - [ ] Body (collapsible, syntax highlighted if JSON)
  - [ ] Timing info:
    - [ ] Duration
    - [ ] Timestamp
  - [ ] Copy buttons for request/response

### 5.11 Configuration View

- [ ] Create `src/components/views/ConfigView.jsx`
  - [ ] Export section:
    - [ ] "Export Configuration" button
    - [ ] Downloads JSON file
  - [ ] Import section:
    - [ ] File upload dropzone
    - [ ] "Validate" button
    - [ ] Validation results display
    - [ ] "Import" button with overwrite option
    - [ ] Import results/errors display
  - [ ] JSON preview of current configuration

### 5.12 Settings View

- [ ] Create `src/components/views/SettingsView.jsx`
  - [ ] Blocked headers management:
    - [ ] List current blocked headers
    - [ ] Add/Remove headers
  - [ ] History settings (if configurable via API):
    - [ ] Enable/disable history
    - [ ] Body capture settings
    - [ ] Retention settings
  - [ ] Theme selection
  - [ ] Server connection info display

### 5.13 Routing

- [ ] Create `src/App.jsx` with routes:
  - [ ] `/` - Login (public)
  - [ ] `/dashboard` - Overview (protected)
  - [ ] `/dashboard/origins` - Origins list (protected)
  - [ ] `/dashboard/origins/:id` - Origin editor (protected)
  - [ ] `/dashboard/endpoints` - Endpoints list (protected)
  - [ ] `/dashboard/endpoints/:id` - Endpoint editor (protected)
  - [ ] `/dashboard/history` - Request history (protected)
  - [ ] `/dashboard/config` - Configuration (protected)
  - [ ] `/dashboard/settings` - Settings (protected)
  - [ ] `*` - Redirect to login

### 5.14 Styling

- [ ] Create `src/index.css` with CSS variables
  - [ ] Light theme colors
  - [ ] Dark theme colors
  - [ ] Typography scale
  - [ ] Spacing scale
  - [ ] Border radius
  - [ ] Shadows
  - [ ] Layout variables (sidebar width, header height)

- [ ] Create component-specific CSS files:
  - [ ] `Login.css`
  - [ ] `Dashboard.css`
  - [ ] `DataTable.css`
  - [ ] `Modal.css`
  - [ ] `Header.css`
  - [ ] `Sidebar.css`
  - [ ] `OriginEditor.css`
  - [ ] `EndpointEditor.css`
  - [ ] `HistoryView.css`

### 5.15 Build Configuration

- [ ] Create `vite.config.js`
  - [ ] Path aliases (@, @components, @utils, etc.)
  - [ ] Development proxy to backend
  - [ ] Production build optimization
- [ ] Create `package.json` scripts:
  - [ ] `dev` - Development server
  - [ ] `build` - Production build
  - [ ] `preview` - Preview production build
  - [ ] `lint` - ESLint

---

## Phase 6: Integration and Testing

### 6.1 Backend Integration

- [ ] Update `SwitchboardDaemon.cs` to:
  - [ ] Accept database configuration in settings
  - [ ] Initialize database driver
  - [ ] Load configuration from database on startup
  - [ ] Register management routes
  - [ ] Initialize history service

- [ ] Update `SwitchboardSettings.cs` to include:
  - [ ] `Database` section:
    - [ ] `Type` (Sqlite, Mysql, Postgres, SqlServer)
    - [ ] `ConnectionString` (for advanced configuration)
    - [ ] `Host`, `Port`, `Database`, `Username`, `Password` (simple configuration)
    - [ ] `SslMode` (for MySQL/PostgreSQL)
  - [ ] `Management` section:
    - [ ] `Enabled` (bool)
    - [ ] `BasePath` (string, default `/_sb/v1.0/`, configurable prefix for all management routes)
    - [ ] `AdminToken` (string)
    - [ ] `AllowedOrigins` (CORS)
  - [ ] `History` section (as defined above)

- [ ] Create migration path from JSON to database:
  - [ ] If database is empty and JSON exists, auto-import
  - [ ] Command-line flag for manual import
  - [ ] Backup existing JSON before migration

### 6.2 Runtime Configuration Sync

- [ ] Implement `ConfigurationSyncService.cs`
  - [ ] Load all configuration from database into memory
  - [ ] Initialize runtime state objects
  - [ ] Watch for configuration changes (polling or notification)
  - [ ] Apply changes without restart:
    - [ ] New origin: Add to runtime with initial state
    - [ ] Updated origin: Update config, preserve health state
    - [ ] Deleted origin: Remove from runtime, drain connections
    - [ ] New endpoint: Add to routing table
    - [ ] Updated endpoint: Update routing, preserve round-robin state
    - [ ] Deleted endpoint: Remove from routing
    - [ ] New route: Add to endpoint's route table
    - [ ] Updated route: Update pattern matching
    - [ ] Deleted route: Remove from pattern matching

### 6.3 Thread Safety Verification

- [ ] Review all configuration modification code paths for thread safety
- [ ] Ensure locking is consistent with existing patterns
- [ ] Test concurrent configuration changes and requests
- [ ] Test configuration changes under load

### 6.4 Unit Tests

Create tests in `src/Test.Automated/`:

- [ ] Database driver tests:
  - [ ] `SqliteDatabaseDriverTests.cs`
  - [ ] `MysqlDatabaseDriverTests.cs` (requires MySQL)
  - [ ] `PostgresDatabaseDriverTests.cs` (requires PostgreSQL)
  - [ ] `SqlServerDatabaseDriverTests.cs` (requires SQL Server)
- [ ] Model validation tests
- [ ] Configuration migration tests
- [ ] History service tests
- [ ] API route tests (integration)

### 6.5 Manual Testing

- [ ] Test full workflow: JSON import -> database -> API CRUD
- [ ] Test dashboard: login, CRUD operations, history viewing
- [ ] Test proxy functionality still works correctly
- [ ] Test health checking still works correctly
- [ ] Test load balancing still works correctly
- [ ] Test authentication callback still works correctly
- [ ] Test configuration export matches import

---

## Phase 7: Documentation and Deployment

### 7.1 API Documentation

- [ ] Create `docs/MANAGEMENT-API.md`
  - [ ] Authentication section
  - [ ] Origins API reference
  - [ ] Endpoints API reference
  - [ ] Routes API reference
  - [ ] History API reference
  - [ ] Config API reference
  - [ ] Health API reference
  - [ ] Error response formats

### 7.2 Dashboard Documentation

- [ ] Create `docs/DASHBOARD.md`
  - [ ] Installation instructions
  - [ ] Configuration options
  - [ ] Feature walkthrough
  - [ ] Screenshots

### 7.3 Migration Guide

- [ ] Create `docs/MIGRATION.md`
  - [ ] Pre-migration checklist
  - [ ] Backup procedures
  - [ ] Step-by-step migration process
  - [ ] Rollback procedures
  - [ ] Troubleshooting

### 7.4 Docker Configuration

- [ ] Create `docker/dashboard/Dockerfile`
  - [ ] Multi-stage build (build + nginx)
  - [ ] nginx.conf for SPA routing
- [ ] Update `docker/compose.yaml`
  - [ ] Add dashboard service
  - [ ] Configure networking between services
  - [ ] Volume mounts for database persistence
- [ ] Create database-specific compose files:
  - [ ] `compose.sqlite.yaml`
  - [ ] `compose.mysql.yaml`
  - [ ] `compose.postgres.yaml`
  - [ ] `compose.sqlserver.yaml`

### 7.5 Release Preparation

- [ ] Update `README.md` with new features
- [ ] Update `CHANGELOG.md`
- [ ] Update NuGet package version
- [ ] Create release notes
- [ ] Tag release in git

---

## Appendix: Data Models

### Database Schema (SQLite Example)

```sql
-- Origin Servers
CREATE TABLE origin_servers (
    identifier TEXT PRIMARY KEY,
    hostname TEXT NOT NULL,
    port INTEGER NOT NULL,
    ssl INTEGER NOT NULL DEFAULT 0,
    health_check_interval_ms INTEGER NOT NULL DEFAULT 5000,
    health_check_method TEXT NOT NULL DEFAULT 'HEAD',
    health_check_url TEXT NOT NULL DEFAULT '/',
    unhealthy_threshold INTEGER NOT NULL DEFAULT 2,
    healthy_threshold INTEGER NOT NULL DEFAULT 1,
    max_parallel_requests INTEGER NOT NULL DEFAULT 10,
    rate_limit_requests_threshold INTEGER NOT NULL DEFAULT 30,
    log_request INTEGER NOT NULL DEFAULT 0,
    log_request_body INTEGER NOT NULL DEFAULT 0,
    log_response INTEGER NOT NULL DEFAULT 0,
    log_response_body INTEGER NOT NULL DEFAULT 0,
    created_utc TEXT NOT NULL,
    modified_utc TEXT
);

-- API Endpoints
CREATE TABLE api_endpoints (
    identifier TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    timeout_ms INTEGER NOT NULL DEFAULT 60000,
    load_balancing_mode TEXT NOT NULL DEFAULT 'RoundRobin',
    block_http10 INTEGER NOT NULL DEFAULT 0,
    max_request_body_size INTEGER NOT NULL DEFAULT 536870912,
    log_request_full INTEGER NOT NULL DEFAULT 0,
    log_request_body INTEGER NOT NULL DEFAULT 0,
    log_response_body INTEGER NOT NULL DEFAULT 0,
    include_auth_context_header INTEGER NOT NULL DEFAULT 0,
    auth_context_header TEXT,
    created_utc TEXT NOT NULL,
    modified_utc TEXT
);

-- Endpoint-Origin Mappings
CREATE TABLE endpoint_origin_mappings (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    endpoint_identifier TEXT NOT NULL,
    origin_identifier TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE,
    FOREIGN KEY (origin_identifier) REFERENCES origin_servers(identifier) ON DELETE CASCADE,
    UNIQUE (endpoint_identifier, origin_identifier)
);

-- Endpoint Routes
CREATE TABLE endpoint_routes (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    endpoint_identifier TEXT NOT NULL,
    http_method TEXT NOT NULL,
    url_pattern TEXT NOT NULL,
    requires_authentication INTEGER NOT NULL DEFAULT 0,
    sort_order INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE
);

-- URL Rewrite Rules
CREATE TABLE url_rewrites (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    endpoint_identifier TEXT NOT NULL,
    http_method TEXT NOT NULL,
    source_pattern TEXT NOT NULL,
    target_pattern TEXT NOT NULL,
    FOREIGN KEY (endpoint_identifier) REFERENCES api_endpoints(identifier) ON DELETE CASCADE
);

-- Blocked Headers
CREATE TABLE blocked_headers (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    header_name TEXT NOT NULL UNIQUE
);

-- Request History
CREATE TABLE request_history (
    request_id TEXT PRIMARY KEY,
    timestamp_utc TEXT NOT NULL,
    http_method TEXT NOT NULL,
    request_path TEXT NOT NULL,
    query_string TEXT,
    endpoint_identifier TEXT,
    origin_identifier TEXT,
    client_ip TEXT,
    request_body_size INTEGER NOT NULL DEFAULT 0,
    request_body TEXT,
    request_headers TEXT,
    status_code INTEGER NOT NULL,
    response_body_size INTEGER NOT NULL DEFAULT 0,
    response_body TEXT,
    response_headers TEXT,
    duration_ms INTEGER NOT NULL,
    was_authenticated INTEGER NOT NULL DEFAULT 0,
    error_message TEXT
);

-- Indexes for history queries
CREATE INDEX idx_history_timestamp ON request_history(timestamp_utc);
CREATE INDEX idx_history_endpoint ON request_history(endpoint_identifier);
CREATE INDEX idx_history_status ON request_history(status_code);

-- Schema version tracking
CREATE TABLE schema_version (
    version INTEGER PRIMARY KEY,
    applied_utc TEXT NOT NULL
);

INSERT INTO schema_version (version, applied_utc) VALUES (1, datetime('now'));
```

---

## Appendix: API Reference

### Response Formats

**Success Response:**
```json
{
  "success": true,
  "data": { ... }
}
```

**Error Response:**
```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Identifier already exists",
    "details": { ... }
  }
}
```

**Paginated Response:**
```json
{
  "success": true,
  "data": [ ... ],
  "pagination": {
    "skip": 0,
    "take": 25,
    "total": 100,
    "hasMore": true
  }
}
```

### API Endpoints Summary

| Method | Path | Description |
|--------|------|-------------|
| **Origins** |||
| PUT | `/_sb/v1.0/origins` | Create origin server |
| GET | `/_sb/v1.0/origins` | List origins |
| GET | `/_sb/v1.0/origins/{id}` | Get origin |
| POST | `/_sb/v1.0/origins/{id}` | Update origin |
| DELETE | `/_sb/v1.0/origins/{id}` | Delete origin |
| **Endpoints** |||
| PUT | `/_sb/v1.0/endpoints` | Create endpoint |
| GET | `/_sb/v1.0/endpoints` | List endpoints |
| GET | `/_sb/v1.0/endpoints/{id}` | Get endpoint |
| POST | `/_sb/v1.0/endpoints/{id}` | Update endpoint |
| DELETE | `/_sb/v1.0/endpoints/{id}` | Delete endpoint |
| **Routes** |||
| PUT | `/_sb/v1.0/endpoints/{id}/routes` | Create route |
| GET | `/_sb/v1.0/endpoints/{id}/routes` | List routes |
| POST | `/_sb/v1.0/endpoints/{id}/routes/{rid}` | Update route |
| DELETE | `/_sb/v1.0/endpoints/{id}/routes/{rid}` | Delete route |
| **Origin Mappings** |||
| PUT | `/_sb/v1.0/endpoints/{id}/origins` | Add origin |
| GET | `/_sb/v1.0/endpoints/{id}/origins` | List origins |
| DELETE | `/_sb/v1.0/endpoints/{id}/origins/{oid}` | Remove origin |
| **Rewrites** |||
| PUT | `/_sb/v1.0/endpoints/{id}/rewrites` | Create rewrite |
| GET | `/_sb/v1.0/endpoints/{id}/rewrites` | List rewrites |
| POST | `/_sb/v1.0/endpoints/{id}/rewrites/{rid}` | Update rewrite |
| DELETE | `/_sb/v1.0/endpoints/{id}/rewrites/{rid}` | Delete rewrite |
| **Headers** |||
| PUT | `/_sb/v1.0/headers/blocked` | Add blocked header |
| GET | `/_sb/v1.0/headers/blocked` | List blocked headers |
| DELETE | `/_sb/v1.0/headers/blocked/{name}` | Remove blocked header |
| **History** |||
| GET | `/_sb/v1.0/history` | Query history |
| GET | `/_sb/v1.0/history/{requestId}` | Get request detail |
| DELETE | `/_sb/v1.0/history` | Purge history |
| **Config** |||
| GET | `/_sb/v1.0/config/export` | Export config JSON |
| POST | `/_sb/v1.0/config/import` | Import config JSON |
| POST | `/_sb/v1.0/config/validate` | Validate config JSON |
| **Health** |||
| GET | `/_sb/v1.0/health` | System health |
| GET | `/_sb/v1.0/health/origins` | Origin health details |
| GET | `/_sb/v1.0/stats` | System statistics |

---

## Progress Tracking

Use the checkboxes throughout this document to track implementation progress. Mark items as complete by changing `[ ]` to `[x]`.

**Phase Status:**
- [ ] Phase 1: Database Infrastructure
- [ ] Phase 2: Data Models and Migration
- [ ] Phase 3: Management API
- [ ] Phase 4: Request History System
- [ ] Phase 5: Dashboard Frontend
- [ ] Phase 6: Integration and Testing
- [ ] Phase 7: Documentation and Deployment
