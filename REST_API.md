# Switchboard REST API Reference

This document is a reference for the Switchboard Management REST API. Field names and capitalization
below match exactly what the server sends and accepts.

## Table of Contents

- [Overview](#overview)
- [Authentication](#authentication)
- [Base URL](#base-url)
- [JSON Conventions & Capitalization](#json-conventions--capitalization)
- [Common Response Formats](#common-response-formats)
- [Error Handling](#error-handling)
- [Endpoints](#endpoints)
  - [Health](#health)
  - [Current User](#current-user)
  - [Origin Servers](#origin-servers)
  - [API Endpoints](#api-endpoints)
  - [Endpoint Routes](#endpoint-routes)
  - [Endpoint-Origin Mappings](#endpoint-origin-mappings)
  - [URL Rewrites](#url-rewrites)
  - [Blocked Headers](#blocked-headers)
  - [Users](#users)
  - [Credentials](#credentials)
  - [Request History](#request-history)
  - [Settings](#settings)
  - [System](#system)
  - [Configuration Validation](#configuration-validation)
- [Data Models](#data-models)

---

## Overview

The Switchboard Management API provides RESTful endpoints for configuring and monitoring the
Switchboard proxy at runtime. All endpoints return JSON and accept JSON request bodies where
applicable.

### Features

- Full CRUD operations for all configuration entities
- Bearer token authentication
- Pagination via `skip` and `take` query parameters
- Search/filtering on applicable endpoints

---

## Authentication

All Management API endpoints require authentication via Bearer token.

### Header Format

```
Authorization: Bearer <token>
```

### Token Sources

1. **Static Admin Token**: Configured in `sb.json` via `Management.AdminToken`
2. **Database Credentials**: Bearer tokens stored in the `Credential` table

### Example

```bash
curl -H "Authorization: Bearer sbadmin" http://localhost:8000/_sb/v1.0/health
```

### First-Time Setup

On first startup, Switchboard creates a default administrator:
- **Username**: `admin`
- **Bearer Token**: `sbadmin` (the value of `Management.AdminToken`)
- **Access**: Full administrator — can create, update, and delete resources. Rotate or replace it
  before running in production.

Write operations (POST/PUT/DELETE) require a credential that is not read-only. A read-only credential
may read but receives `403 Forbidden` (`AuthorizationFailed`) on a write; the session is not ended.

---

## Base URL

The default base path is `/_sb/v1.0/`. This can be configured via `Management.BasePath` in settings.

```
http://localhost:8000/_sb/v1.0/
```

---

## JSON Conventions & Capitalization

Capitalization is **not uniform** across the API, so match these conventions exactly:

| Category | Casing | Examples |
|----------|--------|----------|
| **Resource models** (origins, endpoints, routes, mappings, rewrites, blocked headers, users, credentials, request history) | **PascalCase** | `Identifier`, `Hostname`, `Port`, `Ssl`, `TimestampUtc`, `HttpMethod`, `UrlPattern` |
| **Identifier suffixes** | `GUID` / `Id` (as shown) | `GUID`, `EndpointGUID`, `OriginGUID`, `UserGUID`, `Id` |
| **Error responses** | **PascalCase** | `Error`, `Message`, `StatusCode`, `Description` |
| **Settings config tree** | **PascalCase** | `Logging`, `Webserver`, `Database`, `Management` |
| **Settings metadata arrays** | **camelCase** | `restartRequiredSettings`, `runtimeEditableSettings` |
| **Computed endpoints** (`/health`, `/me`, `/history/stats`, `/history/timeseries`, `/history/cleanup`, `/config/validate`) | **camelCase** | `totalRequests`, `bucketStartUtc`, `firstName` |
| **Query-string parameters** | lowercase / camelCase | `skip`, `take`, `search`, `start`, `end`, `intervalMinutes` |

### Request bodies

Send request-body fields in **PascalCase** (matching the resource models). Origins and endpoints
accept either case, but routes, mappings, rewrites, and blocked headers are **case-sensitive and
require PascalCase** — a camelCase body for those resources fails. When in doubt, use PascalCase
everywhere; it is accepted by every endpoint.

The official dashboard converts automatically (PascalCase on the wire, camelCase internally), so it is
consistent with the shapes documented here.

---

## OpenAPI Documentation

| Endpoint | Description |
|----------|-------------|
| `GET /openapi.json` | OpenAPI 3.0.3 specification document |
| `GET /swagger` | Interactive Swagger UI |

These endpoints do not require authentication, to allow API discovery and tooling integration.

```
http://localhost:8000/swagger
```

---

## Common Response Formats

### Success Responses

| Status Code | Description |
|-------------|-------------|
| `200 OK` | Request succeeded, response body contains data |
| `201 Created` | Resource created successfully |
| `204 No Content` | Request succeeded, no response body |

### Pagination

List endpoints support pagination via query-string parameters (lowercase):

| Parameter | Type | Description |
|-----------|------|-------------|
| `skip` | integer | Number of records to skip (default: 0) |
| `take` | integer | Maximum number of records to return (optional) |

Example:
```
GET /_sb/v1.0/origins?skip=10&take=25
```

---

## Error Handling

### Error Response Structure

Error responses are **PascalCase**:

```json
{
  "Error": "NotFound",
  "Message": "The requested resource was not found.",
  "StatusCode": 404,
  "Description": "Origin server not found"
}
```

`Description` is present when the server has additional context; some errors return only `Error`,
`Message`, and `StatusCode`.

### Error Codes

| Error Code | HTTP Status | Description |
|------------|-------------|-------------|
| `AuthenticationFailed` | 401 | Authentication material was not accepted |
| `AuthorizationFailed` | 403 | Authenticated but lacks permission (e.g. a read-only credential attempting a write) |
| `BadGateway` | 502 | No origin servers available |
| `BadRequest` | 400 | Invalid request (URL, query, or body) |
| `Conflict` | 409 | Operation would create a conflict |
| `DeserializationError` | 400 | Invalid request body format |
| `Inactive` | 401 | Account, credentials, or resource is inactive |
| `InternalError` | 500 | Internal server error |
| `InvalidRange` | 400 | Invalid range specified |
| `InUse` | 409 | Resource is currently in use |
| `NotEmpty` | 400 | Resource is not empty |
| `NotFound` | 404 | Resource not found |
| `SlowDown` | 429 | Rate limit exceeded |
| `TokenExpired` | 401 | Authentication token has expired |
| `TooLarge` | 413 | Request body exceeds size limit |
| `UnsupportedHttpVersion` | 505 | HTTP version not supported |

---

## Endpoints

### Health

#### Get System Health

```
GET /_sb/v1.0/health
```

Returns the health status of the Switchboard instance. Response fields are **camelCase**:

```json
{
  "status": "healthy",
  "timestamp": "2026-07-29T10:30:00.0000000Z",
  "version": "4.1.0"
}
```

---

### Current User

#### Get Current User

```
GET /_sb/v1.0/me
```

Returns information about the currently authenticated user. Response fields are **camelCase**:

```json
{
  "guid": "550e8400-e29b-41d4-a716-446655440000",
  "username": "admin",
  "firstName": "Admin",
  "lastName": "User",
  "isAdmin": true,
  "active": true
}
```

---

### Origin Servers

Origin servers are backend services that Switchboard proxies requests to.

#### List Origin Servers

```
GET /_sb/v1.0/origins
```

**Query Parameters:** `search` (filter by identifier or name), `skip`, `take`

**Response:** Array of [OriginServerConfig](#originserverconfig)

#### Create Origin Server

```
POST /_sb/v1.0/origins
```

**Request Body:** [OriginServerConfig](#originserverconfig) (`GUID` is derived from `Identifier`)

**Response:** `201 Created` with the created [OriginServerConfig](#originserverconfig)

#### Get / Update / Delete Origin Server

```
GET    /_sb/v1.0/origins/{guid}
PUT    /_sb/v1.0/origins/{guid}
DELETE /_sb/v1.0/origins/{guid}
```

`GET`/`PUT` return the [OriginServerConfig](#originserverconfig); `DELETE` returns `204 No Content`.

#### List Origin Server Health

```
GET /_sb/v1.0/origins/health
```

Returns the live health status of every origin server, including uptime, a rolling window of individual
check results, and the most recent error. Health is tracked in memory by the background health checker
and is not persisted. Origins that resolve to the same target (method + scheme + host + port + URL) are
probed by a single shared check and report consistent health.

**Response:** Array of [OriginServerHealthStatus](#originserverhealthstatus)

#### Get Origin Server Health

```
GET /_sb/v1.0/origins/{guid}/health
```

Returns the live health status of a single origin server, addressed by the same deterministic `GUID`
used for its configuration.

**Response:** [OriginServerHealthStatus](#originserverhealthstatus) — `400` for a malformed GUID,
`404` when no origin matches the GUID.

**Example:**

```json
{
  "Identifier": "backend-api-1",
  "GUID": "550e8400-e29b-41d4-a716-446655440000",
  "Name": "Backend API Server 1",
  "Hostname": "api.example.com",
  "Port": 443,
  "IsHealthy": true,
  "FirstCheckUtc": "2026-07-30T12:00:00.0000000Z",
  "LastCheckUtc": "2026-07-30T12:05:00.0000000Z",
  "LastHealthyUtc": "2026-07-30T12:00:02.0000000Z",
  "LastUnhealthyUtc": null,
  "LastStateChangeUtc": "2026-07-30T12:00:02.0000000Z",
  "TotalUptimeMs": 298000,
  "TotalDowntimeMs": 2000,
  "UptimePercentage": 99.33,
  "ConsecutiveSuccesses": 60,
  "ConsecutiveFailures": 0,
  "LastError": null,
  "History": [
    { "TimestampUtc": "2026-07-30T12:04:55.0000000Z", "Success": true },
    { "TimestampUtc": "2026-07-30T12:05:00.0000000Z", "Success": true }
  ]
}
```

---

### API Endpoints

API endpoints define the routes that Switchboard handles and how they map to origin servers.

#### List / Create

```
GET  /_sb/v1.0/endpoints          (query: search, skip, take)
POST /_sb/v1.0/endpoints
```

**Response:** Array of / created [ApiEndpointConfig](#apiendpointconfig)

#### Get / Update / Delete

```
GET    /_sb/v1.0/endpoints/{guid}
PUT    /_sb/v1.0/endpoints/{guid}
DELETE /_sb/v1.0/endpoints/{guid}
```

---

### Endpoint Routes

Routes define URL patterns that map to API endpoints.

```
GET    /_sb/v1.0/routes            (query: skip, take)
POST   /_sb/v1.0/routes
GET    /_sb/v1.0/routes/{id}
PUT    /_sb/v1.0/routes/{id}
DELETE /_sb/v1.0/routes/{id}
```

**Body / Response:** [EndpointRoute](#endpointroute). The `{id}` is the integer primary key. Set
`EndpointGUID` on create (it is required).

---

### Endpoint-Origin Mappings

Mappings associate API endpoints with their backend origin servers.

```
GET    /_sb/v1.0/mappings          (query: skip, take)
POST   /_sb/v1.0/mappings
GET    /_sb/v1.0/mappings/{id}
DELETE /_sb/v1.0/mappings/{id}
```

**Body / Response:** [EndpointOriginMapping](#endpointoriginmapping). There is no `PUT` for mappings.
Set both `EndpointGUID` and `OriginGUID` on create.

---

### URL Rewrites

URL rewrites transform request URLs before forwarding to origin servers.

```
GET    /_sb/v1.0/rewrites          (query: skip, take)
POST   /_sb/v1.0/rewrites
GET    /_sb/v1.0/rewrites/{id}
PUT    /_sb/v1.0/rewrites/{id}
DELETE /_sb/v1.0/rewrites/{id}
```

**Body / Response:** [UrlRewrite](#urlrewrite).

---

### Blocked Headers

Globally blocked headers are not forwarded from client requests to origin servers.

```
GET    /_sb/v1.0/headers           (query: skip, take)
POST   /_sb/v1.0/headers
GET    /_sb/v1.0/headers/{id}
DELETE /_sb/v1.0/headers/{id}
```

**Body / Response:** [BlockedHeader](#blockedheader). There is no `PUT` for blocked headers.

---

### Users

User accounts for Management API access.

```
GET    /_sb/v1.0/users             (query: search, skip, take)
POST   /_sb/v1.0/users
GET    /_sb/v1.0/users/{guid}
PUT    /_sb/v1.0/users/{guid}
DELETE /_sb/v1.0/users/{guid}
```

**Body / Response:** [UserMaster](#usermaster) (`GUID` is auto-generated).

---

### Credentials

Bearer token credentials for API authentication.

```
GET    /_sb/v1.0/credentials                 (query: search, skip, take)
POST   /_sb/v1.0/credentials
GET    /_sb/v1.0/credentials/{guid}
PUT    /_sb/v1.0/credentials/{guid}
DELETE /_sb/v1.0/credentials/{guid}
POST   /_sb/v1.0/credentials/{guid}/regenerate
```

**Body / Response:** [Credential](#credential).

> **Important:** `BearerToken` is only returned on creation and on `regenerate`. Store it securely.
> Read-only credentials cannot be updated, deleted, or regenerated.

---

### Request History

Track and analyze requests passing through Switchboard.

#### List Request History

```
GET /_sb/v1.0/history
```

**Query Parameters:** `skip`, `take`, `start` (ISO 8601), `end` (ISO 8601), `endpoint` (GUID),
`origin` (GUID)

**Response:** Array of [RequestHistory](#requesthistory)

#### Recent / Failed

```
GET /_sb/v1.0/history/recent       (query: count, default 100, max 1000)
GET /_sb/v1.0/history/failed       (query: skip, take)
```

**Response:** Array of [RequestHistory](#requesthistory)

#### Get / Delete by ID

```
GET    /_sb/v1.0/history/{id}
DELETE /_sb/v1.0/history/{id}
```

`{id}` is the request's `RequestId` (a GUID), as returned by the list endpoints. Because each row's
`GUID` mirrors its `RequestId`, either value resolves. `GET` returns the [RequestHistory](#requesthistory);
`DELETE` returns `204 No Content`.

#### Cleanup

```
POST /_sb/v1.0/history/cleanup     (query: days)
```

Deletes records older than `days`. Response is **camelCase**:

```json
{ "deletedRecords": 150 }
```

#### Statistics

```
GET /_sb/v1.0/history/stats
```

Response fields are **camelCase**:

```json
{
  "totalRequests": 10000,
  "failedRequests": 50,
  "successRate": 99.5
}
```

#### Activity Time Series

Bucketed request counts over a window, used by the dashboard's activity chart. Empty buckets are
zero-filled so the series is always fixed-width.

```
GET /_sb/v1.0/history/timeseries?start={iso8601}&end={iso8601}&intervalMinutes=60
```

Query parameters are optional: `end` defaults to now, `start` to 24 hours before `end`, and
`intervalMinutes` to 60 (minimum 1). The response wrapper and buckets are **camelCase**:

```json
{
  "startUtc": "2026-07-28T00:00:00.0000000Z",
  "endUtc": "2026-07-29T00:00:00.0000000Z",
  "intervalMinutes": 60,
  "buckets": [
    { "bucketStartUtc": "2026-07-28T00:00:00.0000000Z", "total": 3, "success": 2, "failure": 1, "avgDurationMs": 200 }
  ]
}
```

---

### Settings

#### Get Settings

```
GET /_sb/v1.0/settings
```

Returns the full server configuration with secrets (`Database.Password`, `Management.AdminToken`)
masked as `"********"`. The top-level configuration objects are **PascalCase** (`Logging`,
`Endpoints`, `Origins`, `BlockedHeaders`, `Webserver`, `OpenApi`, `Database`, `Management`,
`RequestHistory`), plus two **camelCase** metadata arrays:

- `restartRequiredSettings` — dotted paths that only take effect after a restart
- `runtimeEditableSettings` — dotted paths that apply immediately

#### Update Settings

```
PUT /_sb/v1.0/settings
```

Accepts the full settings tree. Runtime-editable fields (logging severity, request-history capture,
blocked headers, management options) apply immediately; restart-required fields are persisted but
take effect only after a restart. A masked secret value (`"********"`) is treated as "unchanged".

---

### System

#### Restart Server

```
POST /_sb/v1.0/system/restart
```

Gracefully restarts the server: returns `202 Accepted`, then the process exits so a supervisor
(for example, Docker's `restart: unless-stopped`) brings it back. Requires an admin (non-read-only)
credential.

---

### Configuration Validation

```
POST /_sb/v1.0/config/validate
```

Validates the current configuration, or a proposed one supplied in the request body
(`{ "endpoints": [...], "origins": [...], "routes": [...], "mappings": [...] }`). Response fields are
**camelCase**:

```json
{
  "valid": false,
  "errors": [ { "code": "OriginNotFound", "message": "...", "endpoint": "e1", "origin": "missing" } ],
  "warnings": [ { "code": "DuplicateOriginAddress", "message": "...", "address": "localhost:8001", "origins": ["a", "b"] } ]
}
```

---

## Data Models

> Field names below are the exact PascalCase keys returned and accepted by the API.

### OriginServerConfig

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `GUID` | string (GUID) | No | Derived from `Identifier` | Unique identifier (deterministic; read-only) |
| `Identifier` | string | Yes | - | Unique identifier for referencing |
| `Name` | string | No | null | Display name |
| `Hostname` | string | Yes | `"localhost"` | Server hostname |
| `Port` | integer | Yes | 8000 | TCP port (0-65535) |
| `Ssl` | boolean | No | false | Enable HTTPS |
| `HealthCheckIntervalMs` | integer | No | 5000 | Health check interval (min: 1000) |
| `HealthCheckMethod` | string | No | `"HEAD"` | HTTP method for health checks |
| `HealthCheckUrl` | string | No | `"/"` | URL path for health checks |
| `UnhealthyThreshold` | integer | No | 2 | Failed checks before marking unhealthy |
| `HealthyThreshold` | integer | No | 1 | Successful checks before marking healthy |
| `MaxParallelRequests` | integer | No | 10 | Maximum concurrent requests |
| `RateLimitRequestsThreshold` | integer | No | 30 | Total requests before rate limiting |
| `LogRequest` | boolean | No | false | Log requests to this origin |
| `LogRequestBody` | boolean | No | false | Log request bodies |
| `LogResponse` | boolean | No | false | Log responses |
| `LogResponseBody` | boolean | No | false | Log response bodies |
| `CaptureRequestBody` | boolean | No | false | Capture request body in history |
| `CaptureResponseBody` | boolean | No | false | Capture response body in history |
| `CaptureRequestHeaders` | boolean | No | true | Capture request headers in history |
| `CaptureResponseHeaders` | boolean | No | true | Capture response headers in history |
| `MaxCaptureRequestBodySize` | integer | No | 65536 | Max request body capture size (bytes) |
| `MaxCaptureResponseBodySize` | integer | No | 65536 | Max response body capture size (bytes) |
| `CreatedUtc` | datetime | No | Current time | Creation timestamp |
| `ModifiedUtc` | datetime | No | null | Last modification timestamp |

**Example:**

```json
{
  "GUID": "550e8400-e29b-41d4-a716-446655440000",
  "Identifier": "backend-api-1",
  "Name": "Backend API Server 1",
  "Hostname": "api.example.com",
  "Port": 443,
  "Ssl": true,
  "HealthCheckMethod": "GET",
  "HealthCheckUrl": "/health",
  "UnhealthyThreshold": 2,
  "HealthyThreshold": 1,
  "MaxParallelRequests": 20,
  "RateLimitRequestsThreshold": 50
}
```

---

### OriginServerHealthStatus

Read-only health snapshot for an origin server. Returned by the origin health endpoints; never accepted
as input. All timestamps are UTC. Uptime and downtime include the current in-progress period, so
`UptimePercentage` reflects the moment the snapshot was taken.

| Field | Type | Description |
|-------|------|-------------|
| `Identifier` | string | Origin server identifier |
| `GUID` | string (GUID) | Deterministic GUID (matches the origin's configuration GUID) |
| `Name` | string | Display name (may be null) |
| `Hostname` | string | Origin hostname |
| `Port` | integer | Origin TCP port |
| `IsHealthy` | boolean | Whether the origin is currently considered healthy |
| `FirstCheckUtc` | datetime | First health check since startup (null if none yet) |
| `LastCheckUtc` | datetime | Most recent health check (null if none yet) |
| `LastHealthyUtc` | datetime | Most recent transition to healthy (null if never healthy) |
| `LastUnhealthyUtc` | datetime | Most recent transition to unhealthy (null if never unhealthy) |
| `LastStateChangeUtc` | datetime | Most recent transition in either direction (null if none) |
| `TotalUptimeMs` | integer (int64) | Cumulative healthy time in milliseconds |
| `TotalDowntimeMs` | integer (int64) | Cumulative unhealthy time in milliseconds |
| `UptimePercentage` | number (double) | Uptime percentage (0–100) |
| `ConsecutiveSuccesses` | integer | Consecutive successful checks |
| `ConsecutiveFailures` | integer | Consecutive failed checks |
| `LastError` | string | Error from the most recent failed check (null when the last check succeeded) |
| `History` | array | Rolling 24-hour window of [HealthCheckRecord](#healthcheckrecord) entries |

---

### HealthCheckRecord

A single health check result within the rolling history window.

| Field | Type | Description |
|-------|------|-------------|
| `TimestampUtc` | datetime | When the check was performed (UTC) |
| `Success` | boolean | Whether the check succeeded |

---

### ApiEndpointConfig

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `GUID` | string (GUID) | No | Derived from `Identifier` | Unique identifier (deterministic; read-only) |
| `Identifier` | string | Yes | - | Unique identifier for referencing |
| `Name` | string | No | null | Display name |
| `TimeoutMs` | integer | No | 60000 | Request timeout in milliseconds |
| `LoadBalancingMode` | string | No | `"RoundRobin"` | `"RoundRobin"` or `"Random"` |
| `BlockHttp10` | boolean | No | false | Block HTTP/1.0 requests |
| `MaxRequestBodySize` | integer | No | 536870912 | Maximum request body size (512MB) |
| `LogRequestFull` | boolean | No | false | Log full request details |
| `LogRequestBody` | boolean | No | false | Log request bodies |
| `LogResponseBody` | boolean | No | false | Log response bodies |
| `IncludeAuthContextHeader` | boolean | No | true | Include auth context header |
| `AuthContextHeader` | string | No | `"x-sb-auth-context"` | Auth context header name |
| `UseGlobalBlockedHeaders` | boolean | No | true | Use global blocked headers list |
| `CaptureRequestBody` | boolean | No | false | Capture request body in history |
| `CaptureResponseBody` | boolean | No | false | Capture response body in history |
| `CaptureRequestHeaders` | boolean | No | true | Capture request headers in history |
| `CaptureResponseHeaders` | boolean | No | true | Capture response headers in history |
| `MaxCaptureRequestBodySize` | integer | No | 65536 | Max request body capture size |
| `MaxCaptureResponseBodySize` | integer | No | 65536 | Max response body capture size |
| `CreatedUtc` | datetime | No | Current time | Creation timestamp |
| `ModifiedUtc` | datetime | No | null | Last modification timestamp |

**Example:**

```json
{
  "GUID": "660e8400-e29b-41d4-a716-446655440001",
  "Identifier": "user-api",
  "Name": "User Management API",
  "TimeoutMs": 30000,
  "LoadBalancingMode": "RoundRobin",
  "IncludeAuthContextHeader": true,
  "AuthContextHeader": "x-sb-auth-context"
}
```

---

### EndpointRoute

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `Id` | integer | No | Auto-generated | Primary key |
| `EndpointIdentifier` | string | Yes | - | Parent endpoint identifier |
| `EndpointGUID` | string (GUID) | Yes | - | Parent endpoint GUID (required on create) |
| `HttpMethod` | string | Yes | `"GET"` | HTTP method |
| `UrlPattern` | string | Yes | `"/"` | URL pattern with parameters |
| `RequiresAuthentication` | boolean | No | false | Require authentication |
| `SortOrder` | integer | No | 0 | Matching priority (lower = first) |
| `CreatedUtc` | datetime | No | Current time | Creation timestamp |

**HTTP Methods:** `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `HEAD`, `OPTIONS`

**Example:**

```json
{
  "Id": 1,
  "EndpointIdentifier": "user-api",
  "EndpointGUID": "660e8400-e29b-41d4-a716-446655440001",
  "HttpMethod": "GET",
  "UrlPattern": "/api/users/{id}",
  "RequiresAuthentication": true,
  "SortOrder": 0
}
```

---

### EndpointOriginMapping

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `Id` | integer | No | Auto-generated | Primary key |
| `EndpointIdentifier` | string | Yes | - | Endpoint identifier |
| `EndpointGUID` | string (GUID) | Yes | - | Endpoint GUID (required on create) |
| `OriginIdentifier` | string | Yes | - | Origin server identifier |
| `OriginGUID` | string (GUID) | Yes | - | Origin server GUID (required on create) |
| `SortOrder` | integer | No | 0 | Load balancing priority |
| `CreatedUtc` | datetime | No | Current time | Creation timestamp |

**Example:**

```json
{
  "Id": 1,
  "EndpointIdentifier": "user-api",
  "EndpointGUID": "660e8400-e29b-41d4-a716-446655440001",
  "OriginIdentifier": "backend-api-1",
  "OriginGUID": "550e8400-e29b-41d4-a716-446655440000",
  "SortOrder": 0
}
```

---

### UrlRewrite

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `Id` | integer | No | Auto-generated | Primary key |
| `EndpointIdentifier` | string | Yes | - | Parent endpoint identifier |
| `EndpointGUID` | string (GUID) | No | - | Parent endpoint GUID |
| `HttpMethod` | string | No | `""` | HTTP method this rewrite applies to; an empty value applies to any method |
| `SourcePattern` | string | Yes | - | URL pattern to match |
| `TargetPattern` | string | Yes | - | URL pattern to rewrite to |
| `SortOrder` | integer | No | 0 | Priority (lower = first) |
| `CreatedUtc` | datetime | No | Current time | Creation timestamp |

**Example:**

```json
{
  "Id": 1,
  "EndpointIdentifier": "user-api",
  "HttpMethod": "GET",
  "SourcePattern": "/v2/users/{id}",
  "TargetPattern": "/api/v1/users/{id}",
  "SortOrder": 0
}
```

---

### BlockedHeader

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `Id` | integer | No | Auto-generated | Primary key |
| `HeaderName` | string | Yes | - | Header name (case-insensitive) |
| `CreatedUtc` | datetime | No | Current time | Creation timestamp |

**Example:**

```json
{
  "Id": 1,
  "HeaderName": "x-internal-token"
}
```

**Default Blocked Headers:** `alt-svc`, `connection`, `date`, `host`, `keep-alive`,
`proxy-authorization`, `proxy-connection`, `set-cookie`, `transfer-encoding`, `upgrade`, `via`,
`x-forwarded-for`, `x-request-id`

---

### UserMaster

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `GUID` | string (GUID) | No | Auto-generated | Unique identifier |
| `Username` | string | Yes | - | Login username |
| `Email` | string | No | null | Email address |
| `FirstName` | string | No | null | First name |
| `LastName` | string | No | null | Last name |
| `Active` | boolean | No | true | Account is active |
| `IsAdmin` | boolean | No | false | Administrator privileges |
| `CreatedUtc` | datetime | No | Current time | Creation timestamp |
| `ModifiedUtc` | datetime | No | null | Last modification timestamp |
| `LastLoginUtc` | datetime | No | null | Last login timestamp |

**Example:**

```json
{
  "GUID": "770e8400-e29b-41d4-a716-446655440002",
  "Username": "jsmith",
  "Email": "jsmith@example.com",
  "FirstName": "John",
  "LastName": "Smith",
  "Active": true,
  "IsAdmin": false,
  "CreatedUtc": "2026-07-15T10:00:00.0000000Z"
}
```

---

### Credential

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `GUID` | string (GUID) | No | Auto-generated | Unique identifier |
| `UserGUID` | string (GUID) | Yes | - | Parent user GUID |
| `Name` | string | No | null | Display name (e.g., "API Token 1") |
| `Description` | string | No | null | Description of token usage |
| `BearerToken` | string | No* | Auto-generated | Bearer token value (returned only on create/regenerate) |
| `Active` | boolean | No | true | Credential is active |
| `IsReadOnly` | boolean | No | false | Cannot be modified/deleted |
| `ExpiresUtc` | datetime | No | null | Expiration timestamp (null = never) |
| `CreatedUtc` | datetime | No | Current time | Creation timestamp |
| `ModifiedUtc` | datetime | No | null | Last modification timestamp |
| `LastUsedUtc` | datetime | No | null | Last usage timestamp |

> *`BearerToken` is auto-generated if not provided on creation.

**Example:**

```json
{
  "GUID": "880e8400-e29b-41d4-a716-446655440003",
  "UserGUID": "770e8400-e29b-41d4-a716-446655440002",
  "Name": "Dashboard Access",
  "Description": "Token for dashboard authentication",
  "BearerToken": "abc123xyz...",
  "Active": true,
  "IsReadOnly": false,
  "CreatedUtc": "2026-07-15T10:00:00.0000000Z"
}
```

---

### RequestHistory

| Field | Type | Description |
|-------|------|-------------|
| `Id` | long | Auto-increment ID (not populated by the default SQLite store; use `RequestId`) |
| `GUID` | string (GUID) | Mirrors `RequestId` (stable across reads) |
| `RequestId` | string (GUID) | Request correlation ID (primary key) |
| `TimestampUtc` | datetime | Request timestamp |
| `HttpMethod` | string | HTTP method |
| `RequestPath` | string | Request path (without query) |
| `QueryString` | string | Query string (without leading `?`) |
| `EndpointIdentifier` | string | Matched endpoint identifier |
| `EndpointGUID` | string (GUID) | Matched endpoint GUID |
| `OriginIdentifier` | string | Selected origin identifier |
| `OriginGUID` | string (GUID) | Selected origin GUID |
| `ClientIp` | string | Client IP address |
| `RequestBodySize` | long | Request body size in bytes |
| `RequestBody` | string | Request body (if captured) |
| `RequestHeaders` | string | Request headers as JSON |
| `StatusCode` | integer | Response status code |
| `ResponseBodySize` | long | Response body size in bytes |
| `ResponseBody` | string | Response body (if captured) |
| `ResponseHeaders` | string | Response headers as JSON |
| `DurationMs` | long | Total duration in milliseconds |
| `WasAuthenticated` | boolean | Request was authenticated |
| `ErrorMessage` | string | Error message if failed |
| `Success` | boolean | Request was successful (2xx/3xx) |

**Example:**

```json
{
  "Id": 0,
  "GUID": "aa0e8400-e29b-41d4-a716-446655440005",
  "RequestId": "aa0e8400-e29b-41d4-a716-446655440005",
  "TimestampUtc": "2026-07-28T10:30:45.1230000Z",
  "HttpMethod": "GET",
  "RequestPath": "/api/users/123",
  "QueryString": "include=profile",
  "EndpointIdentifier": "user-api",
  "OriginIdentifier": "backend-api-1",
  "ClientIp": "192.0.2.100",
  "RequestBodySize": 0,
  "StatusCode": 200,
  "ResponseBodySize": 1024,
  "DurationMs": 45,
  "WasAuthenticated": true,
  "Success": true
}
```

---

## Examples

### Complete Workflow: Set Up a New API

Request bodies use PascalCase.

#### 1. Create an Origin Server

```bash
curl -X POST http://localhost:8000/_sb/v1.0/origins \
  -H "Authorization: Bearer sbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "Identifier": "my-backend",
    "Name": "My Backend Server",
    "Hostname": "api.example.com",
    "Port": 443,
    "Ssl": true,
    "HealthCheckUrl": "/health"
  }'
```

#### 2. Create an API Endpoint

```bash
curl -X POST http://localhost:8000/_sb/v1.0/endpoints \
  -H "Authorization: Bearer sbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "Identifier": "my-api",
    "Name": "My API",
    "LoadBalancingMode": "RoundRobin"
  }'
```

The endpoint's `GUID` is derived from its `Identifier`; read it from the response for the next steps.

#### 3. Create Routes

```bash
curl -X POST http://localhost:8000/_sb/v1.0/routes \
  -H "Authorization: Bearer sbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "EndpointIdentifier": "my-api",
    "EndpointGUID": "<endpoint-guid>",
    "HttpMethod": "GET",
    "UrlPattern": "/api/users/{id}",
    "RequiresAuthentication": true
  }'
```

#### 4. Map Endpoint to Origin

```bash
curl -X POST http://localhost:8000/_sb/v1.0/mappings \
  -H "Authorization: Bearer sbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "EndpointIdentifier": "my-api",
    "EndpointGUID": "<endpoint-guid>",
    "OriginIdentifier": "my-backend",
    "OriginGUID": "<origin-guid>"
  }'
```

#### 5. Test the Proxy

```bash
curl http://localhost:8000/api/users/123
```

---

## SDK Access

For programmatic access within .NET applications, use `SwitchboardDaemon.Client` (which exposes the
same models documented above):

```csharp
using Switchboard.Core;

SwitchboardDaemon daemon = new SwitchboardDaemon(settings);

// Configuration
var origins = await daemon.Client.OriginServers.GetAllAsync();
var endpoints = await daemon.Client.ApiEndpoints.GetAllAsync();
var routes = await daemon.Client.EndpointRoutes.GetAllAsync();
var mappings = await daemon.Client.EndpointOriginMappings.GetAllAsync();
var rewrites = await daemon.Client.UrlRewrites.GetAllAsync();
var headers = await daemon.Client.BlockedHeaders.GetAllAsync();

// Users and credentials
var users = await daemon.Client.Users.GetAllAsync();
var credentials = await daemon.Client.Credentials.GetAllAsync();

// Request history
var history = await daemon.Client.RequestHistory.GetRecentAsync(100);
```
