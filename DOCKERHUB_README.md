<img src="https://raw.githubusercontent.com/jchristn/switchboard/main/assets/logo.png" alt="Switchboard" width="120" />

# Switchboard

Switchboard is a lightweight application proxy that combines a reverse proxy and an API gateway. It
sits in front of your backend services and handles endpoint matching, load balancing, health
checking, rate limiting, URL rewriting, and authentication callbacks — configured from a JSON file
or a database, and driven at runtime through a management API and a web dashboard.

Two images make up a deployment:

- **`jchristn77/switchboard`** — the proxy server.
- **`jchristn77/switchboard-ui`** — the web dashboard.

## When to use it

Reach for Switchboard when you want one front door for several backends without standing up a heavy
gateway. It fits a small cluster of services that need round-robin or random load balancing across
healthy origins, public and authenticated route groups on the same endpoint, per-origin rate limits,
and request history you can actually inspect. It runs embedded in a .NET app, as a standalone
binary, or as these containers.

## What's inside

- Reverse proxy with parameterized URL matching (`/users/{id}`) and per-method route groups
- Round-robin and random load balancing over health-checked origin servers
- Per-origin rate limiting and parallel-request caps
- URL rewriting before forwarding (handy for API versioning)
- Pluggable authentication/authorization via a callback, with an auth-context header forwarded to
  origins
- Chunked transfer and Server-Sent Events pass-through
- Request history with retention, plus an OpenAPI document and Swagger UI
- A management API under `/_sb/v1.0` and a full web dashboard (overview with an activity chart,
  settings editing with restart-required flags, a setup wizard, an API explorer, and
  internationalization in English, German, Japanese, and Arabic)
- SQLite, MySQL, PostgreSQL, or SQL Server for configuration storage

## Quick start

Run the server on its own:

```bash
docker run -d --name switchboard -p 8000:8000 \
  -v "$(pwd)/sb.json:/app/sb.json" \
  jchristn77/switchboard:v4.1.0
```

Or run the server and dashboard together with Compose (from the repository's `Docker/` directory):

```bash
docker compose up -d
```

That starts the proxy on `http://localhost:8000` and the dashboard on `http://localhost:3000`.
Open the dashboard, connect with your server URL and an admin bearer token (`sbadmin` on a fresh
install), and the setup wizard will walk you through your first origin server and API endpoint.

## Configuration

The server reads `sb.json` (or a database) at startup. A minimal configuration defines a webserver,
one or more origin servers, and endpoints that map routes to those origins:

```json
{
  "Webserver": { "Hostname": "localhost", "Port": 8000 },
  "Origins": [
    { "Identifier": "backend-1", "Hostname": "api.example.com", "Port": 443, "Ssl": true }
  ],
  "Endpoints": [
    {
      "Identifier": "user-api",
      "LoadBalancing": "RoundRobin",
      "Unauthenticated": { "ParameterizedUrls": { "GET": ["/health"] } },
      "Authenticated": { "ParameterizedUrls": { "GET": ["/users/{id}"] } },
      "OriginServers": ["backend-1"]
    }
  ]
}
```

## Tags

- `v4.1.0` — current release
- `latest` — most recent build

## Links

- Source, full documentation, and issues: https://github.com/jchristn/switchboard
- REST API reference: https://github.com/jchristn/switchboard/blob/main/docs/REST_API.md
- Changelog: https://github.com/jchristn/switchboard/blob/main/CHANGELOG.md

Licensed under the MIT license.
