#!/usr/bin/env bash

# Rebuild FluentKit, regenerate the API reference, copy the machine-readable reference into
# the consumer skill, and run the same freshness checks used by CI.
set -euo pipefail

CONFIGURATION="${1:-Release}"
ROOT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

dotnet build src/FluentKit/FluentKit.csproj -c "$CONFIGURATION"

dotnet run --project tools/FluentKit.ApiReferenceGenerator \
  -c "$CONFIGURATION" -- \
  --assembly "src/FluentKit/bin/$CONFIGURATION/net10.0/FluentKit.dll" \
  --xml "src/FluentKit/bin/$CONFIGURATION/net10.0/FluentKit.xml" \
  --manifest docs/consumer/manifest.json \
  --json docs/reference/api.json \
  --markdown docs/reference/api.md

cp docs/reference/api.json fluentkit-consumer/references/api.json

dotnet run --project tools/FluentKit.ApiReferenceGenerator \
  -c "$CONFIGURATION" -- \
  --assembly "src/FluentKit/bin/$CONFIGURATION/net10.0/FluentKit.dll" \
  --xml "src/FluentKit/bin/$CONFIGURATION/net10.0/FluentKit.xml" \
  --manifest docs/consumer/manifest.json \
  --json docs/reference/api.json \
  --markdown docs/reference/api.md \
  --summary-baseline docs/reference/summary-baseline.json \
  --check-summaries --verify

cmp --silent docs/reference/api.json fluentkit-consumer/references/api.json

PACKAGE_VERSION="$(dotnet msbuild src/FluentKit/FluentKit.csproj -getProperty:Version -nologo)"
SKILL_VERSION="$(sed -nE 's/.*"fluentkitVersion"[[:space:]]*:[[:space:]]*"([^"]+)".*/\1/p' fluentkit-consumer/metadata.json)"
if [[ "$PACKAGE_VERSION" != "$SKILL_VERSION" ]]; then
  printf 'Version mismatch: package=%s skill=%s\n' "$PACKAGE_VERSION" "$SKILL_VERSION" >&2
  exit 1
fi

printf 'Consumer reference is current for FluentKit %s.\n' "$PACKAGE_VERSION"
