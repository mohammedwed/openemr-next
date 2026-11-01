# Scripts

Run everything from the repository root.

| Script | Run by | Purpose |
| --- | --- | --- |
| `setup/new-env.sh` | you, once | Create `.env` from `.env.example` with generated secrets. Never overwrites an existing `.env`. |
| `setup/new-dev-cert.sh` | you, once | Generate the gateway's self-signed TLS certificate in `certs/` (ADR 0039). Never overwrites an existing one. |
| `stack/up.sh` | you | Start or resume the stack. With `--restore`, wipe the volumes and rebuild from the snapshot in `snapshots/` (downloaded from the release if missing). |
| `stack/apply-credentials.sh` | `up.sh`, CI | Write the `.env` credentials into a restored stack: the database password, OpenEMR's `sqlconf.php`, and the AuthFacade client secret and redirect URI. |
| `snapshot/create.sh` | you | Dump the database and sites volume into `snapshots/`. Stops `openemr` and `auth-facade` while it runs. |
| `seeds/authfacade-client.*` | the `seed` Compose service | Register the AuthFacade OAuth client in OpenEMR. Mounted into the container, not run directly. |
| `lib/env.sh` | other scripts, CI | Sourced helpers: `env_get`, `env_set`, `sql_quote`, `env_warn_legacy`. |
| `dev/generate-authfacade-openapi.sh` | you, after changing AuthFacade's API | Regenerate the OpenAPI snapshot `OpenEmrNext.AuthFacade.Client`'s typed client is generated from. No Docker needed; see [ADR 0031](../docs/architecture/0031-nswag-client-generated-from-a-committed-snapshot.md). |

## Requirements

| Tool | Needed by |
| --- | --- |
| Docker with `docker compose` | everything except `setup/new-env.sh`, `dev/generate-authfacade-openapi.sh` |
| `bash` (Git Bash on Windows) and `curl` | `stack/up.sh`, `stack/apply-credentials.sh`, `dev/generate-authfacade-openapi.sh` |
| `jq` | `dev/generate-authfacade-openapi.sh` |
| `openssl` | `setup/new-env.sh`, `setup/new-dev-cert.sh` |
| GitHub CLI `gh`, logged in | `stack/up.sh --restore` when `snapshots/` is empty |

## Optional environment variables

| Variable | Default | Read by |
| --- | --- | --- |
| `SNAPSHOT_TAG` | `snapshot-openemr-8.0.0.3-ci` | `stack/up.sh`. CI sets the same tag in `ci.yml`; change both together. |
| `SITES_VOLUME` | `openemr-next_sitesvolume` | `stack/up.sh`, `stack/apply-credentials.sh`. This is `<compose project>_sitesvolume`; set it if the project name changes. |

## Typical first run

```bash
bash scripts/setup/new-env.sh
bash scripts/setup/new-dev-cert.sh
bash scripts/stack/up.sh --restore
```

`--restore` deletes the local database and OpenEMR volumes. To start or resume without
restoring, run `bash scripts/stack/up.sh`.

## Snapshots

A snapshot is the database dump, the OpenEMR sites archive and an image list. CI and
`up.sh --restore` download them from the GitHub release named by `SNAPSHOT_TAG`.

1. Bring the stack to the state you want, then run `bash scripts/snapshot/create.sh`.
2. Check that `snapshots/` holds no real patient data.
3. Upload the files to the release. These commands are a suggestion, not something the repo
   runs, so check them before relying on them:

   ```bash
   gh release upload "$SNAPSHOT_TAG" snapshots/db.sql snapshots/sites.tar.gz snapshots/images.txt --clobber
   ```

   Use `gh release create` first if the tag does not exist. If the OpenEMR version changes,
   make a new tag and update `SNAPSHOT_TAG` in `up.sh` and `.github/workflows/ci.yml`.

Restoring a snapshot replaces the admin password hash, so `OPENEMR_ADMIN_PASSWORD` must match
the snapshot (`ci-admin-password` for the current one).

Variables are described in [`docs/configuration.md`](../docs/configuration.md).
