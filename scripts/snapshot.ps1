$ErrorActionPreference = "Stop"
$out = "snapshots"
New-Item -ItemType Directory -Force $out | Out-Null

# Stop the apps so the files are consistent. The database stays up.
docker compose stop openemr auth-facade

# Dump the database inside the container, then copy it out.
# Writing from PowerShell redirection can produce UTF-16, which MariaDB can't import.
docker compose exec mysql sh -c 'mariadb-dump -uroot -p"$MYSQL_ROOT_PASSWORD" --databases openemr --routines --events --single-transaction > /tmp/db.sql'
docker compose cp mysql:/tmp/db.sql "$out/db.sql"

# Archive the OpenEMR sites volume (config, DB credentials, encryption keys).
# The project name is set to openemr-next in compose, so the volume is predictable.
docker run --rm `
  -v openemr-next_sitesvolume:/data `
  -v "${PWD}/${out}:/backup" `
  alpine tar czf /backup/sites.tar.gz -C /data .

# Record which images produced this snapshot.
docker compose config --images | Out-File -Encoding ascii "$out/images.txt"

docker compose start openemr auth-facade
Write-Host "Snapshot written to $out"