# v6.4.1 Patch — Worker contract thật (READY FOR W0-T1: YES)

## Worker freeze (hết REQUIRES USER CONFIG cho path/auth)
* `GET https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/` + `?text=&lang=&rate=`. No auth. Root `/`. `audio/mpeg`. text ≤200 (Unity cắt/chia). lang default `vi`, Slice gửi explicit `en-US` (D verify/normalize W0-T1). rate default `normal`.
* Xóa các field Worker không hỗ trợ khỏi boundary: voice/pitch/style/format không gửi.

## Architecture correction
* Worker = Google Translate TTS Source Provider (không phải Cloud Neural2). Capability freeze: language YES, rate LIMITED, voice/pitch/SSML NO.
* `TtsRequest` nội bộ giữ 8 fields (cache key + provider tương lai); provider map subset text/lang/rate (Rate>=0.8→normal).
* VoiceProfile là abstraction (product requirement NPC balanced), không fake voice switching hôm nay.
* Slow master: normal source → pitch-preserving time-stretch 0.60–0.70x → QA → approved; cấm `AudioSource.pitch`; worker rate=slow chỉ fallback.

## Files
WorkerTtsContract.md (rewrite), AUDIO_DESIGN.md (§2/3/5b/8), Services.md, ARCHITECTURE §1/§3/§6, ADR-007, GameInstaller.cs comments.
## Status: READY FOR W0-T1: YES. Audio pre-gen pipeline ưu tiên sớm (Tier 1).
