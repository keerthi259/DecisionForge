[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$pidPath = Join-Path $repositoryRoot '.decisionforge\apphost.pid'
$resourcePath = Join-Path $repositoryRoot '.decisionforge\containers.txt'

if (-not (Test-Path -LiteralPath $pidPath) -and -not (Test-Path -LiteralPath $resourcePath)) {
    Write-Output 'DecisionForge AppHost is not recorded as running.'
    return
}

if (Test-Path -LiteralPath $pidPath) {
    $appHostId = [int](Get-Content -LiteralPath $pidPath -Raw)
    $process = Get-CimInstance Win32_Process -Filter "ProcessId = $appHostId" -ErrorAction SilentlyContinue
    if ($null -ne $process) {
        if ($process.CommandLine -notmatch 'DecisionForge\.AppHost') {
            throw "PID $appHostId does not belong to DecisionForge AppHost; refusing to stop it."
        }

        Stop-Process -Id $appHostId
        Wait-Process -Id $appHostId -Timeout 30 -ErrorAction SilentlyContinue
    }

    Remove-Item -LiteralPath $pidPath -Force
}

if (Test-Path -LiteralPath $resourcePath) {
    $allowedImages = @(
        'axllent/mailpit:v1.30.5',
        'docker.io/library/postgres:18.4'
    )
    foreach ($containerId in Get-Content -LiteralPath $resourcePath) {
        $inspection = docker inspect $containerId | ConvertFrom-Json
        if ($LASTEXITCODE -ne 0) {
            throw "Could not verify Aspire container '$containerId'."
        }

        $container = $inspection[0]
        $image = $container.Config.Image
        $persistent = $container.Config.Labels.'com.microsoft.developer.usvc-dev.persistent'
        if ($image -notin $allowedImages -or $persistent -ne 'false') {
            throw "Container '$containerId' is outside the recorded DecisionForge topology; refusing to remove it."
        }

        docker rm --force $containerId | Out-Null
        if ($LASTEXITCODE -ne 0) {
            throw "Could not remove Aspire container '$containerId'."
        }
    }

    Remove-Item -LiteralPath $resourcePath -Force
}

Write-Output 'DecisionForge AppHost stopped.'
