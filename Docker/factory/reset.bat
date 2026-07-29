@echo off
setlocal enabledelayedexpansion

REM
REM Switchboard Docker Factory Reset
REM
REM Resets the Docker deployment to its factory-default state by:
REM - Stopping the containers
REM - Restoring the factory sb.json configuration
REM - Deleting the SQLite database so the server rebuilds a clean one on next startup
REM - Removing all log files
REM
REM The server recreates an empty database on startup; the default administrator bearer
REM token (Management.AdminToken = "sbadmin") is defined in the configuration and works
REM immediately, so no seeding step is required.
REM

set "SCRIPT_DIR=%~dp0"
set "DOCKER_DIR=%SCRIPT_DIR%.."

echo ========================================================================
echo   SWITCHBOARD FACTORY RESET
echo ========================================================================
echo.
echo   This will completely reset your Switchboard Docker deployment to the
echo   factory default state. ALL data will be lost, including:
echo.
echo     - All origin servers, API endpoints, routes, mappings, and rewrites
echo     - All users and credentials created after first startup
echo     - All request history
echo     - All log files
echo     - Any custom configuration (sb.json is restored to factory default)
echo.
echo   Default administrator after reset:
echo     Admin bearer token: sbadmin
echo.
echo ========================================================================
echo.
set /p CONFIRM="  Type RESET to proceed: "
echo.

if not "%CONFIRM%"=="RESET" (
    echo   Reset cancelled.
    exit /b 1
)

echo   [1/4] Stopping Docker containers...
pushd "%DOCKER_DIR%"
docker compose down 2>nul || docker-compose down 2>nul
popd

echo   [2/4] Restoring factory configuration...
copy /y "%SCRIPT_DIR%sb.json" "%DOCKER_DIR%\sb.json" >nul

echo   [3/4] Deleting the SQLite database (rebuilt clean on next startup)...
del /f /q "%DOCKER_DIR%\data\switchboard.db" 2>nul
del /f /q "%DOCKER_DIR%\data\switchboard.db-shm" 2>nul
del /f /q "%DOCKER_DIR%\data\switchboard.db-wal" 2>nul

echo   [4/4] Removing log files...
for /r "%DOCKER_DIR%\logs" %%f in (*) do (
    if not "%%~nxf"==".gitkeep" del /f /q "%%f" 2>nul
)

echo.
echo ========================================================================
echo   RESET COMPLETE
echo.
echo   Start the deployment with:
echo     cd %DOCKER_DIR%
echo     docker compose up -d
echo.
echo   Dashboard: http://localhost:3000
echo   API:       http://localhost:8000
echo ========================================================================

endlocal
