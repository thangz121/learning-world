# B2 DATA ACQUISITION DECISION - WP-1.9.28 Part 28

> **Tóm tắt (VI):** Chưa có kết quả pilot → **DO_NOT_ACQUIRE_NOW**. Không mua MyST, không đăng ký
> tải corpus restricted, không bắt đầu thu thập. Route xếp hạng giữ nguyên từ WP-1.9.27; quyết định
> mua/thu thập phải chờ pilot dương tính và license/annotation rõ ràng.

## Decision

**DO_NOT_ACQUIRE_NOW** — no purchase, no restricted download, no collection start. The pilot has not
been executed (no human labels), so there is no evidence to justify spending.

## Ranked routes (unchanged from WP-1.9.27)

| rank | route | why needed | scale needed | license | annotation cost | GPU |
|---:|---|---|---|---|---|---|
| 1 | Local collection (Vietnamese-L1 / /r/-dense) | only route solving age + L1 + labels + licensing | 30-50 speakers, 2-4 h | own consent/assent | human phone labels (protocol exists) | GPU for adaptation |
| 2 | Public age-appropriate data (OCSC 303 4-9, JIBO 110 4-7, AusKidTalk 620 3-12, CAPIL 30 5-6) | age + speaker diversity for research | 100+ speakers | research registration / author confirmation (JIBO) | forced alignment + human verification | GPU for adaptation |
| 3 | MyST (LDC2021S05) | only verified commercial-scale route; 470 h, 1371 speakers, lexicon | 10-30 h subset for adaptation | paid commercial (quote required) | alignment + 500-2000 spot labels | GPU |
| 4 | SIAK after legal review | local age-4-6 words | 172 speakers (thin 4-6) | CC-BY-ND unresolved | score-derived only | GPU |
| 5 | PERCEPT-R (research-only) | /r/ benchmark if local supply insufficient | 32.5 h, 280 speakers | non-commercial PhonBank | rhotic labels built in | none (diagnostic) |

## Conditions that must be met before any acquisition

1. Pilot readout is A (GO_TO_DATA_EXPANSION) or at least B (PROMISING) with a mechanism.
2. For MyST: signed quote covering training, derived weights, product embedding, derived
   annotations.
3. For public/restricted corpora: registration/clearance confirmed in writing; phone-label
   generation + verification plan costed.
4. Kill switch not triggered (`09_SUCCESS_CRITERIA/KILL_SWITCH.md`).

If the pilot is C (NOT_JUSTIFIED): no acquisition; the smallest next step is an acceptance/data
diagnosis per `PART 29` (report, not a new encoder search).
