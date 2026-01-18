# Switchboard Dashboard User Guide

The Switchboard Dashboard is a web-based management interface for configuring and monitoring your Switchboard API gateway.

## Getting Started

### Prerequisites

- A running Switchboard server with the Management API enabled
- For development: Node.js 18+
- For Docker: Docker and Docker Compose

### Running the Dashboard

#### Docker (Recommended)

The easiest way to run both the server and dashboard is with Docker Compose:

```bash
cd Docker

# Start with SQLite database (recommended for getting started)
docker compose -f compose.sqlite.yaml up -d
```

This starts:
- **Switchboard Server** at `http://localhost:8000`
- **Web Dashboard** at `http://localhost:3000`

Stop the services:

```bash
docker compose down
```

#### Development Mode

For local development without Docker:

```bash
cd dashboard
npm install
npm run dev
```

The dashboard will be available at `http://localhost:5173`

#### Production Build

```bash
cd dashboard
npm run build
```

The built files will be in `dashboard/dist/` and can be served by any static file server.

### Connecting to Switchboard

1. Open the dashboard in your browser
2. Enter your Switchboard server URL (e.g., `http://localhost:8000`)
3. Enter your admin API token (configured in `sb.json` as `Management.AdminToken`, default: `switchboardadmin`)
4. Click **Connect**

**Docker Default Credentials:**
- Server URL: `http://localhost:8000`
- Admin Token: `switchboardadmin` (from `sb.sqlite.json`)

## Dashboard Sections

### Overview

The Overview page provides a quick summary of your gateway's status:

- **System Health** - Overall health status
- **Origin Servers** - Count and health status of backend servers
- **API Endpoints** - Number of configured endpoints
- **Recent Activity** - Request statistics

### Origin Servers

Manage your backend servers from this section.

#### Viewing Origins

The table displays all configured origin servers with:
- Health status indicator (green = healthy, red = unhealthy)
- Server identifier
- Hostname and port
- SSL status
- Active/pending request counts

#### Adding an Origin

1. Click **Add Origin**
2. Fill in the required fields:
   - **Identifier** - Unique name for this server
   - **Hostname** - Server hostname or IP
   - **Port** - Server port
   - **SSL** - Enable for HTTPS connections
3. Configure health checks:
   - **Health Check URL** - Endpoint to check (e.g., `/health`)
   - **Health Check Method** - HTTP method (HEAD, GET)
   - **Check Interval** - Milliseconds between checks
   - **Unhealthy Threshold** - Failures before marking unhealthy
   - **Healthy Threshold** - Successes before marking healthy
4. Configure rate limiting:
   - **Max Parallel Requests** - Concurrent request limit
   - **Rate Limit Threshold** - Requests per time window
5. Click **Save**

#### Editing an Origin

1. Click the **Edit** button on the origin row
2. Modify the settings
3. Click **Save**

Changes take effect immediately without restarting Switchboard.

#### Deleting an Origin

1. Click the **Delete** button
2. Confirm the deletion

**Note:** You cannot delete an origin that is mapped to endpoints. Remove the mappings first.

### API Endpoints

Manage your API routing configuration.

#### Viewing Endpoints

The table shows:
- Endpoint identifier and name
- Number of routes
- Number of mapped origins
- Load balancing mode

#### Adding an Endpoint

1. Click **Add Endpoint**
2. Fill in basic info:
   - **Identifier** - Unique name
   - **Name** - Display name
   - **Timeout** - Request timeout in milliseconds
   - **Load Balancing** - RoundRobin or Random
3. Click **Save**

After creating, add routes and map origins.

#### Managing Routes

Routes define which URL patterns this endpoint handles.

1. Open the endpoint editor
2. In the **Routes** section, click **Add Route**
3. Configure:
   - **HTTP Method** - GET, POST, PUT, DELETE, etc.
   - **URL Pattern** - Pattern with optional parameters (e.g., `/api/users/{id}`)
   - **Requires Authentication** - Check if auth is required
4. Click **Add**

**URL Pattern Syntax:**
- Literal paths: `/api/users`
- Parameters: `/api/users/{id}` (matches `/api/users/123`)
- Wildcards: `/api/*` (matches anything under `/api/`)

#### Managing Origin Mappings

Map backend servers to this endpoint.

1. Open the endpoint editor
2. In the **Origins** section, click **Add Origin**
3. Select an origin server from the dropdown
4. Click **Add**

Requests are distributed across mapped origins based on the load balancing mode.

#### URL Rewrite Rules

Transform URLs before forwarding to origins.

1. Open the endpoint editor
2. In the **Rewrites** section, click **Add Rewrite**
3. Configure:
   - **HTTP Method** - Method to match
   - **Source Pattern** - Incoming URL pattern
   - **Target Pattern** - Rewritten URL
4. Click **Add**

**Example:**
- Source: `/api/v2/users/{id}`
- Target: `/users/{id}`

Requests to `/api/v2/users/123` are forwarded as `/users/123`.

### Request History

View and analyze request logs.

#### Filtering

Use the filter panel to narrow results:
- **Date Range** - Start and end timestamps
- **Endpoint** - Filter by endpoint
- **Origin** - Filter by origin server
- **Status Code** - Filter by response status
- **HTTP Method** - Filter by method
- **Path Contains** - Search in request path
- **Min Duration** - Filter slow requests

#### Viewing Details

Click any row to see full request details:
- Complete URL and query string
- Client IP address
- Request headers
- Request body (if captured)
- Response status and headers
- Response body (if captured)
- Duration and timing

#### Cleanup

To purge old history:
1. Click **Cleanup**
2. Enter the number of days to retain
3. Confirm deletion

### Settings

Configure dashboard and gateway settings.

#### Blocked Headers

Manage headers that are stripped from forwarded requests.

1. View current blocked headers
2. Click **Add** to block a new header
3. Click **Remove** to unblock a header

Default blocked headers:
- `connection`
- `host`
- `transfer-encoding`
- `x-forwarded-for`
- etc.

#### History Settings

If exposed via API, you can configure:
- Enable/disable history capture
- Body capture settings
- Retention period

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `Esc` | Close modal |
| `Enter` | Submit form (in modals) |

## Troubleshooting

### Cannot Connect

1. Verify the Switchboard server is running
2. Check the server URL is correct (include port)
3. Verify the admin token matches `sb.json` configuration
4. Check browser console for CORS errors

### Changes Not Reflected

Configuration changes are applied immediately. If changes don't appear:
1. Refresh the dashboard
2. Check the Switchboard server logs for errors
3. Verify the database is writable

### Request History Empty

1. Verify `RequestHistory.enable` is `true` in `sb.json`
2. Make some requests through the gateway
3. Check that the database is configured correctly

## Security Considerations

1. **Use HTTPS** - Always use HTTPS in production
2. **Strong Token** - Use a long, random admin token
3. **Network Security** - Restrict access to the Management API
4. **Regular Rotation** - Periodically rotate admin tokens
