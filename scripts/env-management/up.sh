#!/usr/bin/env bash
# One-command local stack: optional snapshot restore, credentials from .env,
# then start OpenEMR and AuthFacade and wait until both respond.
#
#   bash scripts/env-management/up.sh            start or resume the stack
#   bash scripts/env-management/up.sh --restore  wipe volumes and rebuild from the snapshot
set -euo pipefail

SNAPSHOT_TAG=${SNAPSHOT_TAG:-snapshot-openemr-8.0.0.3-ci}
SNAPSHOT_DIR=snapshots
SITES_VOLUME=${SITES_VOLUME:-openemr-next_sitesvolume}
APPLY_ENV=scripts/env-management/apply-env.sh
RESTORE=false
[[ "${1:-}" == "--restore" ]] && RESTORE=true

# Read a value from .env, dropping quotes and Windows line endings.
get() {
  grep -E "^$1=" .env | head -n1 | cut -d= -f2- | tr -d '\r' | sed -e 's/^"//' -e 's/"$//'
}

# Quote a value as a MariaDB string literal.
sql_quote() {
  local s=$1
  s=${s//\\/\\\\}
  s=${s//\'/\'\'}
  printf "'%s'" "$s"
}

[[ -f .env ]] || { echo "Missing .env. Run scripts/env-management/new-env.sh first." >&2; exit 1; }
DB_PASS=$(get DB_PASSWORD)
: "${DB_PASS:?DB_PASSWORD missing from .env}"
[[ -f "$APPLY_ENV" ]] || { echo "Missing $APPLY_ENV" >&2; exit 1; }

# Docker on Windows needs the Windows form of the current folder for -v.
HOST_DIR=$(pwd -W 2>/dev/null || pwd)

if $RESTORE; then
  echo "==> Removing existing volumes"
  docker compose down -v

  if [[ ! -f "$SNAPSHOT_DIR/db.sql" || ! -f "$SNAPSHOT_DIR/sites.tar.gz" ]]; then
    echo "==> Downloading snapshot $SNAPSHOT_TAG"
    mkdir -p "$SNAPSHOT_DIR"
    gh release download "$SNAPSHOT_TAG" --dir "$SNAPSHOT_DIR" \
      --pattern db.sql --pattern sites.tar.gz --pattern images.txt
  fi

  echo "==> Starting database"
  docker compose up -d mysql --wait

  echo "==> Restoring database"
  docker compose cp "$SNAPSHOT_DIR/db.sql" mysql:/tmp/db.sql
  docker compose exec -T mysql sh -c 'mariadb -uroot -p"$MYSQL_ROOT_PASSWORD" < /tmp/db.sql'

  # The dump has no application user, so create it before apply-env changes its password.
  printf '%s\n' \
    "CREATE USER IF NOT EXISTS 'openemr'@'%' IDENTIFIED BY $(sql_quote "$DB_PASS");" \
    "GRANT ALL PRIVILEGES ON openemr.* TO 'openemr'@'%';" \
    "FLUSH PRIVILEGES;" \
    | docker compose exec -T mysql sh -c 'mariadb -uroot -p"$MYSQL_ROOT_PASSWORD"'

  echo "==> Restoring OpenEMR sites"
  docker volume create "$SITES_VOLUME" >/dev/null
  MSYS_NO_PATHCONV=1 docker run --rm \
    -v "$SITES_VOLUME:/data" \
    -v "$HOST_DIR/$SNAPSHOT_DIR:/backup" \
    alpine sh -c "tar xzf /backup/sites.tar.gz -C /data"
else
  echo "==> Starting database"
  docker compose up -d mysql --wait
fi

echo "==> Applying credentials from .env"
bash "$APPLY_ENV"

echo "==> Starting OpenEMR and AuthFacade"
docker compose up -d --build --wait --wait-timeout 1200 openemr auth-facade phpmyadmin

echo "==> Waiting for both endpoints"
deadline=$(( $(date +%s) + 1200 ))
while true; do
  oe=$(curl -k -s -o /dev/null --max-time 10 -w "%{http_code}" https://localhost:9300/meta/health/readyz || true)
  af=$(curl -s -o /dev/null --max-time 10 -w "%{http_code}" http://localhost:7128/api/health || true)
  if [[ "$oe" == 200 && "$af" == 200 ]]; then
    echo "Stack ready: OpenEMR https://localhost:9300  AuthFacade http://localhost:7128"
    break
  fi
  if (( $(date +%s) >= deadline )); then
    echo "Timed out: openemr=$oe auth-facade=$af" >&2
    exit 1
  fi
  sleep 15
done