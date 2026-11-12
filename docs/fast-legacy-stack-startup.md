# Guide: starting a legacy system fast in a test pipeline

How to get a slow-booting legacy system up in under a minute instead of
twenty, by snapshotting its post-installation state instead of letting it
install itself on every run.

This guide documents the approach this project uses
(`scripts/snapshot/create.sh`, `scripts/stack/up.sh`,
`scripts/stack/apply-credentials.sh`, and the restore steps in
`.github/workflows/ci.yml`), explains the reasoning behind each piece, and
is written so you can apply the same technique to a different legacy
system.

> **This document is also published publicly** as a gist:
> <https://gist.github.com/mohammedwed/6233ee1b08439d69a89f192136e1b292>.
> This file is canonical. The gist differs deliberately in three ways: a
> public-facing title ("Why your CI/CD pipeline takes so long to boot —
> and what to do about it"), an added framing header, and a source-file
> index linking the real scripts on GitHub. The body text is otherwise
> identical, so substantive edits here should be carried across with
> `gh gist edit`, or the two will drift.

**The short version:** a legacy system's container image starts in
seconds, but its *first-run installer* takes minutes. CI always starts from
an empty volume, so CI pays that installer cost on every single run.
Capture the state the installer produces — database **and** the files it
generates outside the database — publish it as a versioned artifact, and
restore it instead. Restoring is I/O-bound and predictable; installing is
CPU-bound and variable.

---

## 1. The problem

To run meaningful integration tests against a legacy system you can't
mock, your pipeline has to actually start that system. For this project
that means: a database, the legacy application, its one-shot provisioning
steps, and only then the new services that depend on it.

The sequence is strictly ordered. The new service can't start until the
legacy system is genuinely ready, and the tests can't run until the new
service is up. So the whole pipeline serializes behind the slowest link,
and in a legacy-integration project that link is almost always the legacy
system's own startup.

You can see the problem's shape in how defensively this project had to
configure around it:

- `docker-compose.yml` gives the legacy system's healthcheck a
  `start_period: 3m` — three minutes during which failures don't even
  count against the retry budget, because a fresh install legitimately
  takes that long.
- CI's own start step allows `--wait-timeout 1200`: **twenty minutes**.
- The OAuth client provisioning script (`scripts/seeds/authfacade-client.php`)
  polls for the legacy system's own tables to exist, retrying for up to
  fifteen minutes before giving up.
- The new service polls the legacy system's readiness endpoint at startup
  on top of all of that (`OpenEmrReadinessGate`), as a second line of
  defense.

Every one of those numbers is generous because it has to be. And that has
a second cost beyond waiting: **when something is genuinely broken, the
pipeline takes twenty minutes to tell you.** Long timeouts make slow
failures, not just slow successes.

---

## 2. Why it takes so long

The intuitive assumption is that the container is slow to start. It isn't.
The container starts in seconds. What's slow is what the application does
*the first time it finds an empty database*.

A legacy system's first-run installer typically:

1. **Creates its schema** — often hundreds of tables. This project's
   dump is ~15 MB of DDL and seed data.
2. **Seeds reference data** — code lists, defaults, lookup tables,
   configuration rows. In a clinical system that's a lot of rows.
3. **Generates per-installation secrets** — site encryption keys, used to
   encrypt values it stores in its own database.
4. **Writes configuration files** — database credentials, site paths,
   feature flags, in files *outside* the database.
5. **Runs upgrade/migration logic** — frequently version-by-version, in
   sequence, each step inspecting what the previous one did.

Three properties of that work make it expensive in a pipeline:

**It's CPU- and round-trip-bound, not I/O-bound.** It isn't writing a
large amount of data; it's executing a long sequence of small,
interdependent steps, many of them separate database round trips. That
doesn't parallelize and doesn't benefit from a faster disk.

**It's unavoidable on a fresh volume.** This is the critical point for CI.
A developer pays the installer cost once and then has a warm volume for
weeks. A CI runner is new every time — fresh volume, every run, forever.
The cost that's one-time locally is per-run in the pipeline.

**It's variable.** A cold install takes noticeably longer than a warm
restart, and runner performance fluctuates. You can't set a tight timeout
around work whose duration you can't predict, so you set a generous one —
which is why a hung run costs twenty minutes.

Worth being precise about what is and isn't slow, because it determines
what's worth snapshotting:

| Step | Roughly how long | Worth snapshotting? |
|---|---|---|
| Pulling the image | seconds (cached after first) | No — the registry/layer cache already handles this |
| Starting the container process | seconds | No |
| **First-run installer** | **minutes, variable** | **Yes — this is the entire problem** |
| Starting on an already-installed volume | seconds | No — this is what you're trying to reach |

The goal, stated precisely: **reach the "already-installed volume" state
without ever running the installer.**

---

## 3. The solution: snapshot the post-install state

Run the installer **once**, by hand, on a machine where you don't mind
waiting. Capture everything it produced. Publish that as a versioned
artifact. From then on, every pipeline run and every developer's first
setup restores the artifact instead of installing.

### 3.1 What has to go in the snapshot

This is where most attempts at this technique fail, so it's worth being
exact.

**A database dump alone is not enough.** The installer generates state in
at least two places, and restoring only one leaves you with a system that
starts but doesn't work — usually failing later, confusingly, in a way
that looks like an application bug rather than a restore problem.

This project's snapshot is three files:

| File | What it is | Why it's needed |
|---|---|---|
| `db.sql` | Full database dump — schema, seed data, and everything the installer wrote | The obvious part: the schema and reference data you'd otherwise wait for |
| `sites.tar.gz` | Archive of the legacy system's own writable directory (`sites/`): its configuration file **and its site encryption keys** | The non-obvious part. Without the keys, the application cannot decrypt the encrypted values that are sitting in the database you just restored |
| `images.txt` | The exact image list the snapshot was captured from | A snapshot is only valid for the version that produced it. This records which version that was |

**The encryption-key point deserves emphasis**, because it's the failure
that's hardest to diagnose. If the legacy system encrypts values with
per-installation keys stored on disk, then a database row encrypted during
capture is only readable by the keys from that same installation. Restore
the database with *fresh* keys and the data is intact but unreadable — and
the symptom is typically a feature failing deep in the application, not an
error at restore time.

Generalizing: **inventory every place the installer writes.** Database,
configuration files, key material, generated assets, upload directories.
Whatever it writes that it later depends on has to be in the snapshot.

### 3.2 How much time this actually saves

| | Install path | Restore path |
|---|---|---|
| What happens | Hundreds of sequential DDL/seed operations, key generation, migration logic | Stream a dump into the database; untar an archive into a volume |
| Nature of the work | CPU and round-trip bound | I/O bound, mostly sequential |
| Duration | Minutes, **variable** | Well under a minute, **predictable** |
| On a fresh volume | Full cost, every time | Full cost, every time — but the full cost is now small |

The headline saving for this project is roughly **a few minutes to twenty
minutes of worst-case wait, down to under a minute of restore**, on every
pipeline run and every developer's first setup.

But the second-order benefits are arguably worth more:

- **Predictability lets you tighten timeouts.** Once startup is
  consistently fast, a generous timeout is no longer protecting you from
  normal variance — so a real hang can fail in two minutes instead of
  twenty. Fast failure is a productivity feature.
- **Every run starts from a byte-identical state.** The installer is
  deterministic in principle but sensitive to timing and environment in
  practice. A restored snapshot removes a real source of test flakiness:
  tests that pass or fail depending on what the installer happened to
  produce.
- **You can snapshot an *interesting* state, not just a clean one.**
  The snapshot is whatever the system looked like when you captured it. If
  your tests need a configured, populated system, capture it that way once
  rather than having every pipeline run build it up through the
  application's own slow paths.
- **Developer onboarding collapses.** `up.sh --restore` is a working stack
  in about a minute. Nobody's first day is spent watching an installer.

---

## 4. What makes this tricky (read before implementing)

The technique is simple; three details are not. Knowing them up front
saves a lot of confusion.

### 4.1 A snapshot freezes credentials

Whatever passwords and secrets existed at capture time are baked into the
snapshot, in several places at once: the database's own user accounts, the
application's configuration file, any secret the application stored
encrypted, and the admin account's password hash.

Two ways to handle it:

- **(a) Treat the snapshot's credentials as fixed and well-known.**
  Simple, and acceptable for a disposable local/CI stack — provided you
  state plainly that these are known, shared, non-secret values, and
  compensate with network isolation rather than secrecy.
- **(b) Re-apply fresh credentials after restoring.** More work, but it
  means the same snapshot artifact is reusable indefinitely, by anyone,
  without ever needing its own secrets rotated.

This project does **both**, deliberately: the admin password is (a) — it
must match the snapshot, documented as `ci-admin-password` — while the
database password and OAuth client secret are (b), re-applied by
`scripts/stack/apply-credentials.sh`.

### 4.2 Re-applying a secret may require running the legacy system's own code

This is the subtlest part of the whole technique, and it's worth
understanding even if your legacy system doesn't have this property.

If the legacy system stores a secret **encrypted with its own
per-installation keys**, you cannot produce a valid replacement from
outside it. Writing a plaintext value into the column doesn't work; the
application decrypts and compares, so a plaintext value simply never
matches. You have to encrypt the new value *with the restored
installation's own keys*, which in practice means **running the legacy
application's own crypto code against the restored key material.**

`apply-credentials.sh` does exactly that: it runs the legacy system's own
image, with the restored `sites` volume mounted, boots the application's
bootstrap, and calls its own encryption routine to produce the value it
then writes to the database.

With a sting in the tail, captured in that script's own comments: the
legacy application's bootstrap **queries the database during startup**.
So the throwaway container doing the encryption also needs to reach the
database — which is why the script inspects the running database
container's Docker network and joins it, rather than running in isolation.
Without that, the bootstrap fails before the encryption code is ever
reached.

The generalizable lesson: **when re-applying secrets into a restored
snapshot, expect to run the legacy system's own code, and expect that code
to have its own environmental requirements.** Budget for this; it is
usually the longest part of implementing this technique.

### 4.3 Database users may not be in the dump

In MySQL/MariaDB, user accounts are server-global, not part of any one
database — so a single-database dump doesn't contain them. Restoring the
dump gives you the data with no user able to log in to it.

Hence the explicit user-creation step in both `up.sh` and CI, *before*
credentials are applied. Check the equivalent for your database engine;
the general question is **"what does my dump deliberately not include?"**

---

## 5. How to implement

Five steps. Steps 1–2 you do once (and repeat only when the legacy version
changes); steps 3–5 are the automation that uses the result.

### Step 1 — Capture the snapshot

**Goal:** three artifacts representing a known-good, fully-installed
state.

1. **Get the system to the state you want to freeze.** Let the installer
   run to completion, once. Then apply any configuration your tests need
   — enabled features, API settings, an OAuth client. Everything you do
   here is work your pipeline never has to repeat.

2. **Stop the applications, but leave the database running.** You need the
   database up to dump it, but you need the applications stopped so the
   database and the file state are consistent with each other — a dump
   taken while the application is mid-write can disagree with the files
   you archive a moment later.

   ```bash
   docker compose stop openemr auth-facade
   trap 'docker compose start openemr auth-facade' EXIT   # restart even on failure
   ```

   That `trap` matters: a capture script that fails partway must not leave
   your stack stopped.

3. **Dump the database transactionally, to a temporary file first.**

   ```bash
   docker compose exec -T mysql sh -c \
     'mariadb-dump -uroot -p"$MYSQL_ROOT_PASSWORD" --databases "$DB_NAME" \
        --routines --events --single-transaction' > snapshots/db.sql.tmp
   mv snapshots/db.sql.tmp snapshots/db.sql
   ```

   Three details, each learned the hard way and recorded in
   `create.sh`'s comments:
   - `--single-transaction` for a consistent point-in-time dump.
   - `--routines --events` — stored procedures and scheduled events are
     part of the installed state and are *not* included by default.
   - **Write to a temp file and move it into place.** A failed dump must
     never overwrite a known-good snapshot.
   - Stream to the host rather than writing inside the container: a
     container writing to a path that a previous restore left root-owned
     will fail.

4. **Archive every directory the installer writes**, not just the
   database.

   ```bash
   docker run --rm \
     -v "openemr-next_sitesvolume:/data" \
     -v "$PWD/snapshots:/backup" \
     alpine sh -c "tar czf /backup/sites.tar.gz -C /data . \
                   && chown $(id -u):$(id -g) /backup/sites.tar.gz"
   ```

   The `chown` is there because the container runs as root and would
   otherwise hand you an archive you can't manage as your own user.

5. **Record the versions that produced it.**

   ```bash
   docker compose config --images > snapshots/images.txt
   ```

   A snapshot is only valid for the version that made it. This file is
   what lets you answer "is this snapshot still the right one?" later.

**Done when:** the three artifacts exist, and your stack is running again.

### Step 2 — Check it, then publish it as a versioned artifact

1. **Verify it contains no real data.** A snapshot of a running system
   contains whatever was in that system. Make this an explicit,
   deliberate check before the artifact goes anywhere — synthetic or
   properly sanitized data only.

2. **Do not commit it to the repository.** This project's dump is ~15 MB;
   committed, it would live in git history forever and be re-downloaded by
   every clone, with a new copy added every time it's regenerated.

3. **Publish it under a tag that names the legacy version:**

   ```bash
   gh release create "snapshot-openemr-8.0.0.3-ci"   # first time only
   gh release upload "snapshot-openemr-8.0.0.3-ci" \
     snapshots/db.sql snapshots/sites.tar.gz snapshots/images.txt --clobber
   ```

   The tag naming the version is the point: when the legacy system is
   upgraded, you create a *new* tag rather than overwriting the old one,
   so an older branch still resolves the snapshot that matches its code.

4. **Pin that tag in exactly the places that consume it, and change them
   together.** Here that's `SNAPSHOT_TAG` in `scripts/stack/up.sh` and in
   `.github/workflows/ci.yml` — a mismatch between them is a confusing
   failure, so `scripts/README.md` says explicitly to change both.

### Step 3 — Restore it in CI

The order matters, and each step has a reason. This is CI's actual
sequence:

1. **Download the snapshot by pinned tag.**
   ```bash
   gh release download "$SNAPSHOT_TAG" --dir snapshots \
     --pattern "db.sql" --pattern "sites.tar.gz" --pattern "images.txt"
   ```

2. **Start *only* the database**, and wait for its healthcheck.
   ```bash
   docker compose up -d mysql --wait
   ```
   Only the database. Starting the legacy application now would trigger
   the very installer you're avoiding.

3. **Restore the dump.**
   ```bash
   docker compose cp snapshots/db.sql mysql:/tmp/db.sql
   docker compose exec -T mysql sh -c 'mariadb -uroot -p"$MYSQL_ROOT_PASSWORD" < /tmp/db.sql'
   ```

4. **Create the application's database user** — the dump doesn't have it
   (§4.3). Create it with the run's own generated password.

5. **Restore the file state into its volume.**
   ```bash
   docker volume create openemr-next_sitesvolume
   docker run --rm -v openemr-next_sitesvolume:/data -v "$PWD/snapshots:/backup" \
     alpine sh -c "tar xzf /backup/sites.tar.gz -C /data"
   ```

6. **Apply this run's credentials** (§4.1, §4.2) — rewrite the
   application's config file, re-encrypt and store any secret that needs
   the application's own crypto, and set the database user's password.
   ```bash
   bash scripts/stack/apply-credentials.sh
   ```

7. **Now start everything else**, and wait on the orchestrator's own
   healthchecks.
   ```bash
   docker compose up -d --build --wait --wait-timeout 1200 openemr auth-facade
   ```

The legacy application starts for the first time against an
already-installed volume — so it skips the installer entirely and is
ready in seconds.

### Step 4 — Give developers the same path

Don't make the fast path CI-only, or local setup will stay slow and the
restore path will only ever be exercised by CI (where it's harder to
debug). Wrap the same sequence in one script — here `scripts/stack/up.sh`:

- `up.sh` — start or resume the existing stack.
- `up.sh --restore` — wipe the volumes and rebuild from the snapshot,
  downloading it automatically if `snapshots/` is empty.

Two details worth copying:

- **Download on demand.** A new developer runs one command; the script
  fetches the snapshot if it isn't already there.
- **Warn loudly about what `--restore` destroys.** It runs
  `docker compose down -v`, which deletes local database and application
  volumes. `up.sh`'s header comment says so in capitals, and the obvious
  mitigation is "take a snapshot first" — which is the same script from
  step 1.

### Step 5 — Shorten the timeouts you were forced to inflate

This is the step people forget, and it's where a lot of the real benefit
is. Those generous timeouts existed to accommodate the installer. Once the
installer never runs in your pipeline, revisit:

- The healthcheck's `start_period` — an already-installed system needs a
  fraction of a fresh install's grace period.
- The pipeline's wait timeout — minutes, not tens of minutes.
- Any provisioning script's retry budget — the tables it waits for now
  exist before it starts.

Lower these deliberately and watch for a run or two. **A twenty-minute
timeout on a one-minute operation isn't safety, it's a twenty-minute
delay on every genuine failure.**

---

## 6. Gotchas and failure modes

Things that will go wrong, and what they look like:

| Symptom | Likely cause |
|---|---|
| Restore succeeds, application starts, but a feature fails decrypting its own stored data | You restored the database but not the installation's key material (§3.1). The archive of the application's writable directory is not optional |
| `invalid_client` or equivalent on authentication after restore | A secret was written as plaintext instead of being encrypted with the restored installation's own keys (§4.2) |
| Application can't connect to its own database after restore | Its config file still holds the snapshot's old password, or the database user from §4.3 was never created |
| Admin login fails with the password you expect | The snapshot contains the admin password *hash* from capture time. Your configured admin password has to match the snapshot's |
| Works locally, fails in CI (or vice versa) | A volume or network name derived from the project directory name, which differs between a local checkout and CI. Read it from the running container rather than assuming it |
| The installer runs anyway, slowly | Something started the application before the restore finished. Start the database only, restore, *then* start the rest (§5.3) |
| A failed capture left a broken snapshot | The dump wasn't written to a temp file and moved into place (§5.1.3) |
| A failed capture left the stack stopped | No `trap` to restart the applications on exit (§5.1.2) |

---

## 7. Keeping it healthy over time

A snapshot is a frozen copy of something that keeps moving. Budget for
these:

- **Re-snapshot when the legacy version changes.** New version, new tag,
  update the pins. The old tag stays, so older branches keep working.
- **Re-snapshot when the schema changes** — including when *your own*
  provisioning adds structure the snapshot predates.
- **Watch for drift between the snapshot path and reality.** This is the
  real risk of the technique: your pipeline tests a restored installation
  forever, so a change that breaks the *installer* can't be caught by it.

  **The mitigation is to keep one job that installs from scratch** — not
  on every commit, but nightly or weekly — so the from-scratch path is
  still exercised somewhere. This project does **not** currently do that:
  CI only ever runs the restore path, so an installer-affecting
  regression would go unnoticed. Worth naming as a real gap rather than
  assuming the restore path's green runs cover it.
- **Document the coupling explicitly.** `scripts/README.md` states that
  restoring replaces the admin password hash, so the configured admin
  password must match the snapshot. Couplings like that are invisible and
  infuriating when undocumented.

---

## 8. When not to do this

The technique has real costs — an artifact to maintain, a publishing
step, a staleness risk, credential re-application machinery. Skip it when:

- **The installer is already fast.** Under about a minute, the complexity
  isn't worth it.
- **The installed state changes constantly.** If you'd be re-snapshotting
  weekly, you're maintaining a second build artifact for small gain.
- **You're testing the installer or the upgrade path itself.** Then a
  from-scratch install is the thing under test — don't skip it.
- **A faster layer already solves it.** A pre-baked image containing the
  installed state, or your orchestrator's volume caching, can be simpler
  than a snapshot-and-restore pipeline if the legacy system's licensing
  and your registry allow it. (Note the trade-off: an image with the
  installed state baked in is simpler to restore but harder to inspect,
  sanitize, and prove contains no real data — which is exactly why this
  project uses separable, inspectable dump files instead.)

---

## Summary

1. The legacy system's **installer**, not its container, is what's slow —
   and CI's fresh volume pays that cost on every single run.
2. Run the installer once, by hand. Capture the database dump **and**
   every file the installer generated, especially key material.
3. Publish it as a versioned artifact under a tag naming the legacy
   version. Don't commit it. Pin the tag everywhere it's consumed.
4. In the pipeline: start the database alone → restore → create the
   database user → restore the files → re-apply this run's credentials →
   then start everything else.
5. Give developers the identical path in one command.
6. Then shorten the timeouts the installer forced you to inflate, and
   keep one periodic from-scratch run so the installer path still gets
   exercised.
