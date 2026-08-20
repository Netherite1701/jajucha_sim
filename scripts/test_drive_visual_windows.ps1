# Visual driving proof: send a real bridge motor command and capture the
# standalone client before/after movement.  The JSON records the same pose
# values returned by get_status so the screenshot pair is tied to coordinates.
[CmdletBinding()]
param(
    [string]$ExePath = "",
    [int]$Width = 1280,
    [int]$Height = 720,
    [int]$TimeoutSec = 20,
    [int]$DriveLoops = 4
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe" }
if (-not (Test-Path -LiteralPath $ExePath)) { throw "Standalone executable not found: $ExePath" }
$artifactDir = Join-Path $Root "test-artifacts\drive"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$beforePath = Join-Path $artifactDir "drive_before_$($Width)x$($Height).png"
$afterPath = Join-Path $artifactDir "drive_after_$($Width)x$($Height).png"
$resultPath = Join-Path $artifactDir "drive_visual_$stamp.json"

Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public static class JajuchaDriveWin32 {
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
  [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
  [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
  [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
  [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
  [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
  [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
}
'@

function Get-ClientInfo([IntPtr]$h) {
    $r = New-Object JajuchaDriveWin32+RECT
    [JajuchaDriveWin32]::GetClientRect($h, [ref]$r) | Out-Null
    $p = New-Object JajuchaDriveWin32+POINT
    [JajuchaDriveWin32]::ClientToScreen($h, [ref]$p) | Out-Null
    [pscustomobject]@{ X=[int]$p.X; Y=[int]$p.Y; Width=([int]$r.Right - [int]$r.Left); Height=([int]$r.Bottom - [int]$r.Top) }
}

function Capture-Client([IntPtr]$h, [string]$path) {
    [JajuchaDriveWin32]::ShowWindow($h, 9) | Out-Null
    [JajuchaDriveWin32]::BringWindowToTop($h) | Out-Null
    [JajuchaDriveWin32]::SetForegroundWindow($h) | Out-Null
    Start-Sleep -Milliseconds 100
    $c = Get-ClientInfo $h
    $w = New-Object JajuchaDriveWin32+RECT
    [JajuchaDriveWin32]::GetWindowRect($h, [ref]$w) | Out-Null
    $ww = [int]$w.Right - [int]$w.Left; $wh = [int]$w.Bottom - [int]$w.Top
    $bmp = New-Object System.Drawing.Bitmap($ww, $wh)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $dc = $g.GetHdc()
    $ok = [JajuchaDriveWin32]::PrintWindow($h, $dc, 2)
    $g.ReleaseHdc($dc); $g.Dispose()
    if (-not $ok) { throw "PrintWindow failed" }
    $crop = New-Object System.Drawing.Rectangle(([int]$c.X - [int]$w.Left), ([int]$c.Y - [int]$w.Top), [int]$c.Width, [int]$c.Height)
    $clientBmp = $bmp.Clone($crop, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $clientBmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $clientBmp.Dispose(); $bmp.Dispose()
}

function Send-Request($writer, $reader, [int]$id, [string]$name, [hashtable]$payload=@{}) {
    $m = [ordered]@{ type="command"; id=$id; name=$name }
    if ($payload.Count -gt 0) { $m.payload = $payload }
    $writer.WriteLine(($m | ConvertTo-Json -Compress -Depth 6)); $writer.Flush()
    $line = $reader.ReadLine(); if (-not $line) { throw "No response for $name" }
    $r = $line | ConvertFrom-Json; if ($r.ok -ne $true) { throw "Bridge command failed: $name" }
    return $r
}

$oldTraceEnv = $env:JAJUCHA_STATE_TRACE; $env:JAJUCHA_STATE_TRACE = "1"
$process=$null; $client=$null; $reader=$null; $writer=$null
try {
    $process = Start-Process -FilePath $ExePath -ArgumentList @("-screen-fullscreen","0","-screen-width","$Width","-screen-height","$Height") -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
    $deadline=(Get-Date).AddSeconds($TimeoutSec)
    do { Start-Sleep -Milliseconds 200; $process.Refresh() } while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
    if ($process.MainWindowHandle -eq 0) { throw "Simulator window did not open" }
    do { try { $client=[Net.Sockets.TcpClient]::new("127.0.0.1",8765) } catch { Start-Sleep -Milliseconds 250 } } while ($null -eq $client -and (Get-Date) -lt $deadline)
    if ($null -eq $client) { throw "Bridge did not listen" }
    $stream=$client.GetStream(); $stream.ReadTimeout=5000; $reader=[IO.StreamReader]::new($stream); $writer=[IO.StreamWriter]::new($stream); $writer.NewLine="`n"; $writer.AutoFlush=$true
    $writer.WriteLine((@{type="hello";id=0;protocol=1;client="visual-drive"}|ConvertTo-Json -Compress)); $hello=$reader.ReadLine()|ConvertFrom-Json
    if ($hello.type -ne "hello_ack") { throw "Handshake failed" }
    $before=Send-Request $writer $reader 1 "get_status"
    Start-Sleep -Seconds 2
    Capture-Client $process.MainWindowHandle $beforePath
    # Keep refreshing the motor command under the bridge watchdog while the
    # vehicle visibly advances.  A single command is intentionally stopped
    # after the safety timeout, so this also proves the normal control loop.
    $ack=Send-Request $writer $reader 2 "set_motor" @{left=0;right=0;speed=30}
    for ($i=0; $i -lt $DriveLoops; $i++) {
        Start-Sleep -Milliseconds 300
        [void](Send-Request $writer $reader (3 + $i) "set_motor" @{left=0;right=0;speed=30})
    }
    $after=Send-Request $writer $reader 20 "get_status"
    Capture-Client $process.MainWindowHandle $afterPath
    $p0=$before.payload.vehicle.position_cm; $p1=$after.payload.vehicle.position_cm
    $dx=([double]$p1.x - [double]$p0.x); $dz=([double]$p1.z - [double]$p0.z); $distance=[Math]::Sqrt($dx*$dx+$dz*$dz)
    $grounded = [bool]$after.payload.vehicle.driven_wheel_grounded
    $heightStable = [Math]::Abs([double]$after.payload.vehicle.position_cm.y - [double]$before.payload.vehicle.position_cm.y) -lt 2.0
    $result=[ordered]@{ passed=($distance -gt 0.01 -and $grounded -and $heightStable); timestamp=(Get-Date).ToString("o"); before=$before.payload.vehicle; after=$after.payload.vehicle; distance_cm=$distance; grounded=$grounded; height_stable=$heightStable; screenshots=@($beforePath,$afterPath) }
    if (-not $result.passed) { throw "Vehicle did not remain grounded during safe drive (distance=$distance, grounded=$grounded, heightStable=$heightStable)" }
    $result | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    if (-not $result.passed) { throw "Vehicle did not move ($distance cm)" }
    Write-Host "Visual driving test passed. Result: $resultPath" -ForegroundColor Green
}
finally {
    if ($reader) {$reader.Dispose()}; if ($writer) {$writer.Dispose()}; if ($client) {$client.Dispose()}
    if ($process -and -not $process.HasExited) { $process.CloseMainWindow()|Out-Null; Start-Sleep -Milliseconds 500; if(-not $process.HasExited){$process.Kill()} }
    if ($null -eq $oldTraceEnv) { Remove-Item Env:JAJUCHA_STATE_TRACE -ErrorAction SilentlyContinue } else {$env:JAJUCHA_STATE_TRACE=$oldTraceEnv}
}
