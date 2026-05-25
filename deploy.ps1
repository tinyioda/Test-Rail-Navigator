# Publishes TestRailNavigator in Release and deploys to the IIS site at C:\inetpub\TestRailNavigator.
# Preserves testrail-settings.json, testrailnavigator.db, and the logs\ directory.
# Usage: .\deploy.ps1
[CmdletBinding()]
param(
    [string]$ProjectDir = (Join-Path $PSScriptRoot 'TestRailNavigator'),
    [string]$DeployDir  = 'C:\inetpub\TestRailNavigator',
    [switch]$SkipPublish
)

$ErrorActionPreference = 'Stop'
$PublishDir = Join-Path $ProjectDir 'bin\publish'

if (-not $SkipPublish) {
    Write-Host "==> Publishing (Release) -> $PublishDir" -ForegroundColor Cyan
    Push-Location $ProjectDir
    try {
        dotnet publish -c Release -o $PublishDir --nologo
        if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit $LASTEXITCODE)" }
    } finally {
        Pop-Location
    }
}

if (-not (Test-Path $DeployDir)) {
    throw "Deploy directory not found: $DeployDir"
}

$offline = Join-Path $DeployDir 'app_offline.htm'
Write-Host "==> Taking site offline" -ForegroundColor Cyan
Set-Content -Path $offline -Value '<html><body><h1>Deploying...</h1></body></html>' -Encoding UTF8

try {
    Write-Host "==> Mirroring publish output to $DeployDir" -ForegroundColor Cyan
    $rc = robocopy $PublishDir $DeployDir /MIR /NFL /NDL /NJH /NJS /NP `
        /XF 'testrail-settings.json' 'testrailnavigator.db' 'app_offline.htm' `
        /XD 'logs'
    # Robocopy exit codes: 0-7 success, 8+ error.
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed (exit $LASTEXITCODE)" }
    Write-Host "    robocopy exit $LASTEXITCODE (success)"
} finally {
    Write-Host "==> Bringing site online" -ForegroundColor Cyan
    if (Test-Path $offline) { Remove-Item $offline -Force }
}

Write-Host "==> Recycling app pool" -ForegroundColor Cyan
& "$env:windir\system32\inetsrv\appcmd.exe" recycle apppool /apppool.name:"TestRailNavigator" | Out-Host
Start-Sleep -Seconds 3

Write-Host "==> Warm-up request" -ForegroundColor Cyan
try {
    $r = Invoke-WebRequest -Uri 'https://localhost/GenerateCases' -UseBasicParsing -SkipCertificateCheck -TimeoutSec 180
    Write-Host "    HTTP $($r.StatusCode) - $($r.RawContentLength) bytes" -ForegroundColor Green
} catch {
    Write-Warning "Warm-up failed: $($_.Exception.Message)"
}

Write-Host "==> Done" -ForegroundColor Green
