@echo off
if "%~1"=="" (
    echo Usage: build-dashboard.bat [version-tag]
    echo Example: build-dashboard.bat v5.0.0
    exit /b 1
)

set "VERSION_TAG=%~1"
set "DOCKER_BUILD_CLOUD_REF=jchristn77/jchristn77"
set "DOCKER_BUILD_CLOUD_BUILDER=cloud-jchristn77-jchristn77"

echo Building Switchboard Dashboard %VERSION_TAG% with Docker Build Cloud builder %DOCKER_BUILD_CLOUD_BUILDER%...
docker buildx inspect "%DOCKER_BUILD_CLOUD_BUILDER%" >nul 2>&1
if errorlevel 1 (
    echo Connecting Docker Build Cloud builder %DOCKER_BUILD_CLOUD_REF%...
    docker buildx create --driver cloud "%DOCKER_BUILD_CLOUD_REF%" >nul
    if errorlevel 1 (
        echo Failed to connect Docker Build Cloud builder %DOCKER_BUILD_CLOUD_REF%.
        exit /b %errorlevel%
    )
)

rem The build context is dashboard/; the Dockerfile installs dependencies and builds the Vite app.
rem dashboard/.dockerignore keeps the uploaded context small. Kept on one line to avoid caret-continuation issues.
docker buildx build --builder "%DOCKER_BUILD_CLOUD_BUILDER%" --platform linux/amd64,linux/arm64/v8 -t jchristn77/switchboard-ui:%VERSION_TAG% -t jchristn77/switchboard-ui:latest -f "%~dp0dashboard\Dockerfile" --push "%~dp0dashboard"
if errorlevel 1 (
    echo Switchboard Dashboard build failed.
    exit /b %errorlevel%
)

echo Done.
exit /b 0
