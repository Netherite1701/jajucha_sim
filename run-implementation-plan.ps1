<#
.SYNOPSIS
    Autonomous implementation-plan runner for the Pi coding agent.

.DESCRIPTION
    - Reads task files from ./implementation_plan
    - Runs EACH TASK in a fresh Pi session
    - Uses a separate fresh Pi verification/fix session after implementation
    - Requires thorough testing before a task can be considered complete
    - Repeats verification/fixing until PASS
    - Persists state so interrupted runs can resume
    - Continues until all plan tasks are complete

.REQUIREMENTS
    - `pi` available on PATH
    - Run from the repository root
    - Your Pi provider/model configuration is already working

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File .\run-implementation-plan.ps1

    # Re-run even tasks already marked complete:
    powershell -ExecutionPolicy Bypass -File .\run-implementation-plan.ps1 -Force

    # Change task directory:
    powershell -ExecutionPolicy Bypass -File .\run-implementation-plan.ps1 -PlanDir .\implementation_plan

.NOTES
    Plan ordering:
      1. Numeric prefixes are recommended:
         01-foundation.md
         02-vehicle.md
         03-tests.md
      2. Otherwise files are processed by name.

    A task is NOT complete merely because Pi exits successfully.
    The verifier must write:
        .pi-plan-state/<task>.verification.json
    with:
        "status": "pass"
#>

[CmdletBinding()]
param(
    [string]$PlanDir = ".\implementation_plan",
    [string]$StateDir = ".\.pi-plan-state",
    [string]$LogDir = ".\.pi-plan-logs",

    # Prevent a permanently impossible task from burning API usage forever.
    # Set to 0 for literally unlimited verification/fix attempts.
    # -1 (default) means: read from pi-runner-settings.json (or 8 if absent).
    [int]$MaxVerificationAttempts = -1,

    # Seconds to wait between failed verification retries.
    # -1 (default) means: read from pi-runner-settings.json (or 5 if absent).
    [int]$RestartDelaySeconds = -1,

    # Hard wall-clock limit for a SINGLE Pi session (implement / verify /
    # pre-flight / final-audit). Protects against a looping agent that would
    # otherwise run forever and fill the disk. 0 = unlimited.
    # -1 (default) means: read from pi-runner-settings.json (or 1800 if absent).
    [int]$MaxSessionSeconds = -1,

    # Maximum total bytes written to a single session log file. Once exceeded,
    # logging stops (console rendering continues) so a runaway tool result or
    # looping agent cannot grow the log without bound. 0 = unlimited.
    # -1 (default) means: read from pi-runner-settings.json (or 262144000
    # = 250 MiB if absent).
    [int]$MaxLogBytes = -1,

    # Maximum characters of a single output line saved to the log. Any one
    # NDJSON line (e.g. a tool result embedding a whole file) longer than this
    # is truncated before writing, so one giant line cannot blow up the log.
    [int]$MaxLogLineChars = 100000,

    [string]$SettingsFile = ".\pi-runner-settings.json",

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Encoding: critical for parsing Pi's JSON event stream.
# ---------------------------------------------------------------------------
# Pi (--mode json) writes UTF-8 JSON to stdout. By default, Windows PowerShell
# decodes native-command output using the system code page (e.g. cp949 on
# Korean Windows), which corrupts any line containing non-ASCII bytes. Those
# corrupted lines then FAIL ConvertFrom-Json and fall through to raw-line
# printing, leaking whole JSON blobs into the live text and showing '?'
# garbage instead of em-dashes / other Unicode characters.
#
# Force UTF-8 for both directions so JSON parsing is reliable.
try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding           = [System.Text.Encoding]::UTF8
    [System.Console]::InputEncoding = [System.Text.Encoding]::UTF8
} catch {
    # Some hosts don't allow InputEncoding assignment; ignore.
}

# NOTE on log writing below: Invoke-PiSession opens each session log with a
# single System.IO.StreamWriter configured as UTF-8 (no BOM). This is
# deliberate and required on Windows PowerShell 5.1, where the build-in
# `Add-Content` cmdlet defaults to the system ANSI codepage (cp949 on Korean
# Windows) and would corrupt every non-ASCII byte in the saved NDJSON.

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Section {
    param([string]$Title)

    Write-Host ""
    Write-Host ("=" * 78) -ForegroundColor DarkGray
    Write-Host $Title -ForegroundColor Cyan
    Write-Host ("=" * 78) -ForegroundColor DarkGray
}

function Write-Step {
    param([string]$Text)
    Write-Host "[>] $Text" -ForegroundColor Yellow
}

function Write-Ok {
    param([string]$Text)
    Write-Host "[OK] $Text" -ForegroundColor Green
}

function Write-Fail {
    param([string]$Text)
    Write-Host "[FAIL] $Text" -ForegroundColor Red
}

function Get-SafeName {
    param([string]$Name)

    $safe = [System.IO.Path]::GetFileNameWithoutExtension($Name)
    $safe = $safe -replace '[^A-Za-z0-9._-]', '_'
    return $safe
}

function Get-TaskFiles {
    param([string]$Directory)

    $resolved = Resolve-Path $Directory -ErrorAction Stop

    $files = Get-ChildItem -Path $resolved -File |
        Where-Object {
            $_.Extension -in @(".md", ".txt", ".task", ".plan")
        } |
        Sort-Object Name

    return @($files)
}

