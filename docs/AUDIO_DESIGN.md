# AUDIO_DESIGN.md — Audio Bible (Tier 1, v6)

> Âm thanh là 1 trong 3 pillar Tier 1 (Visual / Audio / Learning Content). Mọi quyết định audio phải trace về file này. TTS: Google TTS qua Cloudflare Worker Proxy hiện có — infra duy nhất. Unity không biết API key, không gọi Google trực tiếp.

## 1. Platform

* Standalone Windows EXE, online-optional. Không phải web, không chạy trình duyệt.
* Core offline: 15 Active vocab + common dialogue pre-gen + world/gameplay/quest.
* Online enhancement: Cloud STT, Pronunciation Assessment, Dynamic AI, Dynamic runtime TTS. Mất mạng → Fallback, quest không kẹt.

## 2. Luồng chuẩn (v6.4.1: Worker = Google Translate TTS Source Provider)

```
Gameplay → DialogueRequest → IAudioDirector → Speech Audio Resolver
→ L1 Memory Cache → L2 Disk Cache → Pre-generated Addressables
→ Cloudflare Worker → Google Translate TTS
```

Worker hiện tại là Google Translate TTS source (không phải Cloud Neural2). Chi tiết request/response: `contracts/WorkerTtsContract.md`.

Cấm: `GoogleTTS.Speak()`, gọi Worker/Google trực tiếp từ `A_World`/`B_Brain`. Chỉ Agent D sở hữu provider + cache + mixer.

## 3. Voice Profiles (v6.4.1: abstraction thật, không fake voice switching)

Gameplay/content chỉ biết ID (`milo_v1`, `learning_v1`, `mia_v1`, pool NPC). **Worker Translate hiện tại KHÔNG đổi voice theo profile** — profile là abstraction để sau này thay provider mà không sửa gameplay (yêu cầu product), không phải cam kết voice thật hôm nay.

| Profile | Tính cách (mục tiêu) | Rate nội bộ | Ghi chú |
|---------|----------------------|-------------|---------|
| `milo_v1` | Warm, playful, friendly, energetic, clear — FIXED identity | 0.85 → worker `normal` | Companion, encouragement ≤8 từ |
| `learning_v1` | Neutral, extremely clear, consistent, minimal emotion — FIXED | normal 0.85 / slow 0.70 | Đọc vocabulary/pronunciation duy nhất |
| `mia_v1` | Friendly, soft, clear, slightly slower — FIXED | 0.9 → worker `normal` | Shopkeeper |
| `npc_female_01..03` / `npc_male_01..03` | Pool cân bằng (product requirement) | 0.9 → worker `normal` | Balanced-random theo NpcId + persist; male/female THẬT phụ thuộc provider tương lai — KHÔNG fake |

* Slice freeze `lang = en-US` (luôn gửi explicit; D verify/normalize ở W0-T1).
* Mapping Profile → Google voice nằm ở Worker/config **khi provider tương lai hỗ trợ**; với Worker hiện tại, mapping là no-op có document. Voice Shootout QA vẫn chạy để chọn provider/voice tương lai, không để claim sai hiện tại.

## 4. Voice assignment deterministic (NPC phụ — v6.1 Part E)

* Assign 1 lần khi NPC spawn: `NpcId → VoiceProfile → persist` (`save.npcVoices`) xuyên session/save. Không random lại mỗi câu thoại.
* Thuật toán freeze: `order = StableShuffle(allNpcIds, hash(worldSeed))`; duyệt order, gán xen kẽ F/M từ pool (`npc_female_01..03`, `npc_male_01..03`) sao cho tổng population cân bằng (10 NPC ≈ 5F/5M, lẻ chênh ≤1). `worldSeed` lưu trong save (ổn định theo profile). Load save ưu tiên assignment cũ; NPC mới xuất hiện lấy slot của giới đang thiếu.
* Cấm `Random.Range()` không persist. Milo (`milo_v1`) / Mia (`mia_v1`) / Learning (`learning_v1`) luôn fixed, không qua selector.

## 5. 3 chế độ audio

* **MODE A — Learning Master (pre-gen, QA, local):** 15 Active × normal + slow; breakdown/syllable chỉ từ khó cần. Ví dụ `apple_normal.mp3`, `apple_slow.mp3`.
* **MODE B — Common Dialogue (pre-gen, zero latency):** pack 30–40 câu (greeting/instruction/encouragement/correct/retry/hint/transition/completion/Milo/Mia). Trẻ bấm → phát ngay, không chờ mạng.
* **MODE C — Dynamic (runtime):** chỉ câu chưa biết trước (AI/personalized). `Validator → Worker (rate normal|slow) → Google Translate TTS → cache → playback`. Worker `rate=slow` chỉ là fallback/runtime convenience.

### 5b. Slow asset pipeline (v6.4.1 — không dùng worker rate cho master)

