[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solutionPath = Join-Path $repositoryRoot 'DecisionForge.sln'
$webPath = Join-Path $repositoryRoot 'src\DecisionForge.Web'

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory)][string] $Command,
        [Parameter(Mandatory)][string[]] $Arguments
    )

    & $Command @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$Command failed with exit code $LASTEXITCODE."
    }
}

& (Join-Path $PSScriptRoot 'validate-tools.ps1')

Push-Location $repositoryRoot
try {
    Invoke-CheckedCommand 'dotnet' @('restore', $solutionPath, '--locked-mode')
    Invoke-CheckedCommand 'dotnet' @('format', $solutionPath, '--verify-no-changes', '--no-restore')
    Invoke-CheckedCommand 'dotnet' @(
        'build',
        $solutionPath,
        '--configuration',
        'Release',
        '--no-restore'
    )

    Push-Location $webPath
    try {
        Invoke-CheckedCommand 'npm' @('ci')
        Invoke-CheckedCommand 'npm' @('run', 'format:check')
        Invoke-CheckedCommand 'npm' @('run', 'lint')
        Invoke-CheckedCommand 'npm' @('run', 'typecheck')
        Invoke-CheckedCommand 'npm' @('run', 'build')
    }
    finally {
        Pop-Location
    }
}
finally {
    Pop-Location
}
