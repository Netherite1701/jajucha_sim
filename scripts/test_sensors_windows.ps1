# Exercise the real bridge sensor endpoints against the Windows standalone:
# left/center/right RGB camera frames and center depth (Gray8).  The saved
# PNGs are the actual sensor RenderTextures, not dashboard screenshots.
[CmdletBinding()]
param([string]$ExePath="", [int]$Width=1280, [int]$Height=720, [int]$TimeoutSec=25)
$ErrorActionPreference="Stop"
$Root=Split-Path -Parent $PSScriptRoot
if(-not $ExePath){$ExePath=Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe"}
if(-not(Test-Path -LiteralPath $ExePath)){throw "Standalone executable not found: $ExePath"}
$dir=Join-Path $Root "test-artifacts\sensors"; New-Item -ItemType Directory -Force -Path $dir|Out-Null
$stamp=Get-Date -Format "yyyyMMdd_HHmmss"; $resultPath=Join-Path $dir "sensor_smoke_$stamp.json"
Add-Type -AssemblyName System.Drawing

function Read-LineBytes($stream){
    $bytes=New-Object System.Collections.Generic.List[byte]
    while($true){$b=$stream.ReadByte(); if($b -lt 0){throw "Bridge closed"}; if($b -eq 10){break}; if($b -ne 13){[void]$bytes.Add([byte]$b)}}
    [Text.Encoding]::UTF8.GetString($bytes.ToArray())
}
function Read-Exact($stream,[int]$length){
    $data=New-Object byte[] $length; $offset=0
    while($offset -lt $length){$n=$stream.Read($data,$offset,$length-$offset); if($n -le 0){throw "Bridge closed in binary payload"}; $offset+=$n}
    $data
}
function Send-Binary($stream,[int]$id,[string]$name,[hashtable]$payload=@{}){
    $m=[ordered]@{type="command";id=$id;name=$name}; if($payload.Count -gt 0){$m.payload=$payload}
    $line=($m|ConvertTo-Json -Compress -Depth 6)+"`n"; $raw=[Text.Encoding]::UTF8.GetBytes($line); $stream.Write($raw,0,$raw.Length); $stream.Flush()
    $header=(Read-LineBytes $stream)|ConvertFrom-Json; if($header.ok -ne $true){throw "$name failed"}
    $n=[int]$header.length; $data=Read-Exact $stream $n
    [pscustomobject]@{header=$header;data=$data}
}
function Save-Rgb([byte[]]$data,[int]$w,[int]$h,[string]$path){
    $bmp=New-Object System.Drawing.Bitmap($w,$h,[System.Drawing.Imaging.PixelFormat]::Format24bppRgb); $i=0
    for($y=0;$y -lt $h;$y++){for($x=0;$x -lt $w;$x++){$bmp.SetPixel($x,$y,[Drawing.Color]::FromArgb($data[$i+0],$data[$i+1],$data[$i+2]));$i+=3}}
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$bmp.Dispose()
}
function Save-Gray([byte[]]$data,[int]$w,[int]$h,[string]$path){
    $bmp=New-Object System.Drawing.Bitmap($w,$h,[Drawing.Imaging.PixelFormat]::Format24bppRgb);$i=0
    for($y=0;$y -lt $h;$y++){for($x=0;$x -lt $w;$x++){$v=$data[$i++];$bmp.SetPixel($x,$y,[Drawing.Color]::FromArgb($v,$v,$v))}}
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$bmp.Dispose()
}
function Save-Lidar([byte[]]$data,[int]$rayCount,[double]$angleMin,[double]$angleIncrement,[double]$maxDistanceCm,[string]$path){
    $size=600;$bmp=New-Object System.Drawing.Bitmap($size,$size,[Drawing.Imaging.PixelFormat]::Format24bppRgb);$g=[Drawing.Graphics]::FromImage($bmp)
    $g.Clear([Drawing.Color]::FromArgb(5,12,18));$cx=$size/2;$cy=$size/2;$radius=($size/2)-20
    $grid=[Drawing.Pen]::new([Drawing.Color]::FromArgb(60,90,100),1);$g.DrawEllipse($grid,$cx-$radius,$cy-$radius,$radius*2,$radius*2);$g.DrawLine($grid,$cx,$cy-$radius,$cx,$cy+$radius);$g.DrawLine($grid,$cx-$radius,$cy,$cx+$radius,$cy)
    $point=[Drawing.Brushes]::LimeGreen;$scale=$radius/[Math]::Max($maxDistanceCm,1)
    for($i=0;$i -lt $rayCount;$i++){
        $distanceCm=[BitConverter]::ToSingle($data,$i*4);$r=[Math]::Min($distanceCm,$maxDistanceCm)*$scale;$rad=($angleMin+$angleIncrement*$i)*[Math]::PI/180.0
        $x=$cx+[Math]::Sin($rad)*$r;$y=$cy-[Math]::Cos($rad)*$r;$g.FillEllipse($point,$x-2,$y-2,4,4)
    }
    $bmp.Save($path,[Drawing.Imaging.ImageFormat]::Png);$g.Dispose();$grid.Dispose();$bmp.Dispose()
}

$old=$env:JAJUCHA_STATE_TRACE;$env:JAJUCHA_STATE_TRACE="1";$p=$null;$tcp=$null
try{
    $p=Start-Process -FilePath $ExePath -ArgumentList @("-screen-fullscreen","0","-screen-width","$Width","-screen-height","$Height") -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
    $deadline=(Get-Date).AddSeconds($TimeoutSec)
    do{Start-Sleep -Milliseconds 200;$p.Refresh()}while($p.MainWindowHandle -eq 0 -and (Get-Date)-lt $deadline)
    if($p.MainWindowHandle -eq 0){throw "Simulator window did not open"}
    do{try{$tcp=[Net.Sockets.TcpClient]::new("127.0.0.1",8765)}catch{Start-Sleep -Milliseconds 250}}while($null-eq $tcp -and (Get-Date)-lt $deadline)
    if($null-eq $tcp){throw "Bridge did not listen"};$s=$tcp.GetStream();$s.ReadTimeout=5000
    $hello=([ordered]@{type="hello";id=0;protocol=1;client="sensor-smoke"}|ConvertTo-Json -Compress)+"`n";$hb=[Text.Encoding]::UTF8.GetBytes($hello);$s.Write($hb,0,$hb.Length);$s.Flush();$ha=(Read-LineBytes $s)|ConvertFrom-Json
    if($ha.type -ne "hello_ack"){throw "Handshake failed"}
    $records=[ordered]@{};$id=1
    foreach($loc in @("left","center","right")){
        $r=Send-Binary $s $id "get_image" @{location=$loc};$id++
        $h=$r.header;$expected=[int]$h.width*[int]$h.height*3
        if($h.payload_type -ne "image" -or $h.format -ne "rgb24" -or [int]$h.length -ne $expected){Write-Host ("Invalid {0} header: {1}" -f $loc, ($h|ConvertTo-Json -Compress));throw "Invalid $loc frame header"}
        $path=Join-Path $dir "${loc}_camera.png";Save-Rgb $r.data ([int]$h.width) ([int]$h.height) $path
        $records[$loc]=[ordered]@{frame_id=$h.frame_id;width=[int]$h.width;height=[int]$h.height;format=$h.format;length=[int]$h.length;path=$path;nonzero=([bool](($r.data|Where-Object{$_ -ne 0}|Select-Object -First 1)))}
    }
    $d=Send-Binary $s $id "get_depth" @{};$dh=$d.header;$expectedDepth=[int]$dh.width*[int]$dh.height
    if($dh.payload_type -ne "depth" -or $dh.format -ne "gray8" -or [int]$dh.length -ne $expectedDepth){throw "Invalid depth frame header"}
    $depthPath=Join-Path $dir "center_depth.png";Save-Gray $d.data ([int]$dh.width) ([int]$dh.height) $depthPath
    $records.depth=[ordered]@{frame_id=$dh.frame_id;width=[int]$dh.width;height=[int]$dh.height;format=$dh.format;length=[int]$dh.length;path=$depthPath;nonzero=([bool](($d.data|Where-Object{$_ -ne 0}|Select-Object -First 1)))}
    $id++
    $lidar=Send-Binary $s $id "get_lidar" @{};$lh=$lidar.header;$rayCount=[int]$lh.ray_count
    if($lh.payload_type -ne "lidar" -or $lh.format -ne "float32_le" -or $rayCount -lt 300 -or [int]$lh.length -ne ($rayCount*4)){throw "Invalid lidar frame header"}
    $lidarPath=Join-Path $dir "lidar.png";Save-Lidar $lidar.data $rayCount ([double]$lh.angle_min_deg) ([double]$lh.angle_increment_deg) ([double]$lh.max_distance_cm) $lidarPath
    $records.lidar=[ordered]@{frame_id=$lh.frame_id;ray_count=$rayCount;angle_min_deg=[double]$lh.angle_min_deg;angle_max_deg=[double]$lh.angle_max_deg;angle_increment_deg=[double]$lh.angle_increment_deg;max_distance_cm=[double]$lh.max_distance_cm;format=$lh.format;length=[int]$lh.length;path=$lidarPath}
    $result=[ordered]@{passed=$true;timestamp=(Get-Date).ToString("o");cameras=$records;lidar=[ordered]@{supported=$true;ray_count=$rayCount;angle_range="0..360 degrees";distance_unit="cm bridge / mm Python";path=$lidarPath}}
    $result|ConvertTo-Json -Depth 12|Set-Content -LiteralPath $resultPath -Encoding UTF8;Write-Host "Sensor smoke passed. Result: $resultPath" -ForegroundColor Green
}finally{
    if($tcp){$tcp.Dispose()};if($p -and -not $p.HasExited){$p.CloseMainWindow()|Out-Null;Start-Sleep -Milliseconds 500;if(-not$p.HasExited){$p.Kill()}}
    if($null-eq $old){Remove-Item Env:JAJUCHA_STATE_TRACE -ErrorAction SilentlyContinue}else{$env:JAJUCHA_STATE_TRACE=$old}
}
