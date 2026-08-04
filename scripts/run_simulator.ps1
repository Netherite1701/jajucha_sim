# run_simulator.ps1 - Launch the standalone simulator build (Step 11.22)
#
# Locates the Windows standalone build and launches it, forwarding optional
# command-line arguments:
#
#   -Course "template_course"
#   -Mode "Drive"
#   -SimulationSpeed 1.0
#   -NoDebugUi
#   -BatchConfig "batch.json"
#
# Examples:
#   .\scripts\run_simulator.ps1
#   .\scripts\run_simulator.ps1 -Course "template_course" -Mode "Drive"
#
# If no standalone build is found, it falls back to opening the Unity Editor
# project (requires Unity + the Editor to be installed).

[CmdletBinding()]
param(
    [string]$Course,
    [string]$Mode,
    [float]$SimulationSpeed = 1.0,
    [switch]$NoDebugUi,
    [string]$BatchConfig
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
    Write-Host "[run][WARN] No standalone build found (looked in dist/ and Builds/)." -ForegroundColor Yellow
    Write-Host "[run][WARN] Build one first with .\scripts\build_windows.ps1, or open the Unity project." -ForegroundColor Yellow
    Write-Host "[run][WARN] Falling back to Unity Editor Play Mode instructions:" -ForegroundColor Yellow
    Write-Host "    Open Assets/JajuchaSim/Scenes/JajuchaSimulator.unity and press Play."
    exit 1
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
