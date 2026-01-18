# Switchboard REST API Reference

This document provides a comprehensive reference for the Switchboard Management REST API.

## Table of Contents

- [Overview](#overview)
- [Authentication](#authentication)
- [Base URL](#base-url)
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
- [Data Models](#data-models)

---

## Overview

The Switchboard Management API provides RESTful endpoints for configuring and monitoring the Switchboard proxy at runtime. All endpoints return JSON responses and accept JSON request bodies where applicable.

### Features

- Full CRUD operations for all configuration entities
- Bearer token authentication
- Pagination support via `skip` and `take` query parameters
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
- **Bearer Token**: `sbadmin` (displayed once on first startup)
- **Read-Only**: The default credential cannot be modified or deleted

---

## Base URL

The default base path is `/_sb/v1.0/`. This can be configured via `Management.BasePath` in settings.

```
http://localhost:8000/_sb/v1.0/
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

List endpoints support pagination via query parameters:

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

```json
{
  "error": "NotFound",
  "message": "The requested resource was not found.",
  "statusCode": 404,
  "description": "Origin server not found",
  "context": null
}
```

### Error Codes

| Error Code | HTTP Status | Description |
|------------|-------------|-------------|
| `AuthenticationFailed` | 401 | Authentication material was not accepted |
| `AuthorizationFailed` | 401 | Authenticated but not authorized |
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

Returns the health status of the Switchboard instance.

**Response:**

```json
{
  "status": "healthy",
  "timestamp": "2024-01-15T10:30:00.000Z",
  "version": "4.0.0"
}
```

---

### Current User

#### Get Current User

```
GET /_sb/v1.0/me
```

Returns information about the currently authenticated user.

**Response:**

```json
{
  "guid": "550e8400-e29b-41d4-a716-446655440000",
  "username": "admin",
  "email": "admin@example.com",
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

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `search` | string | Filter by identifier or name |
| `skip` | integer | Records to skip |
| `take` | integer | Maximum records to return |

**Response:** Array of [OriginServerConfig](#originserverconfig)

#### Create Origin Server

```
POST /_sb/v1.0/origins
```

**Request Body:** [OriginServerConfig](#originserverconfig) (GUID is auto-generated)

**Response:** `201 Created` with created [OriginServerConfig](#originserverconfig)

#### Get Origin Server

```
GET /_sb/v1.0/origins/{guid}
```

**Response:** [OriginServerConfig](#originserverconfig)

#### Update Origin Server

```
PUT /_sb/v1.0/origins/{guid}
```

**Request Body:** [OriginServerConfig](#originserverconfig)

**Response:** Updated [OriginServerConfig](#originserverconfig)

#### Delete Origin Server

```
DELETE /_sb/v1.0/origins/{guid}
```

**Response:** `204 No Content`

---

### API Endpoints

API endpoints define the routes that Switchboard handles and how they map to origin servers.

#### List API Endpoints

```
GET /_sb/v1.0/endpoints
```

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `search` | string | Filter by identifier or name |
| `skip` | integer | Records to skip |
| `take` | integer | Maximum records to return |

**Response:** Array of [ApiEndpointConfig](#apiendpointconfig)

#### Create API Endpoint

```
POST /_sb/v1.0/endpoints
```

**Request Body:** [ApiEndpointConfig](#apiendpointconfig)

**Response:** `201 Created` with created [ApiEndpointConfig](#apiendpointconfig)

#### Get API Endpoint

```
GET /_sb/v1.0/endpoints/{guid}
```

**Response:** [ApiEndpointConfig](#apiendpointconfig)

#### Update API Endpoint

```
PUT /_sb/v1.0/endpoints/{guid}
```

**Request Body:** [ApiEndpointConfig](#apiendpointconfig)

**Response:** Updated [ApiEndpointConfig](#apiendpointconfig)

#### Delete API Endpoint

```
DELETE /_sb/v1.0/endpoints/{guid}
```

**Response:** `204 No Content`

---

### Endpoint Routes

Routes define URL patterns that map to API endpoints.

#### List Routes

```
GET /_sb/v1.0/routes
```

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `skip` | integer | Records to skip |
| `take` | integer | Maximum records to return |

**Response:** Array of [EndpointRoute](#endpointroute)

#### Create Route

```
POST /_sb/v1.0/routes
```

**Request Body:** [EndpointRoute](#endpointroute)

**Response:** `201 Created` with created [EndpointRoute](#endpointroute)

#### Get Route

```
GET /_sb/v1.0/routes/{id}
```

**Response:** [EndpointRoute](#endpointroute)

#### Update Route

```
PUT /_sb/v1.0/routes/{id}
```

**Request Body:** [EndpointRoute](#endpointroute)

**Response:** Updated [EndpointRoute](#endpointroute)

#### Delete Route

```
DELETE /_sb/v1.0/routes/{id}
```

**Response:** `204 No Content`

---

### Endpoint-Origin Mappings

Mappings associate API endpoints with their backend origin servers.

#### List Mappings

```
GET /_sb/v1.0/mappings
```

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `skip` | integer | Records to skip |
| `take` | integer | Maximum records to return |

**Response:** Array of [EndpointOriginMapping](#endpointoriginmapping)

#### Create Mapping

```
POST /_sb/v1.0/mappings
```

**Request Body:** [EndpointOriginMapping](#endpointoriginmapping)

**Response:** `201 Created` with created [EndpointOriginMapping](#endpointoriginmapping)

#### Get Mapping

```
GET /_sb/v1.0/mappings/{id}
```

**Response:** [EndpointOriginMapping](#endpointoriginmapping)

#### Delete Mapping

```
DELETE /_sb/v1.0/mappings/{id}
```

**Response:** `204 No Content`

---

### URL Rewrites

URL rewrites transform request URLs before forwarding to origin servers.

#### List Rewrites

```
GET /_sb/v1.0/rewrites
```

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `skip` | integer | Records to skip |
| `take` | integer | Maximum records to return |

**Response:** Array of [UrlRewrite](#urlrewrite)

#### Create Rewrite

```
POST /_sb/v1.0/rewrites
```

**Request Body:** [UrlRewrite](#urlrewrite)

**Response:** `201 Created` with created [UrlRewrite](#urlrewrite)

#### Get Rewrite

```
GET /_sb/v1.0/rewrites/{id}
```

**Response:** [UrlRewrite](#urlrewrite)

#### Update Rewrite

```
PUT /_sb/v1.0/rewrites/{id}
```

**Request Body:** [UrlRewrite](#urlrewrite)

**Response:** Updated [UrlRewrite](#urlrewrite)

#### Delete Rewrite

```
DELETE /_sb/v1.0/rewrites/{id}
```

**Response:** `204 No Content`

---

### Blocked Headers

Globally blocked headers are not forwarded from client requests to origin servers.

#### List Blocked Headers

```
GET /_sb/v1.0/headers
```

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `skip` | integer | Records to skip |
| `take` | integer | Maximum records to return |

**Response:** Array of [BlockedHeader](#blockedheader)

#### Create Blocked Header

```
POST /_sb/v1.0/headers
```

**Request Body:** [BlockedHeader](#blockedheader)

**Response:** `201 Created` with created [BlockedHeader](#blockedheader)

#### Get Blocked Header

```
GET /_sb/v1.0/headers/{id}
```

**Response:** [BlockedHeader](#blockedheader)

#### Delete Blocked Header

```
DELETE /_sb/v1.0/headers/{id}
```

**Response:** `204 No Content`

---

### Users

User accounts for Management API access.

#### List Users

```
GET /_sb/v1.0/users
```

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `search` | string | Filter by username, email, or name |
| `skip` | integer | Records to skip |
| `take` | integer | Maximum records to return |

**Response:** Array of [UserMaster](#usermaster)

#### Create User

```
POST /_sb/v1.0/users
```

**Request Body:** [UserMaster](#usermaster) (GUID is auto-generated)

**Response:** `201 Created` with created [UserMaster](#usermaster)

#### Get User

```
GET /_sb/v1.0/users/{guid}
```

**Response:** [UserMaster](#usermaster)

#### Update User

```
PUT /_sb/v1.0/users/{guid}
```

**Request Body:** [UserMaster](#usermaster)

**Response:** Updated [UserMaster](#usermaster)

#### Delete User

```
DELETE /_sb/v1.0/users/{guid}
```

**Response:** `204 No Content`

---

### Credentials

Bearer token credentials for API authentication.

#### List Credentials

```
GET /_sb/v1.0/credentials
```

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `search` | string | Filter by name or description |
| `skip` | integer | Records to skip |
| `take` | integer | Maximum records to return |

**Response:** Array of [Credential](#credential)

#### Create Credential

```
POST /_sb/v1.0/credentials
```

**Request Body:** [Credential](#credential)

**Response:** `201 Created` with created [Credential](#credential)

> **Important:** The `bearerToken` is only returned on creation. Store it securely.

#### Get Credential

```
GET /_sb/v1.0/credentials/{guid}
```

**Response:** [Credential](#credential)

#### Update Credential

```
PUT /_sb/v1.0/credentials/{guid}
```

**Request Body:** [Credential](#credential)

**Response:** Updated [Credential](#credential)

> **Note:** Read-only credentials cannot be updated.

#### Delete Credential

```
DELETE /_sb/v1.0/credentials/{guid}
```

**Response:** `204 No Content`

> **Note:** Read-only credentials cannot be deleted.

#### Regenerate Bearer Token

```
POST /_sb/v1.0/credentials/{guid}/regenerate
```

Generates a new bearer token for the credential.

**Response:** Updated [Credential](#credential) with new `bearerToken`

> **Note:** Read-only credentials cannot have their tokens regenerated.

---

### Request History

Track and analyze requests passing through Switchboard.

#### List Request History

```
GET /_sb/v1.0/history
```

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `skip` | integer | Records to skip |
| `take` | integer | Maximum records to return |
| `start` | datetime | Start of time range (ISO 8601) |
| `end` | datetime | End of time range (ISO 8601) |
| `endpoint` | guid | Filter by endpoint GUID |
| `origin` | guid | Filter by origin GUID |

**Response:** Array of [RequestHistory](#requesthistory)

#### Get Recent Requests

```
GET /_sb/v1.0/history/recent
```

**Query Parameters:**

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `count` | integer | 100 | Number of recent requests (max: 1000) |

**Response:** Array of [RequestHistory](#requesthistory)

#### Get Failed Requests

```
GET /_sb/v1.0/history/failed
```

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `skip` | integer | Records to skip |
| `take` | integer | Maximum records to return |

**Response:** Array of [RequestHistory](#requesthistory) where `success = false`

#### Get Request by ID

```
GET /_sb/v1.0/history/{id}
```

The `{id}` parameter accepts either:
- Numeric ID (e.g., `123`)
- GUID (e.g., `550e8400-e29b-41d4-a716-446655440000`)

**Response:** [RequestHistory](#requesthistory)

#### Delete Request History

```
DELETE /_sb/v1.0/history/{id}
```

**Response:** `204 No Content`

#### Run Cleanup

```
POST /_sb/v1.0/history/cleanup
```

Manually trigger history cleanup.

**Query Parameters:**

| Parameter | Type | Description |
|-----------|------|-------------|
| `days` | integer | Delete records older than this many days |

**Response:**

```json
{
  "deletedRecords": 150
}
```

#### Get Statistics

```
GET /_sb/v1.0/history/stats
```

**Response:**

```json
{
  "totalRequests": 10000,
  "failedRequests": 50,
  "successRate": 99.5
}
```

---

## Data Models

### OriginServerConfig

Represents a backend server that Switchboard proxies requests to.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `guid` | string (GUID) | No | Auto-generated | Unique identifier |
| `identifier` | string | Yes | - | Unique identifier for referencing |
| `name` | string | No | null | Display name |
| `hostname` | string | Yes | `"localhost"` | Server hostname |
| `port` | integer | Yes | 8000 | TCP port (0-65535) |
| `ssl` | boolean | No | false | Enable HTTPS |
| `healthCheckIntervalMs` | integer | No | 5000 | Health check interval (min: 1000) |
| `healthCheckMethod` | string | No | `"HEAD"` | HTTP method for health checks |
| `healthCheckUrl` | string | No | `"/"` | URL path for health checks |
| `unhealthyThreshold` | integer | No | 2 | Failed checks before marking unhealthy |
| `healthyThreshold` | integer | No | 1 | Successful checks before marking healthy |
| `maxParallelRequests` | integer | No | 10 | Maximum concurrent requests |
| `rateLimitRequestsThreshold` | integer | No | 30 | Total requests before rate limiting |
| `logRequest` | boolean | No | false | Log requests to this origin |
| `logRequestBody` | boolean | No | false | Log request bodies |
| `logResponse` | boolean | No | false | Log responses |
| `logResponseBody` | boolean | No | false | Log response bodies |
| `captureRequestBody` | boolean | No | false | Capture request body in history |
| `captureResponseBody` | boolean | No | false | Capture response body in history |
| `captureRequestHeaders` | boolean | No | true | Capture request headers in history |
| `captureResponseHeaders` | boolean | No | true | Capture response headers in history |
| `maxCaptureRequestBodySize` | integer | No | 65536 | Max request body capture size (bytes) |
| `maxCaptureResponseBodySize` | integer | No | 65536 | Max response body capture size (bytes) |
| `createdUtc` | datetime | No | Current time | Creation timestamp |
| `modifiedUtc` | datetime | No | null | Last modification timestamp |

**Example:**

```json
{
  "guid": "550e8400-e29b-41d4-a716-446655440000",
  "identifier": "backend-api-1",
  "name": "Backend API Server 1",
  "hostname": "api.example.com",
  "port": 443,
  "ssl": true,
  "healthCheckIntervalMs": 5000,
  "healthCheckMethod": "GET",
  "healthCheckUrl": "/health",
  "unhealthyThreshold": 2,
  "healthyThreshold": 1,
  "maxParallelRequests": 20,
  "rateLimitRequestsThreshold": 50
}
```

---

### ApiEndpointConfig

Represents an API endpoint configuration.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `guid` | string (GUID) | No | Auto-generated | Unique identifier |
| `identifier` | string | Yes | - | Unique identifier for referencing |
| `name` | string | No | null | Display name |
| `timeoutMs` | integer | No | 60000 | Request timeout in milliseconds |
| `loadBalancingMode` | string | No | `"RoundRobin"` | Load balancing algorithm |
| `blockHttp10` | boolean | No | false | Block HTTP/1.0 requests |
| `maxRequestBodySize` | integer | No | 536870912 | Maximum request body size (512MB) |
| `logRequestFull` | boolean | No | false | Log full request details |
| `logRequestBody` | boolean | No | false | Log request bodies |
| `logResponseBody` | boolean | No | false | Log response bodies |
| `includeAuthContextHeader` | boolean | No | true | Include auth context header |
| `authContextHeader` | string | No | `"x-sb-auth-context"` | Auth context header name |
| `useGlobalBlockedHeaders` | boolean | No | true | Use global blocked headers list |
| `captureRequestBody` | boolean | No | false | Capture request body in history |
| `captureResponseBody` | boolean | No | false | Capture response body in history |
| `captureRequestHeaders` | boolean | No | true | Capture request headers in history |
| `captureResponseHeaders` | boolean | No | true | Capture response headers in history |
| `maxCaptureRequestBodySize` | integer | No | 65536 | Max request body capture size |
| `maxCaptureResponseBodySize` | integer | No | 65536 | Max response body capture size |
| `createdUtc` | datetime | No | Current time | Creation timestamp |
| `modifiedUtc` | datetime | No | null | Last modification timestamp |

**Load Balancing Modes:**
- `RoundRobin` - Distribute requests sequentially across origins
- `Random` - Randomly select an origin for each request

**Example:**

```json
{
  "guid": "660e8400-e29b-41d4-a716-446655440001",
  "identifier": "user-api",
  "name": "User Management API",
  "timeoutMs": 30000,
  "loadBalancingMode": "RoundRobin",
  "includeAuthContextHeader": true,
  "authContextHeader": "x-sb-auth-context"
}
```

---

### EndpointRoute

Defines a URL pattern that maps to an API endpoint.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `id` | integer | No | Auto-generated | Primary key |
| `endpointIdentifier` | string | Yes | - | Parent endpoint identifier |
| `endpointGUID` | string (GUID) | No | - | Parent endpoint GUID |
| `httpMethod` | string | Yes | `"GET"` | HTTP method |
| `urlPattern` | string | Yes | `"/"` | URL pattern with parameters |
| `requiresAuthentication` | boolean | No | false | Require authentication |
| `sortOrder` | integer | No | 0 | Matching priority (lower = first) |
| `createdUtc` | datetime | No | Current time | Creation timestamp |

**HTTP Methods:** `GET`, `POST`, `PUT`, `DELETE`, `PATCH`, `HEAD`, `OPTIONS`

**URL Pattern Examples:**
- `/api/users` - Exact match
- `/api/users/{id}` - Parameter match
- `/api/users/{userId}/orders/{orderId}` - Multiple parameters

**Example:**

```json
{
  "id": 1,
  "endpointIdentifier": "user-api",
  "endpointGUID": "660e8400-e29b-41d4-a716-446655440001",
  "httpMethod": "GET",
  "urlPattern": "/api/users/{id}",
  "requiresAuthentication": true,
  "sortOrder": 0
}
```

---

### EndpointOriginMapping

Maps an API endpoint to one or more origin servers.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `id` | integer | No | Auto-generated | Primary key |
| `endpointIdentifier` | string | Yes | - | Endpoint identifier |
| `endpointGUID` | string (GUID) | No | - | Endpoint GUID |
| `originIdentifier` | string | Yes | - | Origin server identifier |
| `originGUID` | string (GUID) | No | - | Origin server GUID |
| `sortOrder` | integer | No | 0 | Load balancing priority |
| `createdUtc` | datetime | No | Current time | Creation timestamp |

**Example:**

```json
{
  "id": 1,
  "endpointIdentifier": "user-api",
  "endpointGUID": "660e8400-e29b-41d4-a716-446655440001",
  "originIdentifier": "backend-api-1",
  "originGUID": "550e8400-e29b-41d4-a716-446655440000",
  "sortOrder": 0
}
```

---

### UrlRewrite

Transforms request URLs before forwarding to origin servers.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `id` | integer | No | Auto-generated | Primary key |
| `endpointIdentifier` | string | Yes | - | Parent endpoint identifier |
| `endpointGUID` | string (GUID) | No | - | Parent endpoint GUID |
| `httpMethod` | string | Yes | `"GET"` | HTTP method this rewrite applies to |
| `sourcePattern` | string | Yes | - | URL pattern to match |
| `targetPattern` | string | Yes | - | URL pattern to rewrite to |
| `sortOrder` | integer | No | 0 | Priority (lower = first) |
| `createdUtc` | datetime | No | Current time | Creation timestamp |

**Example:**

```json
{
  "id": 1,
  "endpointIdentifier": "user-api",
  "httpMethod": "GET",
  "sourcePattern": "/v2/users/{id}",
  "targetPattern": "/api/v1/users/{id}",
  "sortOrder": 0
}
```

---

### BlockedHeader

Headers that are not forwarded to origin servers.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `id` | integer | No | Auto-generated | Primary key |
| `headerName` | string | Yes | - | Header name (case-insensitive) |
| `createdUtc` | datetime | No | Current time | Creation timestamp |

**Example:**

```json
{
  "id": 1,
  "headerName": "x-internal-token"
}
```

**Default Blocked Headers:**
- `alt-svc`
- `connection`
- `date`
- `host`
- `keep-alive`
- `proxy-authorization`
- `proxy-connection`
- `set-cookie`
- `transfer-encoding`
- `upgrade`
- `via`
- `x-forwarded-for`
- `x-request-id`

---

### UserMaster

User account for Management API access.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `guid` | string (GUID) | No | Auto-generated | Unique identifier |
| `username` | string | Yes | - | Login username |
| `email` | string | No | null | Email address |
| `firstName` | string | No | null | First name |
| `lastName` | string | No | null | Last name |
| `active` | boolean | No | true | Account is active |
| `isAdmin` | boolean | No | false | Administrator privileges |
| `createdUtc` | datetime | No | Current time | Creation timestamp |
| `modifiedUtc` | datetime | No | null | Last modification timestamp |
| `lastLoginUtc` | datetime | No | null | Last login timestamp |

**Example:**

```json
{
  "guid": "770e8400-e29b-41d4-a716-446655440002",
  "username": "jsmith",
  "email": "jsmith@example.com",
  "firstName": "John",
  "lastName": "Smith",
  "active": true,
  "isAdmin": false,
  "createdUtc": "2024-01-15T10:00:00Z"
}
```

---

### Credential

Bearer token credential for API authentication.

| Field | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `guid` | string (GUID) | No | Auto-generated | Unique identifier |
| `userGUID` | string (GUID) | Yes | - | Parent user GUID |
| `name` | string | No | null | Display name (e.g., "API Token 1") |
| `description` | string | No | null | Description of token usage |
| `bearerToken` | string | Yes* | Auto-generated | Bearer token value |
| `active` | boolean | No | true | Credential is active |
| `isReadOnly` | boolean | No | false | Cannot be modified/deleted |
| `expiresUtc` | datetime | No | null | Expiration timestamp (null = never) |
| `createdUtc` | datetime | No | Current time | Creation timestamp |
| `modifiedUtc` | datetime | No | null | Last modification timestamp |
| `lastUsedUtc` | datetime | No | null | Last usage timestamp |

> *`bearerToken` is auto-generated if not provided on creation.

**Example:**

```json
{
  "guid": "880e8400-e29b-41d4-a716-446655440003",
  "userGUID": "770e8400-e29b-41d4-a716-446655440002",
  "name": "Dashboard Access",
  "description": "Token for dashboard authentication",
  "bearerToken": "abc123xyz...",
  "active": true,
  "isReadOnly": false,
  "expiresUtc": "2025-01-15T00:00:00Z",
  "createdUtc": "2024-01-15T10:00:00Z"
}
```

---

### RequestHistory

Record of a processed request.

| Field | Type | Description |
|-------|------|-------------|
| `id` | long | Auto-increment ID |
| `guid` | string (GUID) | Unique identifier |
| `requestId` | string (GUID) | Request correlation ID |
| `timestampUtc` | datetime | Request timestamp |
| `httpMethod` | string | HTTP method |
| `requestPath` | string | Request path (without query) |
| `queryString` | string | Query string (without leading ?) |
| `endpointIdentifier` | string | Matched endpoint identifier |
| `endpointGUID` | string (GUID) | Matched endpoint GUID |
| `originIdentifier` | string | Selected origin identifier |
| `originGUID` | string (GUID) | Selected origin GUID |
| `clientIp` | string | Client IP address |
| `requestBodySize` | long | Request body size in bytes |
| `requestBody` | string | Request body (if captured) |
| `requestHeaders` | string | Request headers as JSON |
| `statusCode` | integer | Response status code |
| `responseBodySize` | long | Response body size in bytes |
| `responseBody` | string | Response body (if captured) |
| `responseHeaders` | string | Response headers as JSON |
| `durationMs` | long | Total duration in milliseconds |
| `wasAuthenticated` | boolean | Request was authenticated |
| `errorMessage` | string | Error message if failed |
| `success` | boolean | Request was successful (2xx/3xx) |

**Example:**

```json
{
  "id": 12345,
  "guid": "990e8400-e29b-41d4-a716-446655440004",
  "requestId": "aa0e8400-e29b-41d4-a716-446655440005",
  "timestampUtc": "2024-01-15T10:30:45.123Z",
  "httpMethod": "GET",
  "requestPath": "/api/users/123",
  "queryString": "include=profile",
  "endpointIdentifier": "user-api",
  "originIdentifier": "backend-api-1",
  "clientIp": "192.168.1.100",
  "requestBodySize": 0,
  "statusCode": 200,
  "responseBodySize": 1024,
  "durationMs": 45,
  "wasAuthenticated": true,
  "success": true
}
```

---

## Examples

### Complete Workflow: Set Up a New API

#### 1. Create an Origin Server

```bash
curl -X POST http://localhost:8000/_sb/v1.0/origins \
  -H "Authorization: Bearer sbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "identifier": "my-backend",
    "name": "My Backend Server",
    "hostname": "api.example.com",
    "port": 443,
    "ssl": true,
    "healthCheckUrl": "/health"
  }'
```

#### 2. Create an API Endpoint

```bash
curl -X POST http://localhost:8000/_sb/v1.0/endpoints \
  -H "Authorization: Bearer sbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "identifier": "my-api",
    "name": "My API",
    "loadBalancingMode": "RoundRobin"
  }'
```

#### 3. Create Routes

```bash
# Public health check route
curl -X POST http://localhost:8000/_sb/v1.0/routes \
  -H "Authorization: Bearer sbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "endpointIdentifier": "my-api",
    "httpMethod": "GET",
    "urlPattern": "/health",
    "requiresAuthentication": false
  }'

# Protected user routes
curl -X POST http://localhost:8000/_sb/v1.0/routes \
  -H "Authorization: Bearer sbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "endpointIdentifier": "my-api",
    "httpMethod": "GET",
    "urlPattern": "/api/users/{id}",
    "requiresAuthentication": true
  }'
```

#### 4. Map Endpoint to Origin

```bash
curl -X POST http://localhost:8000/_sb/v1.0/mappings \
  -H "Authorization: Bearer sbadmin" \
  -H "Content-Type: application/json" \
  -d '{
    "endpointIdentifier": "my-api",
    "originIdentifier": "my-backend"
  }'
```

#### 5. Test the Proxy

```bash
curl http://localhost:8000/health
curl http://localhost:8000/api/users/123
```

---

## SDK Access

For programmatic access within .NET applications, use `SwitchboardDaemon.Client`:

```csharp
using Switchboard.Core;

SwitchboardDaemon daemon = new SwitchboardDaemon(settings);

// Access user management
var users = await daemon.Client.Users.GetAllAsync();
var user = await daemon.Client.Users.CreateAsync(new UserMaster("newuser"));

// Access credential management
var credentials = await daemon.Client.Credentials.GetAllAsync();
var credential = await daemon.Client.Credentials.CreateAsync(new Credential(user.GUID));

// Access configuration
var origins = await daemon.Client.OriginServers.GetAllAsync();
var endpoints = await daemon.Client.ApiEndpoints.GetAllAsync();
var routes = await daemon.Client.EndpointRoutes.GetAllAsync();
var mappings = await daemon.Client.EndpointOriginMappings.GetAllAsync();
var rewrites = await daemon.Client.UrlRewrites.GetAllAsync();
var headers = await daemon.Client.BlockedHeaders.GetAllAsync();

// Access request history
var history = await daemon.Client.RequestHistory.GetRecentAsync(100);
var stats = await daemon.Client.RequestHistory.CountAsync();
```
