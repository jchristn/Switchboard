@echo off
if "%~1"=="" (
    echo Usage: build-server.bat [version-tag]
    echo Example: build-server.bat v4.1.0
    exit /b 1
)

set "VERSION_TAG=%~1"
set "DOCKER_BUILD_CLOUD_REF=jchristn77/jchristn77"
set "DOCKER_BUILD_CLOUD_BUILDER=cloud-jchristn77-jchristn77"

echo Building Switchboard Server %VERSION_TAG% with Docker Build Cloud builder %DOCKER_BUILD_CLOUD_BUILDER%...
docker buildx inspect "%DOCKER_BUILD_CLOUD_BUILDER%" >nul 2>&1
if errorlevel 1 (
    echo Connecting Docker Build Cloud builder %DOCKER_BUILD_CLOUD_REF%...
    docker buildx create --driver cloud "%DOCKER_BUILD_CLOUD_REF%" >nul
    if errorlevel 1 (
        echo Failed to connect Docker Build Cloud builder %DOCKER_BUILD_CLOUD_REF%.
        exit /b %errorlevel%
    )
)

rem The build context is src/; the Dockerfile copies the solution and publishes Switchboard.Server.
rem src/.dockerignore keeps the uploaded context small. Kept on one line to avoid caret-continuation issues.
docker buildx build --builder "%DOCKER_BUILD_CLOUD_BUILDER%" --platform linux/amd64,linux/arm64/v8 -t jchristn77/switchboard:%VERSION_TAG% -t jchristn77/switchboard:latest -f "%~dp0src\Switchboard.Server\Dockerfile" --push "%~dp0src"
if errorlevel 1 (
    echo Switchboard Server build failed.
    exit /b %errorlevel%
)

echo Done.
exit /b 0
