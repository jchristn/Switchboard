#!/bin/bash
set -e

if [ -z "$1" ]; then
    echo "Usage: ./build-dashboard.sh [version-tag]"
    echo "Example: ./build-dashboard.sh v4.1.0"
    exit 1
fi

VERSION_TAG="$1"
DOCKER_BUILD_CLOUD_REF="jchristn77/jchristn77"
DOCKER_BUILD_CLOUD_BUILDER="cloud-jchristn77-jchristn77"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "Building Switchboard Dashboard ${VERSION_TAG} with Docker Build Cloud builder ${DOCKER_BUILD_CLOUD_BUILDER}..."
if ! docker buildx inspect "${DOCKER_BUILD_CLOUD_BUILDER}" >/dev/null 2>&1; then
    echo "Connecting Docker Build Cloud builder ${DOCKER_BUILD_CLOUD_REF}..."
    docker buildx create --driver cloud "${DOCKER_BUILD_CLOUD_REF}" >/dev/null
fi

# The build context is dashboard/; the Dockerfile installs dependencies and builds the Vite app.
# dashboard/.dockerignore keeps the uploaded context small (no node_modules or dist).
docker buildx build \
    --builder "${DOCKER_BUILD_CLOUD_BUILDER}" \
    --platform linux/amd64,linux/arm64/v8 \
    -t "jchristn77/switchboard-ui:${VERSION_TAG}" \
    -t "jchristn77/switchboard-ui:latest" \
    -f "${SCRIPT_DIR}/dashboard/Dockerfile" \
    --push \
    "${SCRIPT_DIR}/dashboard"

echo "Done."
