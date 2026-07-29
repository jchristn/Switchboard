# Switchboard Load Generator

A console tool that writes **synthetic request history** (and a realistic example topology of origins,
endpoints, routes, and mappings) directly into the Switchboard database, so the dashboard can be
demonstrated and screenshotted with lifelike data.

The traffic is shaped to look like a real deployment rather than uniform noise:

- **Time-of-day** weighting — quiet overnight, busy through the working day.
- **Weekday** weighting — weekends are lighter.
- **Per-endpoint** traffic shares and per-route method/path weighting.
- **Randomized status codes** (mostly 2xx, with a realistic spread of 3xx/4xx/5xx) and
  **log-normal latencies** (slower for errors, timeouts for 504s, fast rejects for 429s).

## Usage

```bash
cd src/LoadGenerator
dotnet run                                  # last 30 days, ~700 requests/day, deployment DB
dotnet run -- --start 2026-06-01 --end 2026-07-01 --density 1200
dotnet run -- --db /path/to/switchboard.db
```

| Argument | Default | Description |
|---|---|---|
| `--start`, `--from` | 30 days before end | Window start (e.g. `2026-06-28`). |
| `--end`, `--to` | now (UTC) | Window end. |
| `--density`, `-d` | 700 | Average requests per day (varies with weekday, hour, and jitter). |
| `--db`, `--database` | auto | SQLite path. Defaults to the deployment's `Docker/data/switchboard.db` if found, else `./switchboard.db`. |

Positional form is also accepted: `LoadGenerator <start> <end> <density> <db>`.

It prints a summary of everything created when it finishes.

## Notes

- History rows are inserted through the low-level database driver so their timestamps can be
  **backdated** across the window (the normal capture path always stamps "now").
- The tool is safely re-runnable: the example topology is created once and skipped on later runs,
  while history is appended.
- The running server trims request history older than `RequestHistory.RetentionDays`. To keep a full
  month visible, raise that value in `sb.json` (the Docker deployment uses `90`) before starting the
  server.
