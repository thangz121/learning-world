# SESSION REPORT — WP-1.9.25 (LABEL COLLECTION + DATA/LICENSE GATE)

> **Tóm tắt (VI):** Gate research-only xác định liệu dự án có thể chuyển sang B2 design/training hợp lý
> hay chưa. (A) Nhãn người: không có reviewer → **0 nhãn mới**, không tạo nhãn giả; 4/4 TYPE-B chưa
> giải quyết (2 LABEL_LIMITED, 2 MIXED). (B) Dữ liệu: audit 18 nguồn; không nguồn nào đồng thời đạt
> 10–30 h + 30–50+ người + nhãn phone-level + tuổi 4–6 + license thương mại; MyST (393 h) chỉ
> word-level/8–11 tuổi và cần license trả phí; PERCEPT-R (/r/ tốt nhất) non-commercial; SIAK CC-BY-ND
> cần legal review; không có corpus tiếng Việt trẻ em truy cập công khai. Gate:
> **LABELS_INSUFFICIENT_DATA_LICENSE_BLOCKED**; B2 TRAINING = NO.

- **Date:** 2026-10-07
- **Machine:** ASUS · **Branch:** `ux/math-arenas-hotfix-20260930` (= `main`) · **Base commit:** `59b6afe`
- **Scope:** research-only; no production/model/Unity change. **Flags:** all five false; no B2 training.
- **Starting gate:** LABELS_INSUFFICIENT_ENCODER_HYPOTHESIS_SUPPORTED

## 1. Objective
Determine whether (A) human labels are sufficient to resolve the ambiguous encoder false-evidence
cases, and (B) a legally usable child-speech dataset exists with sufficient scale, speaker diversity,
age/domain relevance and phone-level annotation to justify B2. Do not force a positive answer.

## 2. Evidence used
WP-1.9.21 frame cache (2,436 token-windows / 117,128 frames), WP-1.9.22 identity audit, WP-1.9.23
acceptance redesign, WP-1.9.24 encoder audit + 23-candidate blinded pack, local corpora metadata
(speechocean762, SIAK, LWE), and public license documentation verified by web audit on 2026-10-07.
No MAYNODE evidence; no dataset downloaded; no encoder run in this WP.

## 3. Human-review status / new labels
- Reviewer availability: none in-session; `serve_review.py` is hard-coded to the fidelity_v2 pack.
- **Number of new labels: 0** (no fabrication; `HUMAN_LABEL_RESULTS.csv` is an empty schema).
- Existing labels: 28 LWE blind listening labels (16 PRESENT / 12 ABSENT; HIGH 23 / MEDIUM 3 / LOW 2;
  single reviewer), plus SO762 expert phone scores (not listening labels).
- Reviewer agreement: not computable (no second reviewer); documented in `REVIEWER_AGREEMENT.csv`.

## 4. /r/ label status
- 24 /r/ cases (9 LWE + 15 SO762; 19 speakers): LWE = 1 PRESENT (LOW) / 5 ABSENT (HIGH) / 3
  unlabelled; max_A median 0.041 (LWE), 20/24 isolated peaks.
- No general /r/ conclusion drawn from n=1 PRESENT; R-specific label set required
  (15 PRESENT + 15 ABSENT).

## 5. TYPE-B status
- 0/4 ENCODER_FALSE_EVIDENCE_SUPPORTED; classifications: LABEL_LIMITED 2 (014180143_15,
  014350146_16), MIXED 2 (child_07_seven, 014190172_7).
- 3/4 have score-0 only; 2/4 isolated one-frame peaks; alt encoder preserves 4/4 at ≥0.1 but drops
  magnitude ≥50% in 2/4.

## 6. Dataset sources audited / license status
- 18 datasets audited (`DATASET_LICENSE_AUDIT.csv`): CLEAR 1 (speechocean762), CLEAR_WITH_CONDITIONS
  2 (LWE test-only; MyST commercial route), REQUIRES_LEGAL_REVIEW 1 (SIAK CC-BY-ND), RESTRICTED 5
  (CSLU Kids, CMU Kids, PERCEPT-R/GFTA, Providence), LICENSE_UNVERIFIED 6 (PF-STAR, TBALL, CID,
  UltraSuite, Storiza, NCTE), NOT_SUITABLE 3 groups (Vietnamese research/adult corpora).
- Models: wav2vec2-xlsr-53-espeak and lv-60-espeak both apache-2.0 CLEAR.

## 7. Data sufficiency
- B2 minimum (10–30 h, ≥30–50 speakers, speaker-disjoint, phone labels, ages 4–6 preferred,
  commercial): **no dataset meets it** (`DATA_SUFFICIENCY_MATRIX.csv`).
- speechocean762 child subset measured locally: 122 speakers / 2,440 utterances / 2.41 h.
- MyST: 393–448 h / 1,371 speakers, word-level only, ages ~8–11, paid commercial license.
- SIAK: 16,308 utts / 172 speakers, ages 4–12 (age 4–6 only 594 utts), word scores, ND unresolved.

## 8. Vietnamese-L1 findings
- No publicly accessible Vietnamese-L1 child speech corpus with phone-level labels was identified.
- Research datasets exist but are not publicly distributed (VietSpeech database, Charles Sturt
  University; Lee et al. 2024 dataset of 80 children ages 3–7). All downloadable Vietnamese corpora
  audited are adult speech.
- Precise wording used: "No publicly accessible corpus matching the required criteria was identified
  in the audited sources."

## 9. What remains unresolved
- TYPE-B encoder false-evidence vs label ambiguity (needs P0 labels).
- /r/ encoder limitation vs data limitation (needs R-specific labels).
- Whether MyST's commercial license and/or SIAK's CC-BY-ND legal review can unlock scale data.

## 10. Final gate
```
LABELS_INSUFFICIENT_DATA_LICENSE_BLOCKED
```
B2 TRAINING: NO. B2 DESIGN: NOT READY. Next gate recommendation:
`LABEL_COLLECTION_AND_DATA_ACQUISITION`.

## 11. Safety confirmation
- Production unchanged: production_vad=false, router_locked=false, unity_integrated=false,
  scorer_modified=false, production_window_locked=false.
- **B2 training was NOT performed**; no fine-tuning, no encoder replacement, no Unity change.
- No raw audio copied into the repo; no secrets or tokens stored; no dataset downloaded.

## 12. Files
```
Research/Speech/Phase1_9_25/
  LABEL_COLLECTION_REPORT.md, HUMAN_LABEL_RESULTS.csv, REVIEWER_AGREEMENT.csv,
  TYPE_B_CASE_REASSESSMENT.md, TYPE_B_CASES.csv, R_AUDIT.md, R_CASES.csv,
  DATASET_LICENSE_AUDIT.md, DATASET_LICENSE_AUDIT.csv, DATA_SUFFICIENCY_MATRIX.csv,
  VIETNAMESE_CHILD_SPEECH_GAP.md, B2_DATA_ACQUISITION_OPTIONS.md,
  B2_LABEL_REQUIREMENTS.csv, LABEL_DATA_DEPENDENCE_MATRIX.csv,
  B2_READINESS_DECISION.md,
  artifacts/ (evidence_map.json, r_summary.json, typeb_summary.json),
  experiments/ (build_evidence_map.py, build_25_outputs.py)
```
