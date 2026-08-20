# validate_project.ps1 - Project validation script (Step 11.26)
#
# Verifies:
#   * required folders exist
#   * manual exists
#   * template scene exists
#   * both 2026 competition courses and source references exist
#   * Python package imports
#   * required documentation exists
#   * no obvious generated folders are tracked
#   * configuration files parse
#   * course files validate
#   * example scripts compile
#   * bridge protocol files are present
#
# Where practical it also runs the Python automated tests.
#
# Usage:
#   .\scripts\validate_project.ps1

[CmdletBinding()]
param(
    [switch]$SkipPythonTests,
    [switch]$SkipUnityTests
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$failures = @()

function Check([string]$Label, [bool]$Ok, [string]$Detail = "") {
    if ($Ok) {
        Write-Host "[ OK ] $Label" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] $Label $Detail" -ForegroundColor Red
        $script:failures += $Label
    }
}

Write-Host "=== Jajucha Simulator project validation ==="

# Required folders.
foreach ($dir in @("Assets\JajuchaSim", "python", "python\jchm", "python\jchm_sim",
                   "python\examples", "python\user", "python\tests", "docs",
                   "Courses", "Config", "scripts", "implementation_plan")) {
    Check "Folder: $dir" (Test-Path (Join-Path $Root $dir))
}

# Manual exists.
Check "Manual: docs/자주차 매뉴얼.pdf" (Test-Path (Join-Path $Root "docs\자주차 매뉴얼.pdf"))

# Template scene exists.
Check "Template scene: Assets/JajuchaSim/Scenes/JajuchaSimulator.unity" `
    (Test-Path (Join-Path $Root "Assets\JajuchaSim\Scenes\JajuchaSimulator.unity"))

# 2026 courses and extracted artwork exist.
foreach ($stage in @("preliminary", "final")) {
    Check "2026 course: $stage" (Test-Path (Join-Path $Root "Courses\2026_$stage.json"))
}
foreach ($asset in @("track_surface.png", "starting_light_sign.png", "yellow_flag.png",
                     "pit_barrier.png", "dynamic_obstacle.png")) {
    Check "2026 artwork: $asset" `
        (Test-Path (Join-Path $Root "Assets\JajuchaSim\Resources\Competition2026\$asset"))
}
foreach ($pdf in @("2026_signs_A4.pdf", "2026_signs_B4.pdf", "2026_track_build_manual.pdf",
                   "2026_track_summary.pdf")) {
    Check "2026 source PDF: $pdf" (Test-Path (Join-Path $Root "docs\reference\2026\$pdf"))
}

# Required documentation exists.
foreach ($doc in @("README.md", "docs\README.md", "docs\USER_WORKFLOW.md", "docs\ARCHITECTURE.md",
                   "docs\DESIGN_DECISIONS.md", "docs\MANUAL_COMPATIBILITY.md", "docs\CONFIGURATION.md",
                   "docs\COURSE_FORMAT.md", "docs\COMPETITION_2026.md", "docs\SCORING.md", "docs\TESTING.md",
                   "docs\TROUBLESHOOTING.md", "docs\IMPLEMENTATION_STATUS.md",
                   "docs\CHANGELOG.md", "python\user\README.md")) {
    Check "Doc: $doc" (Test-Path (Join-Path $Root $doc))
}

# No obvious generated folders tracked (Library, Temp, Build, Logs, .venv, __pycache__).
foreach ($gen in @("Library", "Temp", "Build", "Builds", "Logs", ".venv")) {
    if (Test-Path (Join-Path $Root $gen)) {
        Write-Host "[WARN] Generated folder present (not tracked by git): $gen" -ForegroundColor Yellow
    }
}

# Configuration files parse.
try {
    $cfg = Get-Content (Join-Path $Root "Config\default_simulator.json") -Raw | ConvertFrom-Json
    Check "Config parses: Config/default_simulator.json" ($null -ne $cfg)
} catch {
    Check "Config parses: Config/default_simulator.json" $false $_
}

# Course files validate (JSON parse + official invariants).
$expectedPanels = @{ A=9; B=2; C=1; D=5; F=2; G=1; H=1; I=1; J=5; K=3;
                     L=1; M=2; N=1; O=1; P=4; Q=2 }
