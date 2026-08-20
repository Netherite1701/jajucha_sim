# Run a real bridge-driven official checkpoint route against the Windows build.
[CmdletBinding()]
param(
    [string]$ExePath = "",
    [string]$Course = "Courses\2026_preliminary.json",
    [int]$TimeoutSec = 130,
    [switch]$UseUiConfiguration
)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe" }
$py = Join-Path $Root ".venv\Scripts\python.exe"
if (-not (Test-Path -LiteralPath $ExePath)) { throw "Standalone executable not found: $ExePath" }
if (-not (Test-Path -LiteralPath $py)) { throw "Python venv not found: $py" }
$out = Join-Path $Root "test-artifacts\scenario\checkpoint_drive_$(Get-Date -Format yyyyMMdd_HHmmss).json"
$oldTrace = $env:JAJUCHA_STATE_TRACE; $env:JAJUCHA_STATE_TRACE = "1"
$p = $null
try {
    if ($UseUiConfiguration) {
        # Optional path: configure the mission through the same real Win32
        # clicks used by the UI smoke.
        & (Join-Path $Root "scripts\test_ui_windows.ps1") -ExePath $ExePath -TimeoutSec 35 -KeepOpen -SensorSmoke
        $p = Get-Process -Name "JajuchaSimulator" -ErrorAction SilentlyContinue | Select-Object -Last 1
        if (-not $p) { throw "Simulator process did not remain open after UI configuration" }
    } else {
        # Default route proof uses the persisted fixed/random mission selected
        # by the user; the bridge start response is asserted by the Python
        # harness. This avoids making the route result depend on trace-file ACLs.
        $p = Start-Process -FilePath $ExePath -ArgumentList @("-screen-fullscreen","0","-screen-width","1280","-screen-height","720","--state-trace") -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
        $deadline = (Get-Date).AddSeconds(25)
        do { Start-Sleep -Milliseconds 200; $p.Refresh() } while ($p.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
        if ($p.MainWindowHandle -eq 0) { throw "Simulator window did not open" }
    }
    Start-Sleep -Seconds 2
    & $py (Join-Path $Root "scripts\checkpoint_drive_live.py") --course (Join-Path $Root $Course) --output $out --overall-timeout ([Math]::Max(20, $TimeoutSec - 15))
    $exit = $LASTEXITCODE
    if (-not (Test-Path -LiteralPath $out)) { throw "checkpoint drive did not produce $out" }
    $record = Get-Content $out -Raw | ConvertFrom-Json
    if ($exit -ne 0 -or -not $record.passed) {
        throw "Checkpoint route did not complete: $($record | ConvertTo-Json -Compress -Depth 8)"
    }
    Write-Host "Checkpoint route passed. Result: $out" -ForegroundColor Green
} finally {
    if ($p -and -not $p.HasExited) { $p.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 500; if (-not $p.HasExited) { $p.Kill() } }
    if ($null -eq $oldTrace) { Remove-Item Env:JAJUCHA_STATE_TRACE -ErrorAction SilentlyContinue } else { $env:JAJUCHA_STATE_TRACE = $oldTrace }
}