function Read-Verification {
    param([string]$VerificationPath)

    if (-not (Test-Path $VerificationPath)) {
        return $null
    }

    try {
        # Force UTF-8: verification records are written as UTF-8 by Pi and may
        # contain non-ASCII (em-dashes, units, etc.). Without -Encoding,
        # Windows PowerShell decodes using the system ANSI codepage (e.g.
        # cp949 on Korean Windows), which corrupts multibyte UTF-8 bytes and
        # silently makes ConvertFrom-Json fail -- causing the runner to treat
        # already-complete steps as NOT done and re-implement them.
        return Get-Content $VerificationPath -Raw -Encoding UTF8 | ConvertFrom-Json
    }
    catch {
        Write-Fail "Verification record is invalid JSON: $VerificationPath"
        return $null
    }
}

function Test-VerificationPassed {
    param([object]$Verification)

    if ($null -eq $Verification) {
        return $false
    }

    if (-not ($Verification.PSObject.Properties.Name -contains "status")) {
        return $false
    }

    return ([string]$Verification.status).ToLowerInvariant() -eq "pass"
}

#---------------------------------------------------------------------------
# JSON-event rendering helpers (for --mode json output)
#---------------------------------------------------------------------------

# Safe property accessor that respects Set-StrictMode -Version Latest.
function Get-SafeField {
    param($Obj, [string]$Name)

    if ($null -eq $Obj) { return $null }
    if ($Obj -is [string] -or $Obj -is [int] -or $Obj -is [bool] -or $Obj -is [double] -or $Obj -is [long]) {
        return $null
    }
    try {
        if ($Obj.PSObject.Properties.Name -contains $Name) {
            return $Obj.$Name
        }
    } catch {
        return $null
    }
    return $null
}

# Read a field from the runner settings object, returning $Default when the
# field is absent or the settings object itself is null. Keeps StrictMode
# happy and lets callers supply sensible per-field fallbacks.
function Get-ConfigField {
    param($Obj, [string]$Name, $Default)

    $v = Get-SafeField $Obj $Name
    if ($null -ne $v -and "$v" -ne "") { return $v }
    return $Default
}

# Produce a one-line, human-readable summary of a tool's arguments.
function Format-ToolArgs {
    param($ToolName, $ToolArgs)

    if ($null -eq $ToolArgs) { return "" }

    $path    = Get-SafeField $ToolArgs "path"
    $command = Get-SafeField $ToolArgs "command"
    $pattern = Get-SafeField $ToolArgs "pattern"
    $content = Get-SafeField $ToolArgs "content"
    $edits   = Get-SafeField $ToolArgs "edits"

    switch ($ToolName) {
        "read"  { if ($path)    { return "path: $path" } }
        "ls"    { if ($path)    { return "path: $path" } }
        "bash"  { if ($command) { return "cmd: $command" } }
        "grep"  { return "pattern: $pattern, path: $path" }
        "find"  { return "path: $path, pattern: $pattern" }
        "write" {
            $len = if ($content) { "$($content.Length) chars" } else { "" }
            return "path: $path, $len"
        }
        "edit"  {
            $count = if ($edits) { "$(@($edits).Count) edits" } else { "0 edits" }
            return "path: $path, $count"
        }
    }

    # Fallback: compact JSON of the whole args object.
    try {
        $s = $ToolArgs | ConvertTo-Json -Compress -Depth 3 -ErrorAction SilentlyContinue
        if ($s -and $s.Length -gt 300) { return $s.Substring(0, 300) + "..." }
        return $s
    } catch {
        return ""
    }
}

# Extract the textual part of a tool result. Tool results typically look like:
#   { "content": [ { "type": "text", "text": "..." } ], "details": {} }
function Get-ResultText {
    param($Result)

    if ($null -eq $Result) { return "" }

    $content = Get-SafeField $Result "content"
    if ($content) {
        $parts = @()
        foreach ($item in $content) {
            $t = Get-SafeField $item "text"
            if ($t) { $parts += $t }
        }
        if ($parts.Count -gt 0) {
            return ($parts -join "`n")
        }
    }

    # Some results carry the text directly.
    $directText = Get-SafeField $Result "text"
    if ($directText) { return $directText }

    # Fallback: stringify.
    try {
        return ($Result | ConvertTo-Json -Compress -Depth 3 -ErrorAction SilentlyContinue)
    } catch {
        return "$Result"
    }
}

function Format-Truncate {
    param([string]$Text, [int]$MaxLen = 600)

    if ([string]::IsNullOrEmpty($Text)) { return "" }
    $Text = $Text -replace "`r`n", "`n"
    if ($Text.Length -gt $MaxLen) {
        return $Text.Substring(0, $MaxLen) + " ...[truncated]"
    }
    return $Text
}

