#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
run_name="$(date -u +%Y%m%dT%H%M%S%3NZ)"
results_dir=".decisionforge/coverage/domain/$run_name"
project="tests/DecisionForge.Domain.UnitTests/DecisionForge.Domain.UnitTests.csproj"

resolve_command() {
  local candidate
  for candidate in "$@"; do
    if command -v "$candidate" >/dev/null 2>&1; then
      printf '%s' "$candidate"
      return
    fi
  done

  return 1
}

dotnet_command="$(resolve_command dotnet.exe dotnet)"
node_command="$(resolve_command node.exe node)"

"$script_dir/validate-tools.sh"
pushd "$repo_root" >/dev/null
"$dotnet_command" restore "$project" --locked-mode
"$dotnet_command" test "$project" \
  --configuration Release \
  --no-restore \
  --collect:'XPlat Code Coverage' \
  --results-directory "$results_dir" \
  -- \
  'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=[DecisionForge.Domain]*'

coverage_file="$(find "$results_dir" -name coverage.cobertura.xml -type f -print -quit)"
if [[ -z "$coverage_file" ]]; then
  echo "Coverage output was not created under $results_dir." >&2
  exit 1
fi

"$node_command" scripts/check-domain-coverage.mjs "$coverage_file"
popd >/dev/null
