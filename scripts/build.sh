#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/.." && pwd)"
web_path="${repository_root}/src/DecisionForge.Web"

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

"${script_dir}/validate-tools.sh"

pushd "${repository_root}" >/dev/null
"${dotnet_command}" restore "DecisionForge.sln" --locked-mode
"${dotnet_command}" format "DecisionForge.sln" --verify-no-changes --no-restore
"${dotnet_command}" build "DecisionForge.sln" --configuration Release --no-restore
popd >/dev/null

pushd "${web_path}" >/dev/null
"${npm_command}" ci
"${npm_command}" run format:check
"${npm_command}" run lint
"${npm_command}" run typecheck
"${npm_command}" run build
popd >/dev/null
