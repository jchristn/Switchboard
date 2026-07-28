@echo off
rem Pull the latest images, recreate the stack, and show container status.
pushd "%~dp0"
docker compose pull && docker compose down && docker compose up -d && docker ps -a
popd
