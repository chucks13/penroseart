#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
unity_bin="${UNITY_BIN:-/Applications/Unity/Hub/Editor/6000.4.7f1/Unity.app/Contents/MacOS/Unity}"
results_file="${OSC_TEST_RESULTS:-/tmp/penrose-osc-tests.xml}"
log_file="${OSC_TEST_LOG:-/tmp/penrose-osc-tests.log}"
filter="${OSC_TEST_FILTER:-RaveSystem.Osc.Tests}"

rm -f "$results_file" "$log_file"

# Unity Test Framework docs state the regular Editor -quit argument is not
# supported while tests are running. The test runner exits Unity itself.
set +e
"$unity_bin" \
  -runTests -batchmode \
  -projectPath "$repo_root" \
  -testPlatform EditMode \
  -testFilter "$filter" \
  -testResults "$results_file" \
  -logFile "$log_file"
status=$?
set -e

printf 'OSC test log: %s\n' "$log_file"
printf 'OSC test results: %s\n' "$results_file"

if [ ! -f "$results_file" ]; then
  printf 'Unity did not write a test results file. Recent relevant log lines:\n' >&2
  grep -nE 'error CS|Test run|Passed|Failed|Exception|RaveSystem\.Osc' "$log_file" 2>/dev/null | tail -80 >&2 || true
  exit "${status:-1}"
fi

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
PY

exit "$status"
