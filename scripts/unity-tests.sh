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
results_file="${UNITY_TEST_RESULTS:-/tmp/penrose-unity-tests.xml}"
log_file="${UNITY_TEST_LOG:-/tmp/penrose-unity-tests.log}"
platform="${UNITY_TEST_PLATFORM:-EditMode}"
filter="${1:-${UNITY_TEST_FILTER:-}}"
assembly_names="${UNITY_TEST_ASSEMBLY_NAMES:-}"
timeout_seconds="${UNITY_EDITOR_TEST_TIMEOUT:-300}"
license_timeout="${UNITY_LICENSE_TIMEOUT:-30}"
process_timeout="${UNITY_TEST_PROCESS_TIMEOUT:-900}"
status_file="${UNITY_TEST_STATUS:-${results_file%.xml}.status}"

print_results() {
  python3 - "$results_file" <<'PY'
import sys
import xml.etree.ElementTree as ET

path = sys.argv[1]
root = ET.parse(path).getroot()
print(
    f"result={root.attrib.get('result')} "
    f"total={root.attrib.get('total')} "
    f"passed={root.attrib.get('passed')} "
    f"failed={root.attrib.get('failed')} "
    f"skipped={root.attrib.get('skipped')} "
    f"duration={root.attrib.get('duration')}"
)
for case in root.iter('test-case'):
    print(f"{case.attrib.get('result')}: {case.attrib.get('fullname')} ({case.attrib.get('duration')}s)")
sys.exit(0 if root.attrib.get('result') == 'Passed' else 1)
PY
}

run_in_open_editor() {
  local request_file
  request_file="$(unity_write_bridge_request \
    "$repo_root" "test" "$platform" "$filter" "$assembly_names" "$results_file" "$status_file" "$log_file")"

  printf 'Unity Editor already has this project open; requested in-Editor test run via %s\n' "$request_file"
  if ! unity_wait_for_bridge_status "$request_file" "$status_file" "$timeout_seconds"; then
    printf 'Timed out waiting for open Unity Editor test results after %ss.\n' "$timeout_seconds" >&2
    printf 'If the Editor is in Play Mode or busy compiling/importing, stop that and rerun.\n' >&2
    printf 'For long all-test runs, set UNITY_EDITOR_TEST_TIMEOUT=<seconds>.\n' >&2
    return 1
  fi

  [ "$(unity_bridge_status_value "$status_file" result)" = "Passed" ]
}

run_batchmode() {
  local args=(
    -runTests -batchmode
    -projectPath "$repo_root"
    -testPlatform "$platform"
    -testResults "$results_file"
    -logFile "$log_file"
  )
  if [ -n "$filter" ]; then
    args+=(-testFilter "$filter")
  fi
  if [ -n "$assembly_names" ]; then
    args+=(-assemblyNames "$assembly_names")
  fi

  unity_require_batchmode_host_access "$repo_root" || return 1

  set +e
  unity_run_supervised \
    "$unity_bin" "$log_file" "$license_timeout" "$process_timeout" \
    "${args[@]}"
  local status=$?
  set -e
  return "$status"
}

rm -f "$results_file" "$log_file" "$status_file"

status=0
if unity_editor_has_project_open "$repo_root"; then
  test_path="open-editor-bridge"
  run_in_open_editor || status=$?
else
  test_path="batchmode"
  run_batchmode || status=$?
fi

printf 'Unity test path: %s (platform %s)\n' "$test_path" "$platform"
printf 'Unity test log: %s\n' "$log_file"
printf 'Unity test results: %s\n' "$results_file"

if [ ! -f "$results_file" ]; then
  printf 'Unity did not write a test results file. Recent relevant log lines:\n' >&2
  if [ -f "$log_file" ]; then
    grep -nE 'Licensing|readonly database|error CS|Test run|Passed|Failed|Exception|RaveSystem\.Osc|PenroseUnityTestBridge' "$log_file" 2>/dev/null | tail -80 >&2 || true
  fi
  [ -f "$status_file" ] && cat "$status_file" >&2
  exit "${status:-1}"
fi

results_status=0
print_results || results_status=$?
if [ "$test_path" = "open-editor-bridge" ] && [ "$status" -ne 0 ]; then
  exit "$status"
fi
exit "$results_status"