function Invoke-PiSession {
    param(
        [Parameter(Mandatory=$true)][string]$SessionName,
        [Parameter(Mandatory=$true)][string]$Prompt,
        [Parameter(Mandatory=$true)][string]$LogPath,
        [string]$Model = ""
    )

    if ($Model) { Write-Step "Model: $Model" }
    Write-Step "Starting fresh Pi session: $SessionName"
    Write-Step "Output log: $LogPath"
    Write-Host ""

    # IMPORTANT:
    # Do NOT use -c / --continue / --session here.
    # Every invocation must get a clean session/context.
    #
    # --mode json = stream ALL session events (tool calls, tool results,
    #   streaming assistant text, errors) to stdout as JSON lines.
    # -p = non-interactive: process the prompt and exit.
    # --name = gives the saved Pi session a useful name.
    #
    # NOTE: The previous version used `pi -p` (text print mode), which only
    # emits the FINAL assistant text after the whole session finishes. That
    # left the console blank for the entire (often lengthy) run, so it looked
    # like the AI was producing no output. JSON mode lets us render every
    # event as it happens, giving real visibility into what the AI is doing.
    #
    # If your installation exposes Pi through a different command, change
    # only the `pi` token on the next line.
    # Build the pi argument list. --model is only added when a model is
    # supplied so that, absent any settings, pi still falls back to its own
    # configured default (preserving prior behavior).
    #
    # IMPORTANT: The prompt is written to a UTF-8 (no BOM) file and delivered
    # to pi via the @file message convention rather than as a literal argv
    # string. Windows PowerShell 5.1 does NOT quote a multi-line string
    # argument as a single argv token when invoking native commands -- the
    # embedded newlines cause CommandLineToArgvW to split the argument on
    # whitespace. Any whitespace-separated token that begins with '-' (e.g. a
    # literal "-30" appearing inside a task description such as
    # "speed must be between -30 and 30") then gets misparsed by pi as an
    # unknown option ("Error: Unknown option: -30"), killing the session
    # before it starts. Routing through a file sidesteps all argv quoting
    # issues and also guarantees correct UTF-8 delivery of any non-ASCII text
    # (Korean, em-dashes, etc.) in the prompt.
    $promptFile = $LogPath + ".prompt.txt"
    $utf8NoBom  = New-Object System.Text.UTF8Encoding $false
    [System.IO.File]::WriteAllText($promptFile, $Prompt, $utf8NoBom)

    $piArgs = @()
    if ($Model) { $piArgs += @("--model", $Model) }
    $piArgs += @("--name", $SessionName, "--mode", "json", "-p", "@$promptFile")

    $maxSessionSec = [int]$script:MaxSessionSeconds
    $maxLogBytes   = [int]$script:MaxLogBytes
    $maxLogLine    = [int]$script:MaxLogLineChars
    $startTime     = Get-Date
    $sessionState  = @{ TimedOut = $false; LogBytes = 0 }

    # Open the log ONCE as UTF-8 (no BOM) and keep it open for the whole
    # session. Two reasons this matters:
    #   1. Encoding correctness on Windows PowerShell 5.1: the built-in
    #      `Add-Content` cmdlet defaults to the system ANSI codepage here,
    #      which would corrupt any non-ASCII bytes (em-dashes, Korean text)
    #      in Pi's NDJSON stream. A UTF8Encoding(false) StreamWriter always
    #      writes UTF-8 regardless of host.
    #   2. Performance: reopening the file for every output line made the
    #      multi-hundred-MB logs that runaway sessions produced extremely slow.
    $logWriter = New-Object System.IO.StreamWriter `
        $LogPath, $false, (New-Object System.Text.UTF8Encoding $false)
    $logWriter.AutoFlush = $true

    try {
    & pi @piArgs 2>&1 | ForEach-Object {
        $rawLine = if ($_ -is [System.Management.Automation.ErrorRecord]) {
            $_.Exception.Message
        } else {
            $_.ToString()
        }

        # Persist the raw line, but cap per-line length and total log size so
        # that a single outrageous tool result (or a looping agent) cannot
        # grow the log without bound.
        if ($maxLogBytes -le 0 -or $sessionState.LogBytes -lt $maxLogBytes) {
            $saveLine = Format-Truncate $rawLine $maxLogLine
            $logWriter.WriteLine($saveLine)
            $sessionState.LogBytes += $saveLine.Length + 2
        }

        # Wall-clock guard: if the agent runs forever, stop draining the
        # pipeline. Breaking out of ForEach-Object closes pi's stdout pipe,
        # which terminates pi; the timedOut flag makes us treat the session as
        # failed so it is retried instead of hanging the runner forever.
        if ($maxSessionSec -gt 0 -and ((Get-Date) - $startTime).TotalSeconds -gt $maxSessionSec) {
            if (-not $sessionState.TimedOut) {
                $sessionState.TimedOut = $true
                Write-Fail "Session exceeded ${maxSessionSec}s time limit; terminating."
            }
            break
        }

        # Try to parse the line as a JSON event.
        $evt = $null
        try {
            $evt = $rawLine | ConvertFrom-Json -ErrorAction Stop
        } catch {
            # Non-JSON line (startup banner, raw stderr). Show it as-is.
            Write-Host $rawLine
            return
        }

        $type = Get-SafeField $evt "type"
        if ([string]::IsNullOrEmpty($type)) { return }

        switch ($type) {
            "session" {
                $id = Get-SafeField $evt "id"
                Write-Host "[session] $id" -ForegroundColor DarkGray
            }
            "agent_start" {
                Write-Host ""
                Write-Host "========== AGENT START ==========" -ForegroundColor Magenta
            }
            "agent_end" {
                Write-Host "=========== AGENT END ===========" -ForegroundColor Magenta
            }
            "turn_start" {
                Write-Host ""
            }
            "message_update" {
                $ae = Get-SafeField $evt "assistantMessageEvent"
                if ($null -eq $ae) { return }
                $aeType = Get-SafeField $ae "type"
                if ($aeType -eq "text_delta") {
                    $delta = Get-SafeField $ae "delta"
                    if ($delta) {
                        Write-Host $delta -NoNewline -ForegroundColor White
                    }
                } elseif ($aeType -eq "text_end") {
                    # Close the streamed assistant-text line.
                    Write-Host ""
                }
            }
            "tool_execution_start" {
                $toolName = Get-SafeField $evt "toolName"
                $toolArgs = Get-SafeField $evt "args"
                $argStr   = Format-ToolArgs $toolName $toolArgs
                if ($argStr) {
                    Write-Host "  > $toolName  |  $argStr" -ForegroundColor Cyan
                } else {
                    Write-Host "  > $toolName" -ForegroundColor Cyan
                }
            }
            "tool_execution_end" {
                $toolName = Get-SafeField $evt "toolName"
                $isError  = Get-SafeField $evt "isError"
                $result   = Get-SafeField $evt "result"
                $text     = Format-Truncate (Get-ResultText $result) 600
                if ($isError) {
                    Write-Host "  ! $toolName FAILED" -ForegroundColor Red
                    if ($text) { Write-Host "    $text" -ForegroundColor DarkRed }
                } else {
                    Write-Host "  < $toolName ok" -ForegroundColor Green
                    if ($text) { Write-Host "    $text" -ForegroundColor DarkGray }
                }
            }
            default {
                # Other event types (message_start, message_end, turn_end,
                # tool_execution_update, queue_update, compaction_*,
                # agent_settled, auto_retry_*, etc.) are intentionally not
                # rendered, to keep the live console output readable.
                # The raw JSON for every event is still saved to the log file.
            }
        }
    }
    }  # end of try around the pi pipeline
    finally {
        $logWriter.Close()
    }

    $exitCode = $LASTEXITCODE
    if ($sessionState.TimedOut) { $exitCode = 124 }  # 124 mirrors `timeout(1)`

    Write-Host ""

    if ($exitCode -ne 0) {
        Write-Fail "Pi exited with code $exitCode. See $LogPath"
        return $false
    }

    return $true
}

# ---------------------------------------------------------------------------
# Validation / setup
# ---------------------------------------------------------------------------

Write-Section "PI IMPLEMENTATION PLAN RUNNER"

if (-not (Get-Command pi -ErrorAction SilentlyContinue)) {
    throw "The 'pi' command was not found on PATH."
}

if (-not (Test-Path $PlanDir)) {
    throw "Implementation plan directory does not exist: $PlanDir"
}

# ---------------------------------------------------------------------------
# Load runner settings (model selection + retry policy)
# ---------------------------------------------------------------------------
# pi-runner-settings.json optionally overrides:
#   implementation_model, verification_model, escalation_model,
#   final_audit_model, escalate_after_attempts, max_verification_attempts,
#   restart_delay_seconds, max_session_seconds, max_log_bytes
# Any field omitted there falls back to a built-in default. Explicit script
# parameters (-MaxVerificationAttempts / -RestartDelaySeconds /
# -MaxSessionSeconds / -MaxLogBytes) override the settings file when set to
# a non-negative value.
$runnerSettings = $null
if (Test-Path $SettingsFile) {
    try {
        $runnerSettings = Get-Content $SettingsFile -Raw | ConvertFrom-Json
        Write-Ok "Loaded runner settings from $SettingsFile"
    } catch {
        Write-Fail "Could not parse $SettingsFile ($($_.Exception.Message)); using defaults."
    }
} else {
    Write-Step "No settings file at $SettingsFile; using built-in defaults."
}

$implementationModel   = [string](Get-ConfigField $runnerSettings "implementation_model" "")
$verificationModel     = [string](Get-ConfigField $runnerSettings "verification_model"   "")
$escalationModel       = [string](Get-ConfigField $runnerSettings "escalation_model"     "")
$finalAuditModel       = [string](Get-ConfigField $runnerSettings "final_audit_model"   "")

$escalateAfterAttempts = [int](Get-ConfigField $runnerSettings "escalate_after_attempts"   2)
$settingsMaxAttempts   = [int](Get-ConfigField $runnerSettings "max_verification_attempts" 8)
$settingsRestartDelay  = [int](Get-ConfigField $runnerSettings "restart_delay_seconds"     5)
$settingsMaxSessionSec = [int](Get-ConfigField $runnerSettings "max_session_seconds"      1800)
$settingsMaxLogBytes   = [int](Get-ConfigField $runnerSettings "max_log_bytes"             262144000)

# Script params override the settings file only when explicitly provided.
# We use -1 as the "not provided" sentinel for the numeric knobs.
if ($MaxVerificationAttempts -lt 0) { $MaxVerificationAttempts = $settingsMaxAttempts }
if ($RestartDelaySeconds     -lt 0) { $RestartDelaySeconds     = $settingsRestartDelay }
if ($MaxSessionSeconds        -lt 0) { $MaxSessionSeconds        = $settingsMaxSessionSec }
if ($MaxLogBytes              -lt 0) { $MaxLogBytes              = $settingsMaxLogBytes }

# A blank model string means "let pi use its own default".
function Format-ModelForBanner {
    param([string]$M) if ([string]::IsNullOrEmpty($M)) { return "<pi default>" } else { return $M }
}

Write-Host "Models:"
Write-Host ("  implementation : {0}" -f (Format-ModelForBanner $implementationModel))
Write-Host ("  verification   : {0}" -f (Format-ModelForBanner $verificationModel))
Write-Host ("  escalation     : {0} (after {1} failed attempts)" -f (Format-ModelForBanner $escalationModel), $escalateAfterAttempts)
Write-Host ("  final audit    : {0}" -f (Format-ModelForBanner $finalAuditModel))
Write-Host ""
Write-Host "Max verification attempts : $MaxVerificationAttempts (0 = unlimited)"
Write-Host "Restart delay seconds     : $RestartDelaySeconds"
if ($MaxSessionSeconds -gt 0) {
    Write-Host "Max session seconds       : $MaxSessionSeconds (0 = unlimited)"
} else {
    Write-Host "Max session seconds       : unlimited"
}
if ($MaxLogBytes -gt 0) {
    Write-Host ("Max log bytes             : {0:N0} (0 = unlimited)" -f $MaxLogBytes)
} else {
    Write-Host "Max log bytes             : unlimited"
}
Write-Host "Max log line chars        : $MaxLogLineChars"
Write-Host ""

New-Item -ItemType Directory -Force -Path $StateDir | Out-Null
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null

$taskFiles = Get-TaskFiles $PlanDir

if ($taskFiles.Count -eq 0) {
    throw "No .md/.txt/.task/.plan files found in $PlanDir"
}

Write-Host "Repository:     $(Get-Location)"
Write-Host "Plan directory: $(Resolve-Path $PlanDir)"
Write-Host "Tasks found:    $($taskFiles.Count)"
Write-Host "State:          $(Resolve-Path $StateDir)"
Write-Host "Logs:           $(Resolve-Path $LogDir)"
Write-Host ""

# ---------------------------------------------------------------------------
# Global autonomous-development policy injected into EVERY session
# ---------------------------------------------------------------------------

$commonPolicy = @'
You are operating as an autonomous senior software engineer inside an existing
repository.

GENERAL OPERATING RULES

1. Work autonomously. Do not ask the user questions unless proceeding is
   literally impossible. Inspect the repository and make the best grounded
   engineering decision yourself.

2. Never claim success based only on code inspection. Execute the relevant
   build, static analysis, tests, and runtime/smoke checks whenever the
   repository makes them possible.

3. Before editing:
   - inspect repository structure
   - read AGENTS.md / CLAUDE.md / README / project documentation if present
   - inspect relevant existing implementation and tests
   - inspect git status
   - understand the task's integration points

4. During implementation:
   - implement the COMPLETE task, not a demo or placeholder
   - preserve existing behavior unless the task requires changing it
   - avoid unrelated refactors
   - handle errors and edge cases
   - keep public APIs and formats backward-compatible where reasonable
   - add/update tests for every meaningful behavior you introduce
   - do not weaken, delete, skip, xfail, mute, or bypass existing tests merely
     to obtain a green result

5. TESTING STANDARD — THOROUGH, NOT MINIMAL:
   Determine what is applicable to this repository and run as many of these as
   make sense:
   - focused unit tests for changed behavior
   - regression tests around adjacent behavior
   - edge/boundary/error-case tests
   - integration tests
   - full existing test suite
   - formatter / lint / static analysis / type checking
   - compile/build
   - runtime or smoke test
   - serialization/API/protocol compatibility tests if applicable
   - platform-specific validation if the changed code requires it

   If a test cannot be executed, state the exact technical reason and use the
   strongest available substitute. Do not silently skip validation.

6. When any test/build/check fails:
   - diagnose the actual root cause
   - fix it
   - rerun the failing check
   - rerun appropriate regression/full checks
   Continue the repair loop until the repository is genuinely healthy.

7. Inspect git diff before finishing. Look specifically for:
   - incomplete implementation
   - accidental file changes
   - debug leftovers
   - commented-out code
   - TODO placeholders introduced by you
   - missing tests
   - bad error handling
   - regressions
   - generated/build artifacts that should not be committed

8. Do NOT reset, discard, overwrite, or revert pre-existing user changes.
   Treat existing uncommitted work as valuable.

9. Do NOT stop merely because the first implementation compiles.

10. At the end, provide a concise factual report:
    - what changed
    - exact tests/checks executed
    - their results
    - any limitations that genuinely remain
'@

# ---------------------------------------------------------------------------
# Pre-flight: assess current project completion and choose where to start
# ---------------------------------------------------------------------------
# Rather than blindly re-running every task file from the top (which would
# redo already-complete work), run one fresh assessment session that:
#   - inspects docs/IMPLEMENTATION_STATUS.md, README, existing code/tests
#   - inspects existing .pi-plan-state done markers
#   - decides, per task file, whether its requirements are ALREADY satisfied
#   - writes a passing verification record + done marker for those that are
# This populates the resume state so the normal task loop below starts at the
# first genuinely-incomplete task instead of repeating finished work.
#
# A sentinel file (_preflight.done) records that a pre-flight session has
# already completed successfully, so a normal rerun does NOT re-spawn a fresh
# (expensive, sometimes misbehaving) Pi assessment session every time. Delete
# the sentinel or pass -Force to force a fresh assessment.

$preflightDonePath = Join-Path $StateDir "_preflight.done"

if (-not $Force -and (Test-Path $preflightDonePath)) {
    Write-Ok "Pre-flight already completed (sentinel present): $preflightDonePath"
    Write-Step "Skipping pre-flight. Use -Force (or delete $preflightDonePath) to re-assess."
} elseif (-not $Force) {
    Write-Section "PRE-FLIGHT: ASSESS PROJECT COMPLETION & CHOOSE START"

    $preFlightLog = Join-Path $LogDir (
        "_preflight-assess-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss")
    )

    $taskFileList = ($taskFiles | ForEach-Object { "- $($_.Name)" }) -join "`n"

    $preflightPrompt = @"
$commonPolicy

PROJECT COMPLETION ASSESSMENT (PRE-FLIGHT)
=========================================

This repository has an implementation plan made of task files. Some of them may
already be fully implemented; others are not started. Your job here is NOT to
implement anything new. It is ONLY to assess what is already done and record it
so the outer runner can skip finished work and begin at the right place.

Task files in $(Resolve-Path $PlanDir):
$taskFileList

State directory (done markers + verification records):
$(Resolve-Path $StateDir)

MANDATORY ASSESSMENT PROCEDURE

1. Read the project's own completion record first:
   docs/IMPLEMENTATION_STATUS.md (authoritative status log).
   Then README.md, docs/ARCHITECTURE.md, docs/CHANGELOG.md.

2. Inspect the real repository: existing code under Assets/JajuchaSim, tests,
   assembly definitions, scenes, prefabs, config assets.

3. Verify claims by EXECUTION where possible (the project uses Unity batchmode
   tests; see README.md for the exact commands). If Unity is available on this
   machine, run the EditMode and PlayMode test suites and capture pass counts.
   If Unity cannot be run here, state that explicitly and rely on the project's
   recorded results plus direct code/test inspection.

4. For EACH task file listed above, decide one of:
   - ALREADY COMPLETE: every concrete requirement in that task file is
     satisfied by the current repository, verified by tests/execution where
     technically possible and by code inspection otherwise.
   - NOT COMPLETE: some requirement is missing, partial, stubbed, or untested.
     Do not implement it here; just leave it for the main loop.

   Notes on file roles:
   - 00_MASTER_PLAN.txt is the master engineering rules / project-wide policy.
     Treat it as ALREADY COMPLETE only if those rules are already reflected in
     the repository (folder structure, asmdefs, determinism, docs, etc.).
   - Files that are guidance/clarifications for a step (e.g. 01.txt, 03.txt,
     06.txt, 08.txt) are COMPLETE when the step they refine is complete and the
     clarification is honored.
   - Step spec files (02.txt = Step 1, 04.txt = Step 3, 05.txt = Step 4,
     07.txt = Step 7, 09.txt = Step 8, 10.txt = Step 10, etc.) are COMPLETE
     only when that entire step is implemented, tested, and documented per its
     Definition of Done.

OUTPUT / RECORDS

For every task file you classify ALREADY COMPLETE, write BOTH of:
  1. A passing verification record at
     $(Resolve-Path $StateDir)\<safeName>.verification.json
     using the schema below with status = "pass".
  2. A done marker file at
     $(Resolve-Path $StateDir)\<safeName>.done
     containing:
       task=<file name>
       verified=<iso timestamp>
       assessment=already-complete

  <safeName> is the task file name with the extension removed and any
  non [A-Za-z0-9._-] characters replaced by '_' — exactly as the outer runner
  names these files. (For "00_MASTER_PLAN.txt" -> "00_MASTER_PLAN".)

Do NOT write verification records or done markers for tasks that are NOT
complete; leave them for the implementation+verification loop.

Do NOT modify any source code, tests, or documentation in this session. This
is assessment only.

Verification record JSON schema:
{
  "status": "pass",
  "task": "<file name>",
  "attempt": 0,
  "summary": "Assessed already-complete during pre-flight.",
  "tests_run": [
    "exact command or check and result"
  ],
  "requirements_verified": [
    "requirement/result"
  ],
  "remaining_issues": []
}

Finally, print a short STARTING POINT REPORT listing, for each task file:
  <file> -> ALREADY COMPLETE | NOT COMPLETE
and state which task the runner should begin implementing.
"@

    $preFlightOk = Invoke-PiSession `
        -SessionName "plan-preflight-assess" `
        -Prompt $preflightPrompt `
        -Model $verificationModel `
        -LogPath $preFlightLog

    if (-not $preFlightOk) {
        Write-Fail "Pre-flight assessment Pi process failed; continuing without it."
    } else {
        $assessed = 0
        foreach ($task in $taskFiles) {
            $safeName = Get-SafeName $task.Name
            $vPath = Join-Path $StateDir "$safeName.verification.json"
            $dPath = Join-Path $StateDir "$safeName.done"
            $v = Read-Verification $vPath
            if ((Test-VerificationPassed $v) -and (Test-Path $dPath)) {
                $assessed++
                Write-Ok "Pre-flight marked already-complete: $($task.Name)"
            }
        }
        Write-Step "Pre-flight assessed $assessed task(s) as already complete."

        # Record that pre-flight completed so a normal rerun skips it.
        # Only written when the Pi process itself succeeded (exit 0), so a
        # crashed pre-flight will be retried next run.
        @"
task=_preflight
done=$(Get-Date -Format o)
tasks_assessed=$assessed
"@ | Set-Content -Path $preflightDonePath -Encoding UTF8
    }
}

