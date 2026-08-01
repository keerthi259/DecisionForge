#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
run_name="$(date -u +%Y%m%dT%H%M%S%3NZ)"

"$script_dir/validate-tools.sh"
pushd "$repo_root" >/dev/null
dotnet.exe tool restore
pushd tests/DecisionForge.Domain.UnitTests >/dev/null
dotnet.exe stryker \
  --project DecisionForge.Domain.csproj \
  --mutate 'Policies/Evaluation/**/*.cs' \
  --configuration Release \
  --threshold-high 85 \
  --threshold-low 75 \
  --break-at 75 \
  --reporter ClearText \
  --output "../../.decisionforge/mutation/$run_name/full" \
  --skip-version-check
dotnet.exe stryker \
  --project DecisionForge.Domain.csproj \
  --mutate 'Policies/Evaluation/PolicyConditionEvaluator.cs' \
  --mutate 'Policies/Evaluation/PolicyOutcomeAggregator.cs' \
  --configuration Release \
  --threshold-high 90 \
  --threshold-low 85 \
  --break-at 85 \
  --reporter ClearText \
  --output "../../.decisionforge/mutation/$run_name/critical" \
  --skip-version-check
popd >/dev/null
popd >/dev/null
