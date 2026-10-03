# 12 — LICENSE / CHECKPOINT POLICY (STEP 15)

**Script:** `experiments/write_reports.py`, `01`/`provenance.json`

---

## SO762 license (re-checked)

| source | declaration |
|---|---|
| OpenSLR SLR101 (upstream corpus) | **CC BY 4.0**, free commercial + non-commercial |
| HF mirror card `mispeech/speechocean762` front-matter | **apache-2.0** |

**Discrepancy recorded, not resolved.** Both are permissive and NEITHER is
ND/no-derivatives, so a research checkpoint derived from SO762 is permissible
under either declaration. The discrepancy is flagged for the project owner; no
production/license recommendation is made here.

## Checkpoint policy

- Research checkpoint `checkpoints/b1_head.pt` (sha256 `44d86379…da3e6`) may be
  retained: it is a small head trained on permissive-license SO762, and contains
  **no raw audio**. Encoder weights are unchanged (Apache-2.0 frozen model).
- **No production checkpoint is created or recommended.**
- Because B1's scientific verdict is E (regression), there is no basis to
  distribute it as an improvement regardless of license.
- Status: `CHECKPOINT_RETAINED_RESEARCH_ONLY`.

## Other materials

| item | license | verdict |
|---|---|---|
| wav2vec2-xlsr-53-espeak-cv-ft | Apache-2.0 | CLEAR (unchanged) |
| CMUdict | BSD-like | CLEAR |
| SIAK | CC-BY-ND-4.0 | BLOCKED_PENDING_ND_REVIEW (no training/eval run) |
| LWE child audio | project-internal, privacy | never committed; not used for B1 |
