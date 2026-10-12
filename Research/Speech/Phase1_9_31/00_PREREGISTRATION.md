# 00 — PRE-REGISTRATION: EXTERNAL CHILD STANDARD (WP-1.9.31)

> **Tóm tắt (VI):** Theo chỉ đạo của chủ sản phẩm, vòng review người thứ hai (REV-B) được THAY THẾ
> bằng chuẩn ngoài: **5 chuyên gia độc lập của speechocean762** (nhãn phone 2/{ }/( ) có sẵn, CC BY 4.0)
> trên **trẻ nước ngoài** + **trẻ bản ngữ 4.9 tuổi** (Zenodo 200495, CC BY 4.0) làm mẫu "nói chuẩn".
> WP này đăng ký trước toàn bộ cách ánh xạ nhãn, tập đánh giá, metric, ngưỡng — TRƯỚC khi đọc bất kỳ
> kết quả model nào. Không huấn luyện, không đụng production/Unity. Đây là thí nghiệm MỚI; file
> preregistration cũ (WP-1.9.27) giữ nguyên giá trị cho vòng review người.

- **DATE:** 2026-10-12
- **MACHINE:** new machine (TECHVKITCHEN01; python `D:\speech-lab\venvs\p0\Scripts\python.exe`)
- **BASE COMMIT:** `0e43eae` (WP-1.9.30 migration package), branch `main` (= `ux/math-arenas-hotfix-20260930`)
- **WP:** WP-1.9.31 — label-source substitution: EXTERNAL CHILD STANDARD
- **STATUS BEFORE READOUT:** frozen (this file is written before any model metric is computed)

## 1. Authority and rationale (PO directive, 2026-10-12)

The product owner directed: complete the speech track by using **foreign children's standard productions**
as the measuring stick instead of the owner-as-reviewer or a second human reviewer. Concretely:

- The review-round requirement (REV-A + REV-B on Pack R/Pack P, WP-1.9.27→1.9.30) is **superseded**
  as the label authority for this evaluation. REV-A sessions remain archived, unchanged, archived
  outputs are not deleted or re-interpreted; no REV-B will be requested for this WP.
- No gate threshold is weakened: the frozen numeric thresholds of WP-1.9.27
  (`SUCCESS_CRITERIA.md`, `FAILURE_CRITERIA.md`, `KILL_SWITCH.md`) are re-used unchanged.
- This is a **new experiment** in the sense of WP-1.9.27 §8 (a rerun with different choices requires
  a new pre-registration). The old pre-registration and its files are not modified.
- Rationale recorded honestly: the previous label source (single/first-time listeners, leniency
  drift, unreachable ABSENT supply) capped the achievable evidence; the external expert panel is
  independent, qualified, reproducible, and license-clean (CC BY 4.0, commercial-usable data).

## 2. Label source (frozen)

### 2.1 speechocean762 five-expert panel (primary labels)

- Corpus: `D:\speech-lab\data\speechocean762` (SLR101, CC BY 4.0). 250 speakers; **122 children
  (ages 6–15)** + adults; 5 experts scored every phone independently (corpus README).
- Notation (README): phone score 2 = correct (no symbol); 1 = right but heavy accent `{X}`;
  0 = incorrect or missed `(X)`; `[X]` = inserted phone.
