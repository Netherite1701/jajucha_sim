# Standalone bridge/state smoke test.  It drives the fresh Windows build via
# the same newline-delimited protocol used by the Python client and records
# the observed responses for comparison with the runtime state trace.
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
if (-not (Test-Path -LiteralPath $ExePath)) { throw "Standalone executable not found: $ExePath" }
$artifactDir = Join-Path $Root "test-artifacts\bridge"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$resultPath = Join-Path $artifactDir ("bridge_smoke_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))

$oldTraceEnv = $env:JAJUCHA_STATE_TRACE
$env:JAJUCHA_STATE_TRACE = "1"
$process = Start-Process -FilePath $ExePath -ArgumentList @(
    "-screen-fullscreen", "0", "-screen-width", "$Width", "-screen-height", "$Height"
) -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
$client = $null; $stream = $null; $reader = $null; $writer = $null
$responses = [System.Collections.Generic.List[object]]::new()
$checks = [ordered]@{}
function Send-Request([int]$id, [string]$name, [hashtable]$payload = @{}) {
    $message = [ordered]@{ type = "command"; id = $id; name = $name }
    if ($payload.Count -gt 0) { $message.payload = $payload }
    $writer.WriteLine(($message | ConvertTo-Json -Compress -Depth 6))
    $writer.Flush()
    $line = $reader.ReadLine()
    if (-not $line) { throw "No bridge response for $name" }
    $response = $line | ConvertFrom-Json
    $responses.Add([pscustomobject]@{ request = $name; id = $id; response = $response })
    if ($response.ok -ne $true) { throw "Bridge command failed: $name ($line)" }
    return $response
}
function Assert-True([bool]$condition, [string]$message) {
    if (-not $condition) { throw $message }
}
try {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do { Start-Sleep -Milliseconds 200; $process.Refresh() } while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
    if ($process.MainWindowHandle -eq 0) { throw "Simulator window did not open" }
    do {
        try {
            $client = [Net.Sockets.TcpClient]::new("127.0.0.1", 8765)
        } catch {
            Start-Sleep -Milliseconds 250
        }
    } while ($null -eq $client -and (Get-Date) -lt $deadline)
    if ($null -eq $client) { throw "Bridge did not listen on 127.0.0.1:8765" }
    $stream = $client.GetStream()
    $stream.ReadTimeout = 5000
    $stream.WriteTimeout = 5000
    $reader = [IO.StreamReader]::new($stream)
    $writer = [IO.StreamWriter]::new($stream)
    $writer.NewLine = "`n"
    $writer.AutoFlush = $true
    $writer.WriteLine((@{ type = "hello"; id = 0; protocol = 1; client = "state-smoke" } | ConvertTo-Json -Compress))
    $hello = $reader.ReadLine() | ConvertFrom-Json
    Assert-True ($hello.type -eq "hello_ack" -and $hello.protocol -eq 1) "Handshake failed"
    $status0 = Send-Request 1 "get_status"
    $checks.handshake = $true
    $checks.initialState = $status0.payload.state
    $checks.initialPosition = $status0.payload.vehicle.position_cm

    $motorAck = Send-Request 2 "set_motor" @{ left = 8; right = 8; speed = 8 }
    Start-Sleep -Milliseconds 450
    $statusMoving = Send-Request 3 "get_status"
    $p0 = $status0.payload.vehicle.position_cm
    $p1 = $statusMoving.payload.vehicle.position_cm
    $distance = [Math]::Sqrt(([double]$p1.x-[double]$p0.x)*([double]$p1.x-[double]$p0.x) + ([double]$p1.z-[double]$p0.z)*([double]$p1.z-[double]$p0.z))
    $checks.motorAccepted = $motorAck.ok -eq $true
    $checks.positionChangedAfterMotor = $distance -gt 0.01
    Assert-True $checks.positionChangedAfterMotor ("Vehicle pose did not change after motor command; distance=$distance")

    $pause = Send-Request 4 "sim_pause"
    $paused0 = Send-Request 5 "get_status"
    Start-Sleep -Milliseconds 350
    $paused1 = Send-Request 6 "get_status"
    $checks.pauseAck = $pause.ok -eq $true
    $checks.pauseFreezesTick = $paused0.payload.tick -eq $paused1.payload.tick
    $checks.pauseFreezesPosition = ([Math]::Abs([double]$paused0.payload.vehicle.position_cm.x-[double]$paused1.payload.vehicle.position_cm.x) -lt 0.001) -and
        ([Math]::Abs([double]$paused0.payload.vehicle.position_cm.z-[double]$paused1.payload.vehicle.position_cm.z) -lt 0.001)
    Assert-True $checks.pauseFreezesTick "Paused tick advanced"
    Assert-True $checks.pauseFreezesPosition "Paused vehicle position changed"

    $step = Send-Request 7 "sim_step"
    $stepped = Send-Request 8 "get_status"
    $checks.stepAck = $step.ok -eq $true
    $checks.stepExactlyOneTick = $stepped.payload.tick -eq ($paused1.payload.tick + 1)
    Assert-True $checks.stepExactlyOneTick "sim_step did not advance exactly one tick"

    $reset = Send-Request 9 "sim_reset"
    $resetStatus = Send-Request 10 "get_status"
    $checks.resetAck = $reset.ok -eq $true
    $checks.resetTickZero = $resetStatus.payload.tick -eq 0
    $checks.resetSpeedZero = $resetStatus.payload.vehicle.command.speed -eq 0
    $checks.resetVelocityZero = [Math]::Abs([double]$resetStatus.payload.vehicle.velocity_cm_s.x) -lt 0.001 -and
        [Math]::Abs([double]$resetStatus.payload.vehicle.velocity_cm_s.z) -lt 0.001
    Assert-True $checks.resetTickZero "sim_reset tick was not zero"
    Assert-True $checks.resetSpeedZero "sim_reset left motor speed active"
    Assert-True $checks.resetVelocityZero "sim_reset left vehicle velocity active"

    $result = [ordered]@{
        passed = $true
        timestamp = (Get-Date).ToString("o")
        exe = $ExePath
        checks = $checks
        responses = $responses
    }
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    Write-Host "Bridge smoke passed. Result: $resultPath" -ForegroundColor Green
}
catch {
    $result = [ordered]@{
        passed = $false
        timestamp = (Get-Date).ToString("o")
        error = $_.Exception.Message
        checks = $checks
        responses = $responses
    }
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resultPath -Encoding UTF8
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
