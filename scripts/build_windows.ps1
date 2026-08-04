# build_windows.ps1 - Windows standalone build (Step 11.38)
#
#   1. locate a supported Unity installation
#   2. validate the project (scripts/validate_project.ps1)
#   3. run relevant tests
#   4. build the Windows standalone
#   5. copy required runtime files (default courses, config, python, docs)
#   6. create a clean distribution directory (dist/JajuchaSimulator)
#   7. fail clearly on errors
#
# Usage:
#   .\scripts\build_windows.ps1
#   .\scripts\build_windows.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe"

[CmdletBinding()]
param(
    [string]$UnityPath = "",
    [switch]$SkipValidation
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Dist = Join-Path $Root "dist\JajuchaSimulator"

function Fail([string]$msg) {
    Write-Host "[build][ERROR] $msg" -ForegroundColor Red
    exit 1
}

# 1. Locate Unity.
if (-not $UnityPath) {
    foreach ($candidate in @(
        "C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe",
        "C:\Program Files\Unity\Hub\Editor\2022.3.0f1\Editor\Unity.exe"
    )) {
        if (Test-Path $candidate) { $UnityPath = $candidate; break }
    }
    $hub = Get-ChildItem "C:\Program Files\Unity\Hub\Editor" -Directory -ErrorAction SilentlyContinue |
        Sort-Object Name -Descending | Select-Object -First 1
    if ($hub) {
        $candidate = Join-Path $hub.FullName "Editor\Unity.exe"
        if (Test-Path $candidate) { $UnityPath = $candidate }
    }
}
if (-not $UnityPath -or -not (Test-Path $UnityPath)) {
    Fail "Could not locate a Unity installation. Pass -UnityPath explicitly."
}
Write-Host "[build] Unity: $UnityPath"

# 2. Validate the project.
if (-not $SkipValidation) {
    Write-Host "[build] Validating project..."
    & (Join-Path $PSScriptRoot "validate_project.ps1") -SkipUnityTests
    if ($LASTEXITCODE -ne 0) { Fail "Project validation failed." }
} else {
    Write-Host "[build] Skipping validation (-SkipValidation)."
}

# 3. Run Unity EditMode tests.
Write-Host "[build] Running Unity EditMode tests..."
& $UnityPath -batchmode -nographics -projectPath $Root -runTests `
    -testPlatform editmode -testResults (Join-Path $Root "test-results-build.xml") `
    -logFile (Join-Path $Root "unity-build-editmode.log") | Out-Null
if (Test-Path (Join-Path $Root "test-results-build.xml")) {
    $xml = [xml](Get-Content (Join-Path $Root "test-results-build.xml"))
    if ([int]$xml.'test-run'.failed -ne 0) {
        Fail "Unity EditMode tests failed: $($xml.'test-run'.failed) failed."
    }
    Write-Host "[build] EditMode tests passed: $($xml.'test-run'.total)."
} else {
    Fail "Unity EditMode test results file missing."
}

# 4. Build the Windows standalone into a clean staging folder.
$Staging = Join-Path $Root "build_staging\JajuchaSimulator"
if (Test-Path $Staging) { Remove-Item -Recurse -Force $Staging }
New-Item -ItemType Directory -Force -Path $Staging | Out-Null

Write-Host "[build] Building Windows standalone..."
$logFile = Join-Path $Root "unity-build.log"
& $UnityPath -batchmode -nographics -quit -projectPath $Root `
    -buildWindows64Player (Join-Path $Staging "JajuchaSimulator.exe") `
    -logFile $logFile | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host "[build] Unity build log tail:" -ForegroundColor Red
    Get-Content $logFile -Tail 30
    Fail "Unity Windows build failed (exit $LASTEXITCODE)."
}
if (-not (Test-Path (Join-Path $Staging "JajuchaSimulator.exe"))) {
    Fail "Build finished but JajuchaSimulator.exe is missing."
}

# 5. Copy required runtime files into the distribution.
Write-Host "[build] Assembling distribution..."
if (Test-Path $Dist) { Remove-Item -Recurse -Force $Dist }
Copy-Item -Recurse -Force $Staging $Dist

Copy-Item -Recurse -Force (Join-Path $Root "Courses") (Join-Path $Dist "Courses")
Copy-Item -Recurse -Force (Join-Path $Root "Config") (Join-Path $Dist "Config")
Copy-Item -Recurse -Force (Join-Path $Root "scripts") (Join-Path $Dist "Scripts")

# Python workspace (examples, user, jchm, jchm_sim, requirements.txt).
$distPython = Join-Path $Dist "Python"
New-Item -ItemType Directory -Force -Path $distPython | Out-Null
Copy-Item -Recurse -Force (Join-Path $Root "python\examples") (Join-Path $distPython "examples")
Copy-Item -Recurse -Force (Join-Path $Root "python\user") (Join-Path $distPython "user")
Copy-Item -Recurse -Force (Join-Path $Root "python\jchm") (Join-Path $distPython "jchm")
Copy-Item -Recurse -Force (Join-Path $Root "python\jchm_sim") (Join-Path $distPython "jchm_sim")
Copy-Item -Force (Join-Path $Root "python\requirements.txt") (Join-Path $distPython "requirements.txt")

# Documentation.
Copy-Item -Recurse -Force (Join-Path $Root "docs") (Join-Path $Dist "Docs")
Copy-Item -Force (Join-Path $Root "README.md") (Join-Path $Dist "README.md")

# Remove the staging folder.
Remove-Item -Recurse -Force (Join-Path $Root "build_staging")

Write-Host ""
Write-Host "[build] Distribution created at: $Dist" -ForegroundColor Green
Write-Host "[build] Contents: JajuchaSimulator.exe, JajuchaSimulator_Data/, Courses/, Config/, Scripts/, Python/, Docs/, README.md" -ForegroundColor Green
Write-Host "[build] Launch with: .\scripts\run_simulator.ps1" -ForegroundColor Green
