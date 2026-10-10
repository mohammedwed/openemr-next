#!/bin/sh
# Creates .env from .env.example and fills secret keys with random values.
# Fails loudly if any secret is left empty. Never prints values.
# Usage: scripts/new-env.sh [--force]
set -eu

root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"

force=0
[ "${1:-}" = "--force" ] && force=1

[ -f .env.example ] || { echo "ERROR: .env.example not found in $root" >&2; exit 1; }
if [ -f .env ] && [ "$force" -eq 0 ]; then
  echo "ERROR: .env already exists. Use --force to replace it." >&2
  exit 1
fi

# Random characters from a set, read from the OS random device
rand() { LC_ALL=C tr -dc "$1" < /dev/urandom | head -c "$2"; }

# Database and admin passwords: 24 chars with upper, lower, digit and one of -_.
new_password() {
  while :; do
    p=$(rand 'A-Za-z0-9._-' 24)
    case "$p" in *[A-Z]*) ;; *) continue ;; esac
    case "$p" in *[a-z]*) ;; *) continue ;; esac
    case "$p" in *[0-9]*) ;; *) continue ;; esac
    case "$p" in *[._-]*) ;; *) continue ;; esac
    printf '%s' "$p"
    return
  done
}

# JWT signing key: 64 letters and digits
new_jwt_key() { rand 'A-Za-z0-9' 64; }

# OAuth client ID: 43 url-safe characters, matching OpenEMR's format
new_client_id() { rand 'A-Za-z0-9_-' 43; }

# OAuth client secret: 128 base64 characters, matching the existing format
new_client_secret() {
  head -c 96 /dev/urandom | openssl base64 -A | head -c 128
}

tmp=$(mktemp)
# tr -d '\r' guards against CRLF in the template
tr -d '\r' < .env.example | while IFS= read -r line || [ -n "$line" ]; do
  case "$line" in
    *=*)
      key=${line%%=*}
      value=${line#*=}
      if [ -z "$value" ]; then
        case "$key" in
          DB_ROOT_PASSWORD|DB_PASSWORD|OE_PASS) value=$(new_password) ;;
          JWT_KEY)                    value=$(new_jwt_key) ;;
          AUTHFACADE_CLIENT_ID)       value=$(new_client_id) ;;
          AUTHFACADE_CLIENT_SECRET)   value=$(new_client_secret) ;;
        esac
      fi
      printf '%s=%s\n' "$key" "$value" ;;
    *)
      printf '%s\n' "$line" ;;
  esac
done > "$tmp"

# Verify: none of the generated secrets may be empty
if grep -Eq '^(DB_ROOT_PASSWORD|DB_PASSWORD|OE_PASS|JWT_KEY|AUTHFACADE_CLIENT_ID|AUTHFACADE_CLIENT_SECRET)=$' "$tmp"; then
  rm -f "$tmp"
  echo "ERROR: generation failed, a secret is still empty. .env not written." >&2
  exit 1
fi

mv "$tmp" .env
chmod 600 .env 2>/dev/null || true
echo "Wrote .env. Generated: DB_ROOT_PASSWORD DB_PASSWORD OE_PASS JWT_KEY AUTHFACADE_CLIENT_ID AUTHFACADE_CLIENT_SECRET"
echo "Next: docker compose down -v, then docker compose up -d (new credentials need a fresh volume)."