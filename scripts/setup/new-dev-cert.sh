#!/usr/bin/env bash
# Creates a self-signed TLS certificate for the gateway's local HTTPS listener, the same kind of
# dev-only cert OpenEMR's own image already ships for its :9300 (ADR 0039). Never overwrites an
# existing certs/gateway.{crt,key}.
#
#   bash scripts/setup/new-dev-cert.sh
#
# Needs:   openssl.
# Writes:  certs/gateway.crt, certs/gateway.key (git-ignored; regenerate any time by deleting them).
set -euo pipefail

cd "$(dirname "$0")/../.."

mkdir -p certs

if [[ -f certs/gateway.crt && -f certs/gateway.key ]]; then
  echo "certs/gateway.crt and certs/gateway.key already exist; leaving them as they are." >&2
  echo "Delete both first if you want a fresh certificate." >&2
  exit 0
fi

openssl req -x509 -newkey rsa:2048 -sha256 -days 3650 -nodes \
  -keyout certs/gateway.key -out certs/gateway.crt \
  -subj "/CN=localhost" \
  -addext "subjectAltName=DNS:localhost,IP:127.0.0.1"

# openssl writes the key 600, readable only by you; the gateway container reads it as its own non-root
# user (a different UID), so it needs to be world-readable. Fine for a throwaway local dev cert that
# protects nothing but transport on your own machine -- never do this for a real certificate's key.
chmod 644 certs/gateway.key

echo "Created certs/gateway.crt and certs/gateway.key."
echo "Your browser will warn that this certificate is self-signed -- that's expected locally,"
echo "the same warning OpenEMR's own https://localhost:9300 already gives."
