@ECHO OFF
IF "%1" == "" GOTO :Usage
ECHO.
ECHO Building Switchboard Dashboard for linux/amd64 and linux/arm64/v8...
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag jchristn77/switchboard-ui:%1 --tag jchristn77/switchboard-ui:latest --push .

GOTO :Done

:Usage
ECHO Provide a tag argument for the build.
ECHO Example: build-docker.bat v4.0.2

:Done
ECHO Done
@ECHO ON
