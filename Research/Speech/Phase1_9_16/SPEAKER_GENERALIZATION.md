# SPEAKER GENERALIZATION — Phase 1.9.16 (baseline half)

**Artifacts:** `speaker_metrics.csv` (27/27 test speakers), `age_metrics.csv`,
`split_manifest.json` (overlap [], seed 1515, REPRODUCED from 1.9.15 partition
and verified against the byte-identical MAYNODE SIAK copy).

---

- Every unseen test speaker is reported individually; no averaging hides
  speaker-specific behavior. Worst-speaker inspection is in the CSV.
- The speaker-disjointness guarantee is structural (partition-level, verified
  twice: 1.9.15 SHA-1 audit + 1.9.16 file-level re-verification), not
  model-dependent — it holds for any future child model trained under it.
- Child-model generalization columns exist and are NOT_AVAILABLE (no child
  model). Speaker-specific / age-specific / accent-specific collapse analysis
  for the adapted model is therefore PENDING, not skipped: the harness and the
  held-out speakers (27 test + 5 ages-46 external) are ready.
- L1 note: test speakers span fifi/enuk/othr (release metadata); enuk (UK
  English) is the closest to LWE targets and is retained in-test, not filtered.
