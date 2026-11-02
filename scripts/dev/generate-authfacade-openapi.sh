#!/usr/bin/env bash
# Regenerates the committed OpenAPI snapshot OpenEmrNext.AuthFacade.Client's typed client is generated
# from, by running the real AuthFacade API briefly and reading its own /openapi/v1.json.
#
#   bash scripts/dev/generate-authfacade-openapi.sh
#
# Why a running instance, not build-time generation: AuthFacade's startup validates OpenEmr:ClientId and
# OpenEmr:ClientSecret as required (OpenEmrOptions), and neither is ever committed to a file -- only
# injected via .env/Docker (see docker-compose.yml). A build-time generator (Microsoft.Extensions.
# ApiDescription.Server) runs that same startup validation with no server listening, so it fails the same
# way a bare `dotnet run` would outside Docker. This script supplies placeholder values for exactly those
# two fields -- the same ones TestApi.cs already uses for its in-process tests -- instead of requiring real
# secrets or weakening the validation.
#
# Side effect: starts AuthFacade.Api on a loopback port, waits for /health, reads the document, then
# stops it. Nothing else running is touched.
# Needs: .NET SDK, curl, jq.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."
source scripts/lib/env.sh

API_PROJECT=src/Services/AuthFacade/OpenEmrNext.AuthFacade.Api
OUTPUT=src/Services/AuthFacade/OpenEmrNext.AuthFacade.Client/OpenAPIs/AuthFacade.json

echo "Building AuthFacade.Api..."
dotnet build "$API_PROJECT" --configuration Debug >/dev/null

# A random port in the ephemeral range, not a guaranteed-free one: good enough for a short-lived dev
# script, not worth a second tool just to eliminate the rare collision.
PORT=$(shuf -i 49152-65535 -n 1)
BASE_URL="http://127.0.0.1:$PORT"

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="$BASE_URL"
export OpenEmr__ClientId=openapi-codegen-placeholder
export OpenEmr__ClientSecret=openapi-codegen-placeholder
# Overrides appsettings.Development.json's own OpenEmr:BaseUrl (https://localhost:9300): that one assumes
# whoever's running this has already told their OS to trust OpenEMR's self-signed dev cert, which is true
# for a developer's own machine but not a CI runner. The plain `new HttpClient()` inside
# OpenEmrReadinessGate has no cert bypass, so against the HTTPS address it fails every poll on an
# untrusted cert and silently eats its own full 60-second non-fatal timeout before giving up and starting
# anyway -- longer than this script's own health-check patience below, so it would get killed first. The
# real Compose stack never hits this: it points AuthFacade at OpenEMR over plain HTTP, container-to-
# container (OpenEmr__BaseUrl: "http://openemr:80" in docker-compose.yml). Doing the same here, against
# OpenEMR's plain-HTTP host port, sidesteps the cert entirely instead of just waiting out the timeout.
export OpenEmr__BaseUrl="http://127.0.0.1:$(env_get OPENEMR_HTTP_PORT 8300)"

API_LOG=$(mktemp)
(cd "$API_PROJECT" && exec dotnet run --no-build --no-launch-profile) >"$API_LOG" 2>&1 &
API_PID=$!
trap 'kill "$API_PID" 2>/dev/null || true; rm -f "$API_LOG"' EXIT

echo "Waiting for AuthFacade.Api to become ready on $BASE_URL..."
for _ in $(seq 1 30); do
    if curl -fs "$BASE_URL/health" >/dev/null 2>&1; then
        break
    fi
    sleep 1
done
if ! curl -fs "$BASE_URL/health" >/dev/null; then
    echo "AuthFacade.Api never became ready. Its output:" >&2
    cat "$API_LOG" >&2
    exit 1
fi

mkdir -p "$(dirname "$OUTPUT")"
# `servers` is dropped: it would just be this run's random loopback port, and the generated client never
# reads it (OpenApiReference passes /UseBaseUrl:false; callers supply their own HttpClient.BaseAddress
# instead) -- keeping it would make every regeneration diff from the last even with no real API change.
curl -fs "$BASE_URL/openapi/v1.json" | jq 'del(.servers)' > "$OUTPUT"

echo "Wrote $OUTPUT"
