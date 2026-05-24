#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity_bin="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.4.7f1/Unity.app/Contents/MacOS/Unity}"
log_file="${UNITY_COMPILE_LOG:-/tmp/penrose-unity-compile.log}"

rm -f "$log_file"
set +e
"$unity_bin" -batchmode -quit -projectPath "$repo_root" -logFile "$log_file"
status=$?
set -e

printf 'Unity compile log: %s\n' "$log_file"

if grep -q 'error CS' "$log_file" 2>/dev/null; then
  printf '\nCompile errors:\n'
  grep -n 'error CS' "$log_file"
fi

warning_count="$(grep -c 'warning CS' "$log_file" 2>/dev/null || true)"
printf 'C# warning count: %s\n' "$warning_count"

if [ "$status" -ne 0 ]; then
  exit "$status"
fi
