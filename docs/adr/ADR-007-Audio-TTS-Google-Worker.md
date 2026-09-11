# ADR-007 — Google TTS via Cloudflare Worker (TTS infra duy nhất)

Decision: Google TTS qua Cloudflare Worker Proxy hiện có là TTS infrastructure chính và duy nhất cho output. Unity không giữ Google API key, không gọi Google trực tiếp. STT/input giữ cloud STT + Pronunciation Assessment; core game offline bằng pre-gen audio.
Context: đã có Worker Proxy (có field `{lang}`), game là Windows EXE online-optional, audio là Tier 1, latency phá immersion trẻ 4 tuổi.
Alternatives: Azure TTS song song (thêm SDK/billing/voice lệch màu — loại), fully-offline TTS local (chất lượng chưa đủ cho pillar audio — để sau), gọi Google trực tiếp từ Unity (lộ key — cấm).
Why: 1 boundary duy nhất (`CloudflareTranslateTtsProvider` — canonical name từ W1, alias `CloudflareGoogleTtsProvider` đã xóa), cache L1–L4, gameplay chỉ gọi `IAudioDirector`. VoiceProfile là abstraction nội bộ (v6.4.1: Worker Translate chưa map voice — mapping có hiệu lực khi provider tương lai hỗ trợ).
Consequences: Slice freeze `lang=en-US` nhưng giữ `LanguageCode`; vocab/dialogue pre-gen + `approved=true`; realtime TTS fail không được chặn learning core; lint cấm gọi Worker/Google từ gameplay.
