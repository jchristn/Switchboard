# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build
```bash
# From the src/ directory
dotnet build Switchboard.sln
```

### Run Tests
```bash
# Run comprehensive test suite (includes authentication, health checks, rate limiting, chunked transfer, SSE)
cd Test
dotnet run

# Run URL rewrite test
cd Test.UrlRewrite
dotnet run
```

### Run Switchboard Server (Standalone)
```bash
# Build first
dotnet build Switchboard.Server/Switchboard.Server.csproj

# Run from output directory
cd Switchboard.Server/bin/Debug/net8.0
dotnet Switchboard.Server.dll

# Server reads configuration from sb.json in the current directory
```

### Package for NuGet (Switchboard.Core only)
```bash
dotnet pack Switchboard.Core/Switchboard.Core.csproj -c Release
```

### Docker
```bash
# Build Docker image
# From src/ directory:
build-docker.bat  # or use docker build commands

# Run with Docker Compose (from Docker/ directory)
compose-up.bat    # Windows
compose-up.sh     # Linux/Mac

# Stop Docker Compose
compose-down.bat  # Windows
compose-down.sh   # Linux/Mac
```

## Architecture

Switchboard is a lightweight application proxy that combines reverse proxy and API gateway functionality. The solution is structured as follows:

### Core Components

- **Switchboard.Core**: The main library containing all proxy logic, designed to be embedded in applications or used standalone
- **Switchboard.Server**: A standalone executable server that uses Switchboard.Core
- **Test**: Example integration showing how to use Switchboard.Core with custom authentication/authorization
- **Test.UrlRewrite**: Example demonstrating URL rewriting capabilities

### Key Classes

- **SwitchboardDaemon**: Main entry point that manages the web server and proxy functionality
- **SwitchboardSettings**: Configuration object containing endpoints, origin servers, and webserver settings
- **SwitchboardCallbacks**: Hooks for custom authentication, authorization, and request processing
- **ApiEndpoint**: Defines API routes with authentication requirements and URL rewriting rules
- **OriginServer**: Backend server configuration with health checking and rate limiting
- **GatewayService**: Handles request routing and load balancing
- **HealthCheckService**: Monitors origin server availability

### Configuration

Settings are configured via JSON files (typically `sb.json`) or programmatically via `SwitchboardSettings`. Key configuration areas:

- **Endpoints**: API route definitions with authentication groups and URL rewriting
- **Origins**: Backend server definitions with health check and rate limiting parameters
- **Webserver**: HTTP server configuration including SSL, headers, and access control
- **Logging**: Syslog and file logging configuration

### Load Balancing

Supports multiple load balancing algorithms via `LoadBalancingMode` enum (RoundRobin, etc.)

### Health Checking

Automatic health monitoring of origin servers with configurable:
- Check intervals and URLs
- Healthy/unhealthy thresholds
- HTTP methods for health checks

### Rate Limiting

Per-origin server rate limiting with configurable:
- Maximum parallel requests
- Request threshold limits

### Authentication/Authorization

Flexible callback-based system allowing custom authentication and authorization logic via the `AuthenticateAndAuthorize` callback in `SwitchboardCallbacks`.

### Request Flow

1. **Request Reception**: Watson webserver receives HTTP request
2. **Endpoint Matching**: `GatewayService.FindApiEndpoint()` matches request to API endpoint using URL pattern matching
3. **Authentication Check**: If endpoint requires auth, `AuthenticateAndAuthorize` callback is invoked
4. **Origin Selection**: `GatewayService.FindOriginServer()` selects healthy origin using load balancing algorithm
5. **Rate Limiting Check**: Verifies request count against origin's `RateLimitRequestsThreshold`
6. **URL Rewriting**: `UrlTools.RewriteUrl()` applies any configured URL transformations
7. **Proxy Request**: `GatewayService.ProxyRequest()` forwards to origin server using RestWrapper
8. **Response Handling**: Response (including chunked transfer and SSE) is forwarded back to client

### Special Request Types

- **Chunked Transfer Encoding**: Switchboard forwards chunked requests/responses using `RestRequest.SendChunkAsync()` and handles streaming data
- **Server-Sent Events (SSE)**: Detected by content-type `text/event-stream`, forwarded using `RestResponse.ReadEventAsync()`
- **OPTIONS Requests**: Handled by `GatewayService.OptionsRoute()` for CORS preflight

## Coding Standards

### Namespace and Using Statements
- Namespace declaration must be at the top
- Using statements must be contained INSIDE the namespace block
- Microsoft and standard system library usings first, in alphabetical order
- Other using statements follow, in alphabetical order

### Documentation
- All public members, constructors, and public methods must have XML documentation
- No documentation for private members or private methods
- Document default values, minimum/maximum values where appropriate
- Document thread safety guarantees
- Document nullability in XML comments
- Document exceptions using /// <exception> tags

### Naming Conventions
- Private class member variables start with underscore, then Pascal case: `_FooBar` not `_fooBar`
- Use explicit types, never `var`
- One class or one enum per file

### Properties and Members
- Public members must have explicit getters/setters with backing variables for validation
- Avoid constants for configurable values - use public members with backing private members
- Use nullable reference types (`<Nullable>enable</Nullable>`)

### Async and Threading
- Async methods must accept CancellationToken parameter (unless class has CancellationToken member)
- Use `.ConfigureAwait(false)` where appropriate
- Check cancellation at appropriate places
- Document thread safety guarantees
- Use Interlocked for simple atomic operations
- Prefer ReaderWriterLockSlim over lock for read-heavy scenarios

### Error Handling
- Use specific exception types, not generic Exception
- Include meaningful error messages with context
- Consider custom exception types for domain-specific errors
- Use exception filters when appropriate: `catch (SqlException ex) when (ex.Number == 2601)`
- Validate input parameters with guard clauses
- Use `ArgumentNullException.ThrowIfNull()` for .NET 6+
- Proactively eliminate null exception scenarios

### Resource Management
- Implement IDisposable/IAsyncDisposable for unmanaged resources
- Use 'using' statements or declarations for IDisposable objects
- Follow full Dispose pattern with `protected virtual void Dispose(bool disposing)`
- Always call `base.Dispose()` in derived classes

### LINQ and Collections
- Prefer LINQ methods over manual loops when readable
- Use `.Any()` instead of `.Count() > 0`
- Use `.FirstOrDefault()` with null checks rather than `.First()`
- Be aware of multiple enumeration - consider `.ToList()` when needed
- When implementing IEnumerable methods, create async variants with CancellationToken

### Restrictions
- Avoid tuples unless absolutely necessary
- No `Console.WriteLine` statements in library code
- No assumptions about opaque class members/methods - ask for implementations

## Framework

- .NET 8.0
- Uses Watson webserver framework for HTTP handling
- SerializationHelper for JSON configuration
- SyslogLogging for logging infrastructure
- RestWrapper for HTTP client operations

## Configuration File Structure (sb.json)

Configuration files follow this structure:

```json
{
  "Logging": {
    "Servers": [],           // Syslog servers for logging
    "LogDirectory": "./logs/",
    "LogFilename": "./switchboard.log",
    "ConsoleLogging": true,
    "MinimumSeverity": 1     // 0=Debug, 1=Info, 2=Warn, 3=Error, 4=Alert
  },
  "Endpoints": [
    {
      "Identifier": "unique-id",
      "Name": "Display Name",
      "LoadBalancing": "RoundRobin",  // or "Random"
      "TimeoutMs": 60000,
      "BlockHttp10": false,
      "MaxRequestBodySize": 536870912,
      "AuthContextHeader": "x-sb-auth-context",
      "IncludeAuthContextHeader": true,
      "Unauthenticated": {
        "ParameterizedUrls": {
          "GET": ["/public", "/api/users/{id}"],
          "POST": ["/api/signup"]
        }
      },
      "Authenticated": {
        "ParameterizedUrls": {
          "GET": ["/secure", "/api/profile/{id}"],
          "POST": ["/api/secure"]
        }
      },
      "RewriteUrls": {
        "GET": {
          "/api/users/{id}": "/users/{id}"  // Rewrite before forwarding
        }
      },
      "OriginServers": ["server1", "server2"]
    }
  ],
  "Origins": [
    {
      "Identifier": "server1",
      "Name": "Origin Server 1",
      "Hostname": "localhost",
      "Port": 8001,
      "Ssl": false,
      "HealthCheckIntervalMs": 5000,
      "HealthCheckMethod": "GET",
      "HealthCheckUrl": "/",
      "UnhealthyThreshold": 2,
      "HealthyThreshold": 2,
      "MaxParallelRequests": 10,
      "RateLimitRequestsThreshold": 30
    }
  ],
  "BlockedHeaders": ["connection", "host"],  // Global blocked headers
  "Webserver": {
    "Hostname": "localhost",
    "Port": 8000,
    "Ssl": { "Enable": false }
  }
}
```

## Important Implementation Details

### Thread Safety
- `OriginServer` uses `lock (Lock)` for health status synchronization
- `ApiEndpoint` uses `lock (Lock)` for load balancer index updates
- Origin request counters use `Interlocked` operations
- Semaphore limits parallel requests per origin server

### Error Responses
Switchboard returns structured error responses using `ApiErrorResponse`:
- **400 Bad Request**: No matching API endpoint found
- **401 Unauthorized**: Authentication/authorization failed
- **429 Too Many Requests**: Rate limit exceeded for origin server
- **502 Bad Gateway**: No healthy origin servers available
- **505 HTTP Version Not Supported**: HTTP/1.0 blocked (if configured)

### Health Check Behavior
- Health checks run continuously in background thread (`HealthCheckService`)
- Origin marked unhealthy after `UnhealthyThreshold` consecutive failures
- Origin marked healthy after `HealthyThreshold` consecutive successes
- Load balancer only selects from healthy origins
- If all origins unhealthy, returns 502 Bad Gateway