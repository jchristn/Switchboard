@echo off
if "%~1"=="" (
    echo Usage: build-all.bat [version-tag]
    echo Example: build-all.bat v4.1.0
    exit /b 1
)

set "VERSION_TAG=%~1"
set "DOCKER_BUILD_CLOUD_BUILDER=cloud-jchristn77-jchristn77"

pushd "%~dp0" >nul
if errorlevel 1 exit /b 1

echo Using Docker Build Cloud builder %DOCKER_BUILD_CLOUD_BUILDER%.

rem Called by full path, not by bare name: when NoDefaultCurrentDirectoryInExePath is set -- which
rem Git Bash and some CI shells do -- cmd will not search the current directory, and a bare
rem `call build-dashboard.bat` fails with "not recognized" even after the pushd above.
call "%~dp0build-dashboard.bat" "%VERSION_TAG%"
if errorlevel 1 (
    popd
    exit /b %errorlevel%
)

call "%~dp0build-server.bat" "%VERSION_TAG%"
if errorlevel 1 (
    popd
    exit /b %errorlevel%
)

popd
echo Done.
exit /b 0
