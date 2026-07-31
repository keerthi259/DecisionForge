[CmdletBinding()]
param(
    [string] $ApiBaseUrl = 'http://localhost:5066',
    [string] $WebBaseUrl = 'http://localhost:5173',
    [string] $MailpitBaseUrl = 'http://localhost:8025',
    [ValidateRange(1, 600)][int] $TimeoutSeconds = 180
)

$ErrorActionPreference = 'Stop'
$deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)

function Invoke-HealthRequest {
    param(
        [Parameter(Mandatory)][string] $Uri,
        [hashtable] $Headers = @{}
    )

    do {
        try {
            return Invoke-WebRequest -Uri $Uri -Headers $Headers -UseBasicParsing -TimeoutSec 10
        }
        catch {
            if ([DateTimeOffset]::UtcNow -ge $deadline) {
                throw "Timed out waiting for '$Uri'. Last failure: $($_.Exception.Message)"
            }

            Start-Sleep -Seconds 2
        }
    } while ($true)
}

$web = Invoke-HealthRequest -Uri $WebBaseUrl
if ($web.StatusCode -ne 200 -or $web.Content -notmatch 'DecisionForge') {
    throw 'The Vite frontend did not return the DecisionForge application.'
}

$live = Invoke-HealthRequest -Uri "$ApiBaseUrl/health/live"
if ($live.StatusCode -ne 200 -or $live.Content -ne 'Healthy') {
    throw 'The API liveness endpoint is not healthy.'
}

$ready = Invoke-HealthRequest -Uri "$ApiBaseUrl/health/ready"
if ($ready.StatusCode -ne 200 -or $ready.Content -ne 'Healthy') {
    throw 'The API readiness endpoint is not healthy.'
}

$version = Invoke-HealthRequest -Uri "$ApiBaseUrl/version"
$versionBody = $version.Content | ConvertFrom-Json
if ($versionBody.application -ne 'DecisionForge.Api' -or [string]::IsNullOrWhiteSpace($versionBody.version)) {
    throw 'The version endpoint returned an invalid contract.'
}

$correlationId = 'phase-3-smoke'
$proxied = Invoke-HealthRequest -Uri "$WebBaseUrl/health/live" -Headers @{
    'X-Correlation-ID' = $correlationId
}
if ($proxied.StatusCode -ne 200 -or $proxied.Headers['X-Correlation-ID'] -ne $correlationId) {
    throw 'The Vite same-origin proxy or correlation response header check failed.'
}

$mailpit = Invoke-HealthRequest -Uri "$MailpitBaseUrl/api/v1/info"
if ($mailpit.StatusCode -ne 200) {
    throw 'Mailpit is not healthy.'
}

Write-Output "frontend: PASS ($WebBaseUrl)"
Write-Output "liveness: PASS ($ApiBaseUrl/health/live)"
Write-Output "readiness: PASS ($ApiBaseUrl/health/ready)"
Write-Output "version: PASS ($($versionBody.version))"
Write-Output 'same-origin proxy and correlation: PASS'
Write-Output "mailpit: PASS ($MailpitBaseUrl)"
