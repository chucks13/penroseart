#!/usr/bin/env bash
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
results_file="${OSC_TEST_RESULTS:-/tmp/penrose-osc-tests.xml}"
log_file="${OSC_TEST_LOG:-/tmp/penrose-osc-tests.log}"
filter="${OSC_TEST_FILTER:-RaveSystem.Osc.Tests}"

UNITY_TEST_RESULTS="$results_file" \
UNITY_TEST_LOG="$log_file" \
UNITY_TEST_FILTER="$filter" \
UNITY_TEST_PLATFORM="EditMode" \
"$repo_root/scripts/unity-tests.sh"
