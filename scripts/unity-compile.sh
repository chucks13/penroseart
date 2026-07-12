#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
source "$repo_root/scripts/unity-run.sh"

# Resolve the Unity editor binary. The default tracks the project's own editor version from
# ProjectSettings/ProjectVersion.txt (Unity's source of truth), so editor upgrades don't require
# editing this script. Override UNITY_BIN for a non-default install location or CI.
project_version_file="$repo_root/ProjectSettings/ProjectVersion.txt"
editor_version="$(awk '/^m_EditorVersion:/ {print $2; exit}' "$project_version_file" 2>/dev/null || true)"
if [ -z "${UNITY_BIN:-}" ] && [ -z "$editor_version" ]; then
  printf 'ERROR: could not read m_EditorVersion from %s and UNITY_BIN is unset.\n' "$project_version_file" >&2
  exit 1
fi
unity_bin="${UNITY_BIN:-/Applications/Unity/Hub/Editor/$editor_version/Unity.app/Contents/MacOS/Unity}"
log_file="${UNITY_COMPILE_LOG:-/tmp/penrose-unity-compile.log}"
license_timeout="${UNITY_LICENSE_TIMEOUT:-30}"
process_timeout="${UNITY_COMPILE_TIMEOUT:-300}"

if unity_editor_has_project_open "$repo_root"; then
  printf 'ERROR: Unity Editor already owns this project; refusing to launch a second Editor.\n' >&2
  printf 'Let the open Editor refresh/compile, then inspect: %s\n' "$repo_root/Logs/Editor.log" >&2
  exit 1
fi

unity_require_batchmode_host_access "$repo_root"

rm -f "$log_file"
set +e
unity_run_supervised \
  "$unity_bin" "$log_file" "$license_timeout" "$process_timeout" \
  -batchmode -quit -projectPath "$repo_root" -logFile "$log_file"
status=$?
set -e

printf 'Unity compile log: %s\n' "$log_file"

if [ "$UNITY_RUN_FAILURE_KIND" = "licensing" ]; then
  printf '\nUnity licensing failed; suppressing secondary compiler/package errors.\n' >&2
elif grep -q 'error CS' "$log_file" 2>/dev/null; then
  printf '\nCompile errors:\n'
  grep -n 'error CS' "$log_file"
fi

warning_count="$(grep -c 'warning CS' "$log_file" 2>/dev/null || true)"
printf 'C# warning count: %s\n' "$warning_count"

if [ "$status" -ne 0 ]; then
  exit "$status"
fi
