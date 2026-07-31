#!/bin/bash
set -e

if [ -z "$1" ]; then
    echo "Usage: ./build-server.sh [version-tag]"
    echo "Example: ./build-server.sh v5.0.0"
    exit 1
fi

VERSION_TAG="$1"
DOCKER_BUILD_CLOUD_REF="jchristn77/jchristn77"
DOCKER_BUILD_CLOUD_BUILDER="cloud-jchristn77-jchristn77"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "Building Switchboard Server ${VERSION_TAG} with Docker Build Cloud builder ${DOCKER_BUILD_CLOUD_BUILDER}..."
if ! docker buildx inspect "${DOCKER_BUILD_CLOUD_BUILDER}" >/dev/null 2>&1; then
    echo "Connecting Docker Build Cloud builder ${DOCKER_BUILD_CLOUD_REF}..."
    docker buildx create --driver cloud "${DOCKER_BUILD_CLOUD_REF}" >/dev/null
fi

# The build context is src/; the Dockerfile copies the solution and publishes Switchboard.Server.
# src/.dockerignore keeps the uploaded context small.
docker buildx build \
    --builder "${DOCKER_BUILD_CLOUD_BUILDER}" \
    --platform linux/amd64,linux/arm64/v8 \
    -t "jchristn77/switchboard:${VERSION_TAG}" \
    -t "jchristn77/switchboard:latest" \
    -f "${SCRIPT_DIR}/src/Switchboard.Server/Dockerfile" \
    --push \
    "${SCRIPT_DIR}/src"

echo "Done."
