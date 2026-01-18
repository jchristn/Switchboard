# Migration Guide

This guide covers migrating from Switchboard v3.x to v4.0 and configuring the new features.

## Table of Contents

- [What's New in v4.0](#whats-new-in-v40)
- [Breaking Changes](#breaking-changes)
- [Migration Steps](#migration-steps)
- [Database Configuration](#database-configuration)
- [Management API Configuration](#management-api-configuration)
- [Request History Configuration](#request-history-configuration)
- [Configuration Reference](#configuration-reference)

---

## What's New in v4.0

Switchboard 4.0 introduces several major features:

- **Database Backend** - Store configuration in SQLite, MySQL, PostgreSQL, or SQL Server
- **Management REST API** - Full CRUD operations for runtime configuration
- **Web Dashboard** - React-based UI for configuration and monitoring
- **Request History** - Track and analyze requests with searchable history
- **User & Credential Management** - Multi-user access with bearer tokens
- **.NET 10.0 Support** - Now targets both .NET 8.0 and .NET 10.0

---

## Breaking Changes

### Configuration File Changes

v4.0 adds new top-level configuration sections. Existing v3.x configurations will continue to work, but won't have access to new features until updated.

**New sections in `sb.json`:**

```json
{
  "Database": { ... },      // NEW: Database backend
  "Management": { ... },    // NEW: Management API
  "RequestHistory": { ... } // NEW: Request tracking
}
```

### API Endpoint Configuration

The `Endpoints` array structure remains compatible. However, when using the database backend, endpoint configuration is stored in the database and the JSON `Endpoints` array is only used for initial seeding.

### Origin Server Configuration

The `Origins` array structure remains compatible. New optional fields have been added for request history capture:

```json
{
  "CaptureRequestBody": false,
  "CaptureResponseBody": false,
  "CaptureRequestHeaders": true,
  "CaptureResponseHeaders": true,
  "MaxCaptureRequestBodySize": 65536,
  "MaxCaptureResponseBodySize": 65536
}
```

---

## Migration Steps

### Step 1: Update Configuration File

Add the new sections to your `sb.json`:

```json
{
  "Logging": { ... },
  "Endpoints": [ ... ],
  "Origins": [ ... ],
  "BlockedHeaders": [ ... ],
  "Webserver": { ... },

  "Database": {
    "Type": "Sqlite",
    "Filename": "./data/switchboard.db"
  },
  "Management": {
    "Enable": true,
    "BasePath": "/_sb/v1.0/",
    "AdminToken": "your-secure-token",
    "RequireAuthentication": true
  },
  "RequestHistory": {
    "Enable": true,
    "RetentionDays": 7
  }
}
```

### Step 2: Create Data Directory

If using SQLite, create a directory for the database:

```bash
mkdir data
```

### Step 3: Start Switchboard

Start Switchboard normally. On first startup with the database enabled:

1. Database tables are created automatically
2. Existing `Endpoints` and `Origins` from JSON are imported into the database
3. A default admin user is created with the configured `AdminToken`

### Step 4: Verify Migration

```bash
# Check health
curl http://localhost:8000/_sb/v1.0/health

# List imported origins
curl -H "Authorization: Bearer your-token" \
  http://localhost:8000/_sb/v1.0/origins

# List imported endpoints
curl -H "Authorization: Bearer your-token" \
  http://localhost:8000/_sb/v1.0/endpoints
```

### Step 5: (Optional) Remove JSON Configuration

After verifying the database contains your configuration, you can optionally remove the `Endpoints` and `Origins` arrays from `sb.json`. The database becomes the source of truth.

---

## Database Configuration

### SQLite (Default)

Best for: Single-server deployments, development, small to medium workloads.

```json
{
  "Database": {
    "Type": "Sqlite",
    "Filename": "./data/switchboard.db"
  }
}
```

### MySQL / MariaDB

Best for: Production deployments requiring high availability.

```json
{
  "Database": {
    "Type": "Mysql",
    "Hostname": "localhost",
    "Port": 3306,
    "DatabaseName": "switchboard",
    "Username": "switchboard",
    "Password": "your-password",
    "Ssl": false
  }
}
```

**Setup:**

```sql
CREATE DATABASE switchboard;
CREATE USER 'switchboard'@'%' IDENTIFIED BY 'your-password';
GRANT ALL PRIVILEGES ON switchboard.* TO 'switchboard'@'%';
FLUSH PRIVILEGES;
```

### PostgreSQL

Best for: Production deployments with complex query requirements.

```json
{
  "Database": {
    "Type": "Postgres",
    "Hostname": "localhost",
    "Port": 5432,
    "DatabaseName": "switchboard",
    "Username": "switchboard",
    "Password": "your-password",
    "Ssl": false,
    "TrustServerCertificate": false
  }
}
```

**Setup:**

```sql
CREATE DATABASE switchboard;
CREATE USER switchboard WITH PASSWORD 'your-password';
GRANT ALL PRIVILEGES ON DATABASE switchboard TO switchboard;
```

### SQL Server

Best for: Enterprise environments with existing SQL Server infrastructure.

```json
{
  "Database": {
    "Type": "SqlServer",
    "Hostname": "localhost",
    "Port": 1433,
    "DatabaseName": "switchboard",
    "Username": "switchboard",
    "Password": "your-password",
    "Ssl": false,
    "TrustServerCertificate": true
  }
}
```

**Setup:**

```sql
CREATE DATABASE switchboard;
CREATE LOGIN switchboard WITH PASSWORD = 'your-password';
USE switchboard;
CREATE USER switchboard FOR LOGIN switchboard;
EXEC sp_addrolemember 'db_owner', 'switchboard';
```

### Using Connection Strings

For advanced scenarios, you can provide a full connection string:

```json
{
  "Database": {
    "Type": "Mysql",
    "ConnectionString": "Server=myserver;Port=3306;Database=switchboard;User=myuser;Password=mypassword;SslMode=Required;"
  }
}
```

When `ConnectionString` is provided, it takes precedence over individual parameters.

---

## Management API Configuration

### Full Configuration

```json
{
  "Management": {
    "Enable": true,
    "BasePath": "/_sb/v1.0/",
    "AdminToken": "your-secure-token",
    "RequireAuthentication": true
  }
}
```

### Settings Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enable` | boolean | `false` | Enable the Management API |
| `BasePath` | string | `"/_sb/v1.0/"` | Base path for API endpoints |
| `AdminToken` | string | `"sbadmin"` | Initial admin bearer token |
| `RequireAuthentication` | boolean | `true` | Require authentication for API access |

### Security Recommendations

1. **Use a strong AdminToken** - Generate a random token:
   ```bash
   openssl rand -base64 32
   ```

2. **Enable HTTPS** - Always use SSL in production

3. **Restrict network access** - Use firewall rules to limit access to the Management API

4. **Rotate tokens regularly** - Use the API to create new credentials and disable old ones

---

## Request History Configuration

### Full Configuration

```json
{
  "RequestHistory": {
    "Enable": true,
    "CaptureRequestBody": false,
    "CaptureResponseBody": false,
    "CaptureRequestHeaders": true,
    "CaptureResponseHeaders": true,
    "MaxRequestBodySize": 65536,
    "MaxResponseBodySize": 65536,
    "RetentionDays": 7,
    "MaxRecords": 100000,
    "CleanupIntervalSeconds": 3600
  }
}
```

### Settings Reference

| Setting | Type | Default | Description |
|---------|------|---------|-------------|
| `Enable` | boolean | `true` | Enable request history capture |
| `CaptureRequestBody` | boolean | `false` | Store request bodies |
| `CaptureResponseBody` | boolean | `false` | Store response bodies |
| `CaptureRequestHeaders` | boolean | `true` | Store request headers |
| `CaptureResponseHeaders` | boolean | `true` | Store response headers |
| `MaxRequestBodySize` | integer | `65536` | Max request body size to capture (bytes) |
| `MaxResponseBodySize` | integer | `65536` | Max response body size to capture (bytes) |
| `RetentionDays` | integer | `7` | Days to retain history (0 = unlimited) |
| `MaxRecords` | integer | `100000` | Max records to retain (0 = unlimited) |
| `CleanupIntervalSeconds` | integer | `3600` | Cleanup interval (60-86400 seconds) |

### Performance Considerations

- **Body capture** significantly increases storage usage
- Set appropriate `MaxRequestBodySize` and `MaxResponseBodySize` limits
- Use `RetentionDays` and `MaxRecords` to prevent unbounded growth
- Monitor database size in production

---

## Configuration Reference

### Complete v4.0 Configuration Example

```json
{
  "Logging": {
    "LogDirectory": "./logs/",
    "LogFilename": "switchboard.log",
    "ConsoleLogging": true,
    "MinimumSeverity": 1
  },

  "Database": {
    "Type": "Sqlite",
    "Filename": "./data/switchboard.db"
  },

  "Management": {
    "Enable": true,
    "BasePath": "/_sb/v1.0/",
    "AdminToken": "your-secure-token",
    "RequireAuthentication": true
  },

  "RequestHistory": {
    "Enable": true,
    "CaptureRequestBody": false,
    "CaptureResponseBody": false,
    "CaptureRequestHeaders": true,
    "CaptureResponseHeaders": true,
    "RetentionDays": 7,
    "MaxRecords": 100000
  },

  "Endpoints": [],
  "Origins": [],

  "BlockedHeaders": [
    "connection",
    "host",
    "transfer-encoding"
  ],

  "Webserver": {
    "Hostname": "*",
    "Port": 8000,
    "Ssl": {
      "Enable": false
    }
  }
}
```

### Docker Compose Files

Pre-configured compose files are available in the `Docker/` directory:

| File | Database | Config File |
|------|----------|-------------|
| `compose.sqlite.yaml` | SQLite | `sb.sqlite.json` |
| `compose.mysql.yaml` | MySQL 8.0 | `sb.mysql.json` |
| `compose.postgres.yaml` | PostgreSQL | `sb.postgres.json` |
| `compose.sqlserver.yaml` | SQL Server | `sb.sqlserver.json` |

Example:

```bash
cd Docker
docker compose -f compose.sqlite.yaml up -d
```

---

## Troubleshooting

### Database Connection Issues

**SQLite:**
- Ensure the directory exists and is writable
- Check file permissions

**MySQL/PostgreSQL/SQL Server:**
- Verify hostname and port are correct
- Check firewall rules
- Verify user credentials
- Test connection with database client first

### Migration Not Working

1. Check Switchboard logs for errors
2. Verify JSON syntax in `sb.json`
3. Ensure database is accessible
4. Check that `Database.Type` matches your database

### Management API Returns 401

1. Verify `Authorization: Bearer <token>` header is set
2. Check that token matches `Management.AdminToken`
3. Verify `Management.Enable` is `true`
4. Check if credential has expired

### Request History Empty

1. Verify `RequestHistory.Enable` is `true`
2. Check that database is configured
3. Make requests through the proxy (not directly to origins)
4. Verify endpoint routes are configured correctly

---

## Getting Help

- **Documentation**: See other files in `docs/`
- **REST API Reference**: [docs/REST_API.md](REST_API.md)
- **Dashboard Guide**: [docs/DASHBOARD-GUIDE.md](DASHBOARD-GUIDE.md)
- **Issues**: [GitHub Issues](https://github.com/jchristn/switchboard/issues)
