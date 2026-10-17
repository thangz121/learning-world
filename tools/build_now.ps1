# tools/build_now.ps1 - one-shot project build/suite runner for ANY agent (CLI or computer-use).
# Usage:
#   powershell -ExecutionPolicy Bypass -File tools/build_now.ps1            # player build (TempBuildP62)
#   powershell -ExecutionPolicy Bypass -File tools/build_now.ps1 -Suite     # EditMode test suite
# Writes tools/build_result.txt (1 line: timestamp | status | detail) and exits 0 on success.
# Guards: concurrency (one Unity at a time), hang timeout, license-198 guidance.
param(
  [switch]$Suite,
  [int]$TimeoutMin = 15
)
$ErrorActionPreference = 'Stop'
$root = 'D:\Vscode\little-world-english'
$unity = 'D:\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
$resultFile = Join-Path $root 'tools\build_result.txt'

function Finish([string]$status, [string]$detail) {
  $line = "$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss') | $status | $detail"
  [System.IO.File]::WriteAllText($resultFile, $line + "`r`n", [System.Text.UTF8Encoding]::new($false))
  Write-Output $line
  if ($status -eq 'SUCCEEDED') { exit 0 } else { exit 1 }
}

# 1) Concurrency guard: this machine shares one Unity licensing client - serialize runs.
if (Get-Process Unity -ErrorAction SilentlyContinue) {
  Finish 'BLOCKED' 'another Unity process is already running - wait for it to finish, then rerun'
}

# 2) Prepare log + arguments
$log = Join-Path $root ('Temp\agent-run-' + (Get-Date -Format 'HHmmss') + '.log')
$xmlResults = Join-Path $root 'Temp\agent-suite.xml'

# 3) Launch + bounded wait (Unity.exe launcher returns immediately; poll the real process)
if ($Suite) {
  Remove-Item $xmlResults -ErrorAction SilentlyContinue
  & $unity -batchmode -projectPath $root -runTests -testPlatform EditMode -testResults $xmlResults -logFile $log
} else {
  & $unity -batchmode -projectPath $root -executeMethod TempBuildP62.Build -logFile $log -quit
}
$deadline = (Get-Date).AddMinutes($TimeoutMin)
while ((Get-Process Unity -ErrorAction SilentlyContinue) -and (Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 10
}
if (Get-Process Unity -ErrorAction SilentlyContinue) {
  Get-Process Unity | Stop-Process -Force -ErrorAction SilentlyContinue
  Finish 'HUNG' "timed out after ${TimeoutMin}m and was killed. If the log mentions LicenseClient waiting: close Unity, kill stale Unity.Licensing.Client, start Unity Hub again, then rerun. log=$log"
}

# 4) Parse results
$text = if (Test-Path $log) { Get-Content $log -Raw } else { '' }
if ($Suite) {
  if (-not (Test-Path $xmlResults)) {
    $alt = Join-Path $env:LOCALAPPDATA '..\LocalLow\DefaultCompany\little-world-english\TestResults.xml'
    if (Test-Path $alt) { $xmlResults = $alt }
  }
  if (Test-Path $xmlResults) {
    [xml]$x = Get-Content $xmlResults
    $r = $x.SelectSingleNode('//test-run')
    $detail = "suite total=$($r.total) passed=$($r.passed) failed=$($r.failed) skipped=$($r.skipped) | log=$log"
    if ([int]$r.failed -eq 0) { Finish 'SUCCEEDED' $detail } else { Finish 'FAILED' $detail }
  } else {
    Finish 'FAILED' "no test results xml was produced | log=$log"
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
