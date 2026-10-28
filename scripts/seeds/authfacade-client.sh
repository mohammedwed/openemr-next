#!/bin/sh
# DEV ONLY. Runs inside the `seed` Compose service, not on the host.
# Waits for OpenEMR's installer to create its tables, then registers the AuthFacade OAuth
# client (authfacade-client.sql). Safe to run again: it adds the client only if missing and
# refreshes its scope and redirect URI.
#
# Required environment (set by docker-compose.yml from .env):
#   DB_USER, DB_NAME, AUTHFACADE_CLIENT_ID, AUTHFACADE_REDIRECT_URI, MYSQL_PWD
# Optional:
#   DB_HOST                   database host (default mysql)
#   AUTHFACADE_CLIENT_SECRET  client secret; the client gets a NULL secret if unset
# Exits 1: if a required variable is missing or OpenEMR's tables do not appear within 15 minutes.
set -eu

: "${DB_USER:?Set DB_USER in .env}"
: "${DB_NAME:?Set DB_NAME in .env}"
: "${AUTHFACADE_CLIENT_ID:?Set AUTHFACADE_CLIENT_ID in .env}"
: "${AUTHFACADE_REDIRECT_URI:?Set AUTHFACADE_REDIRECT_URI in the seed service environment}"

DB_HOST="${DB_HOST:-mysql}"
client="mariadb -h $DB_HOST -u$DB_USER $DB_NAME"

# Quote a value as a SQL string literal, escaping backslashes and single quotes
sql_quote() {
  printf "'%s'" "$(printf '%s' "$1" | sed -e 's/\\/\\\\/g' -e "s/'/''/g")"
}

# Wait for the OpenEMR installer to finish (up to 15 minutes)
attempts=0
until $client -N -e "SELECT 1 FROM oauth_clients LIMIT 1" >/dev/null 2>&1; do
  attempts=$((attempts + 1))
  if [ "$attempts" -ge 90 ]; then
    echo "Timed out waiting for OpenEMR install" >&2
    exit 1
  fi
  echo "Waiting for OpenEMR install... ($attempts)"
  sleep 10
done

if [ -n "${AUTHFACADE_CLIENT_SECRET:-}" ]; then
  secret_sql=$(sql_quote "$AUTHFACADE_CLIENT_SECRET")
else
  echo "AUTHFACADE_CLIENT_SECRET not set; inserting client with NULL secret" >&2
  secret_sql=NULL
fi

{
  printf 'SET @CLIENT_ID = %s;\n' "$(sql_quote "$AUTHFACADE_CLIENT_ID")"
  printf 'SET @CLIENT_SECRET = %s;\n' "$secret_sql"
  printf 'SET @REDIRECT_URI = %s;\n' "$(sql_quote "$AUTHFACADE_REDIRECT_URI")"
  cat /seed/authfacade-client.sql
} | $client

echo "AuthFacade seed complete"