#!/usr/bin/env bash
#
# Switchboard Docker Factory Reset
#
# Resets the Docker deployment to its factory-default state by:
# - Stopping the containers
# - Restoring the factory sb.json configuration
# - Deleting the SQLite database so the server rebuilds a clean one on next startup
# - Removing all log files
#
# The server recreates an empty database on startup; the default administrator bearer
# token (Management.AdminToken = "sbadmin") is defined in the configuration and works
# immediately, so no seeding step is required.
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "========================================================================"
echo "  SWITCHBOARD FACTORY RESET"
echo "========================================================================"
echo ""
echo "  This will completely reset your Switchboard Docker deployment to the"
echo "  factory default state. ALL data will be lost, including:"
echo ""
echo "    - All origin servers, API endpoints, routes, mappings, and rewrites"
echo "    - All users and credentials created after first startup"
echo "    - All request history"
echo "    - All log files"
echo "    - Any custom configuration (sb.json is restored to factory default)"
echo ""
echo "  Default administrator after reset:"
echo "    Admin bearer token: sbadmin"
echo ""
echo "========================================================================"
echo ""
read -p "  Type RESET to proceed: " CONFIRM
echo ""

if [ "$CONFIRM" != "RESET" ]; then
    echo "  Reset cancelled."
    exit 1
fi

echo "  [1/4] Stopping Docker containers..."
cd "$DOCKER_DIR"
docker compose down 2>/dev/null || docker-compose down 2>/dev/null || true

echo "  [2/4] Restoring factory configuration..."
cp "$SCRIPT_DIR/sb.json" "$DOCKER_DIR/sb.json"

echo "  [3/4] Deleting the SQLite database (rebuilt clean on next startup)..."
rm -f "$DOCKER_DIR/data/"switchboard.db "$DOCKER_DIR/data/"switchboard.db-shm "$DOCKER_DIR/data/"switchboard.db-wal

echo "  [4/4] Removing log files..."
find "$DOCKER_DIR/logs" -type f ! -name '.gitkeep' -delete 2>/dev/null || true

echo ""
echo "========================================================================"
echo "  RESET COMPLETE"
echo ""
echo "  Start the deployment with:"
echo "    cd $DOCKER_DIR"
echo "    docker compose up -d"
echo ""
echo "  Dashboard: http://localhost:3000"
echo "  API:       http://localhost:8000"
echo "========================================================================"
