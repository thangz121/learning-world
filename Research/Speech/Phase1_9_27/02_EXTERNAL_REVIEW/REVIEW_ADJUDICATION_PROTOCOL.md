# REVIEW ADJUDICATION PROTOCOL — WP-1.9.27

> **Tóm tắt (VI):** Chỉ adjudicate SAU KHI nhãn thô đã đóng băng: các ca DISAGREEMENT hoặc
> UNCERTAIN được chuyển cho người thứ ba; kết quả adjudication lưu riêng, không ghi đè nhãn thô.

## When to adjudicate

- Only after both reviewers have exported and the raw labels are frozen.
- Eligible cases: consensus = DISAGREEMENT, or consensus = UNCERTAIN.
- Do not adjudicate cases where reviewers agreed (including agreed UNCERTAIN) unless a gate
  explicitly requires a decisive label.

## Who

- A third listener (Reviewer C) who has NOT seen either raw label.
- If no third listener exists: keep the case as DISAGREEMENT/UNCERTAIN and report it. Do not
  invent a tie-break.

## Procedure

1. Export `ADJUDICATION_RESULTS.csv` from `label_analysis.py` (lists eligible cases with raw labels
   and confidences).
2. Reviewer C listens blind (same UI, reviewer ID `REV-C`; raw labels hidden) and records one label
   + confidence + note.
3. Fill `adjudicator_label` / `adjudicator_note` in `ADJUDICATION_RESULTS.csv`.
4. Raw labels are never modified; adjudication is a separate column.

## Effect on gates

- A TYPE-B case counts as decisive only with a non-UNCERTAIN consensus from >=2 independent
  reviewers (adjudication may supply the second read only if the original single-reviewer label is
  disclosed as such).
- /r/ gate counts only non-UNCERTAIN consensus labels.
