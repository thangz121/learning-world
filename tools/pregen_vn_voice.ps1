# tools/pregen_vn_voice.ps1 - Vietnamese voice pack generator v2 (SOURCE-SWITCHABLE).
#   -Source azure  : Azure Neural TTS (vi-VN-HoaiMyNeural), key/region from HKCU\Environment.
#   -Source worker : project Cloudflare worker (lang=vi), no key.
# v2 additions: spelling pieces can set "stretch": true (SSML rate 0.5 -> ffmpeg trim +
# pitch-preserving atempo [manifest.stretch_tempo] + small pad) and per-word "lines"
# (teaching spell chain, same stretch recipe). Slow prompt clips stay SSML-baked 0.8x.
# Reads Content/vn_voice/manifest.json -> Assets/A_World/Resources/VietnameseVoice/*.mp3
param(
  [ValidateSet('azure','worker')][string]$Source = 'azure',
  [switch]$DryRun,
  [string]$Manifest = "D:\Vscode\little-world-english\Content\vn_voice\manifest.json",
  [string]$OutDir   = "D:\Vscode\little-world-english\Assets\A_World\Resources\VietnameseVoice",
  [string]$WorkerUrl = "https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/"
)

$ErrorActionPreference = "Stop"
$m = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
$stretchTempo = if ($m.stretch_tempo) { [string]$m.stretch_tempo } else { "0.6" }
"Source=$Source | clips=$($m.clips.Count) lines=$($m.lines.Count) | stretch_tempo=$stretchTempo"

$key = $null; $region = $null; $voiceName = 'vi-VN-HoaiMyNeural'
if ($Source -eq 'azure') {
  $key = (Get-ItemProperty -Path 'HKCU:\Environment' -Name LWE_AZURE_SPEECH_KEY -ErrorAction SilentlyContinue).LWE_AZURE_SPEECH_KEY
  $region = (Get-ItemProperty -Path 'HKCU:\Environment' -Name LWE_AZURE_SPEECH_REGION -ErrorAction SilentlyContinue).LWE_AZURE_SPEECH_REGION
  if (-not $key) { throw "Azure key not found in HKCU\Environment (LWE_AZURE_SPEECH_KEY)" }
  if (-not $region) { $region = 'southeastasia' }
  "Azure region=$region voice=$voiceName"
}

$ffmpeg = $null
if ($Source -eq 'azure') {
  $ffmpeg = Get-ChildItem "$env:LOCALAPPDATA\Microsoft\WinGet\Packages" -Recurse -File -Filter 'ffmpeg.exe' -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty FullName
  if (-not $ffmpeg) { $ffmpeg = 'ffmpeg' }
}

if (-not $DryRun) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }
$tmpRaw = Join-Path ([System.IO.Path]::GetTempPath()) "vnvoice_raw.mp3"

function ConvertTo-Escaped([string]$t) { return $t.Replace('&','&amp;').Replace('<','&lt;').Replace('>','&gt;') }

function Get-AzureAudio([string]$ssmlInner) {
  $ssml = "<speak version='1.0' xml:lang='vi-VN'><voice name='$voiceName'>$ssmlInner</voice></speak>"
  $uri = "https://$region.tts.speech.microsoft.com/cognitiveservices/v1"
  $headers = @{
    'Ocp-Apim-Subscription-Key' = $key
    'Content-Type' = 'application/ssml+xml'
    'X-Microsoft-OutputFormat' = 'audio-24khz-48kbitrate-mono-mp3'
  }
  return Invoke-WebRequest -Uri $uri -Method Post -Headers $headers -Body ([Text.Encoding]::UTF8.GetBytes($ssml)) -TimeoutSec 30 -UseBasicParsing
}

function Save-Stretched([byte[]]$rawBytes, [string]$dest) {
  [System.IO.File]::WriteAllBytes($tmpRaw, $rawBytes)
  $af = "silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.05,areverse,silenceremove=start_periods=1:start_threshold=-45dB:start_silence=0.05,areverse,atempo=$stretchTempo,apad=pad_dur=0.15"
  # PS 5.1: native stderr becomes ErrorRecords -> must run with Continue + silence both streams
  $prev = $ErrorActionPreference
  $ErrorActionPreference = 'Continue'
  & $ffmpeg -hide_banner -loglevel error -y -i $tmpRaw -af $af $dest 2>$null 1>$null
  $code = $LASTEXITCODE
  $ErrorActionPreference = $prev
  if ($code -ne 0) { throw "ffmpeg exited $code for $dest" }
}

