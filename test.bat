@echo off
rem
rem Smoke test for Switchboard.
rem
rem Builds and starts the SampleApplication (an in-process Switchboard proxy in front of three
rem WatsonWebserver origin servers), exercises each configured route, prints the results, and shuts
rem the server down.
rem
rem Usage: test.bat [proxyPort]   (default proxy port: 18080)
rem
setlocal enabledelayedexpansion

set "ROOT=%~dp0"
set "PROXY_PORT=%~1"
if "%PROXY_PORT%"=="" set "PROXY_PORT=18080"
set "BASE=http://localhost:%PROXY_PORT%"
set "PROJECT=%ROOT%src\SampleApplication\SampleApplication.csproj"
set "DLL=%ROOT%src\SampleApplication\bin\Release\net8.0\SampleApplication.dll"

echo Building SampleApplication...
dotnet build "%PROJECT%" -c Release -f net8.0 -v quiet
if errorlevel 1 ( echo Build failed. & exit /b 1 )

echo Starting server on %BASE% ...
start "SwitchboardSmoke" /b cmd /c dotnet "%DLL%" %PROXY_PORT%

echo Waiting for server...
set "READY="
for /l %%i in (1,1,40) do (
  if not defined READY (
    curl -s -o NUL "%BASE%/"
    if not errorlevel 1 ( set "READY=1" ) else ( ping -n 2 127.0.0.1 >NUL )
  )
)
if not defined READY ( echo Server did not become ready. & goto :shutdown )

echo.
echo =================== Switchboard smoke test ===================
echo.
echo --- GET / ^(any node; repeat to see round-robin^) ---
for /l %%i in (1,1,3) do ( curl -s "%BASE%/" & echo. )
echo --- GET /route1 ^(node 1 or 2^) ---
curl -s "%BASE%/route1" & echo.
echo --- GET /route2 ^(node 2 or 3^) ---
curl -s "%BASE%/route2" & echo.
echo --- GET /route3 ^(node 1 or 3^) ---
curl -s "%BASE%/route3" & echo.
echo --- POST /echo ---
curl -s -X POST -d "smoke test payload" "%BASE%/echo" & echo.
echo --- GET /not-configured ^(expect 400^) ---
curl -s -o NUL -w "HTTP %%{http_code}\n" "%BASE%/not-configured"

:shutdown
echo.
echo Shutting down server...
for /f "tokens=5" %%p in ('netstat -ano ^| findstr ":%PROXY_PORT% " ^| findstr LISTENING') do taskkill /PID %%p /F >NUL 2>&1
echo Done.
endlocal
