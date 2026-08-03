<#
.SYNOPSIS
  Supervisor that sits ABOVE run-implementation-plan.ps1.

  It repeatedly:
    1. Runs the implementation-plan runner (capturing all output live + to a
       log file so nothing is lost).
    2. Detects when the runner is "down" -- i.e. it exited with a non-zero
       code OR its output contains recognizable error signatures (a crashed
       pi session, a native-command error, an "Unknown option" parse failure,
       a missing command, etc.).
    3. Extracts the relevant error context (the crash message + surrounding
       lines) from the runner log.
    4. Pastes that error into a FRESH, non-interactive pi session asking pi
       to diagnose and fix the root cause in the repository (typically
       run-implementation-plan.ps1 / pi-runner-settings.json / a task file /
       the pi invocation), and to test the fix.
    5. Re-runs the runner.

  The loop repeats until the runner exits cleanly (code 0 with no error
  signatures) or until MaxAttempts is exhausted.

  This supervisor is deliberately SELF-CONTAINED: it does not dot-source the
  runner, so it keeps working even when the runner script itself is what broke.

.PARAMETER Runner
  Path to the implementation-plan runner script.
  Default: .\run-implementation-plan.ps1

.PARAMETER RunnerArgs
  Extra arguments to forward to the runner (as a single string).
  Example: -RunnerArgs "-Force"

.PARAMETER LogDir
  Where supervisor + runner logs are written.
  Default: .\.pi-plan-logs

.PARAMETER StateDir
  Where the runner keeps task state (done markers). Used only so the fix
  prompt can tell pi NOT to clobber completed work.
  Default: .\.pi-plan-state

.PARAMETER FixModel
  Optional model pattern for the fix session. If blank, pi uses its default.

.PARAMETER MaxAttempts
  Maximum number of (run -> detect -> fix -> re-run) cycles.
  Default: 10. 0 = unlimited (not recommended).

.PARAMETER RestartDelaySeconds
  Seconds to wait between a failed runner attempt and the fix session, and
  between the fix session and the next runner attempt.
  Default: 3

.PARAMETER MaxRunnerSeconds
  Hard wall-clock limit per single runner run. 0 = unlimited.
  Default: 0 (the runner has its own per-session timeouts).

.PARAMETER MaxFixSessionSeconds
  Hard wall-clock limit per pi fix session.
  Default: 1800

.PARAMETER ErrorPatterns
  Optional regex array overriding the default signatures used to decide the
  runner is "down".

