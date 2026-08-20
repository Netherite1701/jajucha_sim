# Runtime contract smoke for both official 2026 stages.
#
# The harness changes only the persisted stage selector, launches the real
# Windows executable, and compares the post-physics trace with bridge status.
# The original user settings are restored in finally.
[CmdletBinding()]
param(
    [string]$ExePath = "",
    [int]$Width = 1280,
    [int]$Height = 720,
    [int]$TimeoutSec = 25,
    [string[]]$Stages = @("preliminary", "final")
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe" }
if (-not (Test-Path -LiteralPath $ExePath)) { throw "Standalone executable not found: $ExePath" }

$prefPath = Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\UserConfig\competition_2026.json"
$artifactDir = Join-Path $Root "test-artifacts\stages"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$resultPath = Join-Path $artifactDir ("stage_runtime_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
$tracePath = Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\Logs\state-trace.jsonl"
$originalPrefs = if (Test-Path -LiteralPath $prefPath) { Get-Content -Raw -LiteralPath $prefPath | ConvertFrom-Json } else { [pscustomobject]@{} }
$results = [System.Collections.Generic.List[object]]::new()

function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Read-LastTrace {
    if (-not (Test-Path -LiteralPath $tracePath)) { return $null }
    $fs = $null; $sr = $null; $last = $null
    try {
        $fs = [IO.FileStream]::new($tracePath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $sr = [IO.StreamReader]::new($fs)
        while (-not $sr.EndOfStream) { $last = $sr.ReadLine() }
    } catch { return $null }
    finally { if ($sr) { $sr.Dispose() } elseif ($fs) { $fs.Dispose() } }
    if (-not $last) { return $null }
    try { return ($last | ConvertFrom-Json) } catch { return $null }
}
function Read-TraceRecords {
    $records = [System.Collections.Generic.List[object]]::new()
    if (-not (Test-Path -LiteralPath $tracePath)) { return $records }
    $fs = $null; $sr = $null
    try {
        $fs = [IO.FileStream]::new($tracePath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $sr = [IO.StreamReader]::new($fs)
        while (-not $sr.EndOfStream) {
            $line = $sr.ReadLine()
            if ($line) { try { $records.Add(($line | ConvertFrom-Json)) } catch {} }
        }
    } finally { if ($sr) { $sr.Dispose() } elseif ($fs) { $fs.Dispose() } }
    return $records
}
function Wait-Trace([scriptblock]$predicate, [string]$description) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        $state = Read-LastTrace
        if ($state -and (& $predicate $state)) { return $state }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    throw "Timed out waiting for $description"
}
function Send-Request($writer, $reader, [int]$id, [string]$name, [hashtable]$payload = @{}) {
    $message = [ordered]@{ type = "command"; id = $id; name = $name }
    if ($payload.Count -gt 0) { $message.payload = $payload }
    $writer.WriteLine(($message | ConvertTo-Json -Compress -Depth 8)); $writer.Flush()
    $line = $reader.ReadLine(); if (-not $line) { throw "No bridge response for $name" }
    return ($line | ConvertFrom-Json)
}

$oldTraceEnv = $env:JAJUCHA_STATE_TRACE
$env:JAJUCHA_STATE_TRACE = "1"
try {
    foreach ($stage in $Stages) {
        if ($stage -notin @("preliminary", "final")) { throw "Unsupported stage: $stage" }
        $prefs = if (Test-Path -LiteralPath $prefPath) { Get-Content -Raw -LiteralPath $prefPath | ConvertFrom-Json } else { [pscustomobject]@{} }
        $prefs.lastStage = $stage
        if (-not $prefs.mode -or [int]$prefs.mode -eq 0) { $prefs.mode = 1 }
        if (-not $prefs.missionType -or [int]$prefs.missionType -eq 0) { $prefs.missionType = 1 }
        if ([string]::IsNullOrEmpty([string]$prefs.candidateId)) { $prefs.candidateId = "candidate_1" }
        $prefs | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $prefPath -Encoding UTF8
        Remove-Item -LiteralPath $tracePath -Force -ErrorAction SilentlyContinue

        $process = Start-Process -FilePath $ExePath -ArgumentList @("-screen-fullscreen", "0", "-screen-width", "$Width", "-screen-height", "$Height") -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
        $client = $null; $reader = $null; $writer = $null
        try {
            $deadline = (Get-Date).AddSeconds($TimeoutSec)
            do { Start-Sleep -Milliseconds 200; $process.Refresh() } while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
            Assert-True ($process.MainWindowHandle -ne 0) "Simulator window did not open for $stage"
            $ready = Wait-Trace { param($s) $s.ready -and $s.scenarioState -eq "Ready" -and $s.course.stage -eq $stage } "$stage Ready trace"

            do { try { $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 8765) } catch { Start-Sleep -Milliseconds 200 } } while ($null -eq $client -and (Get-Date) -lt $deadline)
            Assert-True ($null -ne $client) "Bridge did not listen for $stage"
            $stream = $client.GetStream(); $stream.ReadTimeout = 5000; $stream.WriteTimeout = 5000
            $reader = [IO.StreamReader]::new($stream); $writer = [IO.StreamWriter]::new($stream); $writer.NewLine = "`n"; $writer.AutoFlush = $true
            $writer.WriteLine((@{ type = "hello"; id = 0; protocol = 1; client = "stage-runtime" } | ConvertTo-Json -Compress))
            $hello = $reader.ReadLine() | ConvertFrom-Json
            Assert-True ($hello.type -eq "hello_ack" -and $hello.protocol -eq 1) "Handshake failed for $stage"
            $status = Send-Request $writer $reader 1 "get_status"
            $records = Read-TraceRecords
            $tickRecords = @($records | Where-Object { $_.recordType -eq "tick" -and $_.course.stage -eq $stage })
            $last = $tickRecords | Select-Object -Last 1
            Assert-True ($last.course.origin -eq "OfficialReadOnly" -and $last.course.readOnly) "$stage not official read-only"
            Assert-True ([int]$last.course.structures -eq 2) "$stage structure count is not 2"
            Assert-True ([int]$last.course.roadTiles -gt 0 -and [int]$last.course.lineTiles -gt 0) "$stage road/line mask is empty"
            Assert-True ([int]$last.sensors.lidarRayCount -eq 360) "$stage lidar ray count is not 360"
            Assert-True ([int]$last.sensors.centerWidth -gt 0 -and [int]$last.sensors.centerHeight -gt 0) "$stage center camera frame missing"
            $start = Send-Request $writer $reader 2 "start_run"
            Assert-True ($start.ok -eq $true) "$stage configured mission did not start"
            $countdown = Wait-Trace { param($s) $s.course.stage -eq $stage -and $s.scenarioState -eq "Countdown" } "$stage countdown"
            Send-Request $writer $reader 3 "abort_run" | Out-Null
            $aborted = Wait-Trace { param($s) $s.course.stage -eq $stage -and $s.scenarioState -eq "Aborted" } "$stage aborted"
            $results.Add([ordered]@{
                stage = $stage; passed = $true; handshake = $true; officialReadOnly = $true
                roadTiles = [int]$last.course.roadTiles; lineTiles = [int]$last.course.lineTiles
                structures = [int]$last.course.structures; objects = [int]$last.course.objects; triggers = [int]$last.course.triggers
                lidarRayCount = [int]$last.sensors.lidarRayCount
                camera = ("{0}x{1}" -f $last.sensors.centerWidth, $last.sensors.centerHeight)
                startAccepted = [bool]$start.ok; countdown = $countdown.scenarioState; aborted = $aborted.scenarioState
            })
        } finally {
            if ($reader) { $reader.Dispose() }; if ($writer) { $writer.Dispose() }; if ($client) { $client.Dispose() }
            if ($process -and -not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 500; if (-not $process.HasExited) { $process.Kill() } }
        }
    }
    $output = [ordered]@{ passed = $true; timestamp = (Get-Date).ToString("o"); exe = $ExePath; stages = $results }
    $output | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    Write-Host "Stage runtime smoke passed. Result: $resultPath" -ForegroundColor Green
} catch {
    $output = [ordered]@{ passed = $false; timestamp = (Get-Date).ToString("o"); error = $_.Exception.Message; stages = $results }
    $output | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    throw
} finally {
    if ($null -ne $originalPrefs) { $originalPrefs | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $prefPath -Encoding UTF8 }
    if ($null -eq $oldTraceEnv) { Remove-Item Env:JAJUCHA_STATE_TRACE -ErrorAction SilentlyContinue } else { $env:JAJUCHA_STATE_TRACE = $oldTraceEnv }
}
