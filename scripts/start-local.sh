#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/.." && pwd)"
state_path="${repository_root}/.decisionforge"
pid_path="${state_path}/apphost.pid"
log_path="${state_path}/apphost.log"
resource_path="${state_path}/containers.txt"

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

dotnet_command="$(resolve_command dotnet.exe dotnet)"
npm_command="$(resolve_command npm npm.cmd)"
docker_command="$(resolve_command docker.exe docker)"

if [[ "${dotnet_command}" == *.exe ]] && command -v powershell.exe >/dev/null 2>&1; then
  mkdir -p "${state_path}"
  powershell_script="$(wslpath -w "${script_dir}/start-local.ps1")"
  wsl_log_path="${state_path}/wsl-lifecycle.log"
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "${powershell_script}" \
    >"${wsl_log_path}" 2>&1 &
  launcher_pid=$!
  disown "${launcher_pid}" 2>/dev/null || true

  if ! "${script_dir}/smoke-local.sh"; then
    powershell.exe -NoProfile -ExecutionPolicy Bypass \
      -File "$(wslpath -w "${script_dir}/stop-local.ps1")" || true
    tail -100 "${wsl_log_path}" >&2
    exit 1
  fi

  deadline=$((SECONDS + 30))
  until [[ -f "${pid_path}" && -f "${resource_path}" ]]; do
    if (( SECONDS >= deadline )); then
      echo 'PowerShell lifecycle completed smoke but did not record its resources.' >&2
      exit 1
    fi
    sleep 1
  done

  recorded_pid="$(tr -d '\r\n' <"${pid_path}")"
  echo "DecisionForge local topology started with AppHost PID ${recorded_pid}."
  echo "Logs: ${state_path}/apphost.log"
  exit 0
fi

"${script_dir}/validate-tools.sh"
"${docker_command}" info >/dev/null || {
  echo 'Docker is unavailable. Start Docker Desktop and retry.' >&2
  exit 1
}

if [[ -f "${pid_path}" ]]; then
  existing_pid="$(<"${pid_path}")"
  if kill -0 "${existing_pid}" 2>/dev/null; then
    echo "DecisionForge AppHost is already running with PID ${existing_pid}." >&2
    exit 1
  fi
fi

mkdir -p "${state_path}"
mapfile -t containers_before < <("${docker_command}" ps --all --quiet)

pushd "${repository_root}" >/dev/null
"${dotnet_command}" restore DecisionForge.sln --locked-mode
"${dotnet_command}" build DecisionForge.sln --configuration Release --no-restore
popd >/dev/null

pushd "${repository_root}/src/DecisionForge.Web" >/dev/null
"${npm_command}" ci
popd >/dev/null

pushd "${repository_root}" >/dev/null
nohup "${dotnet_command}" run \
  --project 'src/DecisionForge.AppHost/DecisionForge.AppHost.csproj' \
  --configuration Release --no-build >"${log_path}" 2>&1 &
apphost_pid=$!
popd >/dev/null
printf '%s\n' "${apphost_pid}" >"${pid_path}"

if ! "${script_dir}/smoke-local.sh"; then
  for container_id in $("${docker_command}" ps --all --quiet); do
    if [[ ! " ${containers_before[*]} " =~ (^|[[:space:]])${container_id}($|[[:space:]]) ]]; then
      printf '%s\n' "${container_id}" >>"${resource_path}"
    fi
  done
  "${script_dir}/stop-local.sh"
  exit 1
fi

declare -A existing_containers=()
for container_id in "${containers_before[@]}"; do
  existing_containers["${container_id}"]=1
done

created_containers=()
while IFS= read -r container_id; do
  if [[ -z "${existing_containers[${container_id}]+present}" ]]; then
    created_containers+=("${container_id}")
  fi
done < <("${docker_command}" ps --all --quiet)

if [[ ${#created_containers[@]} -ne 2 ]]; then
  "${script_dir}/stop-local.sh"
  echo "Expected Aspire to create two containers, found ${#created_containers[@]}." >&2
  exit 1
fi
printf '%s\n' "${created_containers[@]}" >"${resource_path}"

echo "DecisionForge local topology started with AppHost PID ${apphost_pid}."
echo "Logs: ${log_path}"
