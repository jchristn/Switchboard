# Load Balancing and Routing in Switchboard

Switchboard decides where each request goes through a small, predictable pipeline rather than a single
algorithm. A request arrives, matches an endpoint, and then the gateway works down a fixed sequence of
decisions — is this an explicit canary, which origins are even usable right now, which priority tier is
in play, is this client pinned to an origin, and finally which of the remaining origins the endpoint's
load-balancing mode prefers. Every feature in this document is one layer in that sequence, so you can
combine them freely: weighted routing with sticky sessions and passive ejection is just three layers
turned on at once, not a special mode you have to pick.

The configuration lives in three places, and knowing which is which makes everything else easier. An
**origin server** owns properties of the backend itself — how aggressively to eject it when it misbehaves,
how long to ramp it after it recovers. An **endpoint** owns the routing policy for a group of routes — the
load-balancing mode, whether sessions are sticky, how many times to retry. An **endpoint-origin mapping**
owns the relationship between the two — this origin's weight and priority *for this endpoint*, and any
canary header that should pin traffic to it. Weight and priority sit on the mapping deliberately: the same
backend can be a full-weight member of one endpoint and a 5%-weight canary of another.

## The selection pipeline

For every request the gateway runs `OriginSelector` over the endpoint's origins, in this order.

1. **Canary header match.** If any origin bound to the endpoint declares a `canaryHeader`/`canaryValue`
   and the incoming request carries that header with that value, routing is restricted to the matching
   origins. This is how you send a specific request — a QA client, an internal tester, a header injected by
   an upstream — to a specific build. If the matched origin happens to be unavailable, the request falls
   through to the normal pool rather than failing, so a canary target going down never takes traffic with it.
2. **Availability filter.** Origins are dropped if they are failing active health checks, currently ejected
   by passive health checking, drained (`weight` of 0), or already tried earlier in this same request during
   a retry. What remains is the set of origins that can actually serve the request right now.
3. **Priority tier.** Among the available origins, only the lowest `priority` number present is kept.
   Priority 0 is the primary tier; higher numbers are backups that see no traffic until the entire tier
   below them is gone. This gives you cold-standby and active/passive topologies without a separate feature.
