# 00 — RESEARCH STATE (WP-1.9.26 Part 1)

> **Tóm tắt (VI):** Tái dựng trạng thái nghiên cứu từ WP-1.9.21–1.9.25 và phân loại bằng chứng:
> cái đã biết, cái chưa giải quyết, cái mâu thuẫn, cái phụ thuộc nhãn người, cái phụ thuộc dữ liệu,
> cái có thể lấy không cần nhãn mới, cái cần dữ liệu ngoài, cái cần phép pháp lý. WP này bổ sung:
> pipeline nhãn hoàn chỉnh (276 ứng viên), so sánh encoder mở rộng (609 token), audit /r/ với
> F1/F2/F3, và audit license/dữ liệu mở rộng (18 → 28 nguồn).

**Starting gate:** `LABELS_INSUFFICIENT_DATA_LICENSE_BLOCKED` (WP-1.9.25, commit `8ba65d2`).

## 1. Known evidence (established)

| evidence | value | source |
|---|---|---|
| production decision reconstructed | 609/609 cache; 65/65 archived | WP-1.9.22 |
| acceptance census | 527/609 accepted: identity 501 (104 rank>0), similarity 26, posterior 0 | WP-1.9.22 |
| acceptance rules | 0/68 FRR-first safe; best D2 removes 3/10 TYPE-A | WP-1.9.23 |
| TYPE-B | 4 cases, 0 confirmed encoder false evidence, 2 LABEL_LIMITED + 2 MIXED | WP-1.9.24/1.9.25 |
| missing evidence | 87/528 labeled presents max_A < 0.02 (16.5%) | WP-1.9.24 |
| representation dependence | alt encoder gains 50 / loses 55 tokens at the 0.10 level; agreement 82.8% | WP-1.9.26 |
| /r/ | 24 cases, 20 isolated peaks, LWE median max_A 0.041, 1 PRESENT LOW | WP-1.9.25/1.9.26 |
| labels | 28 LWE listening labels (single reviewer); so762/SIAK are scores | WP-1.9.12/1.9.25 |
| frame cache | 2,436 token-windows / 117,128 frames | WP-1.9.21 |

## 2. Unresolved evidence

- TYPE-B: encoder false evidence vs label ambiguity (needs 4 listening labels).
- /r/: encoder limitation vs data limitation (needs 15 PRESENT + 15 ABSENT /r/).
- rank-2..5 identity accepts: how often the similarity pick is right (104/501 accepts).
- Window-sensitive identity (11.5% of tokens): representation vs crop artifact.

## 3. Contradictory evidence

- Strong TYPE-B evidence persists across encoders for `child_07_seven` (0.63 → 0.94) but collapses
  for `014180143_15` (0.68 → 0.25): "encoder false evidence" is not a stable acoustic fact.
- The alternative encoder reduces aggregate false-evidence (17.2% → 13.8%) but increases
  missing-evidence (16.5% → 18.8%): no encoder dominates.

## 4. Evidence classification

| class | items |
|---|---|
| dependent on human labels | TYPE-B verdicts; /r/ limitation; rank-2..5 correctness; weak-present vs weak-false separation |
| dependent on dataset availability | full-scale B2; age-4-6 phone-labeled training data; Vietnamese-L1 domain |
| obtainable without new labels | encoder comparison (done: 609 tokens); /r/ acoustics (done: F1/F2/F3); acceptance/identity audits (done); phone confusion profile (done) |
| requires external data | child-speech adaptation; speaker-diverse validation beyond 45 speakers |
| requires legal permission | SIAK (CC-BY-ND); MyST commercial; PERCEPT-R; CSLU/CMU; OCSC/JIBO terms |

## 5. What WP-1.9.26 adds

1. A validated blind review pipeline (server + 276-candidate pack + protocol) — the label blocker is
   now a human-availability problem, not a tooling problem.
2. Expanded encoder comparison over all 609 local tokens.
3. /r/ acoustic deep audit (duration, RMS, voiced fraction, F1/F2/F3).
4. Dataset discovery expanded to 28 sources including three new age-4-6 public corpora
   (OCSC 303 children 4–9; JIBO 110 children 4–7; CAPIL 30 children 5–6).
5. A concrete data roadmap (research-only route vs commercial route separated) and a defensible
   local pilot design.

## 6. Machine-readable state

`artifacts/research_state.json` mirrors this document with the same categories.
