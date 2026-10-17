# tools/build_now.ps1 - one-shot project build/suite runner for ANY agent (CLI or computer-use).
# This machine's rule (HANDOFF/SESSION_BRIEF): batchmode EditMode suite + production build ONLY -
# never open the game/journey windows here (real-input journeys belong to the dedicated node).
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/build_now.ps1            # player build (TempBuildP62)
#   powershell -ExecutionPolicy Bypass -File tools/build_now.ps1 -Suite     # full EditMode suite (journey pins incl.)
#   powershell -ExecutionPolicy Bypass -File tools/build_now.ps1 -PlayMode  # REAL-LOOP PlayMode smoke (headless, code-driven)
#   powershell -ExecutionPolicy Bypass -File tools/build_now.ps1 -Suite -TestFilter CT_S14   # targeted journey fixture
#   ... -ProjectPath D:\OtherProject -ExecuteMethod Some.Editor.Build      # any project/method
# Writes build_result.txt (next to this script): 1 line, timestamp | project/mode | status | detail.
param(
  [switch]$Suite,
  [switch]$PlayMode,
  [int]$TimeoutMin = 15,
  [string]$ProjectPath = 'D:\Vscode\little-world-english',
  [string]$ExecuteMethod = 'TempBuildP62.Build',
  [string]$TestFilter = ''
)
$ErrorActionPreference = 'Stop'
$root = $ProjectPath
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$unity = 'D:\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
$resultFile = Join-Path $scriptDir 'build_result.txt'
$tempDir = Join-Path $root 'Temp'
if (-not (Test-Path $tempDir)) { New-Item -ItemType Directory -Force -Path $tempDir | Out-Null }
$mode = if ($PlayMode) { 'playmode' } elseif ($Suite) { if ([string]::IsNullOrEmpty($TestFilter)) { 'suite' } else { "suite($TestFilter)" } } else { 'build' }
$projTag = Split-Path -Leaf $root

# Only the real Unity EDITOR counts as a build process — Unity Hub ships a helper also named
# "unity.exe" (serve) which must never trip the concurrency guard or be killed.
function Get-EditorProcesses {
  Get-Process Unity -ErrorAction SilentlyContinue | Where-Object {
    try { $_.Path -like 'D:\Unity\Hub\Editor\*' } catch { $false }
  }
}

function Finish([string]$status, [string]$detail) {
  $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $projTag/$mode | $status | $detail"
  [System.IO.File]::WriteAllText($resultFile, $line + "`r`n", [System.Text.UTF8Encoding]::new($false))
  Write-Output $line
  if ($status -eq 'SUCCEEDED') { exit 0 } else { exit 1 }
}
if ($Suite -and $PlayMode) { Finish 'BLOCKED' 'choose either -Suite (EditMode) or -PlayMode (PlayMode), not both' }

# 1) Concurrency guard: this machine shares one Unity licensing client - serialize runs.
if (Get-EditorProcesses) {
  Finish 'BLOCKED' 'another Unity EDITOR process is already running - wait for it to finish, then rerun'
}

# 2) Prepare log + arguments
$log = Join-Path $root ('Temp\agent-run-' + (Get-Date -Format 'HHmmss') + '.log')
$xmlResults = Join-Path $root 'Temp\agent-suite.xml'
if ($PlayMode) { $xmlResults = Join-Path $root 'Temp\agent-playmode.xml' }

# 3) Launch + bounded wait (Unity.exe launcher returns immediately; poll the real process)
$runStart = Get-Date
if ($Suite -or $PlayMode) {
  Remove-Item $xmlResults -ErrorAction SilentlyContinue
  $platform = if ($PlayMode) { 'PlayMode' } else { 'EditMode' }
  $suiteArgs = @('-batchmode', '-projectPath', $root, '-runTests', '-testPlatform', $platform,
    '-testResults', $xmlResults, '-logFile', $log)
  if (-not [string]::IsNullOrEmpty($TestFilter)) { $suiteArgs += @('-testFilter', $TestFilter) }
  & $unity @suiteArgs
} else {
  & $unity -batchmode -projectPath $root -executeMethod $ExecuteMethod -logFile $log -quit
}
$deadline = (Get-Date).AddMinutes($TimeoutMin)
while ((Get-EditorProcesses) -and (Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 10
}
if (Get-EditorProcesses) {
  Get-EditorProcesses | Stop-Process -Force -ErrorAction SilentlyContinue
  Finish 'HUNG' "timed out after ${TimeoutMin}m and was killed. If the log mentions LicenseClient waiting: close Unity, kill stale Unity.Licensing.Client, start Unity Hub again, then rerun. log=$log"
}

# 4) Parse results
$text = if (Test-Path $log) { Get-Content $log -Raw } else { '' }
if ($Suite -or $PlayMode) {
  if (-not (Test-Path $xmlResults)) {
    $alt = Join-Path $env:LOCALAPPDATA '..\LocalLow\DefaultCompany\little-world-english\TestResults.xml'
    if (Test-Path $alt) { $xmlResults = $alt }
  }
  # Honesty gate: only accept a results file WRITTEN after this run started (a stale
  # LocalLow fallback once produced a false SUCCEEDED while scripts had compiler errors).
  $xmlItem = Get-Item $xmlResults -ErrorAction SilentlyContinue
  $fresh = $xmlItem -and ($xmlItem.LastWriteTime -ge $runStart)
  if ($fresh) {
    [xml]$x = Get-Content $xmlResults
    $r = $x.SelectSingleNode('//test-run')
    $detail = "suite total=$($r.total) passed=$($r.passed) failed=$($r.failed) skipped=$($r.skipped) | log=$log"
    if ([int]$r.failed -eq 0) { Finish 'SUCCEEDED' $detail } else { Finish 'FAILED' $detail }
  } else {
    Finish 'FAILED' "no FRESH test results xml (run aborted? check log: $log)"
  }
} else {
  $m = [regex]::Match($text, '\[TempBuildP62\] result=(\w+) errors=(\d+) warnings=(\d+) size=(\d+)')
  if ($m.Success -and $m.Groups[1].Value -eq 'Succeeded') {
    Finish 'SUCCEEDED' "build $($m.Value) | log=$log"
  } elseif ($text -match 'return code 198') {
    Finish 'FAILED' "license error (exit 198). Fix: close Unity, kill stale Unity.Licensing.Client, start Unity Hub (MSIX), rerun | log=$log"
  } else {
    Finish 'FAILED' "build did not succeed | log=$log"
  }
}
