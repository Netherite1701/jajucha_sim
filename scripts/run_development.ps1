# run_development.ps1 - Combined development launcher (Step 11.23)
#
# Convenience wrapper that:
#   1. verifies .venv exists (runs setup_python.ps1 if missing)
#   2. launches the standalone simulator (or prints instructions)
#   3. waits for bridge readiness (scripts/check_bridge.py)
#   4. launches python/user/main.py
#   5. forwards useful logs
#   6. stops safely when interrupted
#
# The simulator and the Python program remain independently runnable; this
# script is only a convenience.

[CmdletBinding()]
param(
    [string]$Course = "2026_preliminary",
    [string]$Mode = "Drive",
    [float]$SimulationSpeed = 1.0,
    [switch]$NoDebugUi
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$VenvPy = Join-Path $Root ".venv\Scripts\python.exe"

# 1. Verify .venv.
if (-not (Test-Path $VenvPy)) {
    Write-Host "[dev] .venv missing; running setup..."
    & (Join-Path $PSScriptRoot "setup_python.ps1")
}

# 2. Locate the standalone simulator executable.
$exe = $null
foreach ($candidate in @(
    (Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe"),
    (Join-Path $Root "Builds\JajuchaSimulator\JajuchaSimulator.exe"),
    (Join-Path $Root "Build\JajuchaSimulator\JajuchaSimulator.exe")
)) {
    if (Test-Path $candidate) { $exe = $candidate; break }
}

if (-not $exe) {
    Write-Host "[dev][ERROR] No standalone build found. Build one first:" -ForegroundColor Red
    Write-Host "    .\scripts\build_windows.ps1"
    Write-Host "  or run the simulator from the Unity Editor and retry."
    exit 1
}

$simArgs = @()
if ($Course) { $simArgs += "--course", $Course }
if ($Mode)   { $simArgs += "--mode", $Mode }
if ($SimulationSpeed -ne 1.0) { $simArgs += "--simulation-speed", "$SimulationSpeed" }
if ($NoDebugUi) { $simArgs += "--no-debug-ui" }

Write-Host "[dev] Launching simulator: $exe $($simArgs -join ' ')"
$simProcess = Start-Process -FilePath $exe -ArgumentList $simArgs -PassThru

# 3. Wait for bridge readiness.
Write-Host "[dev] Waiting for bridge readiness..."
$ready = $false
for ($i = 0; $i -lt 60; $i++) {
    & $VenvPy (Join-Path $PSScriptRoot "check_bridge.py") *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 2
}
if (-not $ready) {
    Write-Host "[dev][ERROR] Simulator bridge did not become ready. Is the simulator running?" -ForegroundColor Red
    if ($simProcess -and -not $simProcess.HasExited) { Stop-Process -Id $simProcess.Id -Force }
    exit 1
}
Write-Host "[dev] Bridge ready."

# 4. Launch the user program.
Write-Host "[dev] Launching python/user/main.py (Ctrl+C to stop)..."
try {
    & $VenvPy (Join-Path $Root "python\user\main.py")
} finally {
    Write-Host "[dev] Stopping safely."
    if ($simProcess -and -not $simProcess.HasExited) {
        Stop-Process -Id $simProcess.Id -Force
    }
}
