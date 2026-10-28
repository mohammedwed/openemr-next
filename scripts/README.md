# Scripts

Run everything from the repository root.

| Script | Run by | Purpose |
| --- | --- | --- |
| `setup/new-env.sh` | you, once | Create `.env` from `.env.example` with generated secrets. Never overwrites an existing `.env`. |
| `stack/up.sh` | you | Start or resume the stack. With `--restore`, wipe the volumes and rebuild from the snapshot in `snapshots/` (downloaded from the release if missing). |
| `stack/apply-credentials.sh` | `up.sh`, CI | Write the `.env` credentials into a restored stack: the database password, OpenEMR's `sqlconf.php`, and the AuthFacade client secret and redirect URI. |
| `snapshot/create.ps1` | you (PowerShell) | Dump the database and sites volume into `snapshots/`. |
| `seeds/authfacade-client.*` | the `seed` Compose service | Register the AuthFacade OAuth client in OpenEMR. Mounted into the container, not run directly. |
| `lib/env.sh` | other scripts, CI | Sourced helpers: `env_get`, `env_set`, `sql_quote`, `env_warn_legacy`. |

Typical first run:

```bash
bash scripts/setup/new-env.sh
bash scripts/stack/up.sh --restore
```

Variables are described in [`docs/configuration.md`](../docs/configuration.md).
