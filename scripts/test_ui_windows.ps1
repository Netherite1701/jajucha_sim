# Real-input smoke test for the 2026 dashboard.
#
# This deliberately sends Windows mouse/keyboard input to the standalone
# window.  Assertions come from the post-physics state-trace JSONL rather than
# from pixels alone.  Enable with the current source build only; an older dist
# executable cannot validate the new dashboard.

[CmdletBinding()]
param(
    [string]$ExePath = "",
    [int]$Width = 1280,
    [int]$Height = 720,
    [int]$TimeoutSec = 30,
    [switch]$KeepOpen,
    [switch]$CaptureOnly,
    [switch]$CameraViews,
    [switch]$FinalCameraViews,
    [switch]$DumpControls,
    [switch]$ProbeCourseCopy,
    [switch]$EditSmoke,
    [switch]$EditProbe,
    [switch]$SensorSmoke,
    [switch]$CourseLifecycleSmoke
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
if (-not $ExePath) { $ExePath = Join-Path $Root "dist\JajuchaSimulator\JajuchaSimulator.exe" }
if (-not (Test-Path -LiteralPath $ExePath)) { throw "Standalone executable not found: $ExePath" }

Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Drawing;
using System.Runtime.InteropServices;
public static class JajuchaWin32 {
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public MOUSEKEYBDHARDWAREINPUT data; }
    [StructLayout(LayoutKind.Explicit)] public struct MOUSEKEYBDHARDWAREINPUT {
        [FieldOffset(0)] public MOUSEINPUT mi;
        [FieldOffset(0)] public KEYBDINPUT ki;
    }
    [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
    [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
    [DllImport("user32.dll")] public static extern IntPtr SetFocus(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
    public const uint WM_MOUSEMOVE=0x0200, WM_LBUTTONDOWN=0x0201, WM_LBUTTONUP=0x0202, WM_MOUSEWHEEL=0x020A, WM_KEYDOWN=0x0100, WM_KEYUP=0x0101, MK_LBUTTON=0x0001;
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int X, int Y);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] public static extern uint SendInput(uint nInputs, INPUT[] inputs, int cbSize);
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
    public const uint INPUT_MOUSE=0, INPUT_KEYBOARD=1, MOUSEEVENTF_MOVE=0x0001, MOUSEEVENTF_LEFTDOWN=0x0002, MOUSEEVENTF_LEFTUP=0x0004, KEYEVENTF_KEYUP=0x0002;
    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);
    public const uint SWP_NOSIZE=0x0001, SWP_NOMOVE=0x0002, SWP_NOACTIVATE=0x0010, SWP_SHOWWINDOW=0x0040;
}
'@

$artifactDir = Join-Path $Root "test-artifacts\ui"
New-Item -ItemType Directory -Force -Path $artifactDir | Out-Null

