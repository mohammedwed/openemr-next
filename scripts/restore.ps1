$ErrorActionPreference = "Stop"
$src = "snapshots"

# Read the app password from .env. PowerShell doesn't load .env automatically.
$dbPass = ((Get-Content .env | Where-Object { $_ -match '^DB_PASSWORD=' }) -replace '^DB_PASSWORD=', '').Trim()
if (-not $dbPass) { throw "DB_PASSWORD not found in .env" }

# Start from empty volumes.
docker compose down -v
docker compose up -d mysql --wait

# Load the database dump.
docker compose cp "$src/db.sql" mysql:/tmp/db.sql
docker compose exec mysql sh -c 'mariadb -uroot -p"$MYSQL_ROOT_PASSWORD" < /tmp/db.sql'

# Dumps don't include database users, so recreate the app user.
$tmp = Join-Path $env:TEMP "create-user.sql"
"CREATE USER IF NOT EXISTS 'openemr'@'%' IDENTIFIED BY '$dbPass'; GRANT ALL PRIVILEGES ON openemr.* TO 'openemr'@'%'; FLUSH PRIVILEGES;" | Set-Content -Path $tmp -Encoding ascii
docker compose cp $tmp mysql:/tmp/create-user.sql
Remove-Item $tmp
docker compose exec mysql sh -c 'mariadb -uroot -p"$MYSQL_ROOT_PASSWORD" < /tmp/create-user.sql'

# Restore the OpenEMR sites volume.
docker volume create openemr-next_sitesvolume | Out-Null
docker run --rm `
  -v openemr-next_sitesvolume:/data `
  -v "${PWD}/${src}:/backup" `
  alpine sh -c "tar xzf /backup/sites.tar.gz -C /data"

# Start everything. OpenEMR should detect its existing config and skip setup.
docker compose up -d --wait --wait-timeout 600