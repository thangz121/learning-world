# tools/pregen_vn_voice.ps1 - Vietnamese voice pack generator (works with the existing
# CloudflareTranslateTtsProvider worker; lang=vi; no API key required).
# Reads Content/vn_voice/manifest.json -> downloads mp3 -> Assets/A_World/Resources/VietnameseVoice/
# Verifies HTTP 200 + audio/mpeg + size>0. Writes PROVENANCE.txt + a report.
# Usage:
#   powershell -File tools/pregen_vn_voice.ps1 -DryRun
#   powershell -File tools/pregen_vn_voice.ps1
param(
  [switch]$DryRun,
  [string]$Manifest = "D:\Vscode\little-world-english\Content\vn_voice\manifest.json",
  [string]$OutDir   = "D:\Vscode\little-world-english\Assets\A_World\Resources\VietnameseVoice",
  [string]$BaseUrl  = "https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/"
)

$ErrorActionPreference = "Stop"
$m = Get-Content -LiteralPath $Manifest -Raw | ConvertFrom-Json
"Manifest clips: $($m.clips.Count)"
if (-not $DryRun) { New-Item -ItemType Directory -Force -Path $OutDir | Out-Null }

$ok = 0; $fail = 0; $report = New-Object System.Collections.Generic.List[string]
foreach ($c in $m.clips) {
  $url = $BaseUrl + "?text=" + [uri]::EscapeDataString($c.text) + "&lang=vi&rate=" + $c.rate
  if ($DryRun) { $report.Add("DRY $($c.key) [$($c.rate)] '$($c.text)'"); continue }
  try {
    $resp = Invoke-WebRequest -Uri $url -TimeoutSec 25 -UseBasicParsing
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
  $prov = "Vietnamese voice pack - ONLINE TTS (CloudflareTranslateTtsProvider worker, lang=vi).`r`n" +
          "KHONG phai ban ghi nguoi that (not human recordings). Generated: " + (Get-Date -Format 'yyyy-MM-dd HH:mm') + "`r`n" +
          "Source manifest: Content/vn_voice/manifest.json (keys pinned == runtime lookups).`r`n" +
          "Can be replaced/supplemented by studio recordings or an Azure TTS batch later."
  [System.IO.File]::WriteAllText((Join-Path $OutDir "PROVENANCE.txt"), $prov, [System.Text.UTF8Encoding]::new($false))
}
$reportPath = "D:\Vscode\little-world-english\tools\pregen_vn_report.txt"
[System.IO.File]::WriteAllLines($reportPath, $report, [System.Text.UTF8Encoding]::new($false))
"OK=$ok FAIL=$fail -> report: $reportPath"
