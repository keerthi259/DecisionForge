[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'DecisionForge.sln'

& (Join-Path $PSScriptRoot 'validate-tools.ps1')

Push-Location $repositoryRoot
try {
    dotnet restore $solutionPath --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    dotnet test $solutionPath --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}
