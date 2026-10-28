#!/usr/bin/env bash
# Shared helpers for the scripts and CI. Source this file; run from the repo root.
#
#   source scripts/lib/env.sh
#   env_get KEY [DEFAULT]   read a value from .env
#   env_set KEY VALUE       replace a value in .env, or append it
#   sql_quote VALUE         quote a value as a MariaDB string literal
#   env_warn_legacy         warn about variable names that were renamed

# Read a value from .env, dropping quotes and Windows line endings.
# Prints DEFAULT (or nothing) when the key is missing or empty.
env_get() {
  local value
  value=$(grep -E "^$1=" .env | head -n1 | cut -d= -f2- | tr -d '\r' | sed -e 's/^"//' -e 's/"$//' || true)
  printf '%s' "${value:-${2:-}}"
}

# Replace the line for KEY in .env, or append it. The value is passed through the
# environment, so any character is safe.
env_set() {
  local tmp
  tmp=$(mktemp)
  KEY=$1 VAL=$2 awk '
    BEGIN { k = ENVIRON["KEY"]; v = ENVIRON["VAL"]; found = 0 }
    index($0, k "=") == 1 { print k "=" v; found = 1; next }
    { print }
    END { if (!found) print k "=" v }
  ' .env > "$tmp"
  cat "$tmp" > .env
  rm -f "$tmp"
}

# Quote a value as a MariaDB string literal: escape backslashes, then single quotes.
sql_quote() {
  local s=$1
  s=${s//\\/\\\\}
  s=${s//\'/\'\'}
  printf "'%s'" "$s"
}

# Warn when .env still uses a variable name that has been renamed.
env_warn_legacy() {
  local pair old new
  for pair in WT_HTTP_PORT:OPENEMR_HTTP_PORT WT_HTTPS_PORT:OPENEMR_HTTPS_PORT \
              WT_MYSQL_PORT:DB_PORT WT_PMA_PORT:PHPMYADMIN_PORT \
              FACADE_HTTP_PORT:AUTHFACADE_HTTP_PORT \
              OE_USER:OPENEMR_ADMIN_USER OE_PASS:OPENEMR_ADMIN_PASSWORD; do
    old=${pair%%:*}
    new=${pair##*:}
    if grep -q "^$old=" .env; then
      echo "WARNING: .env sets $old, which is ignored. Rename it to $new." >&2
    fi
  done
}
