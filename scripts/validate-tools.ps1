[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$requiredDotnet = '10.0.302'
$requiredNode = 'v24.18.1'
$requiredNodeFileValue = '24.18.1'
$failures = [System.Collections.Generic.List[string]]::new()

function Get-RepositoryRoot {
    return (Split-Path -Parent $PSScriptRoot)
}

function Read-Pin {
    param([Parameter(Mandatory)][string] $Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        $failures.Add("Required version pin is missing: $Path")
        return $null
    }

    return (Get-Content -Raw -LiteralPath $Path).Trim()
}

function Get-CommandVersion {
    param(
        [Parameter(Mandatory)][string] $Name,
        [Parameter(Mandatory)][scriptblock] $VersionCommand
    )

    if ($null -eq (Get-Command $Name -ErrorAction SilentlyContinue)) {
        $failures.Add("$Name is not installed or is not available on PATH.")
        return $null
    }

    try {
        $output = @(& $VersionCommand 2>&1)
        $exitCode = $LASTEXITCODE
        if ($exitCode -ne 0) {
            $summary = ($output | Select-Object -First 1).ToString().Trim()
            $failures.Add("Unable to execute ${Name} (exit code $exitCode): $summary")
            return $null
        }

        if ($output.Count -eq 0) {
            $failures.Add("Unable to determine the $Name version: command returned no output.")
            return $null
        }

        return ($output | Select-Object -First 1).ToString().Trim()
    }
    catch {
        $failures.Add("Unable to execute ${Name}: $($_.Exception.Message)")
        return $null
    }
}

function Format-Version {
    param([AllowNull()][string] $Version)

    if ([string]::IsNullOrWhiteSpace($Version)) {
        return 'MISSING'
    }

    return $Version
}

$repositoryRoot = Get-RepositoryRoot
$globalJsonPath = Join-Path $repositoryRoot 'global.json'
$nvmrcPath = Join-Path $repositoryRoot '.nvmrc'
$nodeVersionPath = Join-Path $repositoryRoot '.node-version'

try {
    $globalJson = Get-Content -Raw -LiteralPath $globalJsonPath | ConvertFrom-Json
    $dotnetPin = [string]$globalJson.sdk.version
    if ($dotnetPin -ne $requiredDotnet) {
        $failures.Add("global.json must pin .NET SDK $requiredDotnet; found '$dotnetPin'.")
    }
    if ([string]$globalJson.sdk.rollForward -ne 'disable') {
        $failures.Add("global.json must set sdk.rollForward to 'disable'.")
    }
    if ([bool]$globalJson.sdk.allowPrerelease) {
        $failures.Add('global.json must reject prerelease SDKs.')
    }
}
catch {
    $failures.Add("global.json is missing or invalid JSON: $($_.Exception.Message)")
}

$nvmrcPin = Read-Pin -Path $nvmrcPath
$nodeVersionPin = Read-Pin -Path $nodeVersionPath
if ($null -ne $nvmrcPin -and $nvmrcPin -ne $requiredNodeFileValue) {
    $failures.Add(".nvmrc must pin Node.js $requiredNodeFileValue; found '$nvmrcPin'.")
}
if ($null -ne $nodeVersionPin -and $nodeVersionPin -ne $requiredNodeFileValue) {
    $failures.Add(".node-version must pin Node.js $requiredNodeFileValue; found '$nodeVersionPin'.")
}

$dotnetVersion = Get-CommandVersion -Name 'dotnet' -VersionCommand { dotnet --version }
$nodeVersion = Get-CommandVersion -Name 'node' -VersionCommand { node --version }
$npmVersion = Get-CommandVersion -Name 'npm' -VersionCommand { npm --version }
$gitVersion = Get-CommandVersion -Name 'git' -VersionCommand { git --version }
$dockerVersion = Get-CommandVersion -Name 'docker' -VersionCommand { docker --version }

if ($null -ne $dotnetVersion -and $dotnetVersion -ne $requiredDotnet) {
    $failures.Add(".NET SDK $requiredDotnet is required; active version is '$dotnetVersion'. Install the pinned SDK or correct PATH.")
}
if ($null -ne $nodeVersion -and $nodeVersion -ne $requiredNode) {
    $failures.Add("Node.js $requiredNode is required; active version is '$nodeVersion'. Select the pinned version from .nvmrc.")
}

Write-Output 'DecisionForge tool validation'
Write-Output "Repository: $repositoryRoot"
Write-Output "dotnet: $(Format-Version -Version $dotnetVersion) (required $requiredDotnet)"
Write-Output "node: $(Format-Version -Version $nodeVersion) (required $requiredNode)"
Write-Output "npm: $(Format-Version -Version $npmVersion)"
Write-Output "git: $(Format-Version -Version $gitVersion)"
Write-Output "docker: $(Format-Version -Version $dockerVersion)"

if ($failures.Count -gt 0) {
    throw ("Tool validation failed:`n- " + ($failures -join "`n- "))
}

Write-Output 'Tool validation passed.'
