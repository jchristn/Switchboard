# Switchboard Sample Application

A standalone console app that runs the **Switchboard daemon in-process** (no Docker) as a reverse
proxy / API gateway in front of three [WatsonWebserver](https://www.nuget.org/packages/Watson) origin
servers. Several routes are load balanced across different subsets of the origins so the routing and
load-balancing behavior is easy to observe with `curl`.

## What it sets up

- **Three origins** (WatsonWebserver), one per node, on `localhost:9001`, `:9002`, `:9003`. Every
  response says which node served it.
- **The Switchboard proxy** on `localhost:8000` (override with a port argument), configured entirely
  in code — no database seeding or management API required.

## Routes

| Method & path | Eligible origins | Response body |
|---|---|---|
| `GET /` | any node (1, 2, 3) | `Hello from node {N}` |
| `GET /route1` | nodes 1, 2 | `Hello from route1, served by node {N} (valid values: 1 or 2)` |
| `GET /route2` | nodes 2, 3 | `Hello from route2, served by node {N} (valid values: 2 or 3)` |
| `GET /route3` | nodes 1, 3 | `Hello from route3, served by node {N} (valid values: 1 or 3)` |
| `POST /echo` | any node | `Hello from the echo route, served by node {N}.  You said: {request body}` |

Repeat any request to watch the load balancer rotate (round-robin) between the eligible nodes.

## Run it

```bash
cd src/SampleApplication
dotnet run                # proxy on http://localhost:8000
dotnet run -- 8100        # or choose a different proxy port
```

Then, from another terminal:

```bash
curl http://localhost:8000/
curl http://localhost:8000/route1
curl http://localhost:8000/route2
curl http://localhost:8000/route3
curl -X POST -d 'switchboard rocks' http://localhost:8000/echo
```

Press `Ctrl+C` to stop. The daemon writes a local SQLite file (`sampleapplication.db`) in the working
directory for its configuration store.