# ---------------------------------------------------------------------------
# Task loop
# ---------------------------------------------------------------------------

$completedCount = 0
$skippedCount = 0
$failedTasks = @()

for ($i = 0; $i -lt $taskFiles.Count; $i++) {
    $task = $taskFiles[$i]
    $taskNumber = $i + 1
    $safeName = Get-SafeName $task.Name

    $verificationPath = Join-Path $StateDir "$safeName.verification.json"
    $donePath = Join-Path $StateDir "$safeName.done"

    Write-Section "TASK $taskNumber / $($taskFiles.Count): $($task.Name)"

    if (-not $Force -and (Test-Path $donePath)) {
        $existingVerification = Read-Verification $verificationPath
        if (Test-VerificationPassed $existingVerification) {
            Write-Ok "Already verified complete; skipping."
            $skippedCount++
            continue
        }

        Write-Step "Done marker exists but verification is absent/invalid. Re-verifying."
        Remove-Item $donePath -Force -ErrorAction SilentlyContinue
    }

    # Remove stale pass/fail report before starting this task.
    Remove-Item $verificationPath -Force -ErrorAction SilentlyContinue

    $taskText = Get-Content $task.FullName -Raw

    # -----------------------------------------------------------------------
    # Fresh session 1: implementation
    # -----------------------------------------------------------------------

    $implementLog = Join-Path $LogDir (
        "{0:D3}-{1}-implement-{2}.log" -f
        $taskNumber,
        $safeName,
        (Get-Date -Format "yyyyMMdd-HHmmss")
    )

    $implementationPrompt = @"
$commonPolicy

CURRENT TASK
============

Plan file:
$($task.FullName)

Task contents:
----------------
$taskText
----------------

THIS IS THE IMPLEMENTATION SESSION FOR THIS TASK.

Implement every requirement in the task file. Treat the plan text as the source
of truth, but inspect the real repository before deciding how to implement it.

Required workflow:
0. STARTING POINT DETERMINATION (do this first, before writing any code):
   - Read docs/IMPLEMENTATION_STATUS.md, README.md, and the files in
     $StateDir (*.done / *.verification.json) to learn what is already
     complete.
   - Inspect existing code, tests, and docs for this task's scope.
   - Run the relevant existing tests (e.g. Unity EditMode/PlayMode per README)
     to confirm what actually passes right now.
   - Decide which requirements of THIS task are already satisfied and which
     remain. Do NOT redo already-complete work. Implement ONLY the missing /
     incomplete / broken pieces of this task. If you determine the entire task
     is already complete, fix nothing, run the validation suite to prove it,
     and report that fact clearly in your final summary (the separate
     verification session will record the final pass).
1. Inspect and understand the relevant code.
2. Implement the full task (only the missing parts per step 0).
3. Add or improve meaningful automated tests.
4. Run focused tests while developing.
5. Run broader regression checks and the full relevant suite.
6. Run build/lint/type/static checks that apply.
7. Perform a smoke/runtime test where feasible.
8. Repair every issue you find and rerun validation.
9. Inspect the final diff for quality and completeness.

Do NOT create a completion marker. A separate fresh verification session will
judge and, if necessary, repair your implementation.
"@

    $implementationOk = Invoke-PiSession `
        -SessionName "plan-$taskNumber-$safeName-implement" `
        -Prompt $implementationPrompt `
        -Model $implementationModel `
        -LogPath $implementLog

    if (-not $implementationOk) {
        Write-Step "Implementation session failed at process level; verifier will still inspect and repair."
    }

    # -----------------------------------------------------------------------
    # Fresh session(s): adversarial verification + repair
    # -----------------------------------------------------------------------

    $attempt = 0
    $verified = $false

    while (-not $verified) {
        $attempt++

        if ($MaxVerificationAttempts -gt 0 -and $attempt -gt $MaxVerificationAttempts) {
            Write-Fail "Reached MaxVerificationAttempts=$MaxVerificationAttempts for $($task.Name)"
            break
        }

        # Escalate to a stronger model after repeatedly failing verification.
        # attempt 1..escalateAfterAttempts  -> verification_model
        # attempt > escalateAfterAttempts   -> escalation_model
        $verifyModel = $verificationModel
        if ($escalateAfterAttempts -gt 0 -and $attempt -gt $escalateAfterAttempts -and $escalationModel) {
            $verifyModel = $escalationModel
            if ($attempt -eq ($escalateAfterAttempts + 1)) {
                Write-Step "Escalating verification model to: $escalationModel"
            }
        }

        Remove-Item $verificationPath -Force -ErrorAction SilentlyContinue

        $verifyLog = Join-Path $LogDir (
            "{0:D3}-{1}-verify-{2:D2}-{3}.log" -f
            $taskNumber,
            $safeName,
            $attempt,
            (Get-Date -Format "yyyyMMdd-HHmmss")
        )

        $verificationPrompt = @"
$commonPolicy

CURRENT TASK TO VERIFY
======================

Plan file:
$($task.FullName)

Task contents:
----------------
$taskText
----------------

THIS IS AN INDEPENDENT VERIFICATION + REPAIR SESSION.

Assume the previous implementer may have made subtle mistakes. Do not trust its
claims. Audit the repository against the task from scratch.

Your job is BOTH reviewer and fixer.

MANDATORY VERIFICATION PROCEDURE

A. REQUIREMENT AUDIT
   - Translate every requirement in the task into a concrete checklist.
   - Inspect the actual changed code and surrounding integration.
   - Verify each requirement is implemented completely.
   - Search for placeholders, stubs, TODOs, hard-coded fake success, disabled
     functionality, dead code, and accidental regressions.

B. TEST AUDIT
   - Inspect existing tests and tests added for this task.
   - Identify important missing cases.
   - Add tests for missing normal, edge, boundary, failure, and regression cases.
   - Prefer behavioral tests over shallow implementation-detail tests.

C. EXECUTION
   Run all applicable validation, including:
   - focused tests for the task
   - tests for neighboring/affected components
   - complete relevant test suite
   - build/compile
   - lint/format/static analysis/type checking
   - integration tests
   - runtime/smoke test where technically possible

D. ADVERSARIAL REVIEW
   Specifically try to make the implementation fail:
   - malformed/empty/unusual inputs
   - boundary values
   - repeated calls/state transitions
   - missing resources
   - invalid configuration
   - backwards compatibility
   - platform/runtime integration
   - race/lifecycle/cleanup issues where relevant

E. REPAIR LOOP
   If ANY defect or failed check is found:
   1. fix the implementation or test correctly
   2. rerun the specific failed check
   3. rerun broader regression checks
   Repeat until clean.

F. FINAL DIFF REVIEW
   Inspect git diff and git status.
   Ensure no unrelated destructive edits and no debug/generated junk.

PASS CRITERIA

You may mark PASS only when:
- every task requirement is implemented
- meaningful automated coverage exists
- all executable relevant tests/checks pass
- build succeeds when the project supports building
- no known task-related defect remains

VERIFICATION RECORD

Before ending, write EXACTLY ONE JSON object to:

$verificationPath

Use this schema:

{
  "status": "pass" | "fail",
  "task": "$($task.Name)",
  "attempt": $attempt,
  "summary": "brief factual summary",
  "tests_run": [
    "exact command or check and result"
  ],
  "requirements_verified": [
    "requirement/result"
  ],
  "remaining_issues": [
    "issue"
  ]
}

Rules:
- status MUST be "fail" if a required validation fails or a known task defect
  remains.
- status MAY be "pass" only after the repair loop is complete.
- Always write the record, even on failure.
- Do not write or touch the .done marker; the outer runner owns it.
"@

        $verifyProcessOk = Invoke-PiSession `
            -SessionName "plan-$taskNumber-$safeName-verify-$attempt" `
            -Prompt $verificationPrompt `
            -Model $verifyModel `
            -LogPath $verifyLog

        if (-not $verifyProcessOk) {
            Write-Fail "Verification Pi process failed. Retrying with a fresh session."
            continue
        }

        $verification = Read-Verification $verificationPath

        if (Test-VerificationPassed $verification) {
            $verified = $true

            # The outer runner, not the LLM, creates the done marker.
            @"
task=$($task.Name)
verified=$(Get-Date -Format o)
verification=$verificationPath
"@ | Set-Content -Path $donePath -Encoding UTF8

            Write-Ok "Task verified PASS."
            $completedCount++
        }
        else {
            if ($null -ne $verification) {
                Write-Fail "Verifier reported FAIL."

                if ($verification.PSObject.Properties.Name -contains "remaining_issues") {
                    foreach ($issue in @($verification.remaining_issues)) {
                        Write-Host "    - $issue" -ForegroundColor Red
                    }
                }
            }
            else {
                Write-Fail "Verifier did not produce a valid verification record."
            }

            Write-Step "Starting another fresh verification/repair session."
            if ($RestartDelaySeconds -gt 0) {
                Write-Step "Waiting $RestartDelaySeconds seconds before retry..."
                Start-Sleep -Seconds $RestartDelaySeconds
            }
        }
    }

    if (-not $verified) {
        $failedTasks += $task.Name

        # Default behavior is to STOP here rather than building later tasks on
        # an unverified foundation.
        Write-Fail "Cannot safely advance past an unverified task."
        break
    }
}

