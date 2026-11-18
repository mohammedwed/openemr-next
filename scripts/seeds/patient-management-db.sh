#!/usr/bin/env bash
# Creates the patient_management_outbox table inside OpenEMR's own database, and a least-privilege
# user scoped to exactly that table plus patient_data, insurance_data, documents, categories,
# categories_to_documents, sequences, users (id and uuid only), uuid_registry (insert only), and list_options (ADR 0049, ADR 0050) -- never OpenEMR's
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
-- The audit trail (SEC-9, ADR 0078): who did what to which record, field names only, never values. Append-only: the service user is granted INSERT
-- and SELECT below and nothing else. Subject is the record's uuid as text, or a short key such as patient:42. Widths here must match AuditEntry's limits.
CREATE TABLE IF NOT EXISTS \`${DB_NAME}\`.\`patient_management_audit\` (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  at DATETIME(6) NOT NULL,
  actor VARCHAR(100) NOT NULL,
  actor_id BIGINT DEFAULT NULL,
  actor_verified TINYINT(1) NOT NULL DEFAULT 0,
  action VARCHAR(50) NOT NULL,
  subject VARCHAR(36) NOT NULL,
  outcome VARCHAR(10) NOT NULL,
  correlation_id VARCHAR(100) DEFAULT NULL,
  source_address VARCHAR(45) DEFAULT NULL,
  details VARCHAR(1000) DEFAULT NULL,
  KEY ix_patient_management_audit_subject (subject, at),
  KEY ix_patient_management_audit_at (at)
);
-- Idempotency keys (API-5, ADR 0084): what a creating request answered, by (user, key), kept 24 hours so a retried request is answered, not repeated. Only the hash of the
-- request and the answer (identifiers, status, location) are held, never the request body. The primary key is what makes claiming a key atomic.
CREATE TABLE IF NOT EXISTS \`${DB_NAME}\`.\`patient_management_idempotency\` (
  actor_id BIGINT NOT NULL,
  idem_key VARCHAR(64) NOT NULL,
  fingerprint CHAR(64) NOT NULL,
  state VARCHAR(8) NOT NULL,
  claim_token VARCHAR(36) NOT NULL,
  claimed_at DATETIME(6) NOT NULL,
  completed_at DATETIME(6) DEFAULT NULL,
  status_code INT DEFAULT NULL,
  content_type VARCHAR(100) DEFAULT NULL,
  location VARCHAR(300) DEFAULT NULL,
  body TEXT DEFAULT NULL,
  PRIMARY KEY (actor_id, idem_key),
  KEY ix_patient_management_idempotency_claimed (claimed_at)
);
-- The relay's bookkeeping (EVT-7, ADR 0066): attempts and back-off, a lease so a crashed relay loses nothing, and a
-- dead-letter mark. ADD COLUMN IF NOT EXISTS keeps this idempotent on a database that already has the older table.
ALTER TABLE \`${DB_NAME}\`.\`patient_management_outbox\`
  ADD COLUMN IF NOT EXISTS attempts INT NOT NULL DEFAULT 0,
  ADD COLUMN IF NOT EXISTS next_attempt_at DATETIME DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS locked_until DATETIME DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS dead_lettered_at DATETIME DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS last_error VARCHAR(1000) DEFAULT NULL;
CREATE INDEX IF NOT EXISTS ix_patient_management_outbox_pending
  ON \`${DB_NAME}\`.\`patient_management_outbox\` (published_at, dead_lettered_at, id);
CREATE USER IF NOT EXISTS '${PATIENT_MANAGEMENT_DB_USER}'@'%';
ALTER USER '${PATIENT_MANAGEMENT_DB_USER}'@'%' IDENTIFIED BY $(sql_quote "$PATIENT_MANAGEMENT_DB_PASSWORD");
-- Start from nothing, then grant exactly the list below. CREATE USER IF NOT EXISTS keeps whatever an older version of this
-- script granted, so without this a grant removed or narrowed here would linger on an existing database (it is how the
-- broad users grant below is actually replaced).
REVOKE ALL PRIVILEGES, GRANT OPTION FROM '${PATIENT_MANAGEMENT_DB_USER}'@'%';
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
-- The single shared id-generation counter (legacy's own QueryUtils::generateId()) -- "SET id =
-- LAST_INSERT_ID(id + 1)" needs SELECT too, not just UPDATE: MariaDB requires read privilege on any
-- column referenced on the right-hand side of a SET expression, even one from the row being updated.
-- Confirmed the hard way: UPDATE-only produced "SELECT command denied... for column 'id'" against the
-- real seeded user, a gap the Testcontainers integration tests never catch (that fixture connects
-- with a more permissive user, not this exact least-privilege grant set).
GRANT SELECT, UPDATE ON \`${DB_NAME}\`.sequences TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
-- FHIR Patient API: read-only lookups for generalPractitioner and every coded extension -- this service never writes to
-- either. users is granted on id and uuid (EfPatientFhirRepository selects both) and on active, which the verified-user
-- check reads to refuse a deactivated account (ADR 0075), and nothing else: the table also holds a password column and
-- other credential data this service must never see (finding F-CC-7).
GRANT SELECT (id, uuid, active) ON \`${DB_NAME}\`.users TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
GRANT SELECT ON \`${DB_NAME}\`.list_options TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
-- Legacy records every uuid it hands out here; this service now does the same for the ones it creates (finding F-CC-3).
-- Insert-only: it adds its own rows and never reads or edits anyone's.
GRANT INSERT ON \`${DB_NAME}\`.uuid_registry TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
GRANT ALL PRIVILEGES ON \`${DB_NAME}\`.patient_management_outbox TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
-- The audit trail is append-only by privilege: no UPDATE, no DELETE (ADR 0078).
GRANT INSERT, SELECT ON \`${DB_NAME}\`.patient_management_audit TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
-- The idempotency table is fully owned: claims are inserted, completed, released and swept.
GRANT SELECT, INSERT, UPDATE, DELETE ON \`${DB_NAME}\`.patient_management_idempotency TO '${PATIENT_MANAGEMENT_DB_USER}'@'%';
FLUSH PRIVILEGES;
SQL

echo "patient_management_outbox table and user '${PATIENT_MANAGEMENT_DB_USER}' ready."
