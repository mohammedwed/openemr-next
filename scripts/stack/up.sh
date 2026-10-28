#!/usr/bin/env bash
# One-command local stack: optional snapshot restore, credentials from .env,
# then start OpenEMR and AuthFacade and wait until both respond.
#
#   bash scripts/stack/up.sh            start or resume the stack
#   bash scripts/stack/up.sh --restore  wipe volumes and rebuild from the snapshot
#
# WARNING: --restore runs `docker compose down -v`, which DELETES the database and OpenEMR
#          sites volumes, including any local changes. Take a snapshot first if you need them.
#
# Needs:   docker compose, curl, and a .env (see scripts/setup/new-env.sh).
#          With --restore and no files in snapshots/, also the GitHub CLI (`gh`), logged in
#          with access to this repository's releases.
# Reads:   .env (see docs/configuration.md), plus these optional environment variables:
#            SNAPSHOT_TAG   release to download the snapshot from
#                           (default snapshot-openemr-8.0.0.3-ci)
#            SITES_VOLUME   name of the OpenEMR sites volume
#                           (default openemr-next_sitesvolume, i.e. <compose project>_sitesvolume)
# Exits 1: if .env is missing, or the stack is not healthy within 20 minutes.
set -euo pipefail

SNAPSHOT_TAG=${SNAPSHOT_TAG:-snapshot-openemr-8.0.0.3-ci}
SNAPSHOT_DIR=snapshots
SITES_VOLUME=${SITES_VOLUME:-openemr-next_sitesvolume}
APPLY_ENV=scripts/stack/apply-credentials.sh
RESTORE=false
[[ "${1:-}" == "--restore" ]] && RESTORE=true

source "$(dirname "${BASH_SOURCE[0]}")/../lib/env.sh"

[[ -f .env ]] || { echo "Missing .env. Run scripts/setup/new-env.sh first." >&2; exit 1; }
env_warn_legacy
DB_PASS=$(env_get DB_PASSWORD)
DB_USER=$(env_get DB_USER openemr)
OE_PORT=$(env_get OPENEMR_HTTPS_PORT 9300)
FACADE_PORT=$(env_get AUTHFACADE_HTTP_PORT 7128)
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

  # The dump has no application user, so create it before apply-credentials.sh changes its password.
  printf '%s\n' \
    "CREATE USER IF NOT EXISTS $(sql_quote "$DB_USER")@'%' IDENTIFIED BY $(sql_quote "$DB_PASS");" \
    "GRANT ALL PRIVILEGES ON \`$(env_get DB_NAME openemr)\`.* TO $(sql_quote "$DB_USER")@'%';" \
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
  oe=$(curl -k -s -o /dev/null --max-time 10 -w "%{http_code}" https://localhost:$OE_PORT/meta/health/readyz || true)
  af=$(curl -s -o /dev/null --max-time 10 -w "%{http_code}" http://localhost:$FACADE_PORT/api/health || true)
  if [[ "$oe" == 200 && "$af" == 200 ]]; then
    echo "Stack ready: OpenEMR https://localhost:$OE_PORT  AuthFacade http://localhost:$FACADE_PORT"
    break
  fi
  if (( $(date +%s) >= deadline )); then
    echo "Timed out: openemr=$oe auth-facade=$af" >&2
    exit 1
  fi
  sleep 15
done