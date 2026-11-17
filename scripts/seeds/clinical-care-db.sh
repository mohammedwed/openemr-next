#!/usr/bin/env bash
# Creates the clinical_care_outbox table inside OpenEMR's own database, and a least-privilege user scoped to
# exactly the tables Clinical Care reads and writes (ADR 0064, building on ADR 0049 and ADR 0050) -- never
# OpenEMR's existing app user, and never GRANT ... ON db.*, so every other table's privileges stay as they are.
# Every grant below carries a one-line reason (LEG-6). Where a legacy table holds columns this service has no
# business reading (users), the grant is on named columns only.
#
# Runs as a one-shot Compose service (seed-clinical-care-db) against the bare MariaDB image already used for the
# mysql service itself -- no extra image to pull. It also runs, unchanged, inside the integration-test
# container (QLT-8): the tests connect as the user this script creates, so the grants are tested, not assumed.
#
# Must run after OpenEMR's own installer has created its tables.
#
# Idempotent: safe to rerun, same as seed-patient-management-db (LEG-18). Compose reruns a one-shot service
# whenever its resolved config changes and something still depends on it, so a changed
# CLINICAL_CARE_DB_PASSWORD takes effect on the next `docker compose up` without any manual step.
#
# Also backfills clinical_care_patient_ref once from patient_data (see the end of the SQL).
#
# Needs:  DB_NAME (OpenEMR's own database), CLINICAL_CARE_DB_USER, CLINICAL_CARE_DB_PASSWORD and MYSQL_PWD
#         (the root password) in the environment -- set by docker-compose.yml, not passed on the command line.
# Optional: DB_HOST, the MariaDB host to connect to (default: mysql, the Compose service name).
set -euo pipefail

: "${DB_NAME:?DB_NAME is required}"
: "${CLINICAL_CARE_DB_USER:?CLINICAL_CARE_DB_USER is required}"
: "${CLINICAL_CARE_DB_PASSWORD:?CLINICAL_CARE_DB_PASSWORD is required}"
: "${MYSQL_PWD:?MYSQL_PWD (the root password) is required}"
DB_HOST="${DB_HOST:-mysql}"