.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\supervise-implementation-plan.ps1
.EXAMPLE
  powershell -ExecutionPolicy Bypass -File .\supervise-implementation-plan.ps1 `
            -FixModel "opencode-go/deepseek-v4-flash" -MaxAttempts 8

.NOTES
  Requires: `pi` on PATH, `run-implementation-plan.ps1` present.
  Author: pi supervisor harness.
#>
[CmdletBinding()]
param(
    [string]$Runner        = ".\run-implementation-plan.ps1",
    [string]$RunnerArgs    = "",
    [string]$LogDir        = ".\.pi-plan-logs",
    [string]$StateDir      = ".\.pi-plan-state",
    [string]$FixModel      = "",
    [int]   $MaxAttempts   = 10,
    [int]   $RestartDelaySeconds = 3,
    [int]   $MaxRunnerSeconds   = 0,
    [int]   $MaxFixSessionSeconds = 1800,
    [string[]]$ErrorPatterns = @(),
    [switch]$SelfTest
)

$ErrorActionPreference = "Continue"
$ProgressPreference     = "SilentlyContinue"

# ===========================================================================
# Small helpers (self-contained; do NOT depend on the runner)
# ===========================================================================

function Write-Section2($t) {
    Write-Host ""
    Write-Host ("=" * 78) -ForegroundColor Black
    Write-Host $t -ForegroundColor White
    Write-Host ("=" * 78) -ForegroundColor Black
}
function Write-Step2($t) { Write-Host "[>] $t" -ForegroundColor Cyan }
function Write-Ok2($t)    { Write-Host "[OK] $t" -ForegroundColor Green }
function Write-Fail2($t)  { Write-Host "[!!] $t" -ForegroundColor Red }
function Write-Info2($t)  { Write-Host "[i] $t" -ForegroundColor DarkGray }

function Get-Stamp { Get-Date -Format "yyyyMMdd-HHmmss" }

function New-Utf8NoBom { New-Object System.Text.UTF8Encoding $false }

# Default error signatures that mark a runner run as "down". Case-insensitive.
# These catch pi option-parse errors, native-command crashes, missing pi,
# and the runner's own "Pi exited with code N" failure line.
if ($null -eq $ErrorPatterns -or $ErrorPatterns.Count -eq 0) {
    $ErrorPatterns = @(
        "Unknown option",
        "Error:\s",
        "NativeCommandError",
        "RemoteException\]",
        "FullyQualifiedErrorId",
        "Pi exited with code [^0]",
        "Pi exited with code -",
        "is not recognized as",
        "The term '\w+' is not recognized",
        "A positional parameter cannot be found",
        "Cannot bind argument",
        "ObjectNotFound",
        "CommandNotFoundException",
        "ParameterBindingException",
        "ParserError",
        "reports an error to be shown",
        "RuntimeException"
    )
}

# ===========================================================================
# Run the runner as a child process with LIVE + captured output.
# Returns @{ ExitCode; LogPath }.
# ===========================================================================
function Invoke-Runner {
    param(
        [Parameter(Mandatory)][string]$ScriptPath,
        [string]$ExtraArgs,
        [Parameter(Mandatory)][string]$LogPath,
        [int]$TimeoutSeconds = 0
    )

    # Build argument list for a new powershell.exe that runs the runner.
    # Using a fresh powershell.exe keeps the runner's $LASTEXITCODE / state
    # isolated and lets us kill the whole tree on timeout.
    $psArgs = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $ScriptPath)
    if ($ExtraArgs -and $ExtraArgs.Trim().Length -gt 0) {
        $psArgs += $ExtraArgs.Trim() -split '(\s+)' |
            Where-Object { $_ -and $_.Trim().Length -gt 0 }
    }

    $utf8NoBom = New-Utf8NoBom
    $log = New-Object System.IO.StreamWriter $LogPath, $false, $utf8NoBom
    $log.AutoFlush = $true
    $syncLog = [System.IO.StreamWriter]::Synchronized($log)

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName               = "powershell.exe"
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError  = $true
    $psi.UseShellExecute        = $false
    $psi.CreateNoWindow         = $true
    # passthrough env
    $psi.EnvironmentVariables["PI_OFFLINE"] = $env:PI_OFFLINE

    # Join args honoring quoting (simple space join is fine because we tokenized).
    $psi.Arguments = ($psArgs | ForEach-Object {
        if ($_ -match '\s') { "`"$_`"" } else { $_ }
    }) -join ' '

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo = $psi

    # Track whether we force-killed it.
    $script:killedByTimeout = $false

    $outLines = New-Object System.Collections.Generic.List[string]
    $errLines = New-Object System.Collections.Generic.List[string]

    # NOTE: event-handler scriptblocks execute in their OWN scope and do NOT
    # capture variables from this scope. We pass the synchronized writer via
    # -MessageData and reach it through $Event.MessageData inside the handler.
    $onOut = {
        if ($EventArgs.Data -ne $null) {
            $line = $EventArgs.Data
            $Event.MessageData.WriteLine($line)
            Write-Host $line
        }
    }
    $onErr = {
        if ($EventArgs.Data -ne $null) {
            $line = $EventArgs.Data
            # stderr lines often are the actual error -> highlight faintly.
            $Event.MessageData.WriteLine($line)
            Write-Host $line -ForegroundColor DarkYellow
        }
    }

    # Register via events (PowerShell-friendly).
    $outEvt = Register-ObjectEvent -InputObject $proc -EventName "OutputDataReceived" -Action $onOut -MessageData $syncLog
    $errEvt = Register-ObjectEvent -InputObject $proc -EventName "ErrorDataReceived"  -Action $onErr -MessageData $syncLog

    $null = $proc.Start()
    $proc.BeginOutputReadLine()
    $proc.BeginErrorReadLine()

    $watchStart = Get-Date
    $timedOut = $false
    while (-not $proc.HasExited) {
        if ($TimeoutSeconds -gt 0 -and ((Get-Date) - $watchStart).TotalSeconds -gt $TimeoutSeconds) {
            $timedOut = $true
            break
        }
        Start-Sleep -Milliseconds 200
    }

    if ($timedOut -and -not $proc.HasExited) {
        $script:killedByTimeout = $true
        Write-Fail2 "Runner exceeded ${TimeoutSeconds}s; terminating runner process tree."
        try { $proc.Kill() } catch {}
        try { $proc.WaitForExit(5000) | Out-Null } catch {}
        # Give async readers a moment to flush.
        Start-Sleep -Milliseconds 300
    } else {
        # Ensure exit + flush remaining buffers.
        $proc.WaitForExit() | Out-Null
        Start-Sleep -Milliseconds 200
    }

    Unregister-Event -SourceIdentifier $outEvt.Name -ErrorAction SilentlyContinue
    Unregister-Event -SourceIdentifier $errEvt.Name -ErrorAction SilentlyContinue

    $code = if ($script:killedByTimeout) { 124 } else { $proc.ExitCode }
    $log.Close()

    return @{ ExitCode = $code; LogPath = $LogPath; TimedOut = $script:killedByTimeout }
}

