#!/usr/bin/env bash
# Creates AuthFacade's own dedicated database and least-privilege user (ADR 0043), separate from
# OpenEMR's own. Runs as a one-shot Compose service (seed-authfacade-db), against the bare MariaDB
# image already used for the mysql service itself -- no extra image to pull.
#
# Idempotent: safe to rerun, same as the `seed` service (ADR 0025) it sits alongside. Compose reruns a
# one-shot service whenever its resolved config changes and something still depends on it, so a changed
# AUTHFACADE_DB_PASSWORD takes effect on the next `docker compose up` without any manual step.
#
# Needs:  AUTHFACADE_DB_NAME, AUTHFACADE_DB_USER, AUTHFACADE_DB_PASSWORD and MYSQL_PWD (the root
#         password) in the environment -- set by docker-compose.yml, not passed on the command line.
set -euo pipefail

: "${AUTHFACADE_DB_NAME:?AUTHFACADE_DB_NAME is required}"
: "${AUTHFACADE_DB_USER:?AUTHFACADE_DB_USER is required}"
: "${AUTHFACADE_DB_PASSWORD:?AUTHFACADE_DB_PASSWORD is required}"
: "${MYSQL_PWD:?MYSQL_PWD (the root password) is required}"

# Quotes a value as a MariaDB string literal: escape backslashes, then single quotes. Mirrors
# scripts/lib/env.sh's sql_quote -- that file is not available inside this container, so it is repeated
# here rather than shared.
sql_quote() {
  local s=$1
  s=${s//\\/\\\\}
  s=${s//\'/\'\'}
  printf "'%s'" "$s"
}

# The database and user names are operator-controlled config, not secrets, so they are interpolated
# directly as identifiers/usernames; only the password (sql_quote'd below) can hold arbitrary characters.
mariadb -h mysql -uroot <<SQL
CREATE DATABASE IF NOT EXISTS \`${AUTHFACADE_DB_NAME}\`;
CREATE USER IF NOT EXISTS '${AUTHFACADE_DB_USER}'@'%';
ALTER USER '${AUTHFACADE_DB_USER}'@'%' IDENTIFIED BY $(sql_quote "$AUTHFACADE_DB_PASSWORD");
GRANT ALL PRIVILEGES ON \`${AUTHFACADE_DB_NAME}\`.* TO '${AUTHFACADE_DB_USER}'@'%';
FLUSH PRIVILEGES;
SQL

echo "AuthFacade database '${AUTHFACADE_DB_NAME}' and user '${AUTHFACADE_DB_USER}' ready."
