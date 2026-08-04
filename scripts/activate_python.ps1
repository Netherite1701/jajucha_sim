# activate_python.ps1 - Activate the project-local virtual environment (Step 11.21)
#
# If PowerShell execution policy blocks scripts, run this one-session command
# first (it does NOT change machine-wide policy):
#
#   Set-ExecutionPolicy -Scope Process Bypass
#
# Then:
#   .\scripts\activate_python.ps1

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Activate = Join-Path $Root ".venv\Scripts\Activate.ps1"

if (-not (Test-Path $Activate)) {
    Write-Host "[activate][ERROR] .venv not found. Run .\scripts\setup_python.ps1 first." -ForegroundColor Red
    exit 1
}

& $Activate
Write-Host "[activate] Virtual environment active."
Write-Host "[activate] Run examples with: python .\python\examples\01_motor_test.py"
