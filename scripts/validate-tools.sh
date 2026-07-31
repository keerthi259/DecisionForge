#!/usr/bin/env bash
set -uo pipefail

required_dotnet='10.0.302'
required_node='v24.18.1'
required_node_file_value='24.18.1'
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/.." && pwd)"
failures=()

resolve_executable() {
  local candidate
  for candidate in "$@"; do
    if command -v "${candidate}" >/dev/null 2>&1; then
      printf '%s' "${candidate}"
      return
    fi
  done
}

command_version() {
  local executable="$1"
  shift
  if [[ -z "${executable}" ]]; then
    printf '%s' 'MISSING'
    return
  fi

  local output
  if ! output="$("${executable}" "$@" 2>&1)"; then
    printf '%s' 'ERROR'
    return
  fi
  output="${output//$'\r'/}"
  printf '%s' "${output%%$'\n'*}"
}

read_pin() {
  local path="$1"
  if [[ ! -f "${path}" ]]; then
    failures+=("Required version pin is missing: ${path}")
    printf '%s' 'MISSING'
    return
  fi
  tr -d '[:space:]' < "${path}"
}

dotnet_executable="$(resolve_executable dotnet.exe dotnet)"
node_executable="$(resolve_executable node.exe node)"
npm_executable="$(resolve_executable npm npm.cmd)"
git_executable="$(resolve_executable git.exe git)"
docker_executable="$(resolve_executable docker.exe docker)"

if [[ ! -f "${repository_root}/global.json" ]]; then
  failures+=("Required version pin is missing: ${repository_root}/global.json")
  global_json=''
else
  global_json="$(tr -d '[:space:]' < "${repository_root}/global.json")"
  [[ "${global_json}" == *'"version":"10.0.302"'* ]] || failures+=("global.json must pin .NET SDK ${required_dotnet}.")
  [[ "${global_json}" == *'"rollForward":"disable"'* ]] || failures+=("global.json must set sdk.rollForward to disable.")
  [[ "${global_json}" == *'"allowPrerelease":false'* ]] || failures+=("global.json must reject prerelease SDKs.")
fi

nvmrc_pin="$(read_pin "${repository_root}/.nvmrc")"
node_version_pin="$(read_pin "${repository_root}/.node-version")"
[[ "${nvmrc_pin}" == "${required_node_file_value}" ]] || failures+=(".nvmrc must pin Node.js ${required_node_file_value}; found '${nvmrc_pin}'.")
[[ "${node_version_pin}" == "${required_node_file_value}" ]] || failures+=(".node-version must pin Node.js ${required_node_file_value}; found '${node_version_pin}'.")

dotnet_version="$(command_version "${dotnet_executable}" --version)"
node_version="$(command_version "${node_executable}" --version)"
npm_version="$(command_version "${npm_executable}" --version)"
git_version="$(command_version "${git_executable}" --version)"
docker_version="$(command_version "${docker_executable}" --version)"

[[ "${dotnet_version}" != 'MISSING' && "${dotnet_version}" != 'ERROR' ]] || failures+=("dotnet is not installed or could not be executed from PATH.")
[[ "${node_version}" != 'MISSING' && "${node_version}" != 'ERROR' ]] || failures+=("node is not installed or could not be executed from PATH.")
[[ "${npm_version}" != 'MISSING' && "${npm_version}" != 'ERROR' ]] || failures+=("npm is not installed or could not be executed from PATH.")
[[ "${git_version}" != 'MISSING' && "${git_version}" != 'ERROR' ]] || failures+=("git is not installed or could not be executed from PATH.")
[[ "${docker_version}" != 'MISSING' && "${docker_version}" != 'ERROR' ]] || failures+=("docker is not installed or could not be executed from PATH.")

[[ "${dotnet_version}" == "${required_dotnet}" ]] || failures+=(".NET SDK ${required_dotnet} is required; active version is '${dotnet_version}'. Install the pinned SDK or correct PATH.")
[[ "${node_version}" == "${required_node}" ]] || failures+=("Node.js ${required_node} is required; active version is '${node_version}'. Select the pinned version from .nvmrc.")

printf '%s\n' 'DecisionForge tool validation'
printf 'Repository: %s\n' "${repository_root}"
printf 'dotnet: %s (required %s)\n' "${dotnet_version}" "${required_dotnet}"
printf 'node: %s (required %s)\n' "${node_version}" "${required_node}"
printf 'npm: %s\n' "${npm_version}"
printf 'git: %s\n' "${git_version}"
printf 'docker: %s\n' "${docker_version}"

if (( ${#failures[@]} > 0 )); then
  printf '%s\n' 'Tool validation failed:' >&2
  printf -- '- %s\n' "${failures[@]}" >&2
  exit 1
fi

printf '%s\n' 'Tool validation passed.'
