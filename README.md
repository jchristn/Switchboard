<div align="center">
  <img src="https://github.com/jchristn/switchboard/blob/main/assets/icon.png?raw=true" width="140" height="128" alt="Switchboard">

  # Switchboard

  **A lightweight reverse proxy and API gateway for .NET**

  [![NuGet Version](https://img.shields.io/nuget/v/SwitchboardApplicationProxy.svg?style=flat)](https://www.nuget.org/packages/SwitchboardApplicationProxy/) [![NuGet Downloads](https://img.shields.io/nuget/dt/SwitchboardApplicationProxy.svg)](https://www.nuget.org/packages/SwitchboardApplicationProxy/) [![Docker Hub](https://img.shields.io/badge/docker-jchristn%2Fswitchboard-blue.svg)](https://hub.docker.com/repository/docker/jchristn/switchboard/general) [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
</div>

---

## Overview

Switchboard is a **production-ready reverse proxy and API gateway** that combines enterprise-grade features with .NET simplicity. Route traffic to multiple backends, automatically handle failures, enforce rate limits, and implement custom authentication—all with minimal configuration.

**🚀 Flexible Deployment:** Embed directly into your .NET application as a library or run as a standalone server.

---

## Table of Contents

- [What is Switchboard?](#what-is-switchboard)
- [Key Features](#key-features)
- [Who is it for?](#who-is-it-for)
- [When to Use Switchboard](#when-to-use-switchboard)
- [What It Does](#what-it-does)
- [What It Doesn't Do](#what-it-doesnt-do)
- [Quick Start](#quick-start)
- [Installation](#installation)
- [Usage Examples](#usage-examples)
  - [Integrated (Library)](#integrated-library)
  - [Standalone Server](#standalone-server)
  - [Docker](#docker)
- [Configuration](#configuration)
- [Advanced Features](#advanced-features)
- [Support](#support)
- [Contributing](#contributing)
- [License](#license)
- [Version History](#version-history)

---

## What is Switchboard?

Switchboard is a **lightweight application proxy** that combines reverse proxy and API gateway functionality for .NET applications. It acts as an intelligent intermediary between your clients and backend services, providing:

- **Traffic routing** to multiple origin servers
- **Automatic health checking** and failover
- **Load balancing** across healthy backends
- **Rate limiting** to protect your services
- **Authentication and authorization** via flexible callbacks
- **URL rewriting** for API versioning
- **Protocol support** for HTTP, chunked transfer encoding, and server-sent events (SSE)

Built on **.NET 8.0**, Switchboard is designed for developers who need a simple, embeddable gateway without the complexity of heavyweight solutions.

---

## Key Features

✅ **Flexible Load Balancing** – Round-robin or random distribution across healthy origin servers
✅ **Automatic Health Checks** – Continuous monitoring with configurable thresholds
✅ **Rate Limiting** – Per-origin concurrent request limits and throttling
✅ **Custom Authentication** – Callback-based auth/authz with context forwarding
✅ **URL Rewriting** – Transform URLs before proxying to backends
✅ **Protocol Support** – HTTP/1.1, chunked transfer encoding, server-sent events
✅ **Smart Routing** – Parameterized URLs with wildcard matching (`/users/{id}`)
✅ **Header Management** – Automatic proxy headers and configurable blocking
✅ **Logging** – Built-in syslog integration with multiple severity levels
✅ **Docker Ready** – Available on [Docker Hub](https://hub.docker.com/repository/docker/jchristn/switchboard/general)
✅ **Embeddable** – Integrate directly into your application via NuGet

---

## Who is it for?

Switchboard is designed for:

- **Backend Developers** building microservices architectures
- **DevOps Engineers** needing lightweight reverse proxy solutions
- **API Platform Teams** implementing centralized gateways
- **.NET Developers** who want embeddable proxy functionality
- **System Architects** designing high-availability systems
- **Startups** seeking simple, cost-effective infrastructure

---

## When to Use Switchboard

**Perfect for:**

- Routing requests to multiple backend microservices
- Load balancing across identical service instances
- Centralizing authentication and authorization
- Implementing API versioning with URL rewrites
- Protecting backends from overload with rate limiting
- Building high-availability systems with automatic failover
- Proxying server-sent events or chunked responses
- Embedding a gateway directly into your .NET application

**Not ideal for:**

- Simple single-backend scenarios (use a direct connection)
- WebSocket proxying (use dedicated WebSocket gateways)
- Advanced routing rules (use full-featured API gateways like Kong, Tyk, or NGINX)
- Layer 4 load balancing (use HAProxy or cloud load balancers)

---

## What It Does

Switchboard provides:

1. **Request Routing** – Match incoming requests to API endpoints using parameterized URLs
2. **Load Balancing** – Distribute traffic across multiple origin servers (round-robin or random)
3. **Health Monitoring** – Automatically detect and route around unhealthy backends
4. **Rate Limiting** – Enforce concurrent request limits per origin server
5. **Authentication** – Invoke custom callbacks for auth/authz decisions
6. **Authorization Context** – Forward auth metadata to origin servers via headers
7. **URL Transformation** – Rewrite URLs before forwarding to backends
8. **Error Handling** – Return structured JSON error responses
9. **Protocol Handling** – Support chunked transfer encoding and server-sent events
10. **Logging** – Comprehensive request/response logging with syslog integration

---

## What It Doesn't Do

Switchboard is **intentionally lightweight** and does not include:

- ❌ WebSocket proxying
- ❌ Advanced traffic shaping or QoS
- ❌ Built-in caching (use Redis or CDN)
- ❌ Request transformation/mutation (header rewriting beyond proxy headers)
- ❌ OAuth/JWT validation (implement via callbacks)
- ❌ GraphQL federation
- ❌ Service mesh features (circuit breakers, retries, distributed tracing)
- ❌ GUI/dashboard (use monitoring tools like Grafana)

For these features, consider integrating Switchboard with specialized tools or using enterprise API gateways.

---

## Quick Start

### 1. Install via NuGet (Integrated)

```bash
dotnet add package SwitchboardApplicationProxy
```

### 2. Run as Standalone Server

```bash
# Clone the repository
git clone https://github.com/jchristn/switchboard.git
cd Switchboard-3.0/src

# Build the solution
dotnet build

# Run the server
cd Switchboard.Server/bin/Debug/net8.0
dotnet Switchboard.Server.dll
```

### 3. Run with Docker

```bash
docker pull jchristn/switchboard
docker run -p 8000:8000 -v $(pwd)/sb.json:/app/sb.json jchristn/switchboard
```

Visit `http://localhost:8000/` to confirm Switchboard is running!

---

## Installation

### NuGet Package

Install the `SwitchboardApplicationProxy` package from NuGet:

```bash
dotnet add package SwitchboardApplicationProxy
```

Or via Package Manager Console:

```powershell
Install-Package SwitchboardApplicationProxy
```

### Docker Image

Pull from Docker Hub:

```bash
docker pull jchristn/switchboard
```

Docker image: **[jchristn/switchboard](https://hub.docker.com/repository/docker/jchristn/switchboard/general)**

### Build from Source

```bash
git clone https://github.com/jchristn/switchboard.git
cd Switchboard-3.0/src
dotnet build
```

---

## Usage Examples

### Integrated (Library)

Embed Switchboard directly into your .NET application:

```csharp
using Switchboard.Core;

// Initialize settings
SwitchboardSettings settings = new SwitchboardSettings();

// Add API endpoint with parameterized URLs
settings.Endpoints.Add(new ApiEndpoint
{
    Identifier = "user-api",
    Name = "User Management API",
    LoadBalancing = LoadBalancingMode.RoundRobin,

    // Public routes (no authentication)
    Unauthenticated = new ApiEndpointGroup
    {
        ParameterizedUrls = new Dictionary<string, List<string>>
        {
            { "GET", new List<string> { "/health", "/status" } }
        }
    },

    // Protected routes (require authentication)
    Authenticated = new ApiEndpointGroup
    {
        ParameterizedUrls = new Dictionary<string, List<string>>
        {
            { "GET", new List<string> { "/users", "/users/{userId}" } },
            { "POST", new List<string> { "/users" } },
            { "PUT", new List<string> { "/users/{userId}" } },
            { "DELETE", new List<string> { "/users/{userId}" } }
        }
    },

    // URL rewriting (e.g., API versioning)
    RewriteUrls = new Dictionary<string, Dictionary<string, string>>
    {
        {
            "GET", new Dictionary<string, string>
            {
                { "/users/{userId}", "/api/v2/users/{userId}" }
            }
        }
    },

    // Associate with origin servers
    OriginServers = new List<string> { "backend-1", "backend-2" }
});

// Add origin servers
settings.Origins.Add(new OriginServer
{
    Identifier = "backend-1",
    Name = "Backend Server 1",
    Hostname = "api1.example.com",
    Port = 443,
    Ssl = true,
    HealthCheckUrl = "/health",
    MaxParallelRequests = 20,
    RateLimitRequestsThreshold = 50
});

settings.Origins.Add(new OriginServer
{
    Identifier = "backend-2",
    Name = "Backend Server 2",
    Hostname = "api2.example.com",
    Port = 443,
    Ssl = true,
    HealthCheckUrl = "/health",
    MaxParallelRequests = 20,
    RateLimitRequestsThreshold = 50
});

// Start Switchboard
using (SwitchboardDaemon sb = new SwitchboardDaemon(settings))
{
    // Define authentication callback
    sb.Callbacks.AuthenticateAndAuthorize = async (ctx) =>
    {
        // Custom authentication logic (e.g., JWT validation)
        string authHeader = ctx.Request.Headers.Get("Authorization");

        if (string.IsNullOrEmpty(authHeader))
        {
            return new AuthContext
            {
                Authentication = new AuthenticationContext { Result = AuthenticationResultEnum.Unauthenticated },
                Authorization = new AuthorizationContext { Result = AuthorizationResultEnum.Unauthorized }
            };
        }

        // Validate token (example)
        bool isValid = ValidateToken(authHeader);

        return new AuthContext
        {
            Authentication = new AuthenticationContext
            {
                Result = isValid ? AuthenticationResultEnum.Success : AuthenticationResultEnum.Unauthenticated,
                Metadata = new Dictionary<string, string> { { "UserId", "12345" } }
            },
            Authorization = new AuthorizationContext
            {
                Result = isValid ? AuthorizationResultEnum.Success : AuthorizationResultEnum.Unauthorized,
                Metadata = new Dictionary<string, string> { { "Role", "Admin" } }
            }
        };
    };

    Console.WriteLine("Switchboard is running on http://localhost:8000");
    Console.ReadLine();
}
```

### Standalone Server

Run Switchboard as an independent server using a configuration file:

#### 1. Create `sb.json` configuration:

```json
{
  "Logging": {
    "SyslogServerIp": null,
    "SyslogServerPort": 514,
    "MinimumSeverity": "Info",
    "LogRequests": true,
    "LogResponses": false,
    "ConsoleLogging": true
  },
  "Endpoints": [
    {
      "Identifier": "my-api",
      "Name": "My API",
      "LoadBalancing": "RoundRobin",
      "Unauthenticated": {
        "ParameterizedUrls": {
          "GET": ["/health", "/status"]
        }
      },
      "Authenticated": {
        "ParameterizedUrls": {
          "GET": ["/users", "/users/{id}"],
          "POST": ["/users"],
          "PUT": ["/users/{id}"],
          "DELETE": ["/users/{id}"]
        }
      },
      "OriginServers": ["backend-1", "backend-2"]
    }
  ],
  "Origins": [
    {
      "Identifier": "backend-1",
      "Name": "Backend 1",
      "Hostname": "localhost",
      "Port": 8001,
      "Ssl": false,
      "HealthCheckIntervalMs": 5000,
      "HealthyThreshold": 2,
      "UnhealthyThreshold": 2,
      "MaxParallelRequests": 10,
      "RateLimitRequestsThreshold": 30
    },
    {
      "Identifier": "backend-2",
      "Name": "Backend 2",
      "Hostname": "localhost",
      "Port": 8002,
      "Ssl": false,
      "HealthCheckIntervalMs": 5000,
      "HealthyThreshold": 2,
      "UnhealthyThreshold": 2,
      "MaxParallelRequests": 10,
      "RateLimitRequestsThreshold": 30
    }
  ],
  "Webserver": {
    "Hostname": "localhost",
    "Port": 8000
  }
}
```

#### 2. Start the server:

```bash
dotnet Switchboard.Server.dll

            _ _      _    _                      _
  ____ __ _(_) |_ __| |_ | |__  ___  __ _ _ _ __| |
 (_-< V  V / |  _/ _| ' \| '_ \/ _ \/ _` | '_/ _` |
 /__/\_/\_/|_|\__\__|_||_|_.__/\___/\__,_|_| \__,_|

Switchboard Server v3.0.x

Loading from settings file ./sb.json
[INFO] Webserver started on http://localhost:8000
[INFO] Switchboard Server started
```

#### 3. Test the endpoint:

```bash
curl http://localhost:8000/health
```

### Docker

#### Using Docker Compose:

```yaml
# docker-compose.yml
version: '3.8'
services:
  switchboard:
    image: jchristn/switchboard:latest
    ports:
      - "8000:8000"
    volumes:
      - ./sb.json:/app/sb.json
    restart: unless-stopped
```

Start with Docker Compose:

```bash
# Use provided scripts
cd Docker
./compose-up.sh  # Linux/Mac
compose-up.bat   # Windows

# Or manually
docker-compose up -d
```

Stop with Docker Compose:

```bash
./compose-down.sh  # Linux/Mac
compose-down.bat   # Windows
```

#### Manual Docker Run:

```bash
docker run -d \
  --name switchboard \
  -p 8000:8000 \
  -v $(pwd)/sb.json:/app/sb.json \
  jchristn/switchboard:latest
```

---

## Configuration

### Default Behavior

By default, Switchboard:

- Listens on **`http://localhost:8000/`**
- Returns a default homepage at `/` (GET/HEAD requests)
- Requires explicit API endpoint and origin server configuration

### Health Check Defaults

If not explicitly configured:

- **Method:** `GET /`
- **Interval:** Every 5 seconds (`HealthCheckIntervalMs`)
- **Unhealthy Threshold:** 2 consecutive failures (`UnhealthyThreshold`)
- **Healthy Threshold:** 2 consecutive successes (`HealthyThreshold`)

### Rate Limiting Defaults

If not explicitly configured:

- **Max Parallel Requests:** 10 per origin (`MaxParallelRequests`)
- **Rate Limit Threshold:** 30 total requests (active + pending) per origin (`RateLimitRequestsThreshold`)

### Configuration File Options

For standalone deployment, customize `sb.json` with:

- **Logging** – Syslog servers, severity levels, console output
- **Endpoints** – API routes, authentication groups, URL rewrites
- **Origins** – Backend servers, health checks, rate limits
- **BlockedHeaders** – Headers to filter from requests/responses
- **Webserver** – Hostname, port, SSL settings

Refer to the `Test` project for a comprehensive configuration example.

---

## Advanced Features

### URL Rewriting

Transform URLs before proxying to backends:

```csharp
RewriteUrls = new Dictionary<string, Dictionary<string, string>>
{
    {
        "GET", new Dictionary<string, string>
        {
            { "/v2/users/{userId}", "/v1/users/{userId}" }, // API versioning
            { "/api/data", "/legacy/data" }                 // Path migration
        }
    }
}
```

### Authentication Context Forwarding

Pass authentication metadata to origin servers:

```csharp
sb.Callbacks.AuthenticateAndAuthorize = async (ctx) =>
{
    return new AuthContext
    {
        Authentication = new AuthenticationContext
        {
            Result = AuthenticationResultEnum.Success,
            Metadata = new Dictionary<string, string>
            {
                { "UserId", "12345" },
                { "Email", "user@example.com" }
            }
        },
        Authorization = new AuthorizationContext
        {
            Result = AuthorizationResultEnum.Success,
            Metadata = new Dictionary<string, string>
            {
                { "Role", "Admin" },
                { "Permissions", "read,write,delete" }
            }
        }
    };
};
```

The auth context is automatically serialized and forwarded via the `x-sb-auth-context` header (base64-encoded).

### Server-Sent Events (SSE)

Switchboard transparently proxies server-sent events:

```csharp
// No special configuration needed
// SSE streams are automatically detected via Content-Type: text/event-stream
```

### Chunked Transfer Encoding

Automatically handled for both requests and responses.

---

## Support

### Getting Help

- **Documentation:** Check the [README](README.md) and code examples in the `Test` project
- **GitHub Issues:** Report bugs or request features at [GitHub Issues](https://github.com/jchristn/switchboard/issues)
- **GitHub Discussions:** Ask questions and share ideas at [GitHub Discussions](https://github.com/jchristn/switchboard/discussions)

We welcome your feedback and contributions!

---

## Contributing

We'd love your help improving Switchboard! Contributions are welcome in many forms:

- **Code:** New features, bug fixes, performance optimizations
- **Documentation:** Improve guides, add examples, fix typos
- **Testing:** Write tests, report bugs, validate fixes
- **Ideas:** Suggest features, share use cases, provide feedback

### How to Contribute:

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to your branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

Please ensure your code follows existing conventions and includes tests where applicable.

---

## License

Switchboard is licensed under the **MIT License**.

See the [LICENSE.md](LICENSE.md) file for details.

**TL;DR:** You can use, modify, and distribute Switchboard freely, including for commercial purposes. No warranty is provided.