# ===========================================================================
# Scan a log file for error signatures; return extracted context (string).
# ===========================================================================
function Get-ErrorContext {
    param([string]$LogPath)

    if (-not (Test-Path -LiteralPath $LogPath)) { return "" }
    $lines = Get-Content -LiteralPath $LogPath -ErrorAction SilentlyContinue
    if (-not $lines) { return "" }

    $matchIdx = New-Object System.Collections.Generic.List[int]
    for ($i = 0; $i -lt $lines.Count; $i++) {
        foreach ($p in $ErrorPatterns) {
            if ($lines[$i] -match $p) { $matchIdx.Add($i); break }
        }
    }

    # Always include the trailing 30 lines of the log -- errors are often
    # the very last thing emitted.
    $tailCount = [Math]::Min(30, $lines.Count)
    for ($i = $lines.Count - $tailCount; $i -lt $lines.Count; $i++) { $matchIdx.Add($i) }

    $unique = ($matchIdx | Sort-Object -Unique | Where-Object { $_ -ge 0 })
    $window = 3   # context lines around each match
    $keep = New-Object 'System.Collections.Generic.HashSet[int]'
    foreach ($idx in $unique) {
        $lo = [Math]::Max(0, $idx - $window)
        $hi = [Math]::Min($lines.Count - 1, $idx + $window)
        for ($j = $lo; $j -le $hi; $j++) { $null = $keep.Add($j) }
    }

    $ordered = $keep | Sort-Object
    $sb = New-Object System.Text.StringBuilder
    $prev = -2
    foreach ($k in $ordered) {
        if ($k -gt $prev + 1) { $null = $sb.AppendLine("...") }
        $null = $sb.AppendLine($lines[$k])
        $prev = $k
    }

    $cap = 6000
    $s = $sb.ToString()
    if ($s.Length -gt $cap) { $s = $s.Substring(0, $cap) + "...[truncated]" }
    return $s
}

