# Verify the first-run 2026 mission gate in the actual Windows executable.
[CmdletBinding()]
param(
    [string]$ExePath = "",
    [int]$Width = 1280,
    [int]$Height = 720,
    [int]$TimeoutSec = 20
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe" }
$artifactDir = Join-Path $Root "test-artifacts\scenario"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$resultPath = Join-Path $artifactDir ("mission_gate_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
$prefsPath = Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\UserConfig\competition_2026.json"
$tracePath = Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\Logs\state-trace.jsonl"
$originalPrefs = if (Test-Path -LiteralPath $prefsPath) { Get-Content -LiteralPath $prefsPath -Raw } else { $null }
$oldTraceEnv = $env:JAJUCHA_STATE_TRACE
$process = $null; $client = $null; $reader = $null; $writer = $null
$checks = [ordered]@{}

function Assert-True([bool]$condition, [string]$message) { if (-not $condition) { throw $message } }
function Send-Request([int]$id, [string]$name) {
    $writer.WriteLine((@{ type = "command"; id = $id; name = $name } | ConvertTo-Json -Compress))
    $line = $reader.ReadLine(); if (-not $line) { throw "No response for $name" }
    return ($line | ConvertFrom-Json)
}
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
function Wait-ReadyTrace {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        $state = Read-LastTrace
        if ($state -and $state.scenarioState -eq "Ready") { return $state }
        Start-Sleep -Milliseconds 100
    } while ((Get-Date) -lt $deadline)
    throw "Trace did not reach a fresh Ready state"
}

$gateSettings = [ordered]@{
    lastStage = "preliminary"; mode = 0; missionType = 0; candidateId = "";
    randomSeed = 2026; practiceSpeedLimitCmS = 20; obstacleWaitSec = 3; obstacleExitSec = 1
}
try {
    $env:JAJUCHA_STATE_TRACE = "1"
    $prefDir = Split-Path -Parent $prefsPath
    New-Item -ItemType Directory -Force -Path $prefDir | Out-Null
    $gateSettings | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $prefsPath -Encoding UTF8
    # A previous process may have left a JSONL trace behind.  Remove it before
    # launch so the gate assertion cannot accidentally read an old run.
    Remove-Item -LiteralPath $tracePath -Force -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath $ExePath -ArgumentList @(
        "-screen-fullscreen", "0", "-screen-width", "$Width", "-screen-height", "$Height"
    ) -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do { Start-Sleep -Milliseconds 200; $process.Refresh() } while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
    Assert-True ($process.MainWindowHandle -ne 0) "Simulator window did not open"
    do { try { $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 8765) } catch { Start-Sleep -Milliseconds 200 } } while ($null -eq $client -and (Get-Date) -lt $deadline)
    Assert-True ($null -ne $client) "Bridge did not listen"
    $stream = $client.GetStream(); $stream.ReadTimeout = 5000; $reader = [IO.StreamReader]::new($stream); $writer = [IO.StreamWriter]::new($stream); $writer.NewLine = "`n"; $writer.AutoFlush = $true
    $writer.WriteLine((@{ type = "hello"; id = 0; protocol = 1; client = "mission-gate-smoke" } | ConvertTo-Json -Compress))
    $hello = $reader.ReadLine() | ConvertFrom-Json
    $readyTrace = Wait-ReadyTrace
    $status = Send-Request 1 "get_run_status"
    $start = Send-Request 2 "start_run"
    $trace = Wait-ReadyTrace
    $checks.handshake = $hello.type -eq "hello_ack" -and $hello.protocol -eq 1
    $checks.readyBeforeStart = $status.ok -eq $true -and $status.payload.state -eq "Ready"
    $checks.startRejected = $start.ok -eq $false
    $checks.errorCode = $start.error.code
    $checks.stayedReady = $trace.scenarioState -eq "Ready"
    Assert-True $checks.handshake "Handshake failed"
    Assert-True $checks.readyBeforeStart "Scenario was not Ready before gate test"
    Assert-True $checks.startRejected -and $checks.errorCode -eq "SCENARIO_NOT_READY" "Unconfigured mission start was not rejected"
    Assert-True $checks.stayedReady "Scenario changed state despite rejected start"
    $result = [ordered]@{ passed = $true; timestamp = (Get-Date).ToString("o"); checks = $checks; response = $start; trace = $trace; preferences = $gateSettings }
    $result | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    Write-Host "Mission gate smoke passed. Result: $resultPath" -ForegroundColor Green
}
catch {
    $result = [ordered]@{ passed = $false; timestamp = (Get-Date).ToString("o"); error = $_.Exception.Message; checks = $checks }
    $result | ConvertTo-Json -Depth 16 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    throw
}
finally {
    if ($reader) { $reader.Dispose() }; if ($writer) { $writer.Dispose() }; if ($client) { $client.Dispose() }
    if ($process -and -not $process.HasExited) { $process.CloseMainWindow() | Out-Null; Start-Sleep -Milliseconds 500; if (-not $process.HasExited) { $process.Kill() } }
    if ($null -eq $originalPrefs) { Remove-Item -LiteralPath $prefsPath -Force -ErrorAction SilentlyContinue }
    else { Set-Content -LiteralPath $prefsPath -Value $originalPrefs -Encoding UTF8 }
    if ($null -eq $oldTraceEnv) { Remove-Item Env:JAJUCHA_STATE_TRACE -ErrorAction SilentlyContinue }
    else { $env:JAJUCHA_STATE_TRACE = $oldTraceEnv }
}