# ---------------------------------------------------------------------------
# Final repository-wide audit
# ---------------------------------------------------------------------------

if ($failedTasks.Count -eq 0) {
    Write-Section "FINAL REPOSITORY-WIDE AUDIT"

    $finalVerificationPath = Join-Path $StateDir "_final.verification.json"
    Remove-Item $finalVerificationPath -Force -ErrorAction SilentlyContinue

    $finalLog = Join-Path $LogDir (
        "_final-audit-{0}.log" -f (Get-Date -Format "yyyyMMdd-HHmmss")
    )

    $allTasks = ($taskFiles | ForEach-Object {
        "### $($_.Name)`n$((Get-Content $_.FullName -Raw))"
    }) -join "`n`n"

    $finalPrompt = @"
$commonPolicy

ALL IMPLEMENTATION PLAN TASKS
=============================

$allTasks

THIS IS THE FINAL CROSS-TASK INTEGRATION AUDIT.

All individual tasks have already passed independent verification. Now assume
there may still be cross-task integration defects.

Perform a repository-wide audit:

1. Check the complete implementation plan against the final repository state.
2. Look for incompatibilities between tasks.
3. Run the broadest practical regression test suite.
4. Run the complete build/compile path.
5. Run lint/static/type/format checks that apply.
6. Run integration and smoke/runtime checks where possible.
7. Add any missing integration/regression tests.
8. Fix every defect found.
9. Repeat tests after repairs.
10. Inspect final git diff/status for accidental changes and unfinished work.

Write the final result to:

$finalVerificationPath

JSON schema:
{
  "status": "pass" | "fail",
  "summary": "final repository audit result",
  "tests_run": [
    "exact command or check and result"
  ],
  "remaining_issues": [
    "issue"
  ]
}

Only use "pass" when the entire implementation plan is integrated and all
relevant executable validation is green.
"@

    $finalProcessOk = Invoke-PiSession `
        -SessionName "implementation-plan-final-audit" `
        -Prompt $finalPrompt `
        -Model $finalAuditModel `
        -LogPath $finalLog

    $finalVerification = Read-Verification $finalVerificationPath

    if (-not $finalProcessOk -or -not (Test-VerificationPassed $finalVerification)) {
        Write-Fail "Final repository-wide audit did not PASS."
        Write-Host "Review: $finalVerificationPath"
        Write-Host "Log:    $finalLog"
        exit 2
    }

    Write-Ok "Final repository-wide audit PASS."
}

# ---------------------------------------------------------------------------
# Summary
# ---------------------------------------------------------------------------

Write-Section "SUMMARY"

Write-Host "Newly completed: $completedCount"
Write-Host "Already complete: $skippedCount"
Write-Host "Total tasks:      $($taskFiles.Count)"

if ($failedTasks.Count -gt 0) {
    Write-Fail "Stopped because these tasks remain unverified:"
    foreach ($name in $failedTasks) {
        Write-Host "  - $name" -ForegroundColor Red
    }
    exit 1
}

Write-Ok "EVERY IMPLEMENTATION PLAN TASK IS VERIFIED COMPLETE."
exit 0
