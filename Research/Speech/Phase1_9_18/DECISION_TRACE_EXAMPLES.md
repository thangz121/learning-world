# DECISION TRACE EXAMPLES — WP-1.9.18

All numbers are measured from `artifacts/lwe_phone_evidence.csv`. Fields:
`E` = deletion-aware final-phone evidence (`da_span_max`), `margin` = deletion LLR per frame,
`free` = free-path presence, baseline span from the production `ctc_align`,
`frame_max` = alignment-free global peak (conflates repeated phones — shown for contrast).

---

## 1. Successful PRESENT (supported)

**`child_02_one` — /n/, human CLEARLY_PRESENT.**
baseline exact (span 2–61, span posterior 0.025 — accepted with no real support);
deletion-aware: free path visits /n/, E = **0.9571** (da frame 36), margin +0.063,
frame_max 0.9571, GOP +3.92 → decision **PRESENT (PRESENT_SUPPORTED)**.
The deletion-aware path confines the phones to the word region; the evidence is real.

## 2. Successful ABSENT (supported deletion)

**`child_06_four` — final /r/, human CLEARLY_ABSENT.**
baseline **exact**, word score 100, final span 7–29 with posterior 0.0047 (unsupported
acceptance); deletion-aware: free path deletes /r/, E = **0.0414**, margin **−0.101**,
GOP −3.04 → decision **ABSENT (ABSENT_DELETION)**. The absent /r/ that the production
pipeline scored 100 is correctly rejected by the evidence layer.
(`child_01_four` E=0.022, margin −0.039; `child_03_four` E=0.056, margin −0.062 — same trace.)

## 3. Correct UNCERTAIN (honest uncertainty, not hiding a failure)

**`child_02_four` — final /r/, human CLEARLY_ABSENT.**
baseline exact (73.5); deletion-aware: free path deletes /r/ but E = **0.1536** (weak
/r/-like evidence), margin −0.025, GOP −1.66. The D rule returns **UNCERTAIN
(UNCERTAIN_COMPETING_SPANS)**: the model has weak evidence that cannot be trusted as
presence, and absence is not positively evidenced either. This is uncertainty on genuinely
ambiguous evidence, not a decision hidden at random.

## 4. False PRESENT (evidence/human conflict)

**`child_07_seven` — final /n/, human PROBABLY_ABSENT.**
baseline exact (20); deletion-aware: free path visits /n/, E = **0.6294** (da frame 40, end of
word), margin **+0.039**, GOP +0.95 → **PRESENT (PRESENT_SUPPORTED)**.
The frozen model finds strong nasal evidence where the reviewer heard none — acoustic/annotation
conflict (MIXED). No decision-layer rule can resolve it without labels that adjudicate the
conflict.

## 5. False ABSENT (present phone, no evidence)

**`child_07_one` — final /n/, human CLEARLY_PRESENT.**
baseline **miss** (word score 0); deletion-aware: E = **0.0005**, frame_max 0.013,
margin −0.066. The full recording contains no /n/ evidence at the end; the raw VAD window
scored 72.5 in 1.9.11 (window full→raw flip). The decision layer returns ABSENT; the failure
is either acoustic-context sensitive or boundary-dependent, not decidable from the full-audio
evidence.

**`child_06_six` — final /s/, human PROBABLY_PRESENT.**
baseline **exact** (25.0) with final span 38–39 and posterior **0.0004** (acceptance on top-1
identity alone); deletion-aware E = **0.0031**, frame_max 0.0031, margin −0.014, GOP −5.73.
No evidence in any view → decision ABSENT (new FRR on a present token). This is the clearest
example of the baseline's unsupported acceptance being masked by the human label agreeing.

## 6. Deletion failure (baseline cannot express absence)

**`child_01_four` — /r/ deleted by the child.**
baseline span 13–43 posterior 0.004, match **soft** accepted (word 72.5);
deletion-aware free path deletes /r/, E 0.022, margin −0.039 → ABSENT. The pipeline had no
state for "no /r/"; the deletion-aware representation does, and resolves all three
zero-evidence /r/ deletions.

## 7. Alignment failure (span stretched into trailing silence)

**`child_01_nine` — final /n/, human PROBABLY_PRESENT (accepted by baseline).**
baseline span **16–42** with span posterior **0.0011** — the monotone DP spread the phones
over trailing silence. Deletion-aware assigns the final /n/ to **frame 12** with E = **0.9793**
(GOP +4.85). The production decision was correct by luck (top-1 identity), not by evidence;
the alignment itself was wrong. The margin is still negative (−0.041) because it compares
whole paths — documented caveat; E, not margin, is the presence support.

## 8. Acoustic limitation (no evidence anywhere)

**`child_06_six`** (above) and **`child_04_four`** (present /r/, LOW review confidence,
E = 0.0033, GOP −5.19): the frozen encoder has no evidence for these human-present finals in
the full audio; a support floor must reject them, and no decision rule can recover them.
This is the boundary of the evidence, and the reason the deletion-aware layer cannot deliver
an FRR-first-safe improvement by itself.

## 9. Mixed failure

**`child_07_seven`** (strong evidence vs human ABSENT) and **`child_02_four`** (weak evidence
vs human ABSENT) are the two mixed cases; both are pressure points where either the annotation
or the acoustic model must be wrong.
