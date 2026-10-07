# SUCCESS CRITERIA (FROZEN) — WP-1.9.27 Part 17

> **Tóm tắt (VI):** Tiêu chí khóa trước kết quả, FRR-first: variant thắng phải không làm tăng FRR
> (chênh <=0.05 tuyệt đối), giảm missing-evidence >=25% tương đối trên PRESENT có nhãn, FAR không
> tăng >0.05, giữ trên cả 2 test speaker, có ích cho /r/ hoặc trung tính, và assessability gate
> sạch. Nếu chỉ đạt một phần → PARTIAL. Không đổi ngưỡng sau khi thấy kết quả.

Evaluated on the speaker-disjoint test split (99 pilot tokens; frozen split) with HUMAN-LISTENING
labels; all metrics also reported per speaker and after the assessability gate.

## Numeric thresholds (justified by pilot size)

Primary (all must hold for SUCCESS):

| # | criterion | threshold | why this number |
|---|---|---|---|
| 1 | FRR (labelled PRESENT) | variant FRR <= baseline FRR + 0.05 | n≈50/side in test; 0.05 is the smallest change not swamped by sampling noise |
| 2 | Missing-evidence on labelled PRESENT | >=25% relative reduction vs baseline | the supported failure mode; smaller reductions are noise at this n |
| 3 | FAR (labelled ABSENT) | <= baseline FAR + 0.05 | symmetric guard against recall-buying |
| 4 | Speaker-disjoint consistency | sign of criteria 1–3 holds for both test speakers | 2 test speakers; a one-speaker win is a failure per Part 26 |
| 5 | /r/ | /r/ FRR not worse; report coverage | /r/ n is tiny in the pilot (8 tokens) — directional report, not proof |
| 6 | Assessability gate | no metric gain disappears when NOT_ASSESSABLE is excluded | prevents guessing credit |
| 7 | Random-feature control (fitted variants) | real variant beats control | guards against overfit |

## Outcome definitions

- **SUCCESS** — criteria 1–4 and 6 pass (and 7 where applicable), and at least one of B2-D/B2-E
  shows the missing-evidence reduction; report which.
- **PARTIAL_SUCCESS** — criterion 1 passes; 2 or 3 partially (direction correct, below threshold);
  consistency holds for >=1 test speaker.
- **FAILURE** — criterion 1 fails or no missing-evidence improvement.
- **INCONCLUSIVE** — labelled test set <30 tokens per side, or labels unavailable/untrustworthy.

## Reporting

Every variant gets the full metric table, per-speaker breakdown, assessability re-run and failure
replay. Negative results are reported with the same weight as positive ones. Criteria are frozen
before any Pack P label exists; no post-hoc threshold moves.