foreach ($stage in @("preliminary", "final")) {
    try {
        $relative = "Courses\2026_$stage.json"
        $course = Get-Content (Join-Path $Root $relative) -Raw | ConvertFrom-Json
        $meta = $course.competition2026
        Check "Course parses: $relative" ($null -ne $course)
        Check "Course metadata: $stage" `
            ($course.tileSizeCm -eq 5 -and $meta.edition -eq 2026 -and
             $meta.stage -eq $stage -and $meta.physicalWidthCm -eq 990 -and
             $meta.physicalLengthCm -eq 540 -and $meta.panels.Count -eq 41 -and
             $meta.missionCandidates.Count -eq 5)
        foreach ($code in $expectedPanels.Keys) {
            $actual = @($meta.panels | Where-Object { $_.code -eq $code }).Count
            Check "Panel $code count ($stage)" ($actual -eq $expectedPanels[$code]) "got $actual"
        }
        foreach ($candidate in $meta.missionCandidates) {
            $dx = $candidate.terminalBCellX - $candidate.terminalACellX
            $dz = $candidate.terminalBCellZ - $candidate.terminalACellZ
            $distance = [Math]::Sqrt($dx*$dx + $dz*$dz) * $course.tileSizeCm
            Check "$($candidate.id) sensor distance ($stage)" ([Math]::Abs($distance - 30) -lt 0.001) "got $distance cm"
        }
    } catch {
        Check "Course validates: $stage" $false $_
    }
}

# Bridge protocol files present.
foreach ($f in @("python\jchm\_protocol.py", "python\jchm\_sim_backend.py",
                 "Assets\JajuchaSim\Bridge\Runtime\BridgeProtocol.cs",
                 "Assets\JajuchaSim\Bridge\Runtime\JajuchaBridgeServer.cs")) {
    Check "Bridge protocol file: $f" (Test-Path (Join-Path $Root $f))
}

# Python package imports + example compile.
$VenvPy = Join-Path $Root ".venv\Scripts\python.exe"
if (-not (Test-Path $VenvPy)) {
    $pythonCommand = Get-Command python -ErrorAction SilentlyContinue
    $VenvPy = if ($pythonCommand) { $pythonCommand.Source } else { "py" }
}

$importCheck = & $VenvPy -c "import sys; sys.path.insert(0, 'python'); import jchm, jchm_sim; print('OK')" 2>&1
Check "Python imports (jchm, jchm_sim)" ($LASTEXITCODE -eq 0) ($importCheck -join " ")

$compileCheck = & $VenvPy -m compileall -q python 2>&1
Check "Python examples compile (compileall)" ($LASTEXITCODE -eq 0) ($compileCheck -join " ")

# Run Python tests.
if (-not $SkipPythonTests) {
    Write-Host "=== Running Python tests ==="
    & $VenvPy -m pytest python\tests\ -q
    Check "Python pytest" ($LASTEXITCODE -eq 0)
}

# Optional: run Unity tests (EditMode) when Unity is available.
if (-not $SkipUnityTests) {
    $unity = Get-Command "C:\Program Files\Unity\Hub\Editor\6000.3.20f1\Editor\Unity.exe" -ErrorAction SilentlyContinue
    if ($unity) {
        Write-Host "=== Running Unity EditMode tests (this can take a while) ==="
        & $unity.Source -batchmode -nographics -projectPath $Root -runTests `
            -testPlatform editmode -testResults (Join-Path $Root "test-results-validate.xml") `
            -logFile (Join-Path $Root "unity-validate.log") | Out-Null
        if (Test-Path (Join-Path $Root "test-results-validate.xml")) {
            $xml = [xml](Get-Content (Join-Path $Root "test-results-validate.xml"))
            $total = [int]$xml.'test-run'.total
            $failed = [int]$xml.'test-run'.failed
            Check "Unity EditMode tests ($total total, $failed failed)" ($failed -eq 0)
        } else {
            Check "Unity EditMode tests" $false "no results file"
        }
    } else {
        Write-Host "[WARN] Unity not found; skipping Unity tests." -ForegroundColor Yellow
    }
}

Write-Host ""
if ($failures.Count -eq 0) {
    Write-Host "=== VALIDATION PASSED ===" -ForegroundColor Green
    exit 0
} else {
    Write-Host "=== VALIDATION FAILED: $($failures.Count) problem(s) ===" -ForegroundColor Red
    $failures | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    exit 1
}
