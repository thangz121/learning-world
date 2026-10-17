# tools/journey_now.ps1 - one-shot JOURNEY runner (stress driver) for ANY agent (CLI or computer-use).
# Runs the BUILT PLAYER (never the Unity editor) with the -stress-* flags. The journey simulates
# REAL OS input (mouse/keyboard) - the machine must not be touched while it runs.
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/journey_now.ps1 -Stage listening
#   powershell -ExecutionPolicy Bypass -File tools/journey_now.ps1 -Stage market -TimeoutMin 60
#   ... -ExePath D:\Vscode\little-world-english-reboot\Builds\RebootComparison\LWE.exe
# Gate (authoritative): player exit code 0 == errors=0 && severe=0 && !cancel.
# Evidence: <ShotDir>\stress_summary.txt + screenshots + "[STRESS] ..." trace in Player.log.
# Result: journey_result.txt next to this script (timestamp | stage | status | detail). Exit 0 on pass.
param(
  [Parameter(Mandatory=$true)][string]$Stage,
  [int]$TimeoutMin = 45,
  [string]$ExePath = 'D:\Vscode\P62Build\LWE.exe',
  [string]$ShotDir = 'D:\Vscode\stress-shots',
  [int]$RunNumber = 1
)
$ErrorActionPreference = 'Stop'
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$resultFile = Join-Path $scriptDir 'journey_result.txt'
$playerLog = Join-Path $env:USERPROFILE 'AppData\LocalLow\DefaultCompany\little-world-english\Player.log'

function Finish([string]$status, [string]$detail) {
  $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $Stage | $status | $detail"
  [System.IO.File]::WriteAllText($resultFile, $line + "`r`n", [System.Text.UTF8Encoding]::new($false))
  Write-Output $line
  if ($status -eq 'SUCCEEDED') { exit 0 } else { exit 1 }
}

$valid = @('geometry','market','city','rabbit','stairs','listening','speaking','all')
if ($valid -notcontains $Stage) { Finish 'BLOCKED' "invalid stage '$Stage' (use: $($valid -join '|'))" }
if (-not (Test-Path $ExePath)) { Finish 'BLOCKED' "player exe not found: $ExePath" }

# Guards: no Unity editor build/test running; no other player of the same name running.
$editors = Get-Process Unity -ErrorAction SilentlyContinue | Where-Object {
  try { $_.Path -like 'D:\Unity\Hub\Editor\*' } catch { $false }
}
if ($editors) { Finish 'BLOCKED' 'a Unity editor build/test is running - one heavy app at a time, wait for it' }
$exeName = [IO.Path]::GetFileNameWithoutExtension($ExePath)
$other = Get-Process -Name $exeName -ErrorAction SilentlyContinue
if ($other) { Finish 'BLOCKED' "another '$exeName' player is already running (pid $($other.Id))" }

if (-not (Test-Path $ShotDir)) { New-Item -ItemType Directory -Force -Path $ShotDir | Out-Null }
Remove-Item (Join-Path $ShotDir 'stress_summary.txt') -ErrorAction SilentlyContinue

$arguments = @()
if ($Stage -eq 'all') { $arguments += '-stress-all' } else { $arguments += @('-stress-stage', $Stage) }
$arguments += @('-shot-dir', $ShotDir, '-stress-run', "$RunNumber")

$started = Get-Date
$proc = Start-Process -FilePath $ExePath -ArgumentList $arguments -PassThru
$deadline = $started.AddMinutes($TimeoutMin)
while (-not $proc.HasExited -and (Get-Date) -lt $deadline) { Start-Sleep -Seconds 10 }
if (-not $proc.HasExited) {
  try { $proc.Kill() } catch { }
  Finish 'HUNG' "timed out after ${TimeoutMin}m and was killed - evidence so far: $ShotDir | player log: $playerLog"
}
$code = $proc.ExitCode
Start-Sleep -Seconds 2

# Evidence: last STRESS_END line (only trust it if Player.log was written after we started)
$endLine = ''
$stale = ''
try {
  $logItem = Get-Item $playerLog -ErrorAction SilentlyContinue
  if ($logItem -and $logItem.LastWriteTime -lt $started) { $stale = ' (Player.log STALE - another player run overwrote it; rely on stress_summary.txt)' }
  if ($logItem) {
    $m = Select-String -Path $playerLog -Pattern 'STRESS_END' -ErrorAction SilentlyContinue | Select-Object -Last 1
    if ($m) { $endLine = $m.Line.Trim() }
  }
} catch { }
$summary = ''
try {
  $sf = Join-Path $ShotDir 'stress_summary.txt'
  if (Test-Path $sf) { $summary = ((Get-Content $sf -TotalCount 2) -join ' ; ') }
} catch { }

if ($code -eq 0) {
  Finish 'SUCCEEDED' "exit=0 | $endLine | summary: $summary | shots: $ShotDir"
} else {
  Finish 'FAILED' "exit=$code (2=cancel/watchdog 1=errors-or-severe 3=relaunch-fail)$stale | $endLine | summary: $summary | shots: $ShotDir"
}
