# B2 DATA ACQUISITION OPTIONS — WP-1.9.25 Phase 8

NO TRAINING. These are acquisition paths only. Numbers are from `DATASET_LICENSE_AUDIT.csv` and
`DATA_SUFFICIENCY_MATRIX.csv`.

| option | data volume | speakers | age relevance | annotation burden | license risk | scientific value | GPU need | likely B2 scope | major limitation |
|---|---|---|---|---|---|---|---|---|---|
| **A. English child speech + existing phone labels** | speechocean762: 2.41 h child; SIAK: 16,308 word utts (age 4–6 only 594) | 122 child (so762) + 172 (SIAK) | so762 6–15; SIAK mostly 8–10; age 4–6 thin | none (labels exist: phone scores / word scores) | so762 CLEAR; SIAK REQUIRES_LEGAL_REVIEW (ND) | medium: phone-level scores exist but 2.4 h is far below 10–30 h | none for evaluation; GPU for adaptation | B2-E/B2-D research evaluation only | scale (2.4 h child) and age/L1 domain; SIAK ND unresolved |
| **B. English child speech + newly generated/reviewed phone labels** | MyST 393–448 h (commercial license) or CSLU Kids ~75–150 h (research) + local LWE/so762 | 1,371 (MyST) / 1,100 (CSLU) | MyST grades 3–5 (8–11); CSLU K–G10 | high: word transcripts → forced alignment + human spot review; phone-level human labels for validation only | MyST commercial license (paid, concrete); CSLU research-only (commercial via OHSU, unverified) | high if MyST license secured: scale + speaker diversity; phone labels generated, not gold | GPU for adaptation | B2-C adaptation + B2-B calibration | no gold phone labels; ages 8–11; no Vietnamese-L1; paid license |
| **C. Vietnamese-L1 child speech** | none publicly accessible | – | 4–6 ideal | unknown | no public license route | highest domain relevance | GPU | not executable now | no public corpus identified (see `VIETNAMESE_CHILD_SPEECH_GAP.md`); requires institutional partnership |
| **D. Mixed child corpus with domain adaptation** | MyST (licensed) + speechocean762 + local LWE (test-only) + SIAK (if cleared) | 1,371 + 122 + 11 + 172 | mixed 4–15 | alignment generation + validation labels | mixed: commercial + CC BY 4.0 + ND review | high for robustness; supports speaker-disjoint evaluation | GPU | B2-C with multi-corpus adaptation | licensing complexity; domain mixing may blur the target age/L1 |
| **E. Small local labelled set + existing public child corpus** | local recordings (LWE 11 children, so762 122 children) + new 60–120 human labels (WP-1.9.25 label plan) | 20+ for the new labels | 4–8 preferred | medium: 60–120 listening labels (speaker-disjoint) | local-only + CC BY 4.0 | medium: supports FRR-first evaluation of B2-E hybrid features and B2-D encoder selection, not large-scale adaptation | none (feature/eval) | B2-E hybrid evidence + B2-D small comparison | too small for encoder adaptation; no Vietnamese-L1 |

## Ranking by evidence

1. **E** (small local labelled set + public child corpus) — immediately executable once the label
   plan is funded; validates B2-E/B2-D without licensing risk.
2. **B** (MyST commercial + generated phone labels) — the only concrete route to scale; blocked by
   cost/licence decision and by the absence of gold phone labels.
3. **D** (mixed corpus adaptation) — scientifically strongest for generalization, highest licensing
   complexity.
4. **A** (existing phone labels only) — useful as an evaluation set, not as B2 training data.
5. **C** (Vietnamese-L1) — not executable from public data; partnership only.

No option currently satisfies Condition B of the WP-1.9.25 gate (a legally usable dataset with
sufficient scale, speaker diversity, age/domain relevance **and** phone-level annotation).
