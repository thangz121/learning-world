# start_speak_assess.ps1 — one-click local speak-assess service (free, offline).
# The speaking arena can also auto-start it; this script is for manual runs
# (visible logs, custom port/engine).
param(
  [int]$Port = 8470,
  [string]$Engine = "base.en"
)
$ErrorActionPreference = "Stop"
$tools = Split-Path -Parent $MyInvocation.MyCommand.Path
$py = "python"
try { & $py --version | Out-Null } catch { $py = "py" }
Write-Host "[speak-assess] starting on port $Port (engine $Engine)..."
& $py (Join-Path $tools "speak_assess_server.py") --port $Port --engine $Engine
