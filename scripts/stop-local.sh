#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/.." && pwd)"
pid_path="${repository_root}/.decisionforge/apphost.pid"
resource_path="${repository_root}/.decisionforge/containers.txt"

resolve_command() {
  local candidate
  for candidate in "$@"; do
    if command -v "${candidate}" >/dev/null 2>&1; then
      printf '%s' "${candidate}"
      return
    fi
  done
  return 1
}

docker_command="$(resolve_command docker.exe docker)"

if [[ "${docker_command}" == *.exe ]] && command -v powershell.exe >/dev/null 2>&1; then
  powershell_script="$(wslpath -w "${script_dir}/stop-local.ps1")"
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "${powershell_script}"
  exit $?
fi

if [[ ! -f "${pid_path}" && ! -f "${resource_path}" ]]; then
  echo 'DecisionForge AppHost is not recorded as running.'
  exit 0
fi

if [[ -f "${pid_path}" ]]; then
  apphost_pid="$(<"${pid_path}")"
  if kill -0 "${apphost_pid}" 2>/dev/null; then
    command_line="$(tr '\0' ' ' <"/proc/${apphost_pid}/cmdline" 2>/dev/null || true)"
    if [[ "${command_line}" != *'DecisionForge.AppHost'* ]]; then
      echo "PID ${apphost_pid} does not belong to DecisionForge AppHost; refusing to stop it." >&2
      exit 1
    fi

    kill -TERM "${apphost_pid}"
    for _ in {1..30}; do
      kill -0 "${apphost_pid}" 2>/dev/null || break
      sleep 1
    done
  fi

  rm -f -- "${pid_path}"
fi

if [[ -f "${resource_path}" ]]; then
  while IFS= read -r container_id; do
    metadata="$("${docker_command}" inspect --format '{{.Config.Image}}|{{index .Config.Labels "com.microsoft.developer.usvc-dev.persistent"}}' "${container_id}")"
    case "${metadata}" in
      'axllent/mailpit:v1.30.5|false'|'docker.io/library/postgres:18.4|false') ;;
      *)
        echo "Container ${container_id} is outside the recorded DecisionForge topology; refusing to remove it." >&2
        exit 1
        ;;
    esac

    "${docker_command}" rm --force "${container_id}" >/dev/null
  done <"${resource_path}"
  rm -f -- "${resource_path}"
fi

echo 'DecisionForge AppHost stopped.'
