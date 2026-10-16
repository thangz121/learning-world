# tools/pregen_vn_voice.ps1 - Vietnamese voice pack generator (SOURCE-SWITCHABLE).
#   -Source azure  : Azure Neural TTS (vi-VN-HoaiMyNeural), key/region from HKCU\Environment
#                    (LWE_AZURE_SPEECH_KEY / LWE_AZURE_SPEECH_REGION). Slow = SSML rate 0.8 baked in.
#   -Source worker : project Cloudflare worker (lang=vi), no key. Slow = worker rate=slow baked in.
# Reads Content/vn_voice/manifest.json -> Assets/A_World/Resources/VietnameseVoice/*.mp3
# Verifies HTTP 200 + audio/mpeg + size>0. Writes PROVENANCE.txt + a report.
# Slow clips are BAKED at 0.8x (natural pitch) -> runtime must play them at natural 1.0.
param(
  [ValidateSet('azure','worker')][string]$Source = 'azure',
  [switch]$DryRun,
  [string]$Manifest = "D:\Vscode\little-world-english\Content\vn_voice\manifest.json",
  [string]$OutDir   = "D:\Vscode\little-world-english\Assets\A_World\Resources\VietnameseVoice",
  [string]$WorkerUrl = "https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/"
)

$ErrorActionPreference = "Stop"
$m = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
"Source=$Source | Manifest clips: $($m.clips.Count)"

$key = $null; $region = $null; $voiceName = 'vi-VN-HoaiMyNeural'
if ($Source -eq 'azure') {
  $key = (Get-ItemProperty -Path 'HKCU:\Environment' -Name LWE_AZURE_SPEECH_KEY -ErrorAction SilentlyContinue).LWE_AZURE_SPEECH_KEY
  $region = (Get-ItemProperty -Path 'HKCU:\Environment' -Name LWE_AZURE_SPEECH_REGION -ErrorAction SilentlyContinue).LWE_AZURE_SPEECH_REGION
  if (-not $key) { throw "Azure key not found in HKCU\Environment (LWE_AZURE_SPEECH_KEY)" }
  if (-not $region) { $region = 'southeastasia' }
  "Azure region=$region voice=$voiceName"
}

if (-not $DryRun) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

function ConvertTo-Ssml([string]$text, [string]$rate) {
  $safe = $text.Replace('&','&amp;').Replace('<','&lt;').Replace('>','&gt;')
  $inner = $safe
  if ($rate -eq 'slow') { $inner = "<prosody rate='0.8'>$safe</prosody>" }
  return "<speak version='1.0' xml:lang='vi-VN'><voice name='$voiceName'>$inner</voice></speak>"
}

$ok = 0; $fail = 0; $report = New-Object System.Collections.Generic.List[string]
foreach ($c in $m.clips) {
  if ($DryRun) { $report.Add("DRY $($c.key) [$($c.rate)] '$($c.text)'"); continue }
  try {
    if ($Source -eq 'azure') {
      $uri = "https://$region.tts.speech.microsoft.com/cognitiveservices/v1"
      $headers = @{
        'Ocp-Apim-Subscription-Key' = $key
        'Content-Type' = 'application/ssml+xml'
        'X-Microsoft-OutputFormat' = 'audio-24khz-48kbitrate-mono-mp3'
      }
      $body = [Text.Encoding]::UTF8.GetBytes((ConvertTo-Ssml $c.text $c.rate))
      $resp = Invoke-WebRequest -Uri $uri -Method Post -Headers $headers -Body $body -TimeoutSec 30 -UseBasicParsing
    } else {
      $uri = $WorkerUrl + "?text=" + [uri]::EscapeDataString($c.text) + "&lang=vi&rate=" + $c.rate
      $resp = Invoke-WebRequest -Uri $uri -TimeoutSec 25 -UseBasicParsing
    }
    $type = [string]$resp.Headers['Content-Type']
    if ($resp.StatusCode -eq 200 -and $type -like '*audio/mpeg*' -and $resp.RawContentLength -gt 0) {
      $dest = Join-Path $OutDir ($c.key + ".mp3")
      [System.IO.File]::WriteAllBytes($dest, $resp.Content)
      $ok++
      $report.Add("OK   $($c.key) [$($c.rate)] $($resp.RawContentLength) bytes")
    } else {
      $fail++
      $report.Add("FAIL $($c.key): HTTP $($resp.StatusCode) type=$type")
    }
  } catch {
    $fail++
    $report.Add("FAIL $($c.key): $($_.Exception.Message)")
  }
}

if (-not $DryRun) {
  $srcLine = "Azure Neural TTS ($voiceName, region $region)" 
  if ($Source -eq 'worker') { $srcLine = "CloudflareTranslateTtsProvider worker (lang=vi)" }
  $prov = "Vietnamese voice pack - $srcLine.`r`n" +
          "KHONG phai ban ghi nguoi that (AI/online synthesized - not human recordings). Generated: " + (Get-Date -Format 'yyyy-MM-dd HH:mm') + "`r`n" +
          "Slow clips are BAKED at 0.8x (SSML prosody, natural pitch) - play them at natural speed.`r`n" +
          "Source manifest: Content/vn_voice/manifest.json (keys pinned == runtime lookups).`r`n" +
          "Regenerate: powershell -File tools/pregen_vn_voice.ps1 -Source azure|worker"
  [System.IO.File]::WriteAllText((Join-Path $OutDir "PROVENANCE.txt"), $prov, [System.Text.UTF8Encoding]::new($false))
}
$reportPath = "D:\Vscode\little-world-english\tools\pregen_vn_report.txt"
[System.IO.File]::WriteAllLines($reportPath, $report, [System.Text.UTF8Encoding]::new($false))
"OK=$ok FAIL=$fail -> report: $reportPath"
