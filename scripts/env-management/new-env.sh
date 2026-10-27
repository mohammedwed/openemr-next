#!/usr/bin/env bash
# Creates .env from .env.example with freshly generated secrets.
# Never overwrites an existing .env.
set -euo pipefail

cd "$(dirname "$0")/../.."

if [[ -f .env ]]; then
  echo ".env already exists; refusing to overwrite it." >&2
  echo "Delete it first if you really want new values." >&2
  exit 1
fi

[[ -f .env.example ]] || { echo "Missing .env.example" >&2; exit 1; }

# 24 random bytes, hex encoded: 48 characters, safe in .env and SQL.
secret() { openssl rand -hex 24; }

cp .env.example .env

source scripts/env-management/env-lib.sh

env_set DB_ROOT_PASSWORD "$(secret)"
env_set DB_PASSWORD "$(secret)"
env_set AUTHFACADE_CLIENT_SECRET "$(secret)"

# The snapshot's admin hash matches this password, so it must not be generated.
env_set OE_PASS "ci-admin-password"

echo "Created .env with generated secrets."
echo "OE_PASS is set to the snapshot's admin password (ci-admin-password)."