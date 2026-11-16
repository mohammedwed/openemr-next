#!/usr/bin/env bash
# Regenerates the committed OpenAPI snapshot OpenEmrNext.ClinicalCare.Client's typed client is
# generated from, by running the real ClinicalCare API briefly and reading its own
# /openapi/v1.json -- the same approach generate-authfacade-openapi.sh uses for AuthFacade's client.
#
#   bash scripts/dev/generate-clinicalcare-openapi.sh
#
# Why a running instance, not build-time generation: ClinicalCareDbOptions, InternalGatewayOptions and
# InternalEventsOptions are all [Required] and ValidateOnStart(), neither ever committed to a file --
# only injected via .env/Docker (see docker-compose.yml). A build-time generator (Microsoft.Extensions.ApiDescription.Server) runs that
# same startup validation with no server listening, so it fails the same way a bare `dotnet run` would
# outside Docker. This script supplies placeholder values for exactly those fields instead of requiring
# real secrets or weakening the validation. The database connection itself is never actually used here:
# EF Core only connects lazily, when a request needs it, and generating the OpenAPI document touches
# no repository.
#
# ClinicalCare.Api has no /health endpoint (unlike AuthFacade); this script polls
# /openapi/v1.json itself as the readiness check instead.
#
# Side effect: starts ClinicalCare.Api on a loopback port, waits for the document, then stops it.
# Nothing else running is touched.
# Needs: .NET SDK, curl, jq.
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/../.."

API_PROJECT=src/Services/ClinicalCare/OpenEmrNext.ClinicalCare.Api
OUTPUT=src/Services/ClinicalCare/OpenEmrNext.ClinicalCare.Client/OpenAPIs/ClinicalCare.json

echo "Building ClinicalCare.Api..."
dotnet build "$API_PROJECT" --configuration Debug >/dev/null

# A random port in the ephemeral range, not a guaranteed-free one: good enough for a short-lived dev
# script, not worth a second tool just to eliminate the rare collision.
PORT=$(shuf -i 49152-65535 -n 1)
BASE_URL="http://127.0.0.1:$PORT"

export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS="$BASE_URL"
export ClinicalCareDb__Host=openapi-codegen-placeholder
export ClinicalCareDb__Port=3306
export ClinicalCareDb__Database=openapi-codegen-placeholder
export ClinicalCareDb__User=openapi-codegen-placeholder
export ClinicalCareDb__Password=openapi-codegen-placeholder
export InternalGateway__Secret=openapi-codegen-placeholder
export InternalEvents__Secret=openapi-codegen-placeholder

API_LOG=$(mktemp)
(cd "$API_PROJECT" && exec dotnet run --no-build --no-launch-profile) >"$API_LOG" 2>&1 &
API_PID=$!
trap 'kill "$API_PID" 2>/dev/null || true; rm -f "$API_LOG"' EXIT

echo "Waiting for ClinicalCare.Api to become ready on $BASE_URL..."
for _ in $(seq 1 30); do
    if curl -fs "$BASE_URL/openapi/v1.json" >/dev/null 2>&1; then
        break
    fi
    sleep 1
done
if ! curl -fs "$BASE_URL/openapi/v1.json" >/dev/null; then
    echo "ClinicalCare.Api never became ready. Its output:" >&2
    cat "$API_LOG" >&2
    exit 1
fi

mkdir -p "$(dirname "$OUTPUT")"
# `servers` is dropped: it would just be this run's random loopback port, and the generated client
# never reads it (OpenApiReference passes /UseBaseUrl:false; callers supply their own
# HttpClient.BaseAddress instead) -- keeping it would make every regeneration diff from the last even
# with no real API change.
curl -fs "$BASE_URL/openapi/v1.json" | jq 'del(.servers)' > "$OUTPUT"

echo "Wrote $OUTPUT"
