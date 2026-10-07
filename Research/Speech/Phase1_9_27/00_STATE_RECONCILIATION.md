# 00 — STATE RECONCILIATION (WP-1.9.27 Part 1)

> **Tóm tắt (VI):** Tái xác nhận trạng thái từ WP-1.9.21–1.9.26 và phân loại mọi việc còn lại:
> cái đã khóa, cái chưa giải quyết, cái tự động giải được, cái cần người, cái cần dữ liệu mới,
> cái cần pháp lý, cái cần GPU. WP-1.9.27 chuyển trọng tâm từ "audit" sang "thi hành": review
> ngoài + pilot local + khóa thiết kế B2-D/B2-E trước khi thấy kết quả.

**Starting gate:** `LABEL_COLLECTION_READY_DATA_BLOCKED` (WP-1.9.26, commit `5dfdc6c`).
**Production flags (unchanged):** production_vad=false · router_locked=false · unity_integrated=false ·
scorer_modified=false · production_window_locked=false. **B2 training = NO.**

## 1. Frozen (do not relitigate)

| item | value | source |
|---|---|---|
| production decision reconstruction | 609/609 cache; 65/65 archived LWE | WP-1.9.22 |
| acceptance census | 527/609 accepted: identity 501 (397 rank-1, 104 rank-2..5), similarity 26, posterior 0 | WP-1.9.22 |
| acceptance rules | 0/68 FRR-first safe; best D2 removes 3/10 TYPE-A | WP-1.9.23 |
| TYPE-B | 4 cases, 0/4 confirmed encoder false evidence; 2 LABEL_LIMITED + 2 MIXED; 3/4 representation-dependent | WP-1.9.24/25/26 |
| missing evidence | 87/528 labeled presents max_A < 0.02 (16.5%) | WP-1.9.24 |
| encoder comparison | production xlsr-53 vs alt lv-60 on 609 tokens: missing 16.48% vs 18.75%, false 17.24% vs 13.79%, agreement 82.76%, 50 gains / 55 losses; no winner | WP-1.9.26 |
| /r/ | 24 cases, 20 isolated peaks, LWE median max_A 0.041, 1 PRESENT LOW; median F3 2812 Hz, F3/F2 1.84 | WP-1.9.25/26 |
| labels | 28 LWE listening labels (single reviewer); SO762/SIAK are scores, not listening labels | WP-1.9.12/25 |
| frame cache | 2,436 token-windows / 117,128 frames (reusable) | WP-1.9.21 |
| review pack | 276 candidates / 49 speakers / 15 pools; server blind, isolation + resume + export validated | WP-1.9.26/27 |
| production model licences | wav2vec2-xlsr-53-espeak Apache-2.0; lv-60-espeak Apache-2.0 | WP-1.9.25/26 |

## 2. Unresolved — and the class of dependency

| unresolved item | dependency class | what resolves it |
|---|---|---|
| TYPE-B verdicts (encoder vs label) | HUMAN | 4 independent listening labels (pack pool F) |
| /r/ encoder-limited vs data-limited | HUMAN (+ optional research benchmark) | 15 PRESENT + 15 ABSENT /r/ labels across >=15 speakers |
| rank-2..5 identity correctness | HUMAN | labels on rank-2..5 accepted tokens |
| weak-present vs weak-false separation | HUMAN | >=60 PRESENT + 60 ABSENT minimum label set |
| pilot GO/NO-GO (encoder adaptation) | HUMAN labels + autonomous run | Pack P labels (546 pilot tokens) then B2-D/B2-E on frozen split |
| phone-level labels at scale | NEW DATA + LEGAL | annotation strategy + license route (MyST paid / research registration) |
| Vietnamese-L1 domain | NEW DATA | collection protocol (design ready; partnership) |
| full B2 adaptation | GPU + LEGAL + DATA | only after a positive local pilot and a signed/cleared data route |

## 3. Resolvable autonomously (this WP, no human, no licence)

- Review-system QA (Pack R): 21/21 PASS.
- Label analysis + agreement + sufficiency pipeline: built, tested, and runnable on imported CSVs.
- Pilot data contract: 10 frozen speaker-disjoint speakers, 200 utts, 546 final-consonant tokens,
  QC PASS 200/200, leakage checks PASS (train/dev/test overlap 0).
- Pilot review pack (Pack P): 546 candidates generated with the same token ids as the extracted
  features (546/546 match); served blind by the same validated server; submit validated.
- Feature extraction (research-only inference): production encoder 546 tokens in 200 s; alternative
  encoder 546 tokens in 169 s; merged `artifacts/pilot_features.csv` (30 evidence/acoustic fields).
- B2-D / B2-E / ablation / success / kill-switch designs frozen before any training.
- Resource audit: what ASUS can run, measured.
- Data/license roadmap consolidated with ranked acquisition routes.

## 4. Requires a human (the primary external action)

1. Two independent reviewers label Pack R (276 candidates; minimum 60P+60A, 15+15 /r/, 4 TYPE-B).
2. Export CSV per reviewer and import into the label pipeline (one command per reviewer).
3. (After review) Pack P labels for the pilot: 546 pilot tokens, minimum 60P+60A of them.

## 5. Requires new data / legal clearance (not started, by design)

- MyST commercial license (quote request; phone labels generated + human-verified).
- OCSC / JIBO / AusKidTalk / CAPIL research registration (research-only track).
- SIAK CC-BY-ND counsel review; PERCEPT-R research-only /r/ benchmark; JIBO license confirmation.
- Vietnamese-L1 prospective collection (partnership; protocol designed in WP-1.9.26).

## 6. Requires future GPU training (explicitly not approved here)

- B2-C encoder adaptation (full/LoRA/last-layers) on 2–4 h pilot or 20–40 h robust data.
- Any model whose derived weights could ship in the product (needs a signed/cleared data route).

## 7. Machine-readable companions in this WP

`artifacts/pilot_split.json`, `artifacts/pilot_data_summary.json`, `artifacts/pilot_checks.json`,
`artifacts/pilot_features.csv` (+ `_prod`/`_alt`), `artifacts/pilot_review_pack_summary.json`,
`03_LABEL_ANALYSIS/label_sufficiency.json`, `01_REVIEW_SYSTEM/REVIEW_SYSTEM_QA.csv`.
