# HANDOFF — Speech/VAD Research (through WP-1.9.31)

**Repo:** `thangz121/learning-world` · **Branch:** `main` (working branch `ux/math-arenas-hotfix-20260930`, same tip)
**Machine:** new machine (TECHVKITCHEN01; python `D:\speech-lab\venvs\p0\Scripts\python.exe`)
**Date:** 2026-10-12 · **Speech Research status:** PILOT READOUT COMPLETE under the external standard
**Last gate:** `B2_PILOT_PROMISING_DATA_REQUIRED` (WP-1.9.31) — data/license decision only; training NO.

> Read this file first, then `SESSION_REPORTS_INDEX.md` and `Research/Speech/Phase1_9_31/`.

## 1. What changed in 1.9.31 (PO directive)

- The second-reviewer round (REV-B on Pack R/P) was **replaced** as the label authority by an
  **external child standard**, per the product owner's directive (2026-10-12): use foreign children's
  standard productions as the measuring stick.
- Implementation: SO762's **5 independent expert annotators** (per-phone 2/{ }/( ) labels, CC BY 4.0)
  over SO762 **children** (BENCH: 4,326 test tokens / 64 kids), plus **Zenodo 200495 native children**
  (~age 4.9, CC BY 4.0) as an FRR reference. Both local; zero downloads; zero new humans.
- REV-A sessions and all frozen 1.9.27–1.9.30 artifacts are archived unchanged. `00_PREREGISTRATION.md`
  of 1.9.31 defines the mapping (PRESENT iff P2+P1>=3; ABSENT iff A0>=3) — frozen before readout.

## 2. Readout in one table (frozen criteria, unchanged)

| item | value |
|---|---|
| BENCH test | 4,326 tokens / 64 children: 4,232 PRESENT / 94 ABSENT |
| Baseline production decision | FRR 8.65% · missing-evidence 16.54% · **FAR 54.26%** · false-evidence 13.83% |
| Best variant (D5_or_max) | FRR **7.68%** · missing **12.19%** (−26.3% rel) · FAR 42.55% · AUC 0.8546 |
| Frozen c1–c4 | D5or PASS all (c4: both pilot test speakers improve) |
| Native ~4–5y | 94/107 accepted (FRR-directional 12.15%); /v/ weakest 3/6 |
| Pilot 546 (continuity) | test 98P/1A: 22.45% → 15.31% FRR |
| Agreement (5 experts) | raw 88.9–89.9% (binary 96.3–96.6%); unanimous 5/5 76–78% |
| TYPE-B | 3/4 SO762 cases expert-labeled (2 agree REV-A, 1 diverges); Zenodo case label-limited |
| /r/ produced | 220P/4A/91spk — absent-side gate unreachable in current corpora |

**GATE: `B2_PILOT_PROMISING_DATA_REQUIRED`** — per the frozen kill switch this authorizes ONLY a
data/license decision for full B2. No training, no production change, no Unity.

## 3. Where things are

- Pre-registration: `Research/Speech/Phase1_9_31/00_PREREGISTRATION.md`
- Reports: `01_EXPERT_LABELS/` … `09_FINAL/FINAL_GO_NO_GO.md`
- Scripts: `Research/Speech/Phase1_9_31/experiments/p31_{labels,agreement,sufficiency,features,eval,native}.py`
- Artifacts: labels (8,695 tokens), bench features (8,149), pilot decisions (546), eval readout JSON,
  per-speaker/per-phone CSVs, native check.
- Frozen prior work: 1.9.27 pilot design, 1.9.28–1.9.30 review machinery (archived; no longer blocking).

## 4. Next actions (recommended order)

1. **Data/license decision for full B2** (the only thing this gate authorizes): weigh MyST (paid,
   commercial) vs research registrations (OCSC/JIBO/AusKidTalk/CAPIL — non-commercial) vs staying
   local-only. No acquisition before that decision; licenses stay a gate.
2. **Acceptance-rule / operating-point redesign** (research-only): lock a target FAR first (e.g.
   FAR<=10–15%), then minimize FRR on the BENCH/NATIVE standards; evaluate the D5 OR-evidence family
   at that operating point. The current production rule accepts 54% of expert-wrong/missing tokens —
   this is the central remaining product issue.
3. /r/ ABSENT side: only reachable via clinical corpora (PERCEPT-R, non-commercial) — decide later.
4. TYPE-B Zenodo case `child_07_seven`: remains label-limited (documented).

## 5. Rules (non-negotiable, unchanged)

- Human listening is authority; FRR-first; no B2 training; no production scorer/VAD/router/Unity change.
- Do not modify frozen 1.9.x artifacts; raw child audio never committed; artifacts = tables only.
- Per work package: create `SESSION_REPORT_<ID>.md` in the repo root, update
  `SESSION_REPORTS_INDEX.md`, then commit + push (branch + main, fast-forward) — standing instruction.
- Contingent downloads require a license check first; treat restricted corpora as research-only.