function Send-Key([System.UInt16]$virtualKey) {
    if ($script:inputHandle) {
        Focus-Simulator $script:inputHandle | Out-Null
        [JajuchaWin32]::PostMessage($script:inputHandle, [JajuchaWin32]::WM_KEYDOWN,
            [IntPtr]$virtualKey, [IntPtr]::Zero) | Out-Null
        [JajuchaWin32]::PostMessage($script:inputHandle, [JajuchaWin32]::WM_KEYUP,
            [IntPtr]$virtualKey, [IntPtr]::Zero) | Out-Null
    }
    $down = New-Object JajuchaWin32+INPUT
    $down.type = [JajuchaWin32]::INPUT_KEYBOARD
    $down.data.ki.wVk = $virtualKey
    $down.data.ki.wScan = 0x3C
    $up = New-Object JajuchaWin32+INPUT
    $up.type = [JajuchaWin32]::INPUT_KEYBOARD
    $up.data.ki.wVk = $virtualKey
    $up.data.ki.wScan = 0x3C
    $up.data.ki.dwFlags = [JajuchaWin32]::KEYEVENTF_KEYUP
    [JajuchaWin32]::SendInput(2, @($down, $up), [Runtime.InteropServices.Marshal]::SizeOf([type][JajuchaWin32+INPUT])) | Out-Null
    [JajuchaWin32]::keybd_event([byte]$virtualKey, 0x3C, 0, [UIntPtr]::Zero)
    [JajuchaWin32]::keybd_event([byte]$virtualKey, 0x3C, [JajuchaWin32]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
}

function Send-CtrlKey([System.UInt16]$virtualKey) {
    if ($script:inputHandle) { Focus-Simulator $script:inputHandle | Out-Null }
    $ctrlDown = New-Object JajuchaWin32+INPUT; $ctrlDown.type = [JajuchaWin32]::INPUT_KEYBOARD; $ctrlDown.data.ki.wVk = 0xA2
    $keyDown = New-Object JajuchaWin32+INPUT; $keyDown.type = [JajuchaWin32]::INPUT_KEYBOARD; $keyDown.data.ki.wVk = $virtualKey
    $keyUp = New-Object JajuchaWin32+INPUT; $keyUp.type = [JajuchaWin32]::INPUT_KEYBOARD; $keyUp.data.ki.wVk = $virtualKey; $keyUp.data.ki.dwFlags = [JajuchaWin32]::KEYEVENTF_KEYUP
    $ctrlUp = New-Object JajuchaWin32+INPUT; $ctrlUp.type = [JajuchaWin32]::INPUT_KEYBOARD; $ctrlUp.data.ki.wVk = 0xA2; $ctrlUp.data.ki.dwFlags = [JajuchaWin32]::KEYEVENTF_KEYUP
    $size = [Runtime.InteropServices.Marshal]::SizeOf([type][JajuchaWin32+INPUT])
    [JajuchaWin32]::SendInput(4, @($ctrlDown, $keyDown, $keyUp, $ctrlUp), $size) | Out-Null
    [JajuchaWin32]::keybd_event(0x11, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 100
    [JajuchaWin32]::keybd_event([byte]$virtualKey, 0, 0, [UIntPtr]::Zero)
    [JajuchaWin32]::keybd_event([byte]$virtualKey, 0, [JajuchaWin32]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 100
    [JajuchaWin32]::keybd_event(0x11, 0, [JajuchaWin32]::KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 200
}

function Get-ClientInfo($handle) {
    $rect = New-Object JajuchaWin32+RECT
    if (-not [JajuchaWin32]::GetClientRect($handle, [ref]$rect)) { throw "GetClientRect failed" }
    $point = New-Object JajuchaWin32+POINT
    if (-not [JajuchaWin32]::ClientToScreen($handle, [ref]$point)) { throw "ClientToScreen failed" }
    [pscustomobject]@{ X=$point.X; Y=$point.Y; Width=($rect.Right-$rect.Left); Height=($rect.Bottom-$rect.Top) }
}

function Focus-Simulator($handle) {
    # Restore and raise the actual Unity window before every capture/input.
    # This matters when the Codex/terminal window is the foreground app.
    $foreground = [JajuchaWin32]::GetForegroundWindow()
    $foregroundPid = [uint32]0
    $foregroundThread = [JajuchaWin32]::GetWindowThreadProcessId($foreground, [ref]$foregroundPid)
    $targetPid = [uint32]0
    $targetThread = [JajuchaWin32]::GetWindowThreadProcessId($handle, [ref]$targetPid)
    $attached = $false
    if ($foregroundThread -and $targetThread -and $foregroundThread -ne $targetThread) {
        $attached = [JajuchaWin32]::AttachThreadInput($targetThread, $foregroundThread, $true)
    }
    [JajuchaWin32]::ShowWindow($handle, 9) | Out-Null # SW_RESTORE
    # Unity can remain visually behind another desktop window even after
    # SetForegroundWindow succeeds. Briefly raise it as topmost so screen
    # capture and physical input address the same pixels, then remove the
    # topmost flag while leaving it foreground.
    [JajuchaWin32]::SetWindowPos($handle, [JajuchaWin32]::HWND_TOPMOST, 0, 0, 0, 0,
        [JajuchaWin32]::SWP_NOMOVE -bor [JajuchaWin32]::SWP_NOSIZE -bor [JajuchaWin32]::SWP_SHOWWINDOW) | Out-Null
    [JajuchaWin32]::BringWindowToTop($handle) | Out-Null
    [JajuchaWin32]::SetForegroundWindow($handle) | Out-Null
    [JajuchaWin32]::SetFocus($handle) | Out-Null
    [JajuchaWin32]::SetWindowPos($handle, [JajuchaWin32]::HWND_NOTOPMOST, 0, 0, 0, 0,
        [JajuchaWin32]::SWP_NOMOVE -bor [JajuchaWin32]::SWP_NOSIZE -bor [JajuchaWin32]::SWP_NOACTIVATE -bor [JajuchaWin32]::SWP_SHOWWINDOW) | Out-Null
    if ($attached) { [JajuchaWin32]::AttachThreadInput($targetThread, $foregroundThread, $false) | Out-Null }
    Start-Sleep -Milliseconds 20
    return [JajuchaWin32]::GetForegroundWindow()
}

function Click-Reference($handle, [double]$x, [double]$y) {
    $focused = Focus-Simulator $handle
    $client = Get-ClientInfo $handle
    $scale = [Math]::Min($client.Width / 1600.0, $client.Height / 900.0)
    $sx = [int]($client.X + $x * $scale)
    $sy = [int]($client.Y + $y * $scale)
    # SendInput mouse coordinates are relative unless ABSOLUTE is set. Move
    # the real cursor first, then send an actual button down/up pair at that
    # screen position; this avoids resolution-dependent relative drift.
    [JajuchaWin32]::SetCursorPos($sx, $sy) | Out-Null
    $down = New-Object JajuchaWin32+INPUT
    $down.type = [JajuchaWin32]::INPUT_MOUSE
    $down.data.mi.dwFlags = [JajuchaWin32]::MOUSEEVENTF_LEFTDOWN
    $up = New-Object JajuchaWin32+INPUT
    $up.type = [JajuchaWin32]::INPUT_MOUSE
    $up.data.mi.dwFlags = [JajuchaWin32]::MOUSEEVENTF_LEFTUP
    $size = [Runtime.InteropServices.Marshal]::SizeOf([type][JajuchaWin32+INPUT])
    $sent = [JajuchaWin32]::SendInput(2, @($down, $up), $size)
    # Unity's native window can reject synthesized input when a different
    # desktop owns the foreground lock. Mirror the same physical click as a
    # client-coordinate Win32 message so the UGUI Button receives it too.
    $localX = [int]($sx - $client.X)
    $localY = [int]($sy - $client.Y)
    $packed = [uint32]((($localY -band 0xFFFF) -shl 16) -bor ($localX -band 0xFFFF))
    [JajuchaWin32]::SendMessage($handle, [JajuchaWin32]::WM_MOUSEMOVE,
        [IntPtr]::Zero, [IntPtr]$packed) | Out-Null
    [JajuchaWin32]::SendMessage($handle, [JajuchaWin32]::WM_LBUTTONDOWN,
        [IntPtr][JajuchaWin32]::MK_LBUTTON, [IntPtr]$packed) | Out-Null
    [JajuchaWin32]::SendMessage($handle, [JajuchaWin32]::WM_LBUTTONUP,
        [IntPtr]::Zero, [IntPtr]$packed) | Out-Null
    Write-Host ("Click reference ({0},{1}) -> screen ({2},{3}), sent={4}, foreground=0x{5:X}" -f $x, $y, $sx, $sy, $sent, $focused)
    Start-Sleep -Milliseconds 180
}

function Click-ClientPixels($handle, [int]$localX, [int]$localY) {
    # Diagnostic helper for DPI/window-coordinate differences: callers pass
    # the pixel coordinates visible in the captured client screenshot.
    $client = Get-ClientInfo $handle
    $focused = Focus-Simulator $handle
    $sx = [int]($client.X + $localX)
    $sy = [int]($client.Y + $localY)
    [JajuchaWin32]::SetCursorPos($sx, $sy) | Out-Null
    Start-Sleep -Milliseconds 100
    $down = New-Object JajuchaWin32+INPUT; $down.type = [JajuchaWin32]::INPUT_MOUSE; $down.data.mi.dwFlags = [JajuchaWin32]::MOUSEEVENTF_LEFTDOWN
    $up = New-Object JajuchaWin32+INPUT; $up.type = [JajuchaWin32]::INPUT_MOUSE; $up.data.mi.dwFlags = [JajuchaWin32]::MOUSEEVENTF_LEFTUP
    $size = [Runtime.InteropServices.Marshal]::SizeOf([type][JajuchaWin32+INPUT])
    [JajuchaWin32]::SendInput(2, @($down, $up), $size) | Out-Null
    [JajuchaWin32]::mouse_event([JajuchaWin32]::MOUSEEVENTF_LEFTDOWN, 0, 0, 0, [UIntPtr]::Zero)
    [JajuchaWin32]::mouse_event([JajuchaWin32]::MOUSEEVENTF_LEFTUP, 0, 0, 0, [UIntPtr]::Zero)
    $packed = [uint32]((($localY -band 0xFFFF) -shl 16) -bor ($localX -band 0xFFFF))
    [JajuchaWin32]::SendMessage($handle, [JajuchaWin32]::WM_MOUSEMOVE, [IntPtr]::Zero, [IntPtr]$packed) | Out-Null
    [JajuchaWin32]::SendMessage($handle, [JajuchaWin32]::WM_LBUTTONDOWN, [IntPtr][JajuchaWin32]::MK_LBUTTON, [IntPtr]$packed) | Out-Null
    [JajuchaWin32]::SendMessage($handle, [JajuchaWin32]::WM_LBUTTONUP, [IntPtr]::Zero, [IntPtr]$packed) | Out-Null
    Write-Host ("Click client ({0},{1}) -> screen ({2},{3}), foreground=0x{4:X}" -f $localX, $localY, $sx, $sy, $focused)
    Start-Sleep -Milliseconds 180
}

function Move-ClientPixels($handle, [int]$localX, [int]$localY) {
    $client = Get-ClientInfo $handle
    Focus-Simulator $handle | Out-Null
    [JajuchaWin32]::SetCursorPos([int]($client.X + $localX), [int]($client.Y + $localY)) | Out-Null
    Start-Sleep -Milliseconds 500
}

function Scroll-ClientPixels($handle, [int]$localX, [int]$localY, [int]$delta) {
    $client = Get-ClientInfo $handle
    Focus-Simulator $handle | Out-Null
    [JajuchaWin32]::SetCursorPos([int]($client.X + $localX), [int]($client.Y + $localY)) | Out-Null
    Start-Sleep -Milliseconds 150
    $packed = [uint32]((($localY -band 0xFFFF) -shl 16) -bor ($localX -band 0xFFFF))
    $wheel = [uint32](([int64]($delta -band 0xFFFF)) -shl 16)
    [JajuchaWin32]::SendMessage($handle, [JajuchaWin32]::WM_MOUSEMOVE, [IntPtr]::Zero, [IntPtr]$packed) | Out-Null
    [JajuchaWin32]::SendMessage($handle, [JajuchaWin32]::WM_MOUSEWHEEL, [IntPtr]$wheel, [IntPtr]$packed) | Out-Null
    $wheelData = if ($delta -lt 0) { [uint32](4294967296 + $delta) } else { [uint32]$delta }
    [JajuchaWin32]::mouse_event(0x0800, 0, 0, $wheelData, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 300
}

function Capture-Screenshot($handle, [string]$name) {
    Focus-Simulator $handle
    $client = Get-ClientInfo $handle
    $windowRect = New-Object JajuchaWin32+RECT
    [JajuchaWin32]::GetWindowRect($handle, [ref]$windowRect) | Out-Null
    Write-Host ("Capture {0}: client=({1},{2}) {3}x{4}, window=({5},{6})-({7},{8}), foreground=0x{9:X}" -f
        $name, $client.X, $client.Y, $client.Width, $client.Height,
        $windowRect.Left, $windowRect.Top, $windowRect.Right, $windowRect.Bottom,
        [JajuchaWin32]::GetForegroundWindow())
    $path = Join-Path $artifactDir ("{0}_{1}x{2}.png" -f $name, $client.Width, $client.Height)
    $windowWidth = $windowRect.Right - $windowRect.Left
    $windowHeight = $windowRect.Bottom - $windowRect.Top
    $bitmap = New-Object System.Drawing.Bitmap($windowWidth, $windowHeight)
    # PrintWindow captures the target HWND even when another window races to
    # the foreground. PW_RENDERFULLCONTENT is supported by current Unity;
    # fall back to screen pixels for older drivers.
    $hdc = [IntPtr]::Zero
    $captureGraphics = $null
    $captured = $false
    try {
        $captureGraphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $hdc = $captureGraphics.GetHdc()
        $captured = [JajuchaWin32]::PrintWindow($handle, $hdc, 2)
    } finally {
        if ($captureGraphics) {
            if ($hdc -ne [IntPtr]::Zero) { $captureGraphics.ReleaseHdc($hdc) }
            $captureGraphics.Dispose()
        }
    }
    if (-not $captured) {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        $graphics.CopyFromScreen($windowRect.Left, $windowRect.Top, 0, 0, $bitmap.Size)
        $graphics.Dispose()
    }
    $cropX = $client.X - $windowRect.Left
    $cropY = $client.Y - $windowRect.Top
    $crop = New-Object System.Drawing.Rectangle($cropX, $cropY, $client.Width, $client.Height)
    $clientBitmap = $bitmap.Clone($crop, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $clientBitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
    $clientBitmap.Dispose()
    $bitmap.Dispose()
    return $path
}

function Find-TracePath {
    $roots = @(
        (Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\Logs"),
        (Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\Logs")
    ) | Select-Object -Unique
    foreach ($dir in $roots) {
        $candidate = Join-Path $dir "state-trace.jsonl"
        if (Test-Path -LiteralPath $candidate) { return $candidate }
    }
    return $null
}

function Get-LastTrace($tracePath) {
    if (-not $tracePath -or -not (Test-Path -LiteralPath $tracePath)) { return $null }
    # The recorder stays open for live diagnostics, so use a compatible share
    # mode rather than PowerShell's default exclusive reader.
    $line = $null
    $stream = $null
    $reader = $null
    try {
        $stream = [System.IO.FileStream]::new($tracePath, [System.IO.FileMode]::Open,
            [System.IO.FileAccess]::Read, [System.IO.FileShare]::ReadWrite)
        $reader = [System.IO.StreamReader]::new($stream)
        while (-not $reader.EndOfStream) { $line = $reader.ReadLine() }
    } catch { return $null }
    finally {
        if ($reader) { $reader.Dispose() }
        elseif ($stream) { $stream.Dispose() }
    }
    if (-not $line) { return $null }
    return ($line | ConvertFrom-Json)
}

function Wait-Trace([scriptblock]$predicate, [string]$description) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        $state = Get-LastTrace $script:tracePath
        if ($state -and (& $predicate $state)) { return $state }
        Start-Sleep -Milliseconds 200
    } while ((Get-Date) -lt $deadline)
    throw "Timed out waiting for $description (trace=$script:tracePath)"
}

$oldTraceEnv = $env:JAJUCHA_STATE_TRACE
$env:JAJUCHA_STATE_TRACE = "1"
$existingTrace = Find-TracePath
if ($existingTrace) {
    # Do not let a previous run satisfy the first READY assertion.
    Remove-Item -LiteralPath $existingTrace -Force -ErrorAction SilentlyContinue
}
$process = Start-Process -FilePath $ExePath -ArgumentList @(
    "-screen-fullscreen", "0", "-screen-width", "$Width", "-screen-height", "$Height", "--state-trace"
) -WorkingDirectory (Split-Path -Parent $ExePath) -PassThru
try {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    do {
        Start-Sleep -Milliseconds 250
        $process.Refresh()
    } while ($process.MainWindowHandle -eq 0 -and (Get-Date) -lt $deadline)
    if ($process.MainWindowHandle -eq 0) { throw "Simulator window did not open" }
    $script:inputHandle = $process.MainWindowHandle

    Write-Host ("Simulator window handle: 0x{0:X}" -f $process.MainWindowHandle)

    [JajuchaWin32]::SetForegroundWindow($process.MainWindowHandle) | Out-Null
    $script:tracePath = $null
    do {
        $script:tracePath = Find-TracePath
        if (-not $script:tracePath) { Start-Sleep -Milliseconds 250 }
    } while (-not $script:tracePath -and (Get-Date) -lt $deadline)
    if (-not $script:tracePath) { throw "Runtime state trace was not created" }

    Wait-Trace { param($s) $s.ready -and $s.simulationState -ne "Uninitialized" } "READY state" | Out-Null
    # The trace starts as soon as the runtime is initialized, while Unity's
    # splash can remain on screen for a short period on a cold process start.
    # Give the first capture a deterministic post-splash settle window.
    Start-Sleep -Seconds 2
    Capture-Screenshot $process.MainWindowHandle "01_ready" | Out-Null
    if ($CameraViews) {
        # Capture the authoritative world without the dashboard overlay from
        # several real camera modes. F3 is the same physical key used by a
        # student: Chase -> TopDown -> Free. The screenshots are intentionally
        # kept as separate artifacts so geometry/holes can be inspected at
        # different projection angles.
        $cameraClient = Get-ClientInfo $process.MainWindowHandle
        $cameraScale = [Math]::Min($cameraClient.Width / 1600.0, $cameraClient.Height / 900.0)
        if ($FinalCameraViews) {
            # Drive's first action button is the official preliminary/final
            # selector. Exercise it with a real click before taking the
            # camera views, then restore preliminary so this diagnostic does
            # not change the user's remembered default.
            $stageX = [int]((18.0 + 638.0) * $cameraScale)
            $stageY = [int](158.0 * $cameraScale)
            Click-ClientPixels $process.MainWindowHandle $stageX $stageY
            Wait-Trace { param($s) $s.course.stage -eq "final" } "final course selection" | Out-Null
        }
        $collapseX = [int]((18.0 + 760.0 - 12.0 - 28.0) * $cameraScale)
        $collapseY = [int]((18.0 + 21.0) * $cameraScale)
        Click-ClientPixels $process.MainWindowHandle $collapseX $collapseY
        Wait-Trace { param($s) $s.ui.collapsed -eq $true } "dashboard collapse for camera views" | Out-Null
        Start-Sleep -Milliseconds 500
        Capture-Screenshot $process.MainWindowHandle "02_chase_world" | Out-Null
        Send-Key 0x72 # F3 -> TopDown
        Start-Sleep -Milliseconds 700
        Capture-Screenshot $process.MainWindowHandle "03_topdown_world" | Out-Null
        Send-Key 0x72 # F3 -> Free
        Start-Sleep -Milliseconds 700
        Capture-Screenshot $process.MainWindowHandle "04_free_world" | Out-Null
        Send-Key 0x72 # F3 -> Chase (leave app in its default view)
        Start-Sleep -Milliseconds 250
        if ($FinalCameraViews) {
            # The dashboard is collapsed; expand it, cycle back to
            # preliminary, and leave the persisted preference unchanged.
            Click-ClientPixels $process.MainWindowHandle $collapseX $collapseY
            Wait-Trace { param($s) $s.ui.collapsed -eq $false } "dashboard expand after final views" | Out-Null
            $stageX = [int]((18.0 + 638.0) * $cameraScale)
            $stageY = [int](158.0 * $cameraScale)
            Click-ClientPixels $process.MainWindowHandle $stageX $stageY
            Wait-Trace { param($s) $s.course.stage -eq "preliminary" } "preliminary restore after final views" | Out-Null
        }
        Write-Host "Camera view capture passed." -ForegroundColor Green
        return
    }
    if ($DumpControls) {
        $rootElement = [System.Windows.Automation.AutomationElement]::FromHandle($process.MainWindowHandle)
        $all = $rootElement.FindAll([System.Windows.Automation.TreeScope]::Descendants, [System.Windows.Automation.Condition]::TrueCondition)
        foreach ($element in $all) {
            try {
                $type = $element.Current.ControlType.ProgrammaticName
                if ($type -like '*Button*') {
                    $rect = $element.Current.BoundingRectangle
                    Write-Host ("UIA button '{0}' rect=({1},{2}) {3}x{4}" -f $element.Current.Name, [int]$rect.X, [int]$rect.Y, [int]$rect.Width, [int]$rect.Height)
                }
            } catch { }
        }
    }
    if ($CaptureOnly) {
        Write-Host "READY capture passed. Trace: $script:tracePath" -ForegroundColor Green
        return
    }

    # Header collapse/expand is a real click at the reference-resolution
    # coordinates used by SimulatorDashboardUI.
    Click-ClientPixels $process.MainWindowHandle 590 30
    Wait-Trace { param($s) $s.ui.collapsed -eq $true } "dashboard collapse" | Out-Null
    Click-ClientPixels $process.MainWindowHandle 590 30
    Wait-Trace { param($s) $s.ui.collapsed -eq $false } "dashboard expand" | Out-Null

    # Switch to Course, create a practice copy, and verify the internal origin.
    Click-ClientPixels $process.MainWindowHandle 195 64
    Wait-Trace { param($s) $s.ui.activeTab -eq "Course" } "Course tab" | Out-Null
    Capture-Screenshot $process.MainWindowHandle "02_course_official" | Out-Null
    if ($ProbeCourseCopy) {
        # Also try the real keyboard navigation path after clicking the tab;
        # this distinguishes a disabled Button from a bad mouse coordinate.
        Send-Key 0x09
        Send-Key 0x0D
        Start-Sleep -Milliseconds 300
        $keyboardProbe = Get-LastTrace $script:tracePath
        Write-Host ("Copy keyboard probe: origin={0} readOnly={1}" -f $keyboardProbe.course.origin, $keyboardProbe.course.readOnly)
        if ($keyboardProbe.course.origin -eq "PracticeCopy") { return }
        $probePoints = @(@(100,145), @(100,155), @(100,165), @(125,150), @(125,160), @(150,155))
        $index = 0
        foreach ($point in $probePoints) {
            Click-ClientPixels $process.MainWindowHandle $point[0] $point[1]
            Start-Sleep -Milliseconds 250
            $probe = Get-LastTrace $script:tracePath
            Write-Host ("Copy probe {0}: ({1},{2}) origin={3} readOnly={4}" -f $index, $point[0], $point[1], $probe.course.origin, $probe.course.readOnly)
            Capture-Screenshot $process.MainWindowHandle ("02_probe_{0}" -f $index) | Out-Null
            if ($probe.course.origin -eq "PracticeCopy") { break }
            $index++
        }
        return
    }
    Click-ClientPixels $process.MainWindowHandle 125 160
    Wait-Trace { param($s) $s.course.origin -eq "PracticeCopy" -and -not $s.course.readOnly } "practice copy" | Out-Null
    Capture-Screenshot $process.MainWindowHandle "03_course_practice" | Out-Null

    # F2 must pause and enter top-down edit mode, then restore Drive.
    Send-Key 0x71
    Wait-Trace { param($s) $s.appMode -eq "MapEditor" -and $s.simulationState -eq "Paused" } "F2 map editor" | Out-Null
    if ($EditSmoke) {
        Capture-Screenshot $process.MainWindowHandle "05_map_editor" | Out-Null
        # Course tab is already active. Select Paint Road and click the
        # visible course area with the real mouse, then verify the road mask
        # changes in the event snapshot rather than relying on pixels.
        Click-ClientPixels $process.MainWindowHandle 230 218
        $beforeEdit = Get-LastTrace $script:tracePath
        $edited = $null
        foreach ($point in @(@(1100,600), @(1000,600), @(900,600), @(1100,500), @(800,500))) {
            Move-ClientPixels $process.MainWindowHandle $point[0] $point[1]
            Click-ClientPixels $process.MainWindowHandle $point[0] $point[1]
            Start-Sleep -Milliseconds 250
            $candidateEdit = Get-LastTrace $script:tracePath
            if ($candidateEdit -and $candidateEdit.course.roadTiles -ne $beforeEdit.course.roadTiles) {
                $edited = $candidateEdit
                break
            }
        }
        if (-not $edited) { throw "Timed out waiting for road paint edit" }
        if ($EditProbe) {
            Capture-Screenshot $process.MainWindowHandle "06_map_editor_edited" | Out-Null
            Scroll-ClientPixels $process.MainWindowHandle 300 380 -1200
            Capture-Screenshot $process.MainWindowHandle "07_course_scrolled" | Out-Null
            return
        }
        # The enlarged small-resolution layout keeps the action row visible;
        # exercise undo/redo through the actual mouse buttons as a player does.
        Click-ClientPixels $process.MainWindowHandle 75 510
        $undone = Wait-Trace { param($s) $s.recordType -eq "course_changed" -and $s.course.roadTiles -eq $beforeEdit.course.roadTiles } "road undo"
        Click-ClientPixels $process.MainWindowHandle 178 510
        $redone = Wait-Trace { param($s) $s.recordType -eq "course_changed" -and $s.course.roadTiles -eq $edited.course.roadTiles } "road redo"
        Capture-Screenshot $process.MainWindowHandle "06_map_editor_edited" | Out-Null

        if ($CourseLifecycleSmoke) {
            # Exercise the real dashboard buttons for test-drive restore,
            # practice save, and reload.  The trace compares the document
            # hash before/after the temporary drive instead of relying on
            # the rendered map alone.
            $beforeTestDrive = Get-LastTrace $script:tracePath
            $practiceDir = Join-Path $env:USERPROFILE "AppData\LocalLow\DefaultCompany\jajucha-sim\JajuchaSim\Courses\Practice"
            $beforeFiles = if (Test-Path -LiteralPath $practiceDir) { @(Get-ChildItem -LiteralPath $practiceDir -Filter "practice_2026_*.json") } else { @() }
            Click-ClientPixels $process.MainWindowHandle 386 510
            Wait-Trace { param($s) $s.course.testDriveActive -eq $true -and $s.appMode -eq "Drive" } "practice test-drive start" | Out-Null
            Click-ClientPixels $process.MainWindowHandle 386 510
            $restored = Wait-Trace { param($s) $s.course.testDriveActive -eq $false -and $s.appMode -eq "MapEditor" } "practice test-drive restore"
            if ($restored.course.documentHash -ne $beforeTestDrive.course.documentHash) {
                throw "Test-drive restore changed the in-memory document hash"
            }
            Click-ClientPixels $process.MainWindowHandle 507 510
            Start-Sleep -Milliseconds 500
            $afterFiles = if (Test-Path -LiteralPath $practiceDir) { @(Get-ChildItem -LiteralPath $practiceDir -Filter "practice_2026_*.json") } else { @() }
            if ($afterFiles.Count -le $beforeFiles.Count) { throw "Practice save did not create a numbered course file" }
            Click-ClientPixels $process.MainWindowHandle 500 466
            Wait-Trace { param($s) $s.course.origin -eq "PracticeCopy" -and $s.course.readOnly -eq $false } "practice course reload" | Out-Null
            Capture-Screenshot $process.MainWindowHandle "09_course_lifecycle" | Out-Null
        }
    }
    Send-Key 0x71
    Wait-Trace { param($s) $s.appMode -eq "Drive" -and $s.simulationState -eq "Running" } "F2 drive mode" | Out-Null

    # Configure the first fixed mission using the visible Drive controls.
    Click-ClientPixels $process.MainWindowHandle 75 64
    Wait-Trace { param($s) $s.ui.activeTab -eq "Drive" } "Drive tab" | Out-Null
    Click-ClientPixels $process.MainWindowHandle 510 158
    Click-ClientPixels $process.MainWindowHandle 510 190
    Click-ClientPixels $process.MainWindowHandle 510 220
    Wait-Trace { param($s) $s.session -and $s.course.stage -ne "" } "mission controls" | Out-Null
    Capture-Screenshot $process.MainWindowHandle "04_drive_configured" | Out-Null

    if ($SensorSmoke) {
        # Open the integrated sensor tab and capture the three RenderTexture
        # previews.  The bridge sensor smoke test separately validates raw
        # bytes; this proves the same textures are wired into the dashboard.
        Click-ClientPixels $process.MainWindowHandle 480 64
        Wait-Trace { param($s) $s.ui.activeTab -eq "Sensors" } "Sensors tab" | Out-Null
        Start-Sleep -Milliseconds 500
        Capture-Screenshot $process.MainWindowHandle "08_sensors_dashboard" | Out-Null
    }

    Write-Host "UI smoke test passed. Trace: $script:tracePath" -ForegroundColor Green
}
finally {
    if (-not $KeepOpen -and $process -and -not $process.HasExited) {
        $process.CloseMainWindow() | Out-Null
        Start-Sleep -Milliseconds 500
        if (-not $process.HasExited) { $process.Kill() }
    }
    if ($null -eq $oldTraceEnv) { Remove-Item Env:JAJUCHA_STATE_TRACE -ErrorAction SilentlyContinue }
    else { $env:JAJUCHA_STATE_TRACE = $oldTraceEnv }
}
