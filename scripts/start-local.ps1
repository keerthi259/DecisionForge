[CmdletBinding()]
param(
    [ValidateRange(1, 600)][int] $TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$appHostPath = Join-Path $repositoryRoot 'src\DecisionForge.AppHost\DecisionForge.AppHost.csproj'
$webPath = Join-Path $repositoryRoot 'src\DecisionForge.Web'
$statePath = Join-Path $repositoryRoot '.decisionforge'
$pidPath = Join-Path $statePath 'apphost.pid'
$resourcePath = Join-Path $statePath 'containers.txt'
$outputPath = Join-Path $statePath 'apphost.log'
$errorPath = Join-Path $statePath 'apphost.error.log'

& (Join-Path $PSScriptRoot 'validate-tools.ps1')
docker info *> $null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker is unavailable. Start Docker Desktop and retry.'
}

if (Test-Path -LiteralPath $pidPath) {
    $existingId = [int](Get-Content -LiteralPath $pidPath -Raw)
    if (Get-Process -Id $existingId -ErrorAction SilentlyContinue) {
        throw "DecisionForge AppHost is already running with PID $existingId."
    }
}

New-Item -ItemType Directory -Path $statePath -Force | Out-Null
$containersBefore = @(docker ps --all --quiet)

Push-Location $repositoryRoot
try {
    dotnet restore DecisionForge.sln --locked-mode
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    dotnet build DecisionForge.sln --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    Push-Location $webPath
    try {
        npm ci
        if ($LASTEXITCODE -ne 0) {
            throw "npm ci failed with exit code $LASTEXITCODE."
        }
    }
    finally {
        Pop-Location
    }

    $process = Start-Process -FilePath 'dotnet' -ArgumentList @(
        'run',
        '--project',
        "`"$appHostPath`"",
        '--configuration',
        'Release',
        '--no-build'
    ) -RedirectStandardOutput $outputPath -RedirectStandardError $errorPath -WindowStyle Hidden -PassThru
    Set-Content -LiteralPath $pidPath -Value $process.Id

    try {
        & (Join-Path $PSScriptRoot 'smoke-local.ps1') -TimeoutSeconds $TimeoutSeconds

        $containersAfter = @(docker ps --all --quiet)
        $createdContainers = @($containersAfter | Where-Object { $_ -notin $containersBefore })
        if ($createdContainers.Count -ne 2) {
            throw "Expected Aspire to create two containers, found $($createdContainers.Count)."
        }

        Set-Content -LiteralPath $resourcePath -Value $createdContainers
    }
    catch {
        $containersAfter = @(docker ps --all --quiet)
        $createdContainers = @($containersAfter | Where-Object { $_ -notin $containersBefore })
        if ($createdContainers.Count -gt 0) {
            Set-Content -LiteralPath $resourcePath -Value $createdContainers
        }

        & (Join-Path $PSScriptRoot 'stop-local.ps1')
        throw
    }

    Write-Output "DecisionForge local topology started with AppHost PID $($process.Id)."
    Write-Output "Logs: $outputPath"
}
finally {
    Pop-Location
}
