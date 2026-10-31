<?php

/**
 * DEV ONLY. Registers the AuthFacade OAuth client and keeps its scope, redirect URI and secret in sync.
 * Runs inside the `seed` Compose service, using the OpenEMR image itself (not a bare MariaDB client), so it
 * can call OpenEMR's own CryptoGen to encrypt the client secret the same way OpenEMR's own
 * ClientRepository::insertNewClient() does. Writing the secret as plaintext (the previous SQL-only seed did
 * this) silently breaks ClientRepository::validateClient() for the authorization_code grant: it decrypts
 * oauth_clients.client_secret and compares, so a plaintext value never matches and OpenEMR's token endpoint
 * refuses the client with invalid_client. The password and refresh grants never caught this, since OpenEMR
 * skips the secret check entirely for those. See ADR 0005 and ADR 0025.
 *
 * Safe to run again: it adds the client only if missing and refreshes its scope, redirect URI and secret.
 *
 * Required environment: DB_HOST, DB_NAME, DB_USER, MYSQL_PWD, AUTHFACADE_CLIENT_ID, AUTHFACADE_REDIRECT_URI.
 * Optional: AUTHFACADE_CLIENT_SECRET (the client gets a NULL secret, and is treated as non-confidential, if unset).
 * Exits non-zero: a required variable is missing, or the oauth_clients table does not appear within 15 minutes.
 */

declare(strict_types=1);

function requireEnv(string $name): string
{
    $value = getenv($name);
    if ($value === false || $value === '') {
        fwrite(STDERR, "Set {$name}\n");
        exit(2);
    }
    return $value;
}

$dbHost = getenv('DB_HOST') ?: 'mysql';
$dbName = requireEnv('DB_NAME');
$dbUser = requireEnv('DB_USER');
$dbPass = requireEnv('MYSQL_PWD');
$clientId = requireEnv('AUTHFACADE_CLIENT_ID');
$redirectUri = requireEnv('AUTHFACADE_REDIRECT_URI');
$clientSecret = getenv('AUTHFACADE_CLIENT_SECRET');
if ($clientSecret === false || $clientSecret === '') {
    fwrite(STDERR, "AUTHFACADE_CLIENT_SECRET not set; inserting client with NULL secret\n");
    $clientSecret = null;
}

$dsn = "mysql:host={$dbHost};dbname={$dbName};charset=utf8mb4";

// Wait for the OpenEMR installer to create its tables (up to 15 minutes), the same wait the old shell
// script did. A plain PDO connection is enough for this; OpenEMR's own bootstrap is not needed yet.
$pdo = null;
for ($attempt = 1; $attempt <= 90; $attempt++) {
    try {
        $pdo = new PDO($dsn, $dbUser, $dbPass, [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]);
        $pdo->query('SELECT 1 FROM oauth_clients LIMIT 1');
        break;
    } catch (PDOException) {
        $pdo = null;
        fwrite(STDERR, "Waiting for OpenEMR install... ({$attempt})\n");
        sleep(10);
    }
}
if ($pdo === null) {
    fwrite(STDERR, "Timed out waiting for OpenEMR install\n");
    exit(1);
}

// OpenEMR's own bootstrap, run only now that the schema exists: it is what gives CryptoGen access to the
// site's real encryption keys. Mirrors bin/console's own bootstrap (without --skip-globals).
$fileRoot = '/var/www/localhost/htdocs/openemr';
require_once "{$fileRoot}/vendor/autoload.php";
$_GET['site'] = 'default';
$ignoreAuth = true;
$sessionAllowWrite = true;
require_once "{$fileRoot}/interface/globals.php";

$encryptedSecret = $clientSecret === null
    ? null
    : (new OpenEMR\Common\Crypto\CryptoGen())->encryptStandard($clientSecret);
$isConfidential = $encryptedSecret === null ? 0 : 1;

$exists = $pdo->prepare('SELECT 1 FROM oauth_clients WHERE client_id = ?');
$exists->execute([$clientId]);

if ($exists->fetchColumn() === false) {
    $insert = $pdo->prepare(
        'INSERT INTO oauth_clients
            (client_id, client_name, client_role, client_secret, redirect_uri, grant_types,
             scope, user_id, site_id, is_confidential, is_enabled,
             skip_ehr_launch_authorization_flow, dsi_type, register_date)
         VALUES
            (?, \'AuthFacade\', \'user\', ?, ?, \'authorization_code\',
             \'openid profile email offline_access\', NULL, \'default\', ?, 1,
             0, 0, NOW())'
    );
    $insert->execute([$clientId, $encryptedSecret, $redirectUri, $isConfidential]);
}

$update = $pdo->prepare(
    'UPDATE oauth_clients
     SET scope = \'openid profile email offline_access\', redirect_uri = ?, client_secret = ?, is_confidential = ?
     WHERE client_id = ?'
);
$update->execute([$redirectUri, $encryptedSecret, $isConfidential, $clientId]);

echo "AuthFacade seed complete\n";
