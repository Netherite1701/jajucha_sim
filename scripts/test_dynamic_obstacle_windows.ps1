# Standalone dynamic-obstacle mission contract test.
[CmdletBinding()]
param(
    [string]$ExePath = "",
    [int]$Width = 1280,
    [int]$Height = 720,
    [int]$TimeoutSec = 25
)
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe" }
$prefPath = Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\UserConfig\competition_2026.json"
$tracePath = Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\Logs\state-trace.jsonl"
$artifactDir = Join-Path $Root "test-artifacts\scenario"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$resultPath = Join-Path $artifactDir ("dynamic_obstacle_{0}.json" -f (Get-Date -Format "yyyyMMdd_HHmmss"))
$original = if (Test-Path $prefPath) { Get-Content -Raw $prefPath | ConvertFrom-Json } else { [pscustomobject]@{} }
$process = $null; $client = $null; $reader = $null; $writer = $null
function Assert-True([bool]$ok, [string]$message) { if (-not $ok) { throw $message } }
function Last-Trace {
    if (-not (Test-Path $tracePath)) { return $null }
    $fs=[IO.FileStream]::new($tracePath,'Open','Read','ReadWrite'); $sr=[IO.StreamReader]::new($fs); $last=$null
    while(-not $sr.EndOfStream){$last=$sr.ReadLine()}; $sr.Dispose();$fs.Dispose()
    if($last){try{return $last|ConvertFrom-Json}catch{}}; return $null
}
function Wait-Trace([scriptblock]$predicate, [string]$description) {
    $end=(Get-Date).AddSeconds($TimeoutSec); do {$s=Last-Trace;if($s -and (&$predicate $s)){return $s};Start-Sleep -Milliseconds 150}while((Get-Date)-lt $end)
    throw "Timed out waiting for $description"
}
function Request($w,$r,[int]$id,[string]$name,[hashtable]$payload=@{}) {
    $m=[ordered]@{type='command';id=$id;name=$name};if($payload.Count -gt 0){$m.payload=$payload};$w.WriteLine(($m|ConvertTo-Json -Compress));$w.Flush();$line=$r.ReadLine();if(!$line){throw "No response for $name"};return $line|ConvertFrom-Json
}
$oldTrace=$env:JAJUCHA_STATE_TRACE;$env:JAJUCHA_STATE_TRACE='1'
try {
    $p=if(Test-Path $prefPath){Get-Content -Raw $prefPath|ConvertFrom-Json}else{[pscustomobject]@{}}
    $p.lastStage='preliminary';$p.mode=1;$p.missionType=2;$p.candidateId='candidate_1';$p.randomSeed=2031
    $p|ConvertTo-Json -Depth 8|Set-Content $prefPath -Encoding UTF8
    Remove-Item $tracePath -Force -ErrorAction SilentlyContinue
    $process=Start-Process -FilePath $ExePath -ArgumentList @('-screen-fullscreen','0','-screen-width',"$Width",'-screen-height',"$Height") -WorkingDirectory (Split-Path $ExePath) -PassThru
    $deadline=(Get-Date).AddSeconds($TimeoutSec);do{Start-Sleep -Milliseconds 200;$process.Refresh()}while($process.MainWindowHandle -eq 0 -and (Get-Date)-lt $deadline)
    Assert-True ($process.MainWindowHandle -ne 0) 'Simulator window did not open'
    $ready=Wait-Trace {param($s)$s.ready -and $s.scenarioState -eq 'Ready'} 'Ready'
    do{try{$client=[Net.Sockets.TcpClient]::new('127.0.0.1',8765)}catch{Start-Sleep -Milliseconds 200}}while(!$client -and (Get-Date)-lt $deadline)
    Assert-True ($null -ne $client) 'Bridge did not listen'
    $stream=$client.GetStream();$stream.ReadTimeout=5000;$reader=[IO.StreamReader]::new($stream);$writer=[IO.StreamWriter]::new($stream);$writer.NewLine="`n";$writer.AutoFlush=$true
    $writer.WriteLine((@{type='hello';id=0;protocol=1;client='dynamic-obstacle'}|ConvertTo-Json -Compress));$hello=$reader.ReadLine()|ConvertFrom-Json
    Assert-True ($hello.type -eq 'hello_ack') 'Handshake failed'
    $start=Request $writer $reader 1 'start_run';Assert-True ($start.ok) 'Dynamic obstacle mission did not start'
    $countdown=Wait-Trace {param($s)$s.scenarioState -eq 'Countdown' -and $s.session.mission -eq 'DynamicObstacle'} 'dynamic mission countdown'
    Assert-True ($countdown.missionObject.type -eq 'DynamicObstacle' -and $countdown.missionObject.active) 'Dynamic obstacle object missing from runtime'
    $initial=$countdown.missionObject.positionCm
    $release=Wait-Trace {param($s)$s.signal.released -and $s.session.mission -eq 'DynamicObstacle'} 'release'
    Request $writer $reader 2 'set_motor' @{left=8;right=8;speed=8}|Out-Null
    Start-Sleep -Seconds 2
    $after=Last-Trace
    Assert-True ([Math]::Abs([double]$after.missionObject.positionCm.x-[double]$initial.x) -lt 0.5 -and [Math]::Abs([double]$after.missionObject.positionCm.z-[double]$initial.z) -lt 0.5) 'Obstacle moved without vehicle approach'
    Request $writer $reader 3 'set_motor' @{left=0;right=0;speed=0}|Out-Null
    Request $writer $reader 4 'abort_run'|Out-Null
    $aborted=Wait-Trace {param($s)$s.scenarioState -eq 'Aborted'} 'aborted'
    $result=[ordered]@{passed=$true;timestamp=(Get-Date).ToString('o');checks=[ordered]@{handshake=$true;startAccepted=$true;mission=$countdown.session.mission;candidate=$countdown.session.candidate;objectActive=$countdown.missionObject.active;initialPosition=$initial;release=$release.signal.phase;stationaryObstacle=$true;aborted=$aborted.scenarioState};trace=$tracePath}
    $result|ConvertTo-Json -Depth 16|Set-Content $resultPath -Encoding UTF8;Write-Host "Dynamic obstacle runtime smoke passed. Result: $resultPath" -ForegroundColor Green
}catch{$result=[ordered]@{passed=$false;timestamp=(Get-Date).ToString('o');error=$_.Exception.Message};$result|ConvertTo-Json -Depth 12|Set-Content $resultPath -Encoding UTF8;throw}
finally{if($reader){$reader.Dispose()};if($writer){$writer.Dispose()};if($client){$client.Dispose()};if($process -and -not $process.HasExited){$process.CloseMainWindow()|Out-Null;Start-Sleep -Milliseconds 500;if(!$process.HasExited){$process.Kill()}};$original|ConvertTo-Json -Depth 8|Set-Content $prefPath -Encoding UTF8;if($null -eq $oldTrace){Remove-Item Env:JAJUCHA_STATE_TRACE -ErrorAction SilentlyContinue}else{$env:JAJUCHA_STATE_TRACE=$oldTrace}}
