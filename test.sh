#!/usr/bin/env bash
#
# Smoke test for Switchboard.
#
# Builds and starts the SampleApplication (an in-process Switchboard proxy in front of three
# WatsonWebserver origin servers), exercises each configured route, prints the results, and shuts
# the server down. Exits non-zero if any check fails.
#
# Usage: ./test.sh [proxyPort]   (default proxy port: 18080)
#
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROXY_PORT="${1:-18080}"
BASE="http://localhost:${PROXY_PORT}"
PROJECT="${ROOT}/src/SampleApplication/SampleApplication.csproj"
DLL="${ROOT}/src/SampleApplication/bin/Release/net8.0/SampleApplication.dll"

PASS=0
FAIL=0

check() {
  # check <name> <expected-substring> <actual>
  if printf '%s' "$3" | grep -qF "$2"; then
    echo "  [PASS] $1"
    PASS=$((PASS + 1))
  else
    echo "  [FAIL] $1"
    echo "         expected to contain: $2"
    echo "         actual:              $3"
    FAIL=$((FAIL + 1))
  fi
}

echo "Building SampleApplication..."
dotnet build "$PROJECT" -c Release -f net8.0 -v quiet || { echo "Build failed."; exit 1; }

WORK="$(mktemp -d 2>/dev/null || echo "${TMPDIR:-/tmp}/sb-smoke.$$")"
mkdir -p "$WORK"

echo "Starting server on ${BASE} ..."
( cd "$WORK" && exec dotnet "$DLL" "$PROXY_PORT" ) >"$WORK/server.log" 2>&1 &
SERVER_PID=$!

cleanup() {
  echo
  echo "Shutting down server (pid ${SERVER_PID})..."
  kill "$SERVER_PID" 2>/dev/null
  wait "$SERVER_PID" 2>/dev/null
  rm -rf "$WORK"
}
trap cleanup EXIT INT TERM

READY=0
for _ in $(seq 1 40); do
  if curl -sf -o /dev/null "${BASE}/" 2>/dev/null; then READY=1; break; fi
  sleep 0.5
done
if [ "$READY" -ne 1 ]; then
  echo "Server did not become ready."
  tail -n 20 "$WORK/server.log"
  exit 1
fi

echo
echo "=================== Switchboard smoke test ==================="
echo
echo "--- GET / (any node; repeat to see round-robin) ---"
LAST=""
for _ in 1 2 3; do LAST="$(curl -s "${BASE}/")"; echo "  ${LAST}"; done
check "GET /" "Hello from node" "$LAST"

echo "--- GET /route1 (node 1 or 2) ---"
R="$(curl -s "${BASE}/route1")"; echo "  ${R}"
check "GET /route1" "Hello from route1, served by node" "$R"

echo "--- GET /route2 (node 2 or 3) ---"
R="$(curl -s "${BASE}/route2")"; echo "  ${R}"
check "GET /route2" "Hello from route2, served by node" "$R"

echo "--- GET /route3 (node 1 or 3) ---"
R="$(curl -s "${BASE}/route3")"; echo "  ${R}"
check "GET /route3" "Hello from route3, served by node" "$R"

echo "--- POST /echo ---"
R="$(curl -s -X POST -d 'smoke test payload' "${BASE}/echo")"; echo "  ${R}"
check "POST /echo echoes the body" "You said: smoke test payload" "$R"

echo "--- GET /not-configured (expect 400) ---"
CODE="$(curl -s -o /dev/null -w '%{http_code}' "${BASE}/not-configured")"; echo "  HTTP ${CODE}"
check "unknown route returns 400" "400" "$CODE"

echo
echo "=================== Result ==================="
echo "  Passed: ${PASS}   Failed: ${FAIL}"
if [ "$FAIL" -eq 0 ]; then echo "  SMOKE TEST PASSED"; else echo "  SMOKE TEST FAILED"; fi
exit "$FAIL"