Worker `rate=slow` chênh lệch không đủ cho production. Cho vocabulary Active:
```
Google TTS normal source → audio processing pipeline → normal asset
→ pitch-preserving time-stretch (≈0.60–0.70x, giữ pitch tự nhiên)
→ slow asset → human QA → approved → Addressables → ship trong EXE
```
Cấm dùng `AudioSource.pitch` đơn thuần cho production slow audio (vỡ pitch). Mục tiêu: normal tự nhiên, slow 0.60–0.70x giữ pitch.

## 6. Audio Priority + Focus + Interruption Policy (v6.1 Part H — Agent D không tự suy diễn)

P0 Safety > P1 Pronunciation > P2 Instruction > P3 Dialogue > P4 Feedback > P5 SFX > P6 Ambient > P7 Music. Một voice tại 1 thời điểm.

| Tình huống | Hành vi freeze |
|------------|----------------|
| P1 đến khi P3 đang phát | P3 fade-out 150ms + PauseResume sau P1 (P1 không bao giờ bị chen) |
| P0 đến bất cứ lúc nào | Interrupt tất cả, phát ngay |
| P2 đến khi P3 đang phát | Queue sau câu P3 hiện tại (không cắt ngang), timeout queue 5s |
| P4 đến khi P1/P2 đang phát | Ignore (feedback chờ lượt sau, tránh spam) |
| P5/P6/P7 khi speech P1–P4 đang phát | Duck (music −80%? music duck 20%, ambient duck 40% khi P1; tắt hẳn SFX không liên quan) |
| Cùng priority | Queue FIFO, max queue 3; quá thì drop cũ nhất + log |
| Duplicate request (cùng text+voice trong 2s) | Dedupe: bỏ qua, không phát 2 lần (chống spam "apple apple") |
| Cancel (quest đổi/hint mới) | CancellationToken hủy TTS promise; audio đã phát >300ms thì fade-out, chưa phát thì drop |
| Scene unload khi audio phát | Stop tất cả voice P1–P4 + cancel request pending; music crossfade |
| Dynamic TTS về sau khi đã cancel | Drop silencely + log (không phát muộn gây lệch ngữ cảnh) |

Khi P1 chạy: music duck 20%, ambient duck 40%, pause NPC voice khác, giữ window tương tác 2–3s.

## 7. Cache L1–L4

Memory → Disk → Cloudflare Edge → Google TTS. Key = SHA256(text+voice+locale+rate+pitch+style+format). Milo và LearningVoice không đè nhau.

## 8. TTS Request (v6.4.1: nội bộ đủ field, Worker dùng subset)

```csharp
record TtsRequest(string Text, VoiceProfileId Voice, LanguageCode Lang, float Rate, float Pitch, SpeechStyle Style, AudioFormat Format, AudioPriority Priority);
```

Nội bộ giữ đủ 8 fields cho cache key + provider tương lai. Worker Translate hiện tại chỉ nhận `text/lang/rate` (mapping: Rate>=0.8→normal, <0.8→slow; Voice/Pitch/Style/Format không gửi — xem WorkerTtsContract + capability matrix).

Ví dụ Milo `("Great job!", milo_v1, en-US, 0.85, 0.0, Excited, Mp3_44100, P4)`; vocab `("Apple", learning_v1, en-US, 0.75, 0.0, Clear, Mp3_44100, P1)`. Cache key = SHA256 của đúng 7 field (text+voice+locale+rate+pitch+style+format). Chi tiết Worker: `contracts/WorkerTtsContract.md`.

## 9b. Pre-gen pipeline + ownership (v6.1 Part K)

```
Content JSON (C: text + approval intent)
→ D chọn VoiceProfile + gửi Worker (lang/rate/pitch/style/format)
→ nhận audio → normalize (44.1kHz, peak −3dB) → validate (không rỗng, đúng format)
→ QA nghe (shootout criteria) → metadata generated=true/approved=true
→ asset vào Addressables/build (D sở hữu file)
```

Source of truth: text thuộc C; voice assignment + file audio thuộc D/config; C và D không sửa cùng 1 audio file (C sửa text → D regen).

## 9. QA Pipeline + Voice Shootout

GENERATE → LISTEN → PRONUNCIATION QA → CHILD COMPREHENSION QA → APPROVE → SHIP. Vocab metadata `audio.approved=true` bắt buộc, CI fail nếu thiếu. Shootout script chung (`Apple. / Can you find the apple? / Great job! / Let's try again! / Look over here!`) chấm clarity/pronunciation/friendliness/warmth/naturalness/slow/emotion/consistency trước khi freeze mapping Google voice.

## 10. Quality rules

* Word-count: NPC thường ≤6, Milo encouragement ≤8 (Validator đếm trước TTS).
* Không learning experience core nào được phụ thuộc realtime TTS thành công.
