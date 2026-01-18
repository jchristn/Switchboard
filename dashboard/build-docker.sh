#!/bin/bash

if [ -z "$1" ]; then
    echo "Provide a tag argument for the build."
    echo "Example: ./build-docker.sh v4.0.2"
    exit 1
fi

echo ""
echo "Building Switchboard Dashboard for linux/amd64 and linux/arm64/v8..."
docker buildx build --builder cloud-jchristn77-jchristn77 --platform linux/amd64,linux/arm64/v8 --tag jchristn77/switchboard-ui:$1 --tag jchristn77/switchboard-ui:latest --push .

echo "Done"