4. **Sticky-session affinity.** If the endpoint enables sticky sessions, the gateway derives an affinity key
   (a named header's value, or the client IP when no header is configured) and consistent-hashes it across
   the surviving tier, weighted by each origin's effective weight. The same key lands on the same origin as
   long as that origin stays available, and the choice overrides the base mode.
5. **Load-balancing mode.** Finally, the endpoint's mode chooses among what's left, using each origin's
   *effective weight* — its configured weight scaled by its slow-start ramp.

Reading the pipeline top to bottom also tells you how features interact. Slow start doesn't fight with
weighted routing; it feeds it a smaller effective weight. Ejection doesn't fight with priority tiers; it
removes an origin before the tier is even computed. Nothing later in the list can resurrect an origin that
an earlier layer removed.

## Load-balancing modes

Six modes are available per endpoint. The first two ignore weight; the rest are weight-aware.

| Mode | Behavior | Reach for it when |
|------|----------|-------------------|
| `RoundRobin` | Rotates through the tier in order. | You want simple, even distribution and your backends are interchangeable. |
| `Random` | Uniform random pick. | You want statelessness and don't care about perfectly even spread. |
| `LeastConnections` | Picks the origin with the fewest in-flight requests, divided by its effective weight. | Requests vary a lot in duration and you want to avoid piling onto a backend that's still working. |
| `PowerOfTwoChoices` | Samples two origins at random and takes the less-busy one (load ÷ effective weight). | You want most of least-connections' benefit without its herd effect or full scan. This is Envoy's default for good reason. |
| `Weighted` | Random pick with probability proportional to effective weight. | Backends have different capacity, or you're splitting traffic for a canary. |
| `LatencyBased` | Picks the lowest exponentially-weighted moving average of response latency; origins with no samples yet are tried first so they gather data. | Latency matters more than connection count and your backends differ in speed. |

Least-connections and power-of-two-choices read the same live counters the rate limiter uses, so they cost
almost nothing. Latency-based maintains one number per origin, updated on every successful response with a
smoothing factor you can tune (`EwmaSmoothingFactor`, default 0.3).

## Weight, priority, and drain

Weight is a relative number, not a percentage. Two origins at weights 1 and 3 split roughly 25/75; the same
split comes from 10 and 30. A weight of **0 drains** the origin — it keeps getting health-checked but never
receives traffic, which is exactly what you want when taking a backend out of rotation for maintenance
without deleting its configuration.

Priority builds tiers. Give your main pool priority 0 and a standby priority 1, and the standby stays idle
until every priority-0 origin is unhealthy or ejected. Because both weight and priority live on the mapping,
one origin can be priority 0 for the endpoint that owns it and priority 1 (a backup) for a neighboring
endpoint that borrows it.

## Weighted canary and header routing

There are two ways to run a canary, and they answer different questions.

**Weighted splitting** answers "what fraction of traffic?" Bind the new build with a small weight next to the
stable build at a large weight, set the endpoint to `Weighted`, and roughly that fraction of requests goes to
the canary. Raise the weight to widen the rollout; drop it to 0 to drain instantly.

**Header routing** answers "which specific requests?" Set a `canaryHeader` and `canaryValue` on the canary
mapping, and any request carrying that header is pinned to the canary regardless of weight. This is the tool
for blue-green cutovers driven by a routing header, or for letting an internal client opt into the new build
while everyone else stays on stable. The two compose: you can run a 5% weighted canary *and* let anyone with
`X-Canary: on` reach it directly.

```json
POST /_sb/v1.0/mappings
{ "endpointIdentifier": "checkout", "endpointGUID": "…", "originIdentifier": "checkout-v2", "originGUID": "…",
  "weight": 5, "priority": 0, "canaryHeader": "X-Canary", "canaryValue": "on" }
```

## Sticky sessions

Enable `stickySessionEnabled` on an endpoint and every client is pinned to one origin. Leave
`stickySessionHeader` unset and the client IP is the affinity key; set it to a header name and that header's
value becomes the key, which is how you pin by session cookie, tenant id, or any routing token an upstream
adds. Affinity is computed by consistent hashing over the currently-available, weight-adjusted origins, so a
client stays put as long as its origin does — and when that origin goes unhealthy, the client is rehashed to
a healthy one rather than getting an error.

Sticky sessions sit above the load-balancing mode in the pipeline, so they work with any mode. The mode still
governs brand-new keys and any request that can't produce an affinity key.

## Slow start

A backend that just passed its health check is often still cold — JIT not warmed, caches empty, connection
pools unfilled. Slow start protects it. Set `slowStartMs` on the origin and, for the first part of that
window after it becomes healthy, its effective weight ramps from a small floor up to full. In weight-aware
modes that means it receives a growing trickle instead of its full share the instant it recovers. Once the
window elapses the origin carries normal weight. Leave `slowStartMs` at 0 and there's no ramp at all.

## Passive health checks and outlier ejection

Active health checks catch a backend that fails its probe. They miss the more common failure: a backend that
answers `GET /` with a cheerful 200 while returning 500s on real traffic. Passive health checking closes that
gap. The gateway watches the outcome of every proxied request, and after `maxFailures` consecutive failures
(a transport error or a 5xx) it **ejects** the origin — removes it from routing for `ejectionDurationMs` —
then lets it back in to try again. A single success resets the counter, so a flaky origin that recovers on
its own is never ejected.

Ejection is deliberately per-origin config, because tolerance is a property of the backend. A critical
payments origin might eject after two failures; a best-effort analytics origin might tolerate fifty. Set
`maxFailures` to 0 to turn passive ejection off for an origin entirely.

## Retries and failover

Ejection helps future requests. Retries help *this* one. When an attempt fails before any bytes have been
sent to the client, and the request method is idempotent, Switchboard can try another origin. `maxRetries`
on the endpoint caps the extra attempts; `retryOn5xx` decides whether an upstream 5xx counts as retryable or
only transport errors do. Each retry excludes the origins already tried, so a request never bounces back onto
the backend that just failed it.

Two rules keep this safe. Only idempotent methods (GET, HEAD, OPTIONS, PUT, DELETE, TRACE) are retried;
POST and PATCH are never silently re-sent. And a response that has already begun streaming to the client —
server-sent events or chunked transfer — can't be retried, because the client has already seen bytes. In
that case the request fails rather than double-delivering.

## Configuration reference

Every field is exposed through the management API, the dashboard forms, `sb.json`, and the OpenAPI document,
and all of them survive a live configuration reload — no restart. The three homes:

**Origin server** (`origin_servers`): `slowStartMs` (default 0), `maxFailures` (default 5, 0 disables),
`ejectionDurationMs` (default 30000).

**Endpoint** (`api_endpoints`): `loadBalancingMode` (default `RoundRobin`), `stickySessionEnabled`
(default false), `stickySessionHeader` (default null → client IP), `maxRetries` (default 0),
`retryOn5xx` (default true).

**Endpoint-origin mapping** (`endpoint_origin_mappings`): `weight` (default 100, 0 drains),
`priority` (default 0), `canaryHeader` and `canaryValue` (default null).

All numeric fields clamp to sane ranges rather than rejecting out-of-band values, and the columns are added
to existing databases by idempotent startup migrations across SQLite, MySQL, PostgreSQL, and SQL Server.

## Putting it together

A realistic checkout endpoint might run latency-based routing across three same-region origins at equal
weight, keep a fourth in a distant region at priority 1 as a cold standby, eject any origin that throws five
consecutive 5xx for thirty seconds, retry failed reads once, and pin logged-in users to an origin by their
session cookie. That's not five features fighting for control — it's five layers of the same pipeline, each
handling the decision it's good at, in an order that always resolves the same way.

Start simple. Round-robin with health checks is enough for interchangeable backends. Add weights when
capacity differs, priority when you want standbys, ejection and retries when reliability matters more than
simplicity, sticky sessions when the backend holds per-client state, and canary controls when you're shipping
something you want to watch before it carries everyone.
