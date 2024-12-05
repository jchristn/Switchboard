if [ -z "${IMG_TAG}" ]; then
  IMG_TAG='v1.0.0'
fi

echo Using image tag $IMG_TAG

if [ ! -f "sb.json" ]
then
  echo Configuration file sb.json not found.
  exit
fi

# Items that require persistence
#   sb.json
#   logs/

# Argument order matters!

docker run \
  -p 8000:8000 \
  -t \
  -i \
  -e "TERM=xterm-256color" \
  -v ./sb.json:/app/sb.json \
  -v ./logs/:/app/logs/ \
  jchristn/switchboard:$IMG_TAG
