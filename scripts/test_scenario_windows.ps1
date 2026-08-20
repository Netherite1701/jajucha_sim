# End-to-end scenario smoke test. It configures a 2026 mission through real
# Windows UI input, then compares bridge responses, the vehicle pose, and the
# tick-level state trace while exercising the four-lamp countdown.
[CmdletBinding()]
param(
    [string]$ExePath = "",
    [int]$Width = 1280,
    [int]$Height = 720,
    [int]$TimeoutSec = 25,
    [switch]$SkipUiConfiguration
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe" }
if (-not (Test-Path -LiteralPath $ExePath)) { throw "Standalone executable not found: $ExePath" }

$artifactDir = Join-Path $Root "test-artifacts\scenario"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$resultPath = Join-Path $artifactDir ("scenario_smoke_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
$tracePath = Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\Logs\state-trace.jsonl"
$process = $null; $client = $null; $reader = $null; $writer = $null
$responses = [System.Collections.Generic.List[object]]::new()
$lampFirstTicks = [ordered]@{}
$checks = [ordered]@{}

function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}

function Send-Request([int]$id, [string]$name, [hashtable]$payload = @{}) {
    $message = [ordered]@{ type = "command"; id = $id; name = $name }
    if ($payload.Count -gt 0) { $message.payload = $payload }
    $writer.WriteLine(($message | ConvertTo-Json -Compress -Depth 8))
    $line = $reader.ReadLine()
    if (-not $line) { throw "No bridge response for $name" }
    $response = $line | ConvertFrom-Json
    $responses.Add([pscustomobject]@{ request = $name; id = $id; response = $response })
    return $response
}

function Read-LastTrace {
    if (-not (Test-Path -LiteralPath $tracePath)) { return $null }
    $fs = $null; $sr = $null; $last = $null
    try {
        $fs = [IO.FileStream]::new($tracePath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $sr = [IO.StreamReader]::new($fs)
        while (-not $sr.EndOfStream) { $last = $sr.ReadLine() }
    } catch { return $null }
    finally {
        if ($sr) { $sr.Dispose() } elseif ($fs) { $fs.Dispose() }
    }
    if (-not $last) { return $null }
    try { return ($last | ConvertFrom-Json) } catch { return $null }
}

function Wait-Trace([scriptblock]$predicate, [string]$description, [int]$seconds = $TimeoutSec) {
    $deadline = (Get-Date).AddSeconds($seconds)
    do {
        $state = Read-LastTrace
        if ($state -and (& $predicate $state)) { return $state }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)
    throw "Timed out waiting for $description"
}

function Distance-Cm($a, $b) {
    $dx = [double]$b.x - [double]$a.x
    $dz = [double]$b.z - [double]$a.z
    return [Math]::Sqrt($dx * $dx + $dz * $dz)
}

function Track-Lamps {
    $state = Read-LastTrace
    if (-not $state -or $state.scenarioState -ne "Countdown") { return $state }
    $count = [int]$state.signal.litLampCount
    $key = "$count"
    if ($count -ge 1 -and -not $lampFirstTicks.Contains($key)) { $lampFirstTicks[$key] = [int64]$state.tick }
    return $state
}

function Read-AllTraceRecords {
    $records = [System.Collections.Generic.List[object]]::new()
    if (-not (Test-Path -LiteralPath $tracePath)) { return $records }
    $fs = $null; $sr = $null
    try {
        $fs = [IO.FileStream]::new($tracePath, [IO.FileMode]::Open, [IO.FileAccess]::Read, [IO.FileShare]::ReadWrite)
        $sr = [IO.StreamReader]::new($fs)
        while (-not $sr.EndOfStream) {
            $line = $sr.ReadLine()
            if ($line) {
                try { $records.Add(($line | ConvertFrom-Json)) } catch { }
            }
        }
    } finally {
        if ($sr) { $sr.Dispose() } elseif ($fs) { $fs.Dispose() }
    }
    return $records
}

$oldTraceEnv = $env:JAJUCHA_STATE_TRACE
$env:JAJUCHA_STATE_TRACE = "1"
try {
    if (-not $SkipUiConfiguration) {
        # This uses the same physical mouse path as the regular UI smoke test;
        # it leaves a configured mission and a live process for this script.
        & (Join-Path $Root "scripts\test_ui_windows.ps1") -ExePath $ExePath -Width $Width -Height $Height -TimeoutSec $TimeoutSec -KeepOpen -SensorSmoke
    } else {
        $process = Start-Process -FilePath $ExePath -ArgumentList @(
            "-screen-fullscreen", "0", "-screen-width", "$Width", "-screen-height", "$Height"
        ) -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
    }

    if (-not $process) {
        $process = Get-Process -Name "JajuchaSimulator" -ErrorAction SilentlyContinue | Select-Object -Last 1
    }
    Assert-True ($null -ne $process) "Simulator process was not found"
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        $process.Refresh()
        if ($process.MainWindowHandle -ne 0) { break }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    Assert-True ($process.MainWindowHandle -ne 0) "Simulator window did not open"
    Wait-Trace { param($s) $s.ready -and $s.scenarioState -eq "Ready" } "scenario Ready" | Out-Null

    do {
        try { $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 8765) } catch { Start-Sleep -Milliseconds 200 }
    } while ($null -eq $client -and (Get-Date) -lt $deadline)
    Assert-True ($null -ne $client) "Bridge did not listen on 127.0.0.1:8765"
    $stream = $client.GetStream(); $stream.ReadTimeout = 5000; $stream.WriteTimeout = 5000
    $reader = [IO.StreamReader]::new($stream); $writer = [IO.StreamWriter]::new($stream); $writer.NewLine = "`n"; $writer.AutoFlush = $true
    $writer.WriteLine((@{ type = "hello"; id = 0; protocol = 1; client = "scenario-smoke" } | ConvertTo-Json -Compress))
    $hello = $reader.ReadLine() | ConvertFrom-Json
    Assert-True ($hello.type -eq "hello_ack" -and $hello.protocol -eq 1) "Handshake failed"
    $checks.handshake = $true

    $status0 = Send-Request 1 "get_run_status"
    Assert-True ($status0.ok -eq $true -and $status0.payload.state -eq "Ready") "Scenario was not Ready before start"
    $checks.readyBeforeStart = $true
    $start = Send-Request 2 "start_run"
    Assert-True ($start.ok -eq $true) "Configured mission could not start"
    $checks.bridgeStartAccepted = $true
    $countdown = Wait-Trace { param($s) $s.scenarioState -eq "Countdown" -and [int]$s.signal.litLampCount -eq 1 } "Lamp1 countdown"
    $startPose = $countdown.vehicle.positionCm
    $checks.countdownStarted = $true

    # A real pre-release motor input must be recorded as false start while
    # the dispatcher clamps the command to zero and leaves the pose unchanged.
    $motorBefore = Send-Request 3 "set_motor" @{ left = 8; right = 8; speed = 20 }
    Start-Sleep -Milliseconds 500
    $blocked = Track-Lamps
    $checks.preReleaseCommandAck = $motorBefore.ok -eq $true
    $checks.preReleasePoseUnchanged = (Distance-Cm $startPose $blocked.vehicle.positionCm) -lt 0.05
    $checks.falseStartRecorded = [bool]$blocked.session.falseStart
    $checks.preReleaseCommandZero = ([int]$blocked.vehicle.command.speed -eq 0)
    Assert-True $checks.preReleasePoseUnchanged "Vehicle moved before release"
    Assert-True $checks.falseStartRecorded "Pre-release input was not recorded as false start"
    Assert-True $checks.preReleaseCommandZero "Pre-release propulsion was not clamped to zero"

    $last = $blocked
    $releaseState = $null
    $pollDeadline = (Get-Date).AddSeconds(14)
    while ((Get-Date) -lt $pollDeadline) {
        $last = Track-Lamps
        if ($last -and $last.signal.released) { $releaseState = $last; break }
        Start-Sleep -Milliseconds 100
    }
    Assert-True ($null -ne $releaseState) "Start signal did not release within 14 seconds"
    # Sampling the last line can skip ticks, so rebuild the lamp timestamps
    # from every trace record belonging to this exact run.
    $runId = $countdown.session.runId
    $lampFirstTicks = [ordered]@{}
    foreach ($record in (Read-AllTraceRecords)) {
        if ($record.recordType -ne "tick" -or $record.session.runId -ne $runId -or $record.scenarioState -ne "Countdown") { continue }
        $count = [int]$record.signal.litLampCount
        $key = "$count"
        if ($count -ge 1 -and -not $lampFirstTicks.Contains($key)) { $lampFirstTicks[$key] = [int64]$record.tick }
    }
    $checks.lampSequence = (($lampFirstTicks.Keys | ForEach-Object { [int]$_ }) -join ",") -eq "1,2,3,4"
    $t1 = [double]([int64]$lampFirstTicks['1'])
    $t2 = [double]([int64]$lampFirstTicks['2'])
    $t3 = [double]([int64]$lampFirstTicks['3'])
    $t4 = [double]([int64]$lampFirstTicks['4'])
    $checks.lampIntervalsSec = @(
        (($t2 - $t1) * 0.01),
        (($t3 - $t2) * 0.01),
        (($t4 - $t3) * 0.01)
    )
    $checks.lampIntervalsWithinTolerance = @($checks.lampIntervalsSec | ForEach-Object { [Math]::Abs([double]$_ - 1.5) -le 0.06 }) -notcontains $false
    $checks.releaseDelaySec = [double]$releaseState.session.releaseDelaySec
    $checks.releaseDelayWithinRange = $checks.releaseDelaySec -ge 3.0 -and $checks.releaseDelaySec -le 6.0
    Assert-True $checks.lampSequence "Lamp sequence did not reach 1,2,3,4"
    Assert-True $checks.lampIntervalsWithinTolerance "Lamp intervals were not 1.5 seconds"
    Assert-True $checks.releaseDelayWithinRange "Release delay was not in the official 3-6 second range"

    $motorAfter = Send-Request 4 "set_motor" @{ left = 8; right = 8; speed = 8 }
    $maxDisplacement = 0.0
    $moveDeadline = (Get-Date).AddSeconds(2.0)
    $afterMove = $releaseState
    while ((Get-Date) -lt $moveDeadline) {
        $afterMove = Track-Lamps
        if ($afterMove) {
            $d = Distance-Cm $releaseState.vehicle.positionCm $afterMove.vehicle.positionCm
            if ($d -gt $maxDisplacement) { $maxDisplacement = $d }
        }
        Start-Sleep -Milliseconds 100
    }
    $checks.postReleaseCommandAck = $motorAfter.ok -eq $true
    $checks.postReleaseMaxDisplacementCm = $maxDisplacement
    $checks.postReleasePoseChanged = $maxDisplacement -gt 0.01
    Send-Request 5 "set_motor" @{ left = 0; right = 0; speed = 0 } | Out-Null
    Assert-True $checks.postReleasePoseChanged "Vehicle did not respond to motor input after release"

    Send-Request 6 "abort_run" | Out-Null
    $aborted = Wait-Trace { param($s) $s.scenarioState -eq "Aborted" } "aborted result"
    $resultResponse = Send-Request 7 "get_result"
    Assert-True ($resultResponse.ok -eq $true) "get_result did not return an aborted run result"
    $resultObject = $resultResponse.payload.result | ConvertFrom-Json
    $checks.resultMissionRecorded = -not [string]::IsNullOrEmpty($resultObject.additionalMission)
    $checks.resultCandidateRecorded = $resultObject.missionCandidateId -match '^candidate_[1-5]$'
    $checks.resultSeedRecorded = [int64]$resultObject.missionRandomSeed -gt 0
    $checks.resultDelayMatchesTrace = [Math]::Abs([double]$resultObject.startReleaseDelaySec - $checks.releaseDelaySec) -le 0.01
    $checks.resultFalseStartRecorded = [bool]$resultObject.falseStart
    $checks.finalStateAborted = $aborted.scenarioState -eq "Aborted"
    Assert-True $checks.resultMissionRecorded "Result mission was empty"
    Assert-True $checks.resultCandidateRecorded "Result candidate was invalid"
    Assert-True $checks.resultDelayMatchesTrace "Result release delay disagreed with trace"
    Assert-True $checks.resultFalseStartRecorded "Result false-start flag was missing"

    $resultAgain = Send-Request 8 "get_result"
    $checks.finalResultStable = $resultAgain.payload.result -eq $resultResponse.payload.result
    Assert-True $checks.finalResultStable "Final result changed after run completion"

    $result = [ordered]@{
        passed = $true
        timestamp = (Get-Date).ToString("o")
        exe = $ExePath
        checks = $checks
        lampFirstTicks = $lampFirstTicks
        responses = $responses
        countdownSnapshot = $countdown
        releaseSnapshot = $releaseState
        abortedSnapshot = $aborted
        result = $resultObject
        trace = $tracePath
    }
    $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    Write-Host "Scenario smoke passed. Result: $resultPath" -ForegroundColor Green
}
catch {
    $result = [ordered]@{
        passed = $false
        timestamp = (Get-Date).ToString("o")
        exe = $ExePath
        error = $_.Exception.Message
        checks = $checks
        lampFirstTicks = $lampFirstTicks
        responses = $responses
        trace = $tracePath
    }
    $result | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    throw
}
finally {
    if ($reader) { $reader.Dispose() }
    if ($writer) { $writer.Dispose() }
    if ($client) { $client.Dispose() }
    if ($process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 500
        if (-not $process.HasExited) { $process.Kill() }
    }
    if ($null -eq $oldTraceEnv) { Remove-Item Env:JAJUCHA_STATE_TRACE -ErrorAction SilentlyContinue }
    else { $env:JAJUCHA_STATE_TRACE = $oldTraceEnv }
}
