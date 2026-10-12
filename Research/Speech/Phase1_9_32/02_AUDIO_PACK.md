# 02 — AUDIO PACK (listening masters) — WP-1.9.32

> **Tóm tắt (VI):** 16 master MP3 (8 từ × normal/slow) được sinh LOCAL bằng Piper, tự kiểm
> ASR 16/16, nằm trong StreamingAssets để PregenSeeder nạp offline; manifest (Content + mirror)
> có 16 entry `listen_*` voice `listen_v1`; validator content+audio PASS; khoá cache khớp runtime
> được khoá bằng test (CT-S21F) và CT-P08B.

## Masters (Assets/StreamingAssets/audio/)

| id | file | bytes |
|---|---|---:|
| listen_apple_normal | listen_apple_normal.mp3 | 4,344 |
| listen_apple_slow | listen_apple_slow.mp3 | 5,352 |
| listen_ball_normal | listen_ball_normal.mp3 | 4,248 |
| listen_ball_slow | listen_ball_slow.mp3 | 4,488 |
| listen_banana_normal | listen_banana_normal.mp3 | 5,256 |
| listen_banana_slow | listen_banana_slow.mp3 | 5,880 |
| listen_orange_normal | listen_orange_normal.mp3 | 5,016 |
| listen_orange_slow | listen_orange_slow.mp3 | 5,952 |
| listen_carrot_normal | listen_carrot_normal.mp3 | 5,184 |
| listen_carrot_slow | listen_carrot_slow.mp3 | 5,688 |
| listen_tomato_normal | listen_tomato_normal.mp3 | 5,448 |
| listen_tomato_slow | listen_tomato_slow.mp3 | 5,640 |
| listen_watermelon_normal | listen_watermelon_normal.mp3 | 6,048 |
| listen_watermelon_slow | listen_watermelon_slow.mp3 | 7,200 |
| listen_grape_normal | listen_grape_normal.mp3 | 5,280 |
| listen_grape_slow | listen_grape_slow.mp3 | 5,856 |

- Mono 24 kHz MP3; mọi file > 2 KB (sàn chống-im-lặng của CT-P08A); frame sync FF F3 hợp lệ.
- Manifest entry dùng chung: `voice=listen_v1`, `lang=en-US`, `pitch=0`, `style=Clear`,
  `format=Mp3_44100`, `rate=0.85` (normal) / `0.70` (slow).

## Tính offline (khoá bằng test)

- Runtime: `ListeningGarden.PlayWord` gọi `SpeakAsync(DialogueRequest(text, listen_v1, en-US,
  0.85|0.70, 0, Clear, Mp3_44100, P1))`.
- `AudioCache.CacheKey` của tuple đó PHẢI bằng `PregenManifestIndex.CacheKeyFor(entry)` —
  được khoá bởi **CT-S21F** (16/16 entry) và gián tiếp bởi CT-P08A/B (mirror hợp lệ, không mồ côi).
- `PregenSeeder` copy master → L2 (`persistentDataPath/audio/<key>.mp3`) lúc boot ⇒ **mọi câu từ
  phát offline 100%**, kể cả lần chơi đầu tiên. Cache miss (nếu thêm từ mới mà quên pregen) rơi
  về worker giống mọi câu khác của sản phẩm — không bao giờ im lặng.

## Kiểm định

- `tools/validate_content.py Content/vocab Content/quests Content/dialogues` → **PASS** (voice
  `listen_v1` được thêm vào pool hợp lệ; không lỗi mirror/binary).
- CT-P08A cập nhật kỳ vọng 12 → **28 entry** (12 cũ + 16 mới) — mirror hợp lệ toàn bộ.
- ASR self-check (faster-whisper base.en): 16/16 khớp chính xác (lần chốt và lần chạy lại).
