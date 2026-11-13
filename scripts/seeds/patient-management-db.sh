#!/usr/bin/env bash
# Creates the patient_management_outbox table inside OpenEMR's own database, and a least-privilege
# user scoped to exactly that table plus patient_data, insurance_data, documents, categories,
# categories_to_documents, sequences, users, and list_options (ADR 0049, ADR 0050) -- never OpenEMR's
# existing app user, and never GRANT ... ON db.*, so every other table's privileges stay exactly as
# they are.
# Runs as a one-shot Compose service (seed-patient-management-db), against the bare MariaDB image
# already used for the mysql service itself -- no extra image to pull.
#
# Must run after OpenEMR's own installer has created patient_data, unlike seed-authfacade-db (which
# only needs the bare mysql service, not openemr itself).
#
# Idempotent: safe to rerun, same as seed-authfacade-db (ADR 0043). Compose reruns a one-shot service
# whenever its resolved config changes and something still depends on it, so a changed
# PATIENT_MANAGEMENT_DB_PASSWORD takes effect on the next `docker compose up` without any manual step.
#
# Needs:  DB_NAME (OpenEMR's own database), PATIENT_MANAGEMENT_DB_USER, PATIENT_MANAGEMENT_DB_PASSWORD
#         and MYSQL_PWD (the root password) in the environment -- set by docker-compose.yml, not passed
#         on the command line.
set -euo pipefail

: "${DB_NAME:?DB_NAME is required}"
: "${PATIENT_MANAGEMENT_DB_USER:?PATIENT_MANAGEMENT_DB_USER is required}"
: "${PATIENT_MANAGEMENT_DB_PASSWORD:?PATIENT_MANAGEMENT_DB_PASSWORD is required}"
: "${MYSQL_PWD:?MYSQL_PWD (the root password) is required}"

# Quotes a value as a MariaDB string literal: escape backslashes, then single quotes. Mirrors
# scripts/lib/env.sh's sql_quote -- that file is not available inside this container, so it is repeated
# here rather than shared, the same way seed-authfacade-db's own script already does.
sql_quote() {
  local s=$1
  s=${s//\\/\\\\}
  s=${s//\'/\'\'}
  printf "'%s'" "$s"
}

# The database and user names are operator-controlled config, not secrets, so they are interpolated
# directly as identifiers/usernames; only the password (sql_quote'd below) can hold arbitrary characters.
mariadb -h mysql -uroot <<SQL
CREATE TABLE IF NOT EXISTS \`${DB_NAME}\`.\`patient_management_outbox\` (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  event_type VARCHAR(255) NOT NULL,
  payload LONGTEXT NOT NULL,
  occurred_at DATETIME NOT NULL,
  published_at DATETIME DEFAULT NULL
);
CREATE USER IF NOT EXISTS '${PATIENT_MANAGEMENT_DB_USER}'@'%';
ALTER USER '${PATIENT_MANAGEMENT_DB_USER}'@'%' IDENTIFIED BY $(sql_quote "$PATIENT_MANAGEMENT_DB_PASSWORD");
GRANT SELECT, INSERT, UPDATE ON \`${DB_NAME}\`.patient_data TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
GRANT SELECT, INSERT, UPDATE ON \`${DB_NAME}\`.insurance_data TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
-- Advance directive documents: this service only ever inserts a new documents row and reads rows
-- back, never updates or deletes one, so no UPDATE/DELETE grant here.
GRANT SELECT, INSERT ON \`${DB_NAME}\`.documents TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
-- Read-only: only ever looked up by name to find the "Advance Directive" category's id.
GRANT SELECT ON \`${DB_NAME}\`.categories TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
-- REPLACE INTO (used to tag a new document under its category) is an INSERT that falls back to a
-- DELETE+INSERT on a duplicate key, so both privileges are needed even though this service never
-- issues a bare DELETE itself.
GRANT SELECT, INSERT, DELETE ON \`${DB_NAME}\`.categories_to_documents TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
-- The single shared id-generation counter (legacy's own QueryUtils::generateId()) -- UPDATE alone
-- covers "SET id = LAST_INSERT_ID(id + 1)", since it reads the row being updated, not a separate one.
GRANT UPDATE ON \`${DB_NAME}\`.sequences TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
-- FHIR Patient API: read-only lookups for generalPractitioner (users.uuid) and every coded extension
-- (list_options) -- this service never writes to either.
GRANT SELECT ON \`${DB_NAME}\`.users TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
GRANT SELECT ON \`${DB_NAME}\`.list_options TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
GRANT ALL PRIVILEGES ON \`${DB_NAME}\`.patient_management_outbox TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
FLUSH PRIVILEGES;
SQL

echo "patient_management_outbox table and user '${PATIENT_MANAGEMENT_DB_USER}' ready."
