#!/usr/bin/env bash
# Creates the two self-signed certificates OpenIddict.Client uses to encrypt and sign its own state
# tokens (ADR 0045), replacing the dev-only certificates it otherwise regenerates fresh every process
# start (ADR 0028's third gap). Never overwrites existing files.
#
#   bash scripts/setup/new-authfacade-certs.sh
#
# Needs:   openssl.
# Writes:  certs/authfacade-encryption.pfx, certs/authfacade-signing.pfx (git-ignored; regenerate any
#          time by deleting them).
set -euo pipefail

cd "$(dirname "$0")/../.."

mkdir -p certs

new_cert() {
  local name=$1
  if [[ -f "certs/$name.pfx" ]]; then
    echo "certs/$name.pfx already exists; leaving it as it is." >&2
    echo "Delete it first if you want a fresh certificate." >&2
    return 0
  fi

  local tmp
  tmp=$(mktemp -d)
  trap 'rm -rf "$tmp"' RETURN

  openssl req -x509 -newkey rsa:2048 -sha256 -days 3650 -nodes \
    -keyout "$tmp/key.pem" -out "$tmp/cert.pem" \
    -subj "/CN=AuthFacade $name"

  # No password: these protect nothing but transport/at-rest confidentiality on your own machine
  # (ADR 0020's existing local-dev maturity level), the same "no passphrase" choice
  # scripts/setup/new-dev-cert.sh already made for the gateway's own certificate. Confirmed .NET's
  # X509CertificateLoader reads an empty-password PFX from OpenSSL 3's default (non-legacy) export fine.
  openssl pkcs12 -export \
    -in "$tmp/cert.pem" -inkey "$tmp/key.pem" \
    -out "certs/$name.pfx" -passout pass:

  # The container reads this as its own non-root user, a different UID than whatever wrote it.
  chmod 644 "certs/$name.pfx"

  echo "Created certs/$name.pfx."
}

new_cert "authfacade-encryption"
new_cert "authfacade-signing"