# Quotes a value as a MariaDB string literal: escape backslashes, then single quotes. Mirrors
# scripts/lib/env.sh's sql_quote -- that file is not available inside this container, so it is repeated here
# rather than shared, the same way the other seed scripts already do.
sql_quote() {
  local s=$1
  s=${s//\\/\\\\}
  s=${s//\'/\'\'}
  printf "'%s'" "$s"
}

# The database and user names are operator-controlled config, not secrets, so they are interpolated directly as
# identifiers/usernames; only the password (sql_quote'd below) can hold arbitrary characters.
mariadb -h "${DB_HOST}" -uroot <<SQL
CREATE TABLE IF NOT EXISTS \`${DB_NAME}\`.\`clinical_care_outbox\` (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  event_type VARCHAR(255) NOT NULL,
  payload LONGTEXT NOT NULL,
  occurred_at DATETIME NOT NULL,
  published_at DATETIME DEFAULT NULL
);

-- The audit trail (SEC-9, ADR 0069): who did what to which record, field names only, never values. Append-only: the service
-- user is granted INSERT and SELECT below and nothing else. Subject is the record's uuid as text so the table reads without a
-- converter. Widths here must match AuditEntry's limits (the unit test pins them).
CREATE TABLE IF NOT EXISTS \`${DB_NAME}\`.\`clinical_care_audit\` (
  id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  at DATETIME(6) NOT NULL,
  actor VARCHAR(100) NOT NULL,
  actor_id BIGINT DEFAULT NULL,
  actor_verified TINYINT(1) NOT NULL DEFAULT 0,
  action VARCHAR(50) NOT NULL,
  subject CHAR(36) NOT NULL,
  outcome VARCHAR(10) NOT NULL,
  correlation_id VARCHAR(100) DEFAULT NULL,
  source_address VARCHAR(45) DEFAULT NULL,
  details VARCHAR(1000) DEFAULT NULL,
  KEY ix_clinical_care_audit_subject (subject, at),
  KEY ix_clinical_care_audit_at (at)
);

-- The patients Clinical Care has been told about: a reference (uuid, pid) and nothing else, never demographics (design set
-- R7, decision D4). Filled by PatientRegistered events (ADR 0066) and by the one-time backfill below. pid is unique because
-- legacy tables still join on it; a different uuid claiming a held pid is a real conflict and must fail loudly.
CREATE TABLE IF NOT EXISTS \`${DB_NAME}\`.\`clinical_care_patient_ref\` (
  patient_uuid BINARY(16) NOT NULL PRIMARY KEY,
  pid BIGINT NOT NULL,
  registered_at DATETIME DEFAULT NULL,
  source VARCHAR(20) NOT NULL DEFAULT 'event' COMMENT 'event or backfill',
  created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UNIQUE KEY uq_clinical_care_patient_ref_pid (pid)
);
-- Which events have been applied (EVT-3). Written in the same transaction as the effect.
CREATE TABLE IF NOT EXISTS \`${DB_NAME}\`.\`clinical_care_processed_events\` (
  event_id CHAR(36) NOT NULL PRIMARY KEY,
  event_type VARCHAR(100) NOT NULL,
  processed_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP
);

CREATE USER IF NOT EXISTS '${CLINICAL_CARE_DB_USER}'@'%';
ALTER USER '${CLINICAL_CARE_DB_USER}'@'%' IDENTIFIED BY $(sql_quote "$CLINICAL_CARE_DB_PASSWORD");
-- Start from nothing, then grant exactly the list below. Without this a grant removed from this script would linger on a
-- database that already ran an older version (CREATE USER IF NOT EXISTS keeps old grants), which is how the interim
-- patient_data grant is retired.
REVOKE ALL PRIVILEGES, GRANT OPTION FROM '${CLINICAL_CARE_DB_USER}'@'%';

-- The encounter itself: this service inserts new rows and reads them back (including the uuid lookup that
-- makes a retried insert safe). No DELETE, and UPDATE only on the named columns below: the other legacy writers
-- still own every other column and the delete paths (ADR 0064 #2, ADR 0068).
GRANT SELECT, INSERT ON \`${DB_NAME}\`.form_encounter TO '${CLINICAL_CARE_DB_USER}'@'%';
-- An encounter edit (M5b, ADR 0068) may change exactly the columns this service owns and nothing else: no billing or collection column,
-- no identity (id, uuid, pid, encounter), no sensitivity (needs the sensitivities ACL, F-CC-2). last_update is not listed: the database sets it.
GRANT UPDATE (\`date\`, onset_date, reason, class_code, pc_catid, facility_id, facility, pos_code, provider_id)
  ON \`${DB_NAME}\`.form_encounter TO '${CLINICAL_CARE_DB_USER}'@'%';

-- Problems (M5c, ADR 0070). The lists table holds every issue type (problems, allergies, medications, surgeries, ...), so this service is granted only the
-- columns a problem uses, for reading and for inserting: it cannot read a medication's udi or a surgery's injury_* columns. SELECT includes type and
-- modifydate (the ETag token, ADR 0068); INSERT includes type because every row this service writes names its own. Which ROWS it touches is a
-- code rule (every statement filters on type), pinned by tests, because a grant cannot say it. UPDATE columns come with the update slice.
-- Allergies (M5c slice 2) add three columns: reaction, severity_al and verification.
-- (No backticks in these comments: this block is an unquoted heredoc, where a backtick runs a command.)
GRANT SELECT (id, uuid, \`date\`, type, title, begdate, enddate, diagnosis, outcome, occurrence, referredby, comments, activity, pid, \`user\`, modifydate,
  reaction, severity_al, verification)
  ON \`${DB_NAME}\`.lists TO '${CLINICAL_CARE_DB_USER}'@'%';
GRANT INSERT (uuid, \`date\`, type, title, begdate, enddate, diagnosis, outcome, occurrence, referredby, comments, activity, pid, \`user\`, groupname,
  reaction, severity_al, verification)
  ON \`${DB_NAME}\`.lists TO '${CLINICAL_CARE_DB_USER}'@'%';
-- Updating an issue (M5c slice 3, ADR 0068): UPDATE on exactly the columns a problem or an allergy owns, and nothing else. No identity (id, uuid, pid, type),
-- no date, user or groupname (who recorded it and when), no activity, none of the other issue types' columns. modifydate is not listed: the database sets it.
-- Which ROWS an update may touch is the type filter in every statement, not this grant.
GRANT UPDATE (title, begdate, enddate, diagnosis, outcome, occurrence, referredby, comments, verification, reaction, severity_al)
  ON \`${DB_NAME}\`.lists TO '${CLINICAL_CARE_DB_USER}'@'%';
-- Linking an issue to an encounter (M5c slice 4): legacy's issue_encounter row. SELECT and INSERT to read and make links, DELETE to remove a link (legacy's encounter
-- screen does so on every save). It is a table of ids and a resolved flag with no clinical text. The link's uuid_registry row is not deleted on unlink (that would need
-- DELETE on a table shared by every uuid in the system); a stale registry row is harmless because a uuid is never reused.
GRANT SELECT, INSERT, DELETE ON \`${DB_NAME}\`.issue_encounter TO '${CLINICAL_CARE_DB_USER}'@'%';
-- The "this list has been addressed" marker the issue screen writes (legacy's summary reads it as listTouched). Tiny table, no clinical content.
GRANT SELECT, INSERT ON \`${DB_NAME}\`.lists_touch TO '${CLINICAL_CARE_DB_USER}'@'%';

-- The legacy 'newpatient' registry row every encounter needs (CC-7). Insert-only for the same reason.
GRANT SELECT, INSERT ON \`${DB_NAME}\`.forms TO '${CLINICAL_CARE_DB_USER}'@'%';

-- A set of vital signs (slice 2): inserted and read back, never updated or deleted (CV-10). form_vitals has no
-- encounter column; its link to an encounter is the forms row granted above.
GRANT SELECT, INSERT ON \`${DB_NAME}\`.form_vitals TO '${CLINICAL_CARE_DB_USER}'@'%';
-- Correcting vitals (M5d, ADR 0068): UPDATE on exactly the measurement columns and nothing else. Not who recorded them or when (user, groupname, date), not authorized or
-- activity: legacy's own update leaves those alone too. last_updated is not listed: the database sets it.
GRANT UPDATE (weight, height, temperature, temp_method, bps, bpd, pulse, respiration, oxygen_saturation, waist_circ, head_circ, BMI, BMI_status, note)
  ON \`${DB_NAME}\`.form_vitals TO '${CLINICAL_CARE_DB_USER}'@'%';

-- Immunizations (M5d slice 2, ADR 0072): recorded and read back, and corrected on exactly the columns the service owns plus update_date, which has no default and
-- no ON UPDATE so this service sets it. Not who recorded it or when (create_date, created_by, updated_by), not the patient, not the uuid, not external_id. No DELETE:
-- an entry made in error is marked, not removed (the permission port gates real deletes).
GRANT SELECT, INSERT ON \`${DB_NAME}\`.immunizations TO '${CLINICAL_CARE_DB_USER}'@'%';
GRANT UPDATE (administered_date, immunization_id, cvx_code, manufacturer, lot_number, administered_by_id, administered_by, education_date, vis_date, note,
              amount_administered, amount_administered_unit, expiration_date, route, administration_site, added_erroneously, completion_status,
              information_source, refusal_reason, ordering_provider, reason_code, reason_description, encounter_id, update_date)
  ON \`${DB_NAME}\`.immunizations TO '${CLINICAL_CARE_DB_USER}'@'%';

-- SOAP notes (M5d slice 3, ADR 0073): recorded and read back, and corrected on the four text sections only. Not who wrote it or when (user, groupname, date), not the patient (pid),
-- not authorized or activity: legacy's own REST update rewrites date and pid, this service does not. A note is linked to its encounter by the forms row granted above. No DELETE.
GRANT SELECT, INSERT ON \`${DB_NAME}\`.form_soap TO '${CLINICAL_CARE_DB_USER}'@'%';
GRANT UPDATE (subjective, objective, assessment, plan) ON \`${DB_NAME}\`.form_soap TO '${CLINICAL_CARE_DB_USER}'@'%';

-- Legacy records every uuid it hands out here (CC-6). This service only ever adds its own; never reads or edits.
GRANT INSERT ON \`${DB_NAME}\`.uuid_registry TO '${CLINICAL_CARE_DB_USER}'@'%';

-- The single shared id counter (legacy's QueryUtils::generateId()). "SET id = LAST_INSERT_ID(id + 1)" needs
-- SELECT too, not just UPDATE: MariaDB requires read privilege on any column referenced on the right-hand side
-- of a SET expression (learned the hard way in ADR 0050's grant for the same table).
GRANT SELECT, UPDATE ON \`${DB_NAME}\`.sequences TO '${CLINICAL_CARE_DB_USER}'@'%';

-- Encounter classes (_ActEncounterCode): only the columns the class lookup reads.
GRANT SELECT (list_id, option_id, title, seq, is_default) ON \`${DB_NAME}\`.list_options TO '${CLINICAL_CARE_DB_USER}'@'%';

-- Facility name and default place-of-service code, copied onto the encounter (CC-8), and whether it is inactive or a service location, for the facility lookup (M5d slice 4).
-- Nothing else about a facility: no address, phone or tax columns.
GRANT SELECT (id, name, pos_code, inactive, service_location) ON \`${DB_NAME}\`.facility TO '${CLINICAL_CARE_DB_USER}'@'%';

-- The visit categories the lookup offers (M5d slice 4): only the columns legacy's own visit form filters and orders on, not the recurrence, colour or description columns.
GRANT SELECT (pc_catid, pc_catname, pc_cattype, pc_active, pc_seq, pc_constant_id) ON \`${DB_NAME}\`.openemr_postcalendar_categories TO '${CLINICAL_CARE_DB_USER}'@'%';

-- The acting provider's username and group, for the legacy forms row (CC-7). users also holds credential and
-- contact columns this service must never see, so the grant is on id and username only.
GRANT SELECT (id, username) ON \`${DB_NAME}\`.users TO '${CLINICAL_CARE_DB_USER}'@'%';
GRANT SELECT (name, \`user\`) ON \`${DB_NAME}\`.\`groups\` TO '${CLINICAL_CARE_DB_USER}'@'%';

-- No grant on patient_data. The interim stand-in that read it was removed when events arrived (ADR 0064 #5, ADR 0066):
-- Clinical Care now learns about patients only from PatientRegistered and its own reference table.

-- This service's own outbox: fully owned, so the relay can mark rows published and purge them (EVT-7).
GRANT ALL PRIVILEGES ON \`${DB_NAME}\`.clinical_care_outbox TO '${CLINICAL_CARE_DB_USER}'@'%';

-- The audit trail is append-only by privilege: no UPDATE, no DELETE, and the retention job never touches it (ADR 0069 #2, #7).
GRANT INSERT, SELECT ON \`${DB_NAME}\`.clinical_care_audit TO '${CLINICAL_CARE_DB_USER}'@'%';

-- Its own patient reference and its own record of processed events: fully owned.
GRANT ALL PRIVILEGES ON \`${DB_NAME}\`.clinical_care_patient_ref TO '${CLINICAL_CARE_DB_USER}'@'%';
GRANT ALL PRIVILEGES ON \`${DB_NAME}\`.clinical_care_processed_events TO '${CLINICAL_CARE_DB_USER}'@'%';

-- ONE-TIME PROVISIONING BACKFILL (ADR 0064 #5): patients registered before events existed have no PatientRegistered to
-- deliver, so their reference is copied once, here, as root. This is provisioning, not a runtime read: the service itself
-- has no privilege on patient_data. INSERT IGNORE makes a rerun harmless and never overwrites a reference an event wrote.
-- It runs at every compose up, which also picks up a patient created by a legacy writer while events are not flowing.
INSERT IGNORE INTO \`${DB_NAME}\`.clinical_care_patient_ref (patient_uuid, pid, source)
  SELECT uuid, pid, 'backfill' FROM \`${DB_NAME}\`.patient_data WHERE uuid IS NOT NULL AND pid > 0;
FLUSH PRIVILEGES;
SQL

echo "clinical_care_outbox and clinical_care_audit tables and user '${CLINICAL_CARE_DB_USER}' ready."
