$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$runName = [DateTimeOffset]::UtcNow.ToString('yyyyMMddTHHmmssfffZ')
$resultsDirectory = Join-Path $repositoryRoot ".decisionforge/coverage/policy/$runName"
$project = Join-Path $repositoryRoot 'tests/DecisionForge.Domain.UnitTests/DecisionForge.Domain.UnitTests.csproj'

& (Join-Path $PSScriptRoot 'validate-tools.ps1')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet restore $project --locked-mode
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet test $project `
    --configuration Release `
    --no-restore `
    --collect:'XPlat Code Coverage' `
    --results-directory $resultsDirectory `
    -- `
    'DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Include=[DecisionForge.Domain]DecisionForge.Domain.Policies.*'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$coverageFile = Get-ChildItem $resultsDirectory -Filter coverage.cobertura.xml -Recurse |
    Select-Object -First 1
if ($null -eq $coverageFile) {
    Write-Error "Coverage output was not created under $resultsDirectory."
    exit 1
}

node (Join-Path $PSScriptRoot 'check-policy-coverage.mjs') $coverageFile.FullName
exit $LASTEXITCODE
