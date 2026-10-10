param(
    [string]$BaseUrl = "http://127.0.0.1:5050",
    [string]$Username
)

$ErrorActionPreference = "Stop"

if (-not $Username) { $Username = (Read-Host "OpenEMR username").Trim() }
$pass = Read-Host "OpenEMR password" -AsSecureString
$plain = [System.Net.NetworkCredential]::new('', $pass).Password

# Log in through the facade
$raw = curl.exe -s -X POST "$BaseUrl/connect/token" `
    -H "Content-Type: application/x-www-form-urlencoded" `
    --data-urlencode "grant_type=password" `
    --data-urlencode "username=$Username" `
    --data-urlencode "password=$plain"
Remove-Variable plain, pass

$resp = $raw | ConvertFrom-Json
if (-not $resp.access_token) {
    Write-Host "Login failed: $($resp.error) $($resp.error_description)" -ForegroundColor Red
    exit 1
}
Write-Host "Login OK (token received, not printed)" -ForegroundColor Green

# Call the protected endpoint. Show the status and body, never the token.
$token = $resp.access_token
curl.exe -i "$BaseUrl/me" -H "Authorization: Bearer $token"
Remove-Variable token, resp, raw