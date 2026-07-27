#!/usr/bin/env bash

# Shared safety boundary for repo-owned Unity batchmode commands.
# Unity licensing needs writable user state and local IPC outside the project,
# which Codex read-only and workspace-write sandboxes intentionally block.

UNITY_RUN_FAILURE_KIND=""
unity_owned_pid=""
unity_owned_log=""
unity_owned_bin=""

# Markers that prove licensing reached a usable state, matched as an ERE against the batchmode log.
# "Licensing is initialized" alone is not enough: Unity logs it only when something queries the
# module while initialization is still pending (paired with "Licensing is not yet initialized").
# When the licensing background thread wins that race, neither line is ever written, and a run that
# licensed perfectly well looks like a licensing failure. Entitlement resolution is logged on every
# successful run and means the license itself resolved, not merely that the IPC channel connected.
UNITY_LICENSING_READY_PATTERN='Licensing is initialized|Successfully resolved entitlement details'

unity_editor_has_project_open() {
  local repo_root="$1"
  local lock_file="$repo_root/Temp/UnityLockfile"

  [ -f "$lock_file" ] || return 1
  if command -v lsof >/dev/null 2>&1; then
    lsof "$lock_file" >/dev/null 2>&1
  else
    return 0
  fi
}

unity_licensing_log_path() {
  case "$(uname -s)" in
    Darwin) printf '%s\n' "$HOME/Library/Logs/Unity/Unity.Licensing.Client.log" ;;
    Linux) printf '%s\n' "$HOME/.config/unity3d/Unity.Licensing.Client.log" ;;
    *) printf '%s\n' "Unity.Licensing.Client.log" ;;
  esac
}

unity_require_batchmode_host_access() {
  local repo_root="$1"
  local project_probe_dir="$repo_root/Temp"
  local host_probe_dir
  local project_probe=""
  local host_probe=""

  case "$(uname -s)" in
    Darwin) host_probe_dir="$HOME/Library/Logs/Unity" ;;
    Linux) host_probe_dir="$HOME/.config/unity3d" ;;
    *)
      printf 'ERROR: Unity batchmode host-access preflight is not implemented for this platform.\n' >&2
      return 1
      ;;
  esac

  if ! mkdir -p "$project_probe_dir" 2>/dev/null; then
    printf 'ERROR: Unity batchmode cannot write the project Temp directory.\n' >&2
    printf 'This is expected in a read-only worker. Do not run Unity from delegated workers.\n' >&2
    return 1
  fi
  if ! project_probe="$(mktemp "$project_probe_dir/.penrose-unity-project-probe.XXXXXX" 2>/dev/null)"; then
    printf 'ERROR: Unity batchmode cannot write the project Temp directory.\n' >&2
    printf 'This is expected in a read-only worker. Do not run Unity from delegated workers.\n' >&2
    return 1
  fi
  rm -f "$project_probe"

  if [ ! -d "$host_probe_dir" ]; then
    if ! mkdir -p "$host_probe_dir" 2>/dev/null; then
      printf 'ERROR: Unity batchmode cannot access its host runtime directory: %s\n' "$host_probe_dir" >&2
      printf 'Codex read-only/workspace-write sandboxes cannot run Unity licensing.\n' >&2
      printf 'Run this script from the unsandboxed coordinator or a normal terminal.\n' >&2
      return 1
    fi
  fi
  if ! host_probe="$(mktemp "$host_probe_dir/.penrose-unity-host-probe.XXXXXX" 2>/dev/null)"; then
    printf 'ERROR: Unity batchmode cannot write its host runtime directory: %s\n' "$host_probe_dir" >&2
    printf 'Codex read-only/workspace-write sandboxes cannot run Unity licensing.\n' >&2
    printf 'Run this script from the unsandboxed coordinator or a normal terminal.\n' >&2
    return 1
  fi
  rm -f "$host_probe"
}

unity_kill_descendants() {
  local parent_pid="$1"
  local child_pid

  command -v pgrep >/dev/null 2>&1 || return 0
  for child_pid in $(pgrep -P "$parent_pid" 2>/dev/null || true); do
    unity_kill_descendants "$child_pid"
    kill -TERM "$child_pid" 2>/dev/null || true
  done
}

