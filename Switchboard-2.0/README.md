<img src="https://github.com/jchristn/switchboard/blob/main/assets/icon.png?raw=true" width="140" height="128" alt="Switchboard">

# Switchboard

Switchboard is a lightweight application proxy combining reverse proxy and API gateway functionality.  Switchboard can be integrated directly into your app or run as a standalone server.

## Help, Feedback, Contribute

If you have any issues or feedback, please file an issue here in Github. We'd love to have you help by contributing code for new features, optimization to the existing codebase, ideas for future releases, or fixes!

## New in v2.0.x

- Added authentication support
- Reorganized API endpoints into groups (`ApiEndpointGroup`) for authenticated `Authenticated` and unauthenticated `Unauthenticated`

## Default Configuration

By default, Switchboard server will listen on `http://localhost:8000/` and is not configured with API endpoints or origin servers (see below for an example).  If you point your browser to `http://localhost:8000/` you will see a default page indicating that the node is operational.  `HEAD` requests to this URL will also return a `200/OK`.

## Example (Integrated)

Refer to the `Test` project for a working example with one API endpoint and two origin servers.

```csharp
using Switchboard.Core;

// initialize settings
SwitchboardSettings settings = new SwitchboardSettings();

// add API endpoints
settings.Endpoints.Add(new ApiEndpoint
{
    Identifier = "my-api-endpoint",
    Name = "My API endpoint",
    LoadBalancing = LoadBalancingMode.RoundRobin,
    Unauthenticated = new ApiEndpointGroup // URLs that do not require authentication via the authentication callback
    {
        ParameterizedUrls = new Dictionary<string, List<string>>
        {
            { "GET", new List<string> { "/unauthenticated" } },
        }
    },
    Authenticated = new ApiEndpointGroup // URLs that require authentication via the authentication callback
    {
        ParameterizedUrls = new Dictionary<string, List<string>>
        {
            { "GET", new List<string> { "/authenticated" } },
            { "GET", new List<string> { "/users/{UserGuid}" } }
        }
    },
    RewriteUrls = new Dictionary<string, Dictionary<string, string>>
    {
        {
            "GET", new Dictionary<string, string> 
            {
                "/users/{UserGuid}", "/{UserGuid}" // rewrite /users/foo to just /foo
            }
        }
    },
    OriginServers = new List<string>
    {
        "my-origin-server"
    }
});

// add origin servers
settings.Origins.Add(new OriginServer
{
    Identifier = "my-origin-server",
    Name = "My origin server",
    Hostname = "localhost",
    Port = 8001,
    Ssl = false
});

// define the authentication and authorization callback
private static async Task<AuthContext> AuthenticateAndAuthorizeRequest(HttpContextBase ctx)
{
    return new AuthContext
    {
        Authentication = new AuthenticationContext
        {
            Result = AuthenticationResultEnum.Success,
            Metadata = new Dictionary<string, string>() // use this object as you wish
            {
                { "Authenticated", "true" }
            }
        },
        Authorization = new AuthorizationContext
        {
            Result = AuthorizationResultEnum.Success,
            Metadata = new Dictionary<string, string>() // use this object as you wish
            {
                { "Authorized", "true" }
            }
        },
        Metadata = new Dictionary<string, string>() // use this object as you wish
        {
            { "Allow", "true" }
        }
    };
}

// start Switchboard
using (SwitchboardDaemon sb = new SwitchboardDaemon(settings))
{
    sb.Callbacks.AuthenticateAndAuthorize = AuthenticateAndAuthorizeRequest;
    ...
}
```

## Example (Standalone)

```csharp
$ cd /path/to/src-directory
$ dotnet build
$ cd Switchboard.Server/bin/Debug/net8.0
$ dotnet Switchboard.Server.dll


            _ _      _    _                      _
  ____ __ _(_) |_ __| |_ | |__  ___  __ _ _ _ __| |
 (_-< V  V / |  _/ _| ' \| '_ \/ _ \/ _` | '_/ _` |
 /__/\_/\_/|_|\__\__|_||_|_.__/\___/\__,_|_| \__,_|


Switchboard Server v1.0.0

Loading from settings file ./sb.json
2024-12-05 03:30:01 INSPIRON-14 Info [SwitchboardDaemon] webserver started on http://localhost:8000
2024-12-05 03:30:01 INSPIRON-14 Info [SwitchboardDaemon] Switchboard Server started using process ID 49308
```

## Docker

A Docker image is available in [Docker Hub](https://hub.docker.com/r/jchristn/switchboard) under `jchristn/switchboard`.  Use the Docker Compose start (`compose-up.sh` and `compose-up.bat`) and stop (`compose-down.sh` and `compose-down.bat`) scripts in the `Docker` directory if you wish to run within Docker Compose.  Ensure that you have a valid configuration file (e.g. `sb.json`) exposed into your container.

## Version History

Refer to CHANGELOG.md for version history.
