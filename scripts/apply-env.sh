#!/usr/bin/env bash
# Re-applies credentials from .env to a restored OpenEMR stack.
set -euo pipefail

# Read a value from .env, dropping quotes, Windows line endings, and surrounding spaces.
get() {
  grep -E "^$1=" .env | head -n1 | cut -d= -f2- | tr -d '\r' | sed -e 's/^"//' -e 's/"$//'
}

DB_PASS=$(get DB_PASSWORD)
CLIENT_SECRET=$(get AUTHFACADE_CLIENT_SECRET)
CLIENT_ID=$(get AUTHFACADE_CLIENT_ID)

: "${DB_PASS:?DB_PASSWORD missing from .env}"
: "${CLIENT_SECRET:?AUTHFACADE_CLIENT_SECRET missing from .env}"
: "${CLIENT_ID:?AUTHFACADE_CLIENT_ID missing from .env}"

# Quote a value as a MariaDB string literal: escape backslashes, then single quotes.
sql_quote() {
  local s=$1
  s=${s//\\/\\\\}
  s=${s//\'/\'\'}
  printf "'%s'" "$s"
}

printf '%s\n' \
  "ALTER USER 'openemr'@'%' IDENTIFIED BY $(sql_quote "$DB_PASS");" \
  "FLUSH PRIVILEGES;" \
  "UPDATE oauth_clients SET client_secret = $(sql_quote "$CLIENT_SECRET") WHERE client_id = $(sql_quote "$CLIENT_ID");" \
  | docker compose exec -T mysql sh -c 'mariadb -uroot -p"$MYSQL_ROOT_PASSWORD" openemr'

echo "Applied DB user password and AuthFacade client secret from .env"