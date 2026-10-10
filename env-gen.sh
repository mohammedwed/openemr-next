#!/bin/sh
# Creates .env from .env.example and fills secret values with random ones.
# Works on Linux, macOS and Git Bash on Windows. Requires openssl or /dev/urandom.
# Usage: scripts/new-env.sh [--force]
set -eu

root=$(cd "$(dirname "$0")/.." && pwd)
cd "$root"

force=0
[ "${1:-}" = "--force" ] && force=1

if [ -f .env ] && [ "$force" -eq 0 ]; then
  echo ".env already exists. Use --force to replace it (this does not reset the database)." >&2
  exit 1
fi
[ -f .env.example ] || { echo ".env.example not found in $root" >&2; exit 1; }

# Random characters from a set safe in .env files, SQL and URLs.
# Reads from /dev/urandom, which exists on Linux, macOS and Git Bash.
rand_from() {  # $1 = alphabet, $2 = length
  LC_ALL=C tr -dc "$1" < /dev/urandom | head -c "$2"
}

# Password: guarantees one upper, lower, digit and symbol, then shuffles
new_password() {
  upper='ABCDEFGHJKLMNPQRSTUVWXYZ'
  lower='abcdefghijkmnopqrstuvwxyz'
  digit='23456789'
  symbol='-_.'
  all="$upper$lower$digit$symbol"
  pw="$(rand_from "$upper" 1)$(rand_from "$lower" 1)$(rand_from "$digit" 1)$(rand_from "$symbol" 1)$(rand_from "$all" 20)"
  # Shuffle the characters so the guaranteed classes aren't always first
  printf '%s' "$pw" | fold -w1 | while IFS= read -r c; do printf '%s\n' "$c"; done \
    | awk 'BEGIN{srand()} {print rand() "\t" $0}' | sort | cut -f2 | tr -d '\n'
  echo
}

# Random key from letters and digits
new_key() {  # $1 = length
  rand_from 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789' "$1"
  echo
}

generated=""
tmp=$(mktemp)
while IFS= read -r line || [ -n "$line" ]; do
  case "$line" in
    DB_ROOT_PASSWORD=|DB_PASSWORD=|OE_PASS=)
      key=${line%%=*}
      printf '%s=%s\n' "$key" "$(new_password)" >> "$tmp"
      generated="$generated $key" ;;
    JWT_KEY=)
      printf 'JWT_KEY=%s\n' "$(new_key 64)" >> "$tmp"
      generated="$generated JWT_KEY" ;;
    *)
      printf '%s\n' "$line" >> "$tmp" ;;
  esac
done < .env.example

mv "$tmp" .env
chmod 600 .env
echo "Wrote .env. Generated:$generated"
echo "Still empty (fill manually): AUTHFACADE_CLIENT_SECRET (and AUTHFACADE_CLIENT_ID if blank)"
echo "Verify with: git check-ignore -v .env"