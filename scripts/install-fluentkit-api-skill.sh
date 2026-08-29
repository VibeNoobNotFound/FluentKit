#!/usr/bin/env bash
set -euo pipefail

repository="https://github.com/VibeNoobNotFound/FluentKit/releases/latest/download"
archive_url="$repository/fluentkit-api.zip"
checksum_url="$archive_url.sha256"
temporary_directory="$(mktemp -d)"
trap 'rm -rf "$temporary_directory"' EXIT

archive_path="$temporary_directory/fluentkit-api.zip"
checksum_path="$temporary_directory/fluentkit-api.zip.sha256"
curl --fail --location --silent --show-error "$archive_url" --output "$archive_path"
curl --fail --location --silent --show-error "$checksum_url" --output "$checksum_path"
expected_hash="$(awk 'NF { print $1; exit }' "$checksum_path")"
if command -v sha256sum >/dev/null 2>&1; then
  actual_hash="$(sha256sum "$archive_path" | awk '{ print $1 }')"
else
  actual_hash="$(shasum -a 256 "$archive_path" | awk '{ print $1 }')"
fi
if [[ -z "$expected_hash" || "$expected_hash" != "$actual_hash" ]]; then
  echo "FluentKit skill checksum verification failed." >&2
  exit 1
fi

unexpected_entries="$(unzip -Z1 "$archive_path" | awk '$0 !~ /^fluentkit-api(\/|$)/ { print; exit }')"
if [[ -n "$unexpected_entries" ]]; then
  echo "The downloaded FluentKit skill archive contains an unexpected path: $unexpected_entries" >&2
  exit 1
fi
unzip -q "$archive_path" -d "$temporary_directory"
source_directory="$temporary_directory/fluentkit-api"
if [[ ! -f "$source_directory/SKILL.md" ||
      ! -f "$source_directory/agents/openai.yaml" ||
      ! -f "$source_directory/scripts/resolve-fluentkit.sh" ||
      ! -f "$source_directory/scripts/resolve-fluentkit.ps1" ]]; then
  echo "The downloaded FluentKit skill archive is invalid." >&2
  exit 1
fi

codex_root="${CODEX_HOME:-$HOME/.codex}"
skills_directory="$codex_root/skills"
destination="$skills_directory/fluentkit-api"
mkdir -p "$skills_directory"
if [[ -e "$destination" ]]; then
  backup="$destination.backup.$(date +%Y%m%d%H%M%S).$$"
  mv "$destination" "$backup"
  echo "Backed up the existing skill to $backup"
fi
mv "$source_directory" "$destination"
chmod +x "$destination/scripts/resolve-fluentkit.sh"
echo "Installed FluentKit API bootstrap at $destination"
