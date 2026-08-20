# Run the supplied-manual Python API against the Windows standalone build.
[CmdletBinding()]
param(
    [string]$ExePath = "",
    [int]$Width = 1280,
    [int]$Height = 720,
    [int]$TimeoutSec = 25
)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe" }
if (-not (Test-Path -LiteralPath $ExePath)) { throw "Standalone executable not found: $ExePath" }
$py = Join-Path $Root ".venv\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $py)) { throw "Python venv not found: $py" }
$artifactDir = Join-Path $Root "test-artifacts\python"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$oldTrace = $env:JAJUCHA_STATE_TRACE
$env:JAJUCHA_STATE_TRACE = "1"
$oldDebug = $env:JCHM_SIM_DEBUG
$env:JCHM_SIM_DEBUG = "1"
$process = $null
try {
    $process = Start-Process -FilePath $ExePath -ArgumentList @(
        "-screen-fullscreen", "0", "-screen-width", "$Width", "-screen-height", "$Height"
    ) -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do { Start-Sleep -Milliseconds 200; $process.Refresh() } while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
    if ($process.MainWindowHandle -eq 0) { throw "Simulator window did not open" }
    # Do not open a disposable probe connection: the bridge deliberately
    # serializes clients, so the real Python client must be the first peer.
    Start-Sleep -Seconds 2
    & $py (Join-Path $Root "scripts\python_live_smoke.py") --output-dir $artifactDir
    if ($LASTEXITCODE -ne 0) { throw "Python live smoke failed with exit code $LASTEXITCODE" }
    Write-Host "Python live smoke passed." -ForegroundColor Green
}
finally {
    if ($process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 500
        if (-not $process.HasExited) { $process.Kill() }
    }
    if ($null -eq $oldTrace) { Remove-Item Env:JAJUCHA_STATE_TRACE -ErrorAction SilentlyContinue }
    else { $env:JAJUCHA_STATE_TRACE = $oldTrace }
    if ($null -eq $oldDebug) { Remove-Item Env:JCHM_SIM_DEBUG -ErrorAction SilentlyContinue }
    else { $env:JCHM_SIM_DEBUG = $oldDebug }
}
