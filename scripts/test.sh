#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/.." && pwd)"

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

"${script_dir}/validate-tools.sh"

pushd "${repository_root}" >/dev/null
"${dotnet_command}" restore "DecisionForge.sln" --locked-mode
"${dotnet_command}" test "DecisionForge.sln" --configuration Release --no-restore
popd >/dev/null
