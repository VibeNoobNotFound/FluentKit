#!/usr/bin/env bash

# Rebuild FluentKit and regenerate or verify the canonical API reference.
set -euo pipefail

CONFIGURATION="${1:-Release}"
MODE="${2:-}"
if [[ "$CONFIGURATION" == "--verify" ]]; then
  CONFIGURATION="Release"
  MODE="--verify"
fi
ROOT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

dotnet build src/FluentKit/FluentKit.csproj -c "$CONFIGURATION"

dotnet run --project tools/FluentKit.ApiReferenceGenerator \
  -c "$CONFIGURATION" -- \
  --assembly "src/FluentKit/bin/$CONFIGURATION/net10.0/FluentKit.dll" \
  --xml "src/FluentKit/bin/$CONFIGURATION/net10.0/FluentKit.xml" \
  --manifest docs/integration/manifest.json \
  --json docs/reference/api.json \
  --markdown docs/reference/api.md \
  --summary-baseline docs/reference/summary-baseline.json \
  --check-summaries $MODE

PACKAGE_VERSION="$(dotnet msbuild src/FluentKit/FluentKit.csproj -getProperty:Version -nologo)"
printf 'API reference is current for FluentKit %s.\n' "$PACKAGE_VERSION"
