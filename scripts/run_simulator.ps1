# run_simulator.ps1 - Launch the standalone simulator build (Step 11.22)
#
# Locates the Windows standalone build and launches it, forwarding optional
# command-line arguments:
#
#   -Course "2026_preliminary"
#   -Mode "Drive"
#   -SimulationSpeed 1.0
#   -NoDebugUi
#   -BatchConfig "batch.json"
#
# Examples:
#   .\scripts\run_simulator.ps1
#   .\scripts\run_simulator.ps1 -Course "2026_preliminary" -Mode "Drive"
#
# The normal development workflow uses the standalone executable. Use
# -BuildIfMissing to build it automatically when it is not present.

[CmdletBinding()]
param(
    [string]$Course,
    [string]$Mode,
    [float]$SimulationSpeed = 1.0,
    [switch]$NoDebugUi,
    [string]$BatchConfig,
    [switch]$BuildIfMissing
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot

# Locate the standalone build: dist/JajuchaSimulator/JajuchaSimulator.exe
$exe = $null
foreach ($candidate in @(
    (Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe"),
    (Join-Path $Root "Builds\JajuchaSimulator\JajuchaSimulator.exe"),
    (Join-Path $Root "Build\JajuchaSimulator\JajuchaSimulator.exe")
)) {
    if (Test-Path $candidate) { $exe = $candidate; break }
}

if (-not $exe) {
    if ($BuildIfMissing) {
        Write-Host "[run] No standalone build found. Building it now..." -ForegroundColor Yellow
        & (Join-Path $Root "scripts\build_windows.ps1")
        if ($LASTEXITCODE -ne 0) {
            Write-Host "[run][ERROR] Standalone build failed." -ForegroundColor Red
            exit $LASTEXITCODE
        }
        $candidate = Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe"
        if (Test-Path $candidate) { $exe = $candidate }
    }

    if (-not $exe) {
        Write-Host "[run][ERROR] No standalone build found." -ForegroundColor Red
        Write-Host "[run][ERROR] Run 'Jajucha: Build Windows standalone' first, or use 'Jajucha: Open Unity Editor' for Play Mode." -ForegroundColor Red
        exit 1
    }
}

# Build the argument list.
$args = @()
if ($Course)      { $args += "--course", $Course }
if ($Mode)        { $args += "--mode", $Mode }
if ($SimulationSpeed -ne 1.0) { $args += "--simulation-speed", "$SimulationSpeed" }
if ($NoDebugUi)   { $args += "--no-debug-ui" }
if ($BatchConfig) { $args += "--batch-config", $BatchConfig }

Write-Host "[run] Launching simulator: $exe $($args -join ' ')"
& $exe @args
exit $LASTEXITCODE
