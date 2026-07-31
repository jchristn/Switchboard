#!/bin/bash
set -e

if [ -z "$1" ]; then
    echo "Usage: ./build-all.sh [version-tag]"
    echo "Example: ./build-all.sh v5.0.0"
    exit 1
fi

VERSION_TAG="$1"
DOCKER_BUILD_CLOUD_BUILDER="cloud-jchristn77-jchristn77"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

echo "Using Docker Build Cloud builder ${DOCKER_BUILD_CLOUD_BUILDER}."

"${SCRIPT_DIR}/build-dashboard.sh" "${VERSION_TAG}"
"${SCRIPT_DIR}/build-server.sh" "${VERSION_TAG}"

echo "Done."