- **Known limitation (recorded):** score 0 conflates "missed" (deletion) with "incorrect"
  (substitution); the external standard is therefore "correctly produced vs not", not a pure
  deletion judgment. This is the same operational question the product asks ("is the target
  final consonant produced well enough to count?").

### 2.2 Zenodo 200495 native-child review set (positive reference)

- Corpus: `Research/Speech/ExternalData/zenodo_200495/extracted/english_children` (Zenodo record
  200495, CC BY 4.0): 11 children (6 native / 5 non-native), mean age 4.9 y, numbers + 5 fixed
  sentences + free speech, 3 microphones; local copy. Project rule "test-only" respected
  (evaluation only; never training).
- Used as an **age-4–5 standard-production FRR reference**, not as a labeled benchmark.

## 3. Label mapping (frozen before readout)

For every evaluation token (utterance + word + phone position in the reference `ref-phones`):

1. Parse each expert's annotation string; drop `[insertions]`; verify the remaining sequence
   equals the reference phone sequence (positional match; verified 0 mismatches on 546/546 pilot
   tokens in the feasibility probe).
2. Per expert, read the mark of the target position: `2` | `1` | `0`.
3. Token-level aggregates: `P2` (#2), `P1` (#1), `A0` (#0); `P2+P1+A0 = 5`.
4. **Primary binary label (frozen):**
   - `PRESENT` iff `P2 + P1 >= 3` (phone produced; accented still produced)
   - `ABSENT` iff `A0 >= 3` (phone not acceptably produced per majority)
   - (no ties possible; every token is one of the two)
5. **Secondary flags (reported, not primary):** `STRICT_CORRECT` = `P2 >= 3`; `ANY_ZERO` = `A0 >= 1`.
6. `NOT_ASSESSABLE` does not exist in this label source. Consequence for the frozen assessability
   criterion (SUCCESS #6): **N/A — disclosed**; no metric may be credited or debited via it.

## 4. Evaluation sets (frozen)

### 4.1 BENCH — SO762 children (primary readout)

- Children = speakers with `age <= 15` in `spk2age` (official corpus metadata).
- **TEST readout:** official `test` split children: 64 speakers, 1,280 utterances, **4,326 final-
  consonant tokens** (consensus P=4,232 / A=94; per phone: t 941/15, z 576/16, n 544/7, d 463/10,
  s 374/17, m 253/4, l 248/10, k 198/1, v 186/0, ŋ 175/4, ɹ 120/1, p 52/3, f 48/2, tʃ 44/3,
  ɡ 30/0, θ 24/0, ʃ 19/1, ð 16/0, dʒ 12/0; counts measured in the sizing probe).
- **FIT set:** official `train` split children: 58 speakers, 1,160 utterances, **3,823 tokens**
  (P=3,789 / A=34). Dev = deterministic 20% of TRAIN speakers (seed 1927) used for threshold
  calibration/inspection only; the remaining 80% = fit. Test is read out once.
- Token definition: final position of a word whose canonical phone is a non-vowel (same rule as
  the frozen pilot; canonicalization via the production inventory adapter).

### 4.2 PILOT-546 (continuity sub-readout)

The frozen WP-1.9.27 pilot set (10 SO762 children, 200 utts, 546 tokens, speakers split 6/2/2,
seed 1927) re-read with the external labels: 541 P / 5 A (98/1 in the frozen test split).
This keeps continuity with the frozen pipeline; it is reported but is not the primary readout.

### 4.3 NATIVE-Z (positive reference, age 4.9)

Zenodo native children 01, 03, 05, 06, 07, 11 (`*_native`), studio mic (port mic fallback):
- numbers 1–10: six→/s/, seven→/n/, eight→/t/, nine→/n/, ten→/n/, one→/n/ (five /v/ included;
  four /ɹ/ EXCLUDED — UK non-rhotic, cannot assume a rhotic final);
- sentence finals: horse→/s/, stable→/l/, pond→/d/, shed→/d/, top→/p/, dog→/g/, front→/t/,
  fish→/ʃ/, house→/s/ (car /ɹ/ excluded, same reason).
- No expert labels exist; the PO-directed standard assumption is "native child production is
  correct". Reported as a directional FRR reference with developmental caveat (at age ~4.9 some
  phones (/s/, /l/, /v/) are still developing even in natives); never used for FAR/negative claims.

## 5. Features (frozen)

- Same extraction code/columns as the frozen pilot (`pilot_features.csv` schema, 30 fields,
  production xlsr-53 + alternative lv-60 + acoustic/formant columns), re-run for BENCH/PILOT.
- Additional **decision addendum** per token: exact production decision replica
  (`best_obs`/`best_sim`/`match_type`/`production_decision`, span-mean top-5), identical logic to
  the WP-1.9.22 verification code.
- Frozen pilot features are NOT regenerated; they are re-used (continuity). Bench features are new.
- Feature hashes recorded at run; no feature added after readout.

## 6. Models and variants (frozen, no training)

- **BASE/D0:** production decision path (exact replica) — reference.
- **B2-D (no fitting):** D0 production mean; D1 production max; D3 alt max; D4 alt mean;
  D5 both encoders (OR/AND of strong evidence, strong = span-max ≥ 0.30 both encoders as frozen
  in the protocol's false-evidence threshold; OR = either, AND = both). Primary set; readout once
  on BENCH test speakers + PILOT test split.
- **B2-E:** per the frozen design, fitted variants require ≥60 PRESENT + ≥60 ABSENT in train+dev.
  Measured beforehand: train+dev ABSENT = 34 (<60) ⇒ **fitted B2-E variants are NOT RUNNABLE and
  are reported as NOT EVALUATED / INCONCLUSIVE by construction** (frozen rule; no threshold move).
  In their place (pre-registered before readout): fixed-threshold curves (no fitting) for the
  primary feature groups E_ENC (target_max), E_ACO (rms_ratio), E_TMP (temporal_support),
  E_ENC_ACO (target_max ∧ rms_ratio grid), reported FRR-first at the same fixed grid
  τ ∈ {0.02, 0.05, 0.10, 0.15, 0.30}. No tuning on test; dev only for the matched-FAR calibration
  described below.
- No encoder fine-tuning, no LoRA, no head training, no deep nets, no stacking. Random-feature
  control accompanies any fitted component actually run (none expected).

## 7. Metrics and readout protocol (frozen)

- **Missing-evidence rate:** fraction of labelled PRESENT with `target_max < 0.02` (frozen
  protocol threshold). Primary for SUCCESS criterion 2.
- **False-evidence rate:** fraction of labelled ABSENT with `target_max >= 0.30` (frozen).
- **FRR@matched-FAR:** for each evidence-score variant, calibrate the decision threshold on DEV
  (train speakers, never test) to make the variant's FAR equal to the baseline's FAR on DEV;
  then read out FRR/FAR on TEST once. Baseline = BASE production decision (exact replica).
- **Per-speaker:** mean/worst/best/variance on test speakers (criterion 4: sign must hold for
  both frozen pilot test speakers; on BENCH report the full per-speaker table).
- **/r/: reported under two frozen definitions:** (a) produced (lenient, `P2+P1>=3`) vs `A0>=3`;
  (b) rhotic-correct (`P2>=3`) vs rest. Gate vocabulary of WP-1.9.26/27 (15+15/10 speakers) is
  evaluated against whichever count exists; unmet side is reported as unmet, no substitution.
- **Age bands:** 6–7 / 8–10 / 11–15 reported separately on BENCH.
- **Assessability:** N/A (§3.6), disclosed.
- **AUC:** where both classes ≥30 (BENCH test satisfies: 4,232/94); reported with CI.
- **Failure replay:** strongest false accepts (labelled ABSENT with high evidence), weakest
  labelled presents, and the 4 frozen TYPE-B cases (SO762 cases get expert labels; the Zenodo
  case remains label-limited and is reported as such).

## 8. Outcome mapping (frozen)

Re-use the frozen criteria files verbatim; outcomes map to the allowed gate vocabulary of
WP-1.9.27/30 plus one explicit new gate:

- all evaluable primary criteria pass → `B2_PILOT_PROMISING_DATA_REQUIRED` (authorizes only the
  next research decision; no training, no production).
- FRR-first fails (criterion 1 or 2) → `B2_NOT_JUSTIFIED`.
- underpowered/untestable criteria dominate → `B2_INCONCLUSIVE_MORE_LABELS_REQUIRED`.
- new gate `B2_PILOT_EXTERNAL_STANDARD_READOUT` = the WP-1.9.31 result is an external-standard
  readout: it informs the same B2 decision, never production.

## 9. Locks (unchanged)

- No training of any kind. Production flags stay false (`production_vad=false ·
  router_locked=false · unity_integrated=false · scorer_modified=false ·
  production_window_locked=false`). No Unity work. No downloads needed (both corpora local);
  contingent downloads would require a separate license check first.
- Raw child audio never committed; artifacts = tables/metrics only.

## 10. Reproducibility

- Config + input hashes recorded per run (`EXPERIMENT_CONFIG_SCHEMA.md` of WP-1.9.27 followed).
- Readout happens once per config; repeats = new config.
- Deliverables: this file; `01..09` reports; `experiments/p31_*.py`; artifacts; root
  `SESSION_REPORT_1_9_31.md`; index update. Commit + push per standing instruction (branch + main,
  fast-forward), including this file BEFORE the readout runs.
