#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 1 ]]; then
  echo "Usage: resolve-fluentkit.sh /absolute/path/to/Consumer.csproj [--property Name=Value ...] [--source PATH ...]" >&2
  exit 2
fi

project_path="$(cd -- "$(dirname -- "$1")" && pwd)/$(basename -- "$1")"
shift
msbuild_properties=()
restore_sources=()
while [[ $# -gt 0 ]]; do
  if [[ $# -lt 2 ]]; then
    echo "Usage: resolve-fluentkit.sh /absolute/path/to/Consumer.csproj [--property Name=Value ...] [--source PATH ...]" >&2
    exit 2
  fi
  case "$1" in
    --property) msbuild_properties+=("-p:$2") ;;
    --source) restore_sources+=("--source" "$2") ;;
    *)
      echo "Usage: resolve-fluentkit.sh /absolute/path/to/Consumer.csproj [--property Name=Value ...] [--source PATH ...]" >&2
      exit 2
      ;;
  esac
  shift 2
done
if [[ ! -f "$project_path" ]]; then
  echo "Consumer project not found: $project_path" >&2
  exit 2
fi

project_directory="$(dirname -- "$project_path")"
restore_command=(dotnet restore "$project_path")
if [[ -f "$project_directory/packages.lock.json" ]]; then
  restore_command+=(--locked-mode)
fi
if (( ${#restore_sources[@]} > 0 )); then restore_command+=("${restore_sources[@]}"); fi
if (( ${#msbuild_properties[@]} > 0 )); then restore_command+=("${msbuild_properties[@]}"); fi
"${restore_command[@]}"

msbuild_command=(dotnet msbuild "$project_path" -nologo)
if (( ${#msbuild_properties[@]} > 0 )); then msbuild_command+=("${msbuild_properties[@]}"); fi
skill_path="$("${msbuild_command[@]}" -getProperty:FluentKitAgentSkillPath | tail -n 1 | tr -d '\r')"
manifest_path="$("${msbuild_command[@]}" -getProperty:FluentKitAgentManifestPath | tail -n 1 | tr -d '\r')"

if [[ -z "$skill_path" || -z "$manifest_path" ]]; then
  echo "FluentKit.Blazor does not expose the agent contract. Upgrade to the first contract-bearing release." >&2
  exit 3
fi
if [[ ! -f "$skill_path" || ! -f "$manifest_path" ]]; then
  echo "FluentKit agent contract is incomplete after restore." >&2
  echo "Skill: $skill_path" >&2
  echo "Manifest: $manifest_path" >&2
  exit 4
fi

printf 'FluentKitAgentSkillPath=%s\n' "$skill_path"
printf 'FluentKitAgentManifestPath=%s\n' "$manifest_path"
