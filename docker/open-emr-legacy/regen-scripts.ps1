<#
.SYNOPSIS
    Creates a .env file for the OpenEMR compose stack.
.DESCRIPTION
    Copies .env.example to .env if .env does not exist, generates random values
    for the local-only secrets, and validates the compose file.
    Existing .env files are never overwritten. Secret values are never printed.
#>

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

$envPath     = Join-Path $PSScriptRoot ".env"
$examplePath = Join-Path $PSScriptRoot ".env.example"

if (Test-Path $envPath) {
    Write-Host ".env already exists. Nothing overwritten." -ForegroundColor Yellow
    Write-Host "Delete it first if you want a fresh one."
    exit 0
}

if (-not (Test-Path $examplePath)) {
    throw ".env.example not found in $PSScriptRoot"
}

# Random value safe for .env and Compose interpolation (no $, +, /, =).
# Uses the instance API so it works on Windows PowerShell 5.1 and PowerShell 7.
function New-Secret {
    $bytes = New-Object byte[] 18
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
    } finally {
        $rng.Dispose()
    }
    [Convert]::ToBase64String($bytes) -replace '[+/=$]', 'x'
}

# Keys whose values are generated locally
$generatedKeys = @(
    'DB_ROOT_PASSWORD',
    'DB_PASSWORD',
    'OE_PASS',
    'COUCHDB_PASSWORD',
    'SE_VNC_PASSWORD',
    'SMTP_PASS'
)

$lines = Get-Content $examplePath | ForEach-Object {
    if ($_ -match '^([A-Z0-9_]+)=$' -and $generatedKeys -contains $Matches[1]) {
        "$($Matches[1])=$(New-Secret)"
    } else {
        $_
    }
}

Set-Content -Path $envPath -Value $lines -Encoding ascii
Write-Host "Created .env with generated local secrets." -ForegroundColor Green

# Warn about values still blank
$blank = Get-Content $envPath | Where-Object { $_ -match '^([A-Z0-9_]+)=$' } |
    ForEach-Object { $Matches[1] }
if ($blank) {
    Write-Host "Still blank (fill these in manually):" -ForegroundColor Yellow
    $blank | ForEach-Object { Write-Host "  $_" }
}

# Validate. Prints nothing on success; errors name the missing variable.
docker compose config --quiet
if ($LASTEXITCODE -eq 0) {
    Write-Host "Compose file resolves. Ready to run 'docker compose up -d'." -ForegroundColor Green
} else {
    Write-Host "Compose validation failed. See the message above." -ForegroundColor Red
    exit $LASTEXITCODE
}