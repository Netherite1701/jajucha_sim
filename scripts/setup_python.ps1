# setup_python.ps1 - Create the project-local Python virtual environment (Step 11.20)
#
#   1. find a supported Python installation
#   2. create .venv
#   3. upgrade pip inside .venv
#   4. install python/requirements.txt
#   5. verify imports
#   6. print the exact command for running examples
#
# Usage:
#   .\scripts\setup_python.ps1

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$VenvDir = Join-Path $Root ".venv"
$Requirements = Join-Path $Root "python\requirements.txt"

Write-Host "[setup] Jajucha Simulator Python setup"
Write-Host "[setup] Project root : $Root"

# 1. Find a supported Python installation (3.9+).
$py = $null
foreach ($candidate in @(
    (Get-Command python -ErrorAction SilentlyContinue).Source,
    (Get-Command python3 -ErrorAction SilentlyContinue).Source,
    (Join-Path $env:LOCALAPPDATA "Programs\Python\Python312\python.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Python\Python311\python.exe"),
    (Join-Path $env:LOCALAPPDATA "Programs\Python\Python310\python.exe")
)) {
    if (-not $candidate -or -not (Test-Path $candidate)) { continue }
    try {
        $ver = & $candidate -c "import sys; print('%d.%d' % sys.version_info[:2])" 2>$null
        if ($ver -match '^(3\.(9|1[0-9]))') {
            $py = $candidate
            Write-Host "[setup] Using Python $ver : $candidate"
            break
        }
    } catch { }
}
if (-not $py) {
    Write-Host "[setup][ERROR] No supported Python (3.9+) found. Install Python 3.10+ and retry." -ForegroundColor Red
    exit 1
}

# 2. Create .venv.
if (-not (Test-Path (Join-Path $VenvDir "Scripts\python.exe"))) {
    Write-Host "[setup] Creating virtual environment at .venv"
    & $py -m venv $VenvDir
    if ($LASTEXITCODE -ne 0) { Write-Host "[setup][ERROR] venv creation failed." -ForegroundColor Red; exit 1 }
} else {
    Write-Host "[setup] .venv already exists"
}

$VenvPy = Join-Path $VenvDir "Scripts\python.exe"

# 3. Upgrade pip.
Write-Host "[setup] Upgrading pip"
& $VenvPy -m pip install --upgrade pip
if ($LASTEXITCODE -ne 0) { Write-Host "[setup][ERROR] pip upgrade failed." -ForegroundColor Red; exit 1 }

# 4. Install requirements.
Write-Host "[setup] Installing python/requirements.txt"
& $VenvPy -m pip install -r $Requirements
if ($LASTEXITCODE -ne 0) { Write-Host "[setup][ERROR] requirements install failed." -ForegroundColor Red; exit 1 }

# 5. Verify imports.
# Note: use single quotes inside the Python code; PowerShell strips
# double quotes when passing arguments to native executables.
Write-Host "[setup] Verifying imports"
$verify = 'import sys; sys.path.insert(0, ''python''); import jchm, jchm_sim; print(''jchm OK, jchm_sim OK'')'
$verifyOut = (& $VenvPy -c $verify 2>&1 | Out-String).Trim()
$verifyExit = $LASTEXITCODE
if ($verifyExit -ne 0) {
    Write-Host "[setup][ERROR] Import verification failed:" -ForegroundColor Red
    Write-Host $verifyOut
    exit 1
}
Write-Host "[setup] $verifyOut"

# 6. Print the exact commands.
Write-Host ""
Write-Host "[setup] Done. To run an example:"
Write-Host "    .\.venv\Scripts\python.exe .\python\examples\01_motor_test.py"
Write-Host "    .\.venv\Scripts\python.exe .\python\user\main.py"
Write-Host ""
Write-Host "[setup] To run the tests:"
Write-Host "    .\.venv\Scripts\python.exe -m pytest python\tests\ -q"