# ===========================================================================
# Decide whether a runner run was "down".
# ===========================================================================
function Test-RunnerDown {
    param([int]$ExitCode, [string]$ErrorContext)
    if ($ExitCode -ne 0) { return $true }
    if ($ErrorContext -and $ErrorContext.Trim().Length -gt 0) {
        foreach ($p in $ErrorPatterns) {
            if ($ErrorContext -match $p) { return $true }
        }
    }
    return $false
}

# ===========================================================================
# Run a FRESH pi fix session. The prompt is delivered via a UTF-8 file and
# the @file convention (same reason as the runner: multi-line + tokens that
# start with '-' must not be argv-split by PowerShell's native quoting).
# Renders JSON events live so the user sees pi working.
# ===========================================================================
function Invoke-PiFixSession {
    param(
        [string]$SessionName,
        [string]$Prompt,
        [string]$LogPath,
        [string]$Model = "",
        [int]$TimeoutSeconds = 1800
    )

    Write-Step2 "Pi FIX session: $SessionName"
    Write-Step2 "Fix log: $LogPath"

    $promptFile = $LogPath + ".prompt.txt"
    $utf8NoBom = New-Utf8NoBom
    [System.IO.File]::WriteAllText($promptFile, $Prompt, $utf8NoBom)

    $piArgs = @()
    if ($Model) { $piArgs += @("--model", $Model) }
    $piArgs += @("--name", $SessionName, "--mode", "json", "-p", "@$promptFile")

    $log = New-Object System.IO.StreamWriter $LogPath, $false, $utf8NoBom
    $log.AutoFlush = $true

    $startTime = Get-Date
    $timedOut = $false

    try {
        & pi @piArgs 2>&1 | ForEach-Object {
            $raw = if ($_ -is [System.Management.Automation.ErrorRecord]) {
                $_.Exception.Message
            } else {
                $_.ToString()
            }
            $log.WriteLine($raw)

            if ($TimeoutSeconds -gt 0 -and ((Get-Date) - $startTime).TotalSeconds -gt $TimeoutSeconds) {
                if (-not $timedOut) { $timedOut = $true; Write-Fail2 "Fix session exceeded ${TimeoutSeconds}s; terminating." }
                return
            }

            $evt = $null
            try { $evt = $raw | ConvertFrom-Json -ErrorAction Stop } catch {
                Write-Host $raw -ForegroundColor DarkGray; return
            }
            $t = $evt.type
            switch ($t) {
                "session"              { Write-Host "[session] $($evt.id)" -ForegroundColor DarkGray }
                "agent_start"          { Write-Host "==== AGENT START ====" -ForegroundColor Magenta }
                "agent_end"            { Write-Host "====  AGENT END  ====" -ForegroundColor Magenta }
                "tool_execution_start" {
                    $a = $evt.args
                    $as = if ($a) { ($a.PSObject.Properties | ForEach-Object { "$($_.Name)=$($_.Value)" }) -join '; ' } else { "" }
                    Write-Host ("  > {0}  |  {1}" -f $evt.toolName, $as) -ForegroundColor Cyan
                }
                "tool_execution_end" {
                    if ($evt.isError) {
                        $r = if ($evt.result) { ($evt.result | Out-String) } else { "" }
                        Write-Host ("  ! {0} FAILED" -f $evt.toolName) -ForegroundColor Red
                        $r | To-LimitedLine 500 | Where-Object { $_ } | ForEach-Object { Write-Host "    $_" -ForegroundColor DarkRed }
                    } else {
                        Write-Host ("  < {0} ok" -f $evt.toolName) -ForegroundColor Green
                    }
                }
                "message_update" {
                    $ae = $evt.assistantMessageEvent
                    if ($ae -and $ae.type -eq "text_delta" -and $ae.delta) {
                        Write-Host $ae.delta -NoNewline -ForegroundColor White
                    } elseif ($ae -and $ae.type -eq "text_end") {
                        Write-Host ""
                    }
                }
                default { <# other events: logged but not rendered #> }
            }
        }
    }
    finally { $log.Close() }

    $code = $LASTEXITCODE
    if ($timedOut) { $code = 124 }

    if ($code -ne 0) {
        Write-Fail2 "Fix pi session exited code $code (see $LogPath)"
        return $false
    }
    return $true
}

# A tiny pipeline helper to cap tool-result output line length.
filter To-LimitedLine($n) {
    if ($null -eq $_) { return }
    $s = [string]$_
    if ($s.Length -gt $n) { $s.Substring(0, $n) + "..." } else { $s }
}

# ===========================================================================
# Build the fix prompt from the error context.
# ===========================================================================
function New-FixPrompt {
    param(
        [int]$Attempt,
        [int]$ExitCode,
        [bool]$TimedOut,
        [string]$ErrorContext,
        [string]$RunnerPath,
        [string]$SettingsFile,
        [string]$PlanDir,
        [string]$StateDir
    )

    $timeoutNote = if ($TimedOut) { "The runner was force-killed because it exceeded the supervisor time limit (it likely HUNG)." } else { "" }

    $repo = (Get-Location).Path

    $p = @"
You are the FIX-AND-RESUME agent for an automated implementation-plan runner.
You run AFTER the runner crashed. Your ONLY job is to diagnose and FIX the
root cause that took the runner down, then verify the fix is real. Do NOT
implement the implementation plan itself -- that is the runner's job.

REPOSITORY: $repo

COMPONENTS OF THE RUNNER SYSTEM (inspect these, in priority order):
  - Runner script:        $RunnerPath
  - Runner settings:      $SettingsFile
  - Plan task files:      $PlanDir
  - Runner state dir:     $StateDir  (do NOT delete or alter *.done /
                            *.verification.json markers there)
  - The `pi` CLI itself    (run `pi --help` to confirm option semantics)

FAILURE REPORT (attempt $Attempt of this supervisor loop):
  - Runner exit code:     $ExitCode   (0 = clean success; the runner only
                            exits 0 when EVERY plan task is verified complete
                            AND the final audit passes. Any non-zero is a
                            real failure.)
  $timeoutNote

  - Extracted error context (from the runner's captured stdout/stderr; `...`
    means lines were elided for brevity):
---------------- ERROR CONTEXT START ----------------
$ErrorContext
----------------  ERROR CONTEXT END  ----------------

DIAGNOSIS & FIX PROCEDURE:
  1. Read the ERROR CONTEXT above first. It usually names the exact failure
     (e.g. "Unknown option: -30", "The term 'pi' is not recognized",
     "Pi exited with code N", a PowerShell parse error, an unhandled
     exception, a model/provider rejection, etc.).
  2. Inspect the relevant component file(s) listed above. Do NOT guess --
     read them. Common root causes include:
       * Argument-quoting bugs when invoking native `pi` from PowerShell
         5.1 (multi-line strings or tokens starting with '-' getting
         argv-split -- these surface as "Unknown option: -NN").
       * Wrong / outdated pi CLI option names or option-argument shapes
         (check the installed `pi --help`).
       * Malformed pi-runner-settings.json (bad model patterns, bad types).
       * A task file containing text that, when interpolated into a prompt,
         breaks the pi command line -- fix the DELIVERY, not the task text.
       * Deleted/renamed files referenced by the runner.
       * Model/provider config issues (auth, unknown model, etc.).
  3. Make the MINIMAL change that fixes the ACTUAL root cause. Avoid
     unrelated refactors. Do NOT weaken or skip the runner's checks.
  4. TEST the fix concretely before stopping:
       - Re-run the specific failing piece in isolation (e.g. reproduce the
         exact `pi` argument vector the runner builds, or run a one-off
         `pi -p` invocation that previously errored).
       - Best: run the runner itself for a SHORT, bounded window to confirm
         it now gets past the crash. If the runner is long-running, at least
         confirm the previously-failing step now succeeds.
       - If you ran the runner as a test, that is great -- the supervisor
         will run it again fully afterward; you do not need to run it to
         completion.
  5. Do NOT touch plan state in $StateDir unless the crash itself corrupted
     a state file; even then, only repair, never erase completed work.
  6. Keep your edits to the runner-system files (runner script, settings,
     and -- only if truly necessary -- task file wording or pi config).

FINAL REPORT (concise, factual):
  - Root cause identified (one paragraph).
  - Exact file(s) and line(s) changed, and the change made.
  - The test(s) you ran to PROVE the crash is gone, with their output.
  - Whether the runner is now expected to proceed.

Do NOT claim success based on code inspection alone -- re-run the failing
invocation and show the result.
"@
    return $p
}

# ===========================================================================
# Pre-flight checks
# ===========================================================================

Write-Section2 "PI RUNNER SUPERVISOR"

if ($SelfTest) {
    Write-Host "(self-test mode)" -ForegroundColor DarkGray
    $probe = Join-Path $env:TEMP ("pi-sup-selftest-" + (Get-Stamp) + ".log")
    $probeLines = @(
        "TASK 5 / 11: 04.txt",
        "[>] Model: opencode-go/deepseek-v4-flash",
        "[>] Starting fresh Pi session: plan-5-04-implement",
        "node.exe : Error: Unknown option: -30",
        "    + CategoryInfo          : NotSpecified: (Error: Unknown option: -30:String) [], RemoteException",
        "    + FullyQualifiedErrorId : NativeCommandError",
        "[!!] Pi exited with code 1. See .\.pi-plan-logs\005-04-implement-20260726-191842.log"
    )
    $probeLines | Set-Content -LiteralPath $probe -Encoding UTF8
    $ctx = Get-ErrorContext -LogPath $probe
    Write-Host "--- extracted error context ---" -ForegroundColor DarkGray
    Write-Host $ctx
    Write-Host "--- /context ---" -ForegroundColor DarkGray
    $down = Test-RunnerDown -ExitCode 1 -ErrorContext $ctx
    $c1 = if ($down) { 'Green' } else { 'Red' }
    Write-Host ("Test-RunnerDown(exit 1) => " + $down) -ForegroundColor $c1
    $down0 = Test-RunnerDown -ExitCode 0 -ErrorContext ""
    $c0 = if (-not $down0) { 'Green' } else { 'Red' }
    Write-Host ("Test-RunnerDown(exit 0, no ctx) => " + $down0) -ForegroundColor $c0
    $fp = New-FixPrompt -Attempt 1 -ExitCode 1 -TimedOut $false -ErrorContext $ctx `
        -RunnerPath (Resolve-Path $Runner) -SettingsFile ".\pi-runner-settings.json" `
        -PlanDir ".\implementation_plan" -StateDir $StateDir
    $hasKey = ($fp -match 'Unknown option: -30')
    $ck = if ($hasKey) { 'Green' } else { 'Red' }
    Write-Host ("New-FixPrompt contains error token => " + $hasKey) -ForegroundColor $ck
    Remove-Item -LiteralPath $probe -Force -ErrorAction SilentlyContinue
    if ($down -and -not $down0 -and $hasKey) { Write-Ok2 "SELF-TEST PASSED"; exit 0 } else { Write-Fail2 "SELF-TEST FAILED"; exit 1 }
}

if (-not (Test-Path -LiteralPath $Runner)) {
    throw "Runner script not found: $Runner"
}
if (-not (Get-Command pi -ErrorAction SilentlyContinue)) {
    throw "The 'pi' command was not found on PATH."
}
$null = New-Item -ItemType Directory -Force -Path $LogDir  -ErrorAction SilentlyContinue
$null = New-Item -ItemType Directory -Force -Path $StateDir -ErrorAction SilentlyContinue

Write-Info2 "Runner        : $Runner"
Write-Info2 "Runner args   : '$RunnerArgs'"
Write-Info2 "Fix model     : $(if ($FixModel) { $FixModel } else { '<pi default>' })"
Write-Info2 "Max attempts  : $(if ($MaxAttempts -gt 0) { $MaxAttempts } else { 'unlimited' })"
Write-Info2 "Runner timeout: $(if ($MaxRunnerSeconds -gt 0) { $MaxRunnerSeconds.ToString() + 's' } else { 'unlimited' })"
Write-Info2 "Fix timeout   : $MaxFixSessionSeconds s"
Write-Info2 "Error patterns: $($ErrorPatterns.Count) loaded"

$settingsFile = ".\pi-runner-settings.json"
$planDir      = ".\implementation_plan"

# ===========================================================================
# Main loop
# ===========================================================================
$attempt    = 0
$lastFixOk  = $false
$cleanRun   = $false

while ($true) {
    $attempt++
    if ($MaxAttempts -gt 0 -and $attempt -gt $MaxAttempts) {
        Write-Fail2 "Exhausted MaxAttempts=$MaxAttempts. Stopping supervisor."
        break
    }

    Write-Section2 "SUPERVISOR ATTEMPT $attempt $(if ($MaxAttempts -gt 0){"of $MaxAttempts"})"

    $stamp = Get-Stamp
    $runLog = Join-Path $LogDir ("sup-run-{0:D2}-{1}.log" -f $attempt, $stamp)

    Write-Step2 "Launching runner: $Runner"
    Write-Step2 "Runner log: $runLog"
    Write-Host ""

    $res = Invoke-Runner -ScriptPath $Runner -ExtraArgs $RunnerArgs -LogPath $runLog -TimeoutSeconds $MaxRunnerSeconds

    Write-Host ""
    $code = [int]$res.ExitCode
    Write-Info2 "Runner exit code: $code$(if ($res.TimedOut) { ' (timed out)' })"

    $ctx = Get-ErrorContext -LogPath $runLog
    $down = Test-RunnerDown -ExitCode $code -ErrorContext $ctx

    if (-not $down) {
        Write-Ok2 "Runner finished clean (exit 0, no error signatures). Plan complete."
        $cleanRun = $true
        break
    }

    Write-Fail2 "Runner is DOWN (exit $code). Extracting error context..."
    if ($ctx.Trim().Length -eq 0) {
        $ctx = "(No signature-matched lines found. Inspect the full runner log: $runLog)"
    }

    if ($RestartDelaySeconds -gt 0) { Start-Sleep -Seconds $RestartDelaySeconds }

    # ---- Fix session ----
    $fixLog = Join-Path $LogDir ("sup-fix-{0:D2}-{1}.log" -f $attempt, (Get-Stamp))
    $fixName = "supervisor-fix-attempt-$attempt"

    $fixPrompt = New-FixPrompt `
        -Attempt $attempt -ExitCode $code -TimedOut $res.TimedOut `
        -ErrorContext $ctx `
        -RunnerPath (Resolve-Path $Runner) `
        -SettingsFile $settingsFile `
        -PlanDir $planDir `
        -StateDir $StateDir

    $null = Invoke-PiFixSession `
        -SessionName $fixName `
        -Prompt $fixPrompt `
        -LogPath $fixLog `
        -Model $FixModel `
        -TimeoutSeconds $MaxFixSessionSeconds

    Write-Step2 "Fix session finished. Re-running the runner."
    if ($RestartDelaySeconds -gt 0) { Start-Sleep -Seconds $RestartDelaySeconds }
}

Write-Section2 "SUPERVISOR DONE"
if ($cleanRun) {
    Write-Ok2 "Implementation plan completed cleanly (runner exit 0)."
    exit 0
} else {
    Write-Fail2 "Supervisor stopped without a clean runner success after $attempt attempt(s)."
    exit 1
}