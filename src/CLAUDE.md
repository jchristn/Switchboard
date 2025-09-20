# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build
```bash
dotnet build
```

### Run Switchboard Server
```bash
cd Switchboard.Server/bin/Debug/net8.0
dotnet Switchboard.Server.dll
```

### Run Test Example
```bash
cd Test
dotnet run
```

### Run URL Rewrite Test
```bash
cd Test.UrlRewrite
dotnet run
```

### Package for NuGet (Switchboard.Core only)
```bash
dotnet pack Switchboard.Core/Switchboard.Core.csproj
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