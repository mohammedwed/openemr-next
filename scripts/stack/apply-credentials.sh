#!/usr/bin/env bash
# Re-applies credentials from .env to a restored OpenEMR stack.
# Run after the database and sites volume are restored, and before the stack starts.
# stack/up.sh and CI call it; you rarely need to.
#
#   bash scripts/stack/apply-credentials.sh
#
# Changes: the `sites/default/sqlconf.php` password in the sites volume; the database user's
#          password; and the AuthFacade OAuth client's secret and redirect URI.
# Needs:   docker compose with the `mysql` service running, and the OpenEMR image from .env.
# Reads:   .env, plus the optional SITES_VOLUME (default openemr-next_sitesvolume).
# Order:   it is not atomic. It updates sqlconf.php first, then the database. If the database
#          step fails, rerun the script; both steps are safe to repeat.
# Exits non-zero: if a required value is missing from .env, or sqlconf.php cannot be rewritten.
set -euo pipefail

source "$(dirname "${BASH_SOURCE[0]}")/../lib/env.sh"

DB_PASS=$(env_get DB_PASSWORD)
CLIENT_SECRET=$(env_get AUTHFACADE_CLIENT_SECRET)
CLIENT_ID=$(env_get AUTHFACADE_CLIENT_ID)
DB_USER=$(env_get DB_USER openemr)
DB_NAME=$(env_get DB_NAME openemr)
REDIRECT_URI="http://localhost:$(env_get AUTHFACADE_HTTP_PORT 7128)/callback"
OPENEMR_IMAGE=$(env_get OPENEMR_IMAGE)
SITES_VOLUME=${SITES_VOLUME:-openemr-next_sitesvolume}

: "${DB_PASS:?DB_PASSWORD missing from .env}"
: "${CLIENT_SECRET:?AUTHFACADE_CLIENT_SECRET missing from .env}"
: "${CLIENT_ID:?AUTHFACADE_CLIENT_ID missing from .env}"
: "${OPENEMR_IMAGE:?OPENEMR_IMAGE missing from .env}"

# 1. OpenEMR config: rewrite the password in sites/default/sqlconf.php.
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
echo "sqlconf.php updated\n";
PHP

# 2. Encrypt the client secret with OpenEMR's own CryptoGen, against the same sites volume (so the same
#    site keys are used). ClientRepository::validateClient() decrypts oauth_clients.client_secret and
#    compares for the authorization_code grant (ADR 0014); a plaintext value never matches, and the
#    password and refresh grants never catch it, since OpenEMR skips the secret check for those (ADR 0025).
#    Unlike step 1, this boots OpenEMR's own globals.php, which queries the database during startup (a
#    translation lookup) — it must join the mysql service's own compose network to resolve and reach the
#    "mysql" host sqlconf.php was just pointed at, or globals.php dies with "prepare() on false" before
#    CryptoGen ever runs. Read from the running container rather than assumed from the project directory
#    name, since that name (and so the network's) can differ between a local checkout and CI's.
MYSQL_NETWORK=$(docker inspect "$(docker compose ps -q mysql)" -f '{{range $k, $v := .NetworkSettings.Networks}}{{$k}}{{end}}')
: "${MYSQL_NETWORK:?Could not determine the mysql service Docker network; is it running?}"
ENCRYPTED_SECRET=$(MSYS_NO_PATHCONV=1 docker run --rm -i --user root --entrypoint php \
  --network "$MYSQL_NETWORK" \
  -e CLIENT_SECRET="$CLIENT_SECRET" \
  -v "$SITES_VOLUME:/var/www/localhost/htdocs/openemr/sites" \
  "$OPENEMR_IMAGE" <<'PHP'
<?php
$fileRoot = '/var/www/localhost/htdocs/openemr';
require_once "{$fileRoot}/vendor/autoload.php";
$_GET['site'] = 'default';
$ignoreAuth = true;
$sessionAllowWrite = true;
require_once "{$fileRoot}/interface/globals.php";
echo (new OpenEMR\Common\Crypto\CryptoGen())->encryptStandard(getenv('CLIENT_SECRET'));
PHP
)
: "${ENCRYPTED_SECRET:?Failed to encrypt the client secret}"

# 3. Database: set the app user's password and the AuthFacade client secret.
printf '%s\n' \
  "ALTER USER $(sql_quote "$DB_USER")@'%' IDENTIFIED BY $(sql_quote "$DB_PASS");" \
  "FLUSH PRIVILEGES;" \
  "UPDATE oauth_clients SET client_secret = $(sql_quote "$ENCRYPTED_SECRET"), redirect_uri = $(sql_quote "$REDIRECT_URI") WHERE client_id = $(sql_quote "$CLIENT_ID");" \
  | docker compose exec -T mysql sh -c "mariadb -uroot -p\"\$MYSQL_ROOT_PASSWORD\" $(printf '%q' "$DB_NAME")"

echo "Applied DB password to sqlconf.php and database, and AuthFacade client secret and redirect URI from .env"