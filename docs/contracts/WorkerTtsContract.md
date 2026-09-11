# contracts/WorkerTtsContract.md — Unity ↔ Cloudflare Worker (v6.4.1 FROZEN, Worker thật)

> Contract khớp Worker thực tế đã deploy (user xác nhận). Không còn REQUIRES USER CONFIG cho path/auth. Source: Google Translate TTS (không phải Cloud Neural2). Unity không giữ key, không gọi Google trực tiếp.

## Request (Unity → Worker) — FREEZE

* Method: `GET`
* Base (freeze): `https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/`
* Path: `/` (root, không thêm path)
* Auth: không cần hiện tại
* Query bắt buộc: `text={URL_ENCODED_TEXT}` (tối đa 200 ký tự — Unity phải cắt/chia câu dài trước khi gửi)
* Query optional: `lang={LANG}` (mặc định Worker: `vi`; Slice luôn gửi explicit, freeze `en-US` — D verify thực tế ở W0-T1, normalize nếu Worker cần `en`, không sửa gameplay)
* Query optional: `rate=normal|slow` (mặc định `normal`)

Ví dụ:
```text
https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/?text=Hello%20world&lang=en&rate=normal
```

## Internal → Worker mapping (CloudflareTranslateTtsProvider, Agent D)

`TtsRequest` nội bộ giữ đủ field cho cache key + provider tương lai, nhưng Worker hiện tại chỉ nhận subset:

| TtsRequest | Gửi Worker | Ghi chú |
|------------|-----------|---------|
| Text (≤200 chars) | `text` | Cắt/chia trước khi gửi |
| LanguageCode | `lang` | Pass-through, Slice `en-US` |
| Rate float | `rate` | `>=0.8 → normal`, `<0.8 → slow` |
| VoiceProfileId | KHÔNG gửi | Worker không đổi voice (xem capability) |
| Pitch / Style / Format | KHÔNG gửi | Không hỗ trợ; vẫn nằm trong cache key nội bộ |

## Response — FREEZE

* HTTP 200, body audio binary, `Content-Type: audio/mpeg`. Unity đọc binary → Disk cache → phát.
* Không header cache nào được đảm bảo → Unity tự tính `cacheKey` = SHA256(text+voice+locale+rate+pitch+style+format) cho L1–L4 nội bộ.

## Current Provider Capability (freeze, không fake)

```text
language selection: YES (lang query)
rate hint: LIMITED (normal|slow, chênh lệch nhỏ)
distinct voice selection: NOT GUARANTEED
male/female selection: NOT GUARANTEED
SSML: NO
pitch control: NO
```

## Error handling → fallback (Unity)

| Lỗi | Hành vi Unity |
|-----|---------------|
| 400 / text >200 chars (client) | Cắt/chia lại ở Unity, không retry mù |
| 429 rate limit | Backoff + cache/pre-gen; dynamic chuyển Queue, quá hạn cancel êm |
| 5xx worker/provider | 1 retry → fallback pre-gen/offline intent |
| timeout/network | Coi như offline → FallbackSpeechProvider + pre-gen local |
| response không phải audio/rỗng | Bỏ, dùng pre-gen, log QA |

* Realtime TTS fail không bao giờ chặn learning core (ship rule).
