$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot 'tests\DecisionForge.PerformanceTests\DecisionForge.PerformanceTests.csproj'
$artifacts = Join-Path $repositoryRoot '.decisionforge\benchmarks\policy'

& (Join-Path $PSScriptRoot 'validate-tools.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Push-Location $repositoryRoot
try {
    dotnet restore $project --locked-mode
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet test $project `
        --configuration Release `
        --no-restore `
        --filter 'FullyQualifiedName~PolicyEvaluatorPerformanceTests' `
        --logger 'console;verbosity=detailed'
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    dotnet run `
        --project $project `
        --configuration Release `
        --no-restore `
        -- `
        --filter '*PolicyEvaluatorBenchmark*' `
        --job short `
        --warmupCount 3 `
        --iterationCount 5 `
        --artifacts $artifacts
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