$ok = 0; $fail = 0; $report = New-Object System.Collections.Generic.List[string]

foreach ($c in $m.clips) {
  if ($DryRun) { $report.Add("DRY $($c.key) [$($c.rate)] stretch=$($c.stretch) '$($c.text)'"); continue }
  try {
    if ($Source -eq 'azure') {
      $inner = ConvertTo-Escaped $c.text
      if ($c.rate -eq 'slow') { $inner = "<prosody rate='0.8'>$inner</prosody>" }
      if ($c.stretch) { $inner = "<prosody rate='0.5'>" + (ConvertTo-Escaped $c.text) + "</prosody>" }
      $resp = Get-AzureAudio $inner
    } else {
      $uri = $WorkerUrl + "?text=" + [uri]::EscapeDataString($c.text) + "&lang=vi&rate=" + $c.rate
      $resp = Invoke-WebRequest -Uri $uri -TimeoutSec 25 -UseBasicParsing
    }
    if ($resp.StatusCode -eq 200 -and ([string]$resp.Headers['Content-Type']) -like '*audio/mpeg*' -and $resp.RawContentLength -gt 0) {
      $dest = Join-Path $OutDir ($c.key + ".mp3")
      if ($c.stretch -and $Source -eq 'azure') { Save-Stretched $resp.Content $dest }
      else { [System.IO.File]::WriteAllBytes($dest, $resp.Content) }
      $ok++
      $report.Add("OK   $($c.key) rate=$($c.rate) stretch=$($c.stretch) raw=$($resp.RawContentLength)")
    } else {
      $fail++; $report.Add("FAIL $($c.key): HTTP $($resp.StatusCode)")
    }
  } catch {
    $fail++; $report.Add("FAIL $($c.key): $($_.Exception.Message)")
  }
}

foreach ($l in $m.lines) {
  if ($DryRun) { $report.Add("DRY LINE $($l.key) parts=$($l.parts.Count)"); continue }
  try {
    $sb = New-Object System.Text.StringBuilder
    foreach ($p in $l.parts) {
      [void]$sb.Append("<prosody rate='0.5'>").Append((ConvertTo-Escaped $p)).Append("</prosody>")
    }
    $resp = Get-AzureAudio $sb.ToString()
    if ($resp.StatusCode -eq 200 -and ([string]$resp.Headers['Content-Type']) -like '*audio/mpeg*' -and $resp.RawContentLength -gt 0) {
      $dest = Join-Path $OutDir ($l.key + ".mp3")
      Save-Stretched $resp.Content $dest
      $ok++
      $report.Add("OK   LINE $($l.key) parts=$($l.parts.Count) raw=$($resp.RawContentLength)")
    } else {
      $fail++; $report.Add("FAIL LINE $($l.key): HTTP $($resp.StatusCode)")
    }
  } catch {
    $fail++; $report.Add("FAIL LINE $($l.key): $($_.Exception.Message)")
  }
}

if (-not $DryRun) {
  $srcLine = if ($Source -eq 'worker') { "CloudflareTranslateTtsProvider worker (lang=vi)" } else { "Azure Neural TTS ($voiceName, region $region)" }
  $prov = "Vietnamese voice pack - $srcLine.`r`n" +
          "KHONG phai ban ghi nguoi that (AI/online synthesized - not human recordings). Generated: " + (Get-Date -Format 'yyyy-MM-dd HH:mm') + "`r`n" +
          "Prompt clips: normal + slow (0.8x SSML bake, natural pitch).`r`n" +
          "Spelling pieces + teaching lines: SSML 0.5x -> ffmpeg trim + atempo $stretchTempo (pitch preserved).`r`n" +
          "Keys pinned == runtime lookups. Regenerate: tools/pregen_vn_voice.ps1 -Source azure|worker"
  [System.IO.File]::WriteAllText((Join-Path $OutDir "PROVENANCE.txt"), $prov, [System.Text.UTF8Encoding]::new($false))
}
$reportPath = "D:\Vscode\little-world-english\tools\pregen_vn_report.txt"
[System.IO.File]::WriteAllLines($reportPath, $report, [System.Text.UTF8Encoding]::new($false))
"OK=$ok FAIL=$fail -> report: $reportPath"
