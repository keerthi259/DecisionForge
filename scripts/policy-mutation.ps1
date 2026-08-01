$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$testProjectDirectory = Join-Path $repositoryRoot 'tests\DecisionForge.Domain.UnitTests'
$runName = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')

& (Join-Path $PSScriptRoot 'validate-tools.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Push-Location $repositoryRoot
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Push-Location $testProjectDirectory
    try {
        dotnet stryker `
            --project DecisionForge.Domain.csproj `
            --mutate 'Policies/Evaluation/**/*.cs' `
            --configuration Release `
            --threshold-high 85 `
            --threshold-low 75 `
            --break-at 75 `
            --reporter ClearText `
            --output "../../.decisionforge/mutation/$runName/full" `
            --skip-version-check
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

        dotnet stryker `
            --project DecisionForge.Domain.csproj `
            --mutate 'Policies/Evaluation/PolicyConditionEvaluator.cs' `
            --mutate 'Policies/Evaluation/PolicyOutcomeAggregator.cs' `
            --configuration Release `
            --threshold-high 90 `
            --threshold-low 85 `
            --break-at 85 `
            --reporter ClearText `
            --output "../../.decisionforge/mutation/$runName/critical" `
            --skip-version-check
        exit $LASTEXITCODE
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}
