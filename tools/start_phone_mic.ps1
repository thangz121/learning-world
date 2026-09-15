# tools/start_phone_mic.ps1 — 1-click launcher for the phone gateway (mic + camera).
#
#   Right-click > Run with PowerShell  (or:  powershell -ExecutionPolicy Bypass -File tools/start_phone_mic.ps1)
#
# What it does (no IP typing, ever):
#   [1] auto-detects the PC LAN IP (prefers 192.168.x),
#   [2] checks tools/lan.crt + tools/lan.key exist and cover that IP,
#   [3] starts the gateway (STEP logs + mic/cam/unified QR PNGs + /qr.png + /cam-qr.png + /phone-qr.png),
#   [4] opens the PC browser at https://127.0.0.1:8443/phone-qr showing the BIG UNIFIED QR,
#   [5] you scan that QR with the phone camera -> mic + camera panel opens, no typing.
#
# Then on the phone: START MIC + START CAMERA > allow mic + camera > speak > STOP.
# Watch EITHER side to see how far it got:
#   PC window: [STEP 1/9]..[STEP 9/9] + [CAM 1/4]..[CAM 4/4]   Phone page: per-panel STEP 1/5..5/5 + shared steps log.
#
# Cloudflare survival (proxy on/off, different AP, DHCP IP change):
#   - Default (no -PublicUrl): LAN-only, offline-first (unchanged).
#   - With -PublicUrl "https://mic.example.com": the phone page fails over
#     between LAN and Cloudflare automatically, and an active stream
#     auto-resumes with a fresh session — no re-scan needed.
#     Requires: cloudflared tunnel --url https://127.0.0.1:<port> --no-tls-verify
param(
  [int]$HttpsPort = 8443,
  [string]$Cert = "$PSScriptRoot\lan.crt",
  [string]$Key = "$PSScriptRoot\lan.key",
  [string]$PublicUrl = ""
)

$ErrorActionPreference = "Stop"
Write-Host "=== LWE phone-mic launcher ===" -ForegroundColor Cyan

# [1] LAN IP (same preference order as tools/lwe_qr.py)
$ips = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
  Where-Object { $_.IPAddress -notmatch '^(127\.|169\.254\.)' -and $_.PrefixOrigin -ne 'WellKnown' } |
  Select-Object -ExpandProperty IPAddress -Unique
$lan = $ips | Where-Object { $_ -like '192.168.*' } | Select-Object -First 1
if (-not $lan) { $lan = $ips | Where-Object { $_ -like '10.*' } | Select-Object -First 1 }
if (-not $lan) { $lan = $ips | Where-Object { $_ -like '172.1[6-9].*' -or $_ -like '172.2*.' -or $_ -like '172.3[01].*' } | Select-Object -First 1 }
if (-not $lan) { Write-Host "FAIL: no LAN IPv4 found (Wi-Fi off?). Candidates: $($ips -join ', ')" -ForegroundColor Red; exit 2 }
Write-Host "[1/3] LAN IP = $lan  (candidates: $($ips -join ', '))" -ForegroundColor Green

# [2] cert + key present? (gateway re-checks coverage as STEP 2 and fails fast)
if (-not (Test-Path $Cert)) { Write-Host "FAIL: cert not found: $Cert" -ForegroundColor Red; exit 2 }
if (-not (Test-Path $Key)) { Write-Host "FAIL: key not found: $Key" -ForegroundColor Red; exit 2 }
Write-Host "[2/3] cert+key OK ($Cert)" -ForegroundColor Green
Write-Host "      (if the gateway STEP 2 says the cert does NOT cover $lan :"
Write-Host "       re-issue it for this IP, or give this PC a DHCP reservation so the IP stops changing.)" -ForegroundColor DarkGray

# [3] start gateway + show QR
Write-Host "[3/3] starting gateway — keep this window open (STEP/CAM logs appear here)..." -ForegroundColor Green
Write-Host "      after STEP 5: scan the QR with the phone camera. No typing." -ForegroundColor Yellow
if ($PublicUrl) { Write-Host "      public fallback (Cloudflare): $PublicUrl" -ForegroundColor Cyan }
Start-Sleep -Seconds 1
Start-Process "https://127.0.0.1:$HttpsPort/phone-qr"
if ($PublicUrl) { & python "$PSScriptRoot\phone_mic_gateway.py" --cert $Cert --key $Key --https-port $HttpsPort --public-url $PublicUrl }
else { & python "$PSScriptRoot\phone_mic_gateway.py" --cert $Cert --key $Key --https-port $HttpsPort }
