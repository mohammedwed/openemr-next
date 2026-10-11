#!/usr/bin/env bash
# Re-applies credentials from .env to a restored OpenEMR stack.
# Run after the database and sites volume are restored, and before the stack starts.
set -euo pipefail

# Read a value from .env, dropping quotes, Windows line endings, and surrounding spaces.
get() {
  grep -E "^$1=" .env | head -n1 | cut -d= -f2- | tr -d '\r' | sed -e 's/^"//' -e 's/"$//'
}

DB_PASS=$(get DB_PASSWORD)
CLIENT_SECRET=$(get AUTHFACADE_CLIENT_SECRET)
CLIENT_ID=$(get AUTHFACADE_CLIENT_ID)
OPENEMR_IMAGE=$(get OPENEMR_IMAGE)
SITES_VOLUME=${SITES_VOLUME:-openemr-next_sitesvolume}

: "${DB_PASS:?DB_PASSWORD missing from .env}"
: "${CLIENT_SECRET:?AUTHFACADE_CLIENT_SECRET missing from .env}"
: "${CLIENT_ID:?AUTHFACADE_CLIENT_ID missing from .env}"
: "${OPENEMR_IMAGE:?OPENEMR_IMAGE missing from .env}"

# Quote a value as a MariaDB string literal: escape backslashes, then single quotes.
sql_quote() {
  local s=$1
  s=${s//\\/\\\\}
  s=${s//\'/\'\'}
  printf "'%s'" "$s"
}

# 1. OpenEMR config: rewrite both password entries in sites/default/sqlconf.php.
#    The password is passed as an environment variable, so it never appears in the
#    script text or the command line. MSYS_NO_PATHCONV stops Git Bash on Windows
#    from rewriting the container path.
MSYS_NO_PATHCONV=1 docker run --rm -i --user root --entrypoint php \
  -e NEW_PASS="$DB_PASS" \
  -v "$SITES_VOLUME:/var/www/localhost/htdocs/openemr/sites" \
  "$OPENEMR_IMAGE" <<'PHP'
<?php
$file = '/var/www/localhost/htdocs/openemr/sites/default/sqlconf.php';
$content = file_get_contents($file);
if ($content === false) {
    fwrite(STDERR, "sqlconf.php not found\n");
    exit(1);
}
$new = var_export(getenv('NEW_PASS'), true);
$patterns = [
    '/(\$pass\s*=\s*)(?:\'(?:[^\'\\\\]|\\\\.)*\'|"(?:[^"\\\\]|\\\\.)*")/',
];
foreach ($patterns as $re) {
    $count = 0;
    $content = preg_replace_callback($re, fn ($m) => $m[1] . $new, $content, 1, $count);
    if ($count !== 1) {
        fwrite(STDERR, "expected one match for $re, found $count\n");
        exit(1);
    }
}
file_put_contents($file, $content);
echo "sqlconf.php updated (both password entries)\n";
PHP

# 2. Database: set the app user's password and the AuthFacade client secret.
printf '%s\n' \
  "ALTER USER 'openemr'@'%' IDENTIFIED BY $(sql_quote "$DB_PASS");" \
  "FLUSH PRIVILEGES;" \
  "UPDATE oauth_clients SET client_secret = $(sql_quote "$CLIENT_SECRET") WHERE client_id = $(sql_quote "$CLIENT_ID");" \
  | docker compose exec -T mysql sh -c 'mariadb -uroot -p"$MYSQL_ROOT_PASSWORD" openemr'

echo "Applied DB password to sqlconf.php and database, and AuthFacade client secret from .env"