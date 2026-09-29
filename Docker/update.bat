@echo off
rem Pull the latest published images FIRST, then recreate the stack, then show container status.
rem Non-destructive: named volumes and the bind-mounted data/ and logs/ directories are preserved.
pushd "%~dp0"

echo [1/4] Pulling latest images...
docker compose pull
if errorlevel 1 goto :failed

echo [2/4] Stopping the stack...
docker compose down
if errorlevel 1 goto :failed

rem --pull always re-checks the registry at start so a stale local image can never be reused.
echo [3/4] Starting the stack...
docker compose up -d --pull always
if errorlevel 1 goto :failed

echo [4/4] Container status:
docker ps -a
popd
exit /b 0

:failed
echo Update failed; see the error above.
popd
exit /b 1