unity_cleanup_owned_processes() {
  local pid="${unity_owned_pid:-}"
  local license_pid=""
  local license_command=""
  local unity_contents=""
  local deadline

  [ -n "$pid" ] || return 0

  if [ -f "${unity_owned_log:-}" ]; then
    license_pid="$(sed -n 's/.*Successfully launched the LicensingClient (PId: \([0-9][0-9]*\)).*/\1/p' "$unity_owned_log" | tail -1)"
  fi

  unity_kill_descendants "$pid"
  kill -TERM "$pid" 2>/dev/null || true

  if [ -n "$license_pid" ] && [ -n "${unity_owned_bin:-}" ]; then
    license_command="$(ps -p "$license_pid" -o command= 2>/dev/null || true)"
    unity_contents="$(cd "$(dirname "$unity_owned_bin")/.." 2>/dev/null && pwd || true)"
    case "$license_command" in
      "$unity_contents"/Helpers/UnityLicensingClient.app/*)
        kill -TERM "$license_pid" 2>/dev/null || true
        ;;
    esac
  fi

  deadline=$((SECONDS + 5))
  while kill -0 "$pid" 2>/dev/null && [ "$SECONDS" -lt "$deadline" ]; do
    sleep 1
  done
  if kill -0 "$pid" 2>/dev/null; then
    unity_kill_descendants "$pid"
    kill -KILL "$pid" 2>/dev/null || true
  fi
  wait "$pid" 2>/dev/null || true
  unity_owned_pid=""
}

unity_run_supervised() {
  local unity_bin="$1"
  local log_file="$2"
  local license_timeout="$3"
  local process_timeout="$4"
  shift 4

  local started_at=$SECONDS
  local license_deadline=$((SECONDS + license_timeout))
  local process_deadline=$((SECONDS + process_timeout))
  local licensing_ready=0
  local licensing_ready_after=""
  local status=0

  UNITY_RUN_FAILURE_KIND=""
  unity_owned_log="$log_file"
  unity_owned_bin="$unity_bin"

  "$unity_bin" "$@" &
  unity_owned_pid=$!

  trap 'unity_cleanup_owned_processes' EXIT
  trap 'unity_cleanup_owned_processes; exit 130' INT TERM HUP

  while kill -0 "$unity_owned_pid" 2>/dev/null; do
    if [ "$licensing_ready" -eq 0 ] && grep -qE "$UNITY_LICENSING_READY_PATTERN" "$log_file" 2>/dev/null; then
      licensing_ready=1
      licensing_ready_after=$((SECONDS - started_at))
    fi

    if grep -q 'Licensing initialization failed' "$log_file" 2>/dev/null; then
      UNITY_RUN_FAILURE_KIND="licensing"
      printf 'ERROR: Unity licensing initialization failed. Stopping the owned batchmode process.\n' >&2
      unity_cleanup_owned_processes
      break
    fi

    if [ "$licensing_ready" -eq 0 ] && [ "$SECONDS" -ge "$license_deadline" ]; then
      UNITY_RUN_FAILURE_KIND="licensing"
      printf 'ERROR: Unity licensing did not initialize within %ss. Stopping the owned batchmode process.\n' "$license_timeout" >&2
      unity_cleanup_owned_processes
      break
    fi

    if [ "$SECONDS" -ge "$process_deadline" ]; then
      UNITY_RUN_FAILURE_KIND="timeout"
      printf 'ERROR: Unity batchmode exceeded its %ss process timeout. Stopping the owned process.\n' "$process_timeout" >&2
      unity_cleanup_owned_processes
      break
    fi

    sleep 1
  done

  if [ -n "$unity_owned_pid" ]; then
    set +e
    wait "$unity_owned_pid"
    status=$?
    set -e
    unity_owned_pid=""
  fi

  trap - EXIT INT TERM HUP

  if [ "$licensing_ready" -eq 0 ] && grep -qE "$UNITY_LICENSING_READY_PATTERN" "$log_file" 2>/dev/null; then
    licensing_ready=1
    licensing_ready_after=$((SECONDS - started_at))
  fi

  if [ -n "$UNITY_RUN_FAILURE_KIND" ]; then
    printf 'Unity batchmode log: %s\n' "$log_file" >&2
    printf 'Unity licensing log: %s\n' "$(unity_licensing_log_path)" >&2
    if [ "$UNITY_RUN_FAILURE_KIND" = "licensing" ]; then
      printf 'Compiler/package errors after this point are not trustworthy.\n' >&2
    fi
    return 70
  fi

  if [ "$licensing_ready" -eq 0 ]; then
    UNITY_RUN_FAILURE_KIND="licensing"
    printf 'ERROR: Unity exited without confirming licensing initialization.\n' >&2
    printf 'Unity batchmode log: %s\n' "$log_file" >&2
    printf 'Unity licensing log: %s\n' "$(unity_licensing_log_path)" >&2
    return 70
  fi

  printf 'Unity licensing initialized after %ss.\n' "$licensing_ready_after"
  return "$status"
}
