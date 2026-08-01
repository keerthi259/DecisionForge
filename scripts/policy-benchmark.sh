#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
project="tests/DecisionForge.PerformanceTests/DecisionForge.PerformanceTests.csproj"

"$script_dir/validate-tools.sh"
pushd "$repo_root" >/dev/null
dotnet.exe restore "$project" --locked-mode
dotnet.exe test "$project" \
  --configuration Release \
  --no-restore \
  --filter 'FullyQualifiedName~PolicyEvaluatorPerformanceTests' \
  --logger 'console;verbosity=detailed'
dotnet.exe run \
  --project "$project" \
  --configuration Release \
  --no-restore \
  -- \
  --filter '*PolicyEvaluatorBenchmark*' \
  --job short \
  --warmupCount 3 \
  --iterationCount 5 \
  --artifacts .decisionforge/benchmarks/policy
popd >/dev/null
