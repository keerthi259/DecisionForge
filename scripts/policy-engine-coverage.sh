#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
run_name="$(date -u +%Y%m%dT%H%M%S%3NZ)"
results_dir=".decisionforge/coverage/policy-engine/$run_name"
project="tests/DecisionForge.Domain.UnitTests/DecisionForge.Domain.UnitTests.csproj"

"$script_dir/validate-tools.sh"
pushd "$repo_root" >/dev/null
dotnet.exe restore "$project" --locked-mode
dotnet.exe test "$project" \
  --configuration Release \
  --no-restore \
  --collect:'XPlat Code Coverage' \
  --results-directory "$results_dir" \
  -- \
  'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=[DecisionForge.Domain]DecisionForge.Domain.Policies.Evaluation.*'

coverage_file="$(find "$results_dir" -name coverage.cobertura.xml -type f -print -quit)"
if [[ -z "$coverage_file" ]]; then
  echo "Coverage output was not created under $results_dir." >&2
  exit 1
fi

node.exe scripts/check-policy-engine-coverage.mjs "$coverage_file"
popd >/dev/null
