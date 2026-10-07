# FINAL GATE DECISION — WP-1.9.26 Part 31

**Final gate:** `LABEL_COLLECTION_READY_DATA_BLOCKED`

Rationale: the label blocker now has a concrete, validated removal mechanism (blind server +
276-candidate pack + protocol; only human reviewers are missing), while the data blocker is
partially refined but not removed: the age-4–6 speaker-diversity gap is closable with public
research-only corpora (OCSC 303 speakers 4–9, JIBO 110 speakers 4–7), but **phone-level labels at
scale plus commercial rights** remain unavailable in any single audited source. The route must be
concrete and evidence-backed to choose a data-ready gate; it is not yet.

## Per-blocker status

| # | blocker | status | evidence |
|---|---|---|---|
| A | human labels | **REMOVABLE (human action)** | pipeline validated (blind, multi-reviewer, resume/export); 276 candidates; 0 labels so far |
| B | /r/ | **PARTIALLY REMOVABLE** | 24-case acoustic audit (F1/F2/F3, RMS); 15+15 labels required; PERCEPT-R research benchmark (non-commercial) |
| C | TYPE-B | **REMOVABLE (4 labels)** | 0/4 confirmed; 2 LABEL_LIMITED + 2 MIXED; 3/4 representation-dependent |
| D | data volume | **REMOVABLE (research) / PAID (commercial)** | OCSC 303 / JIBO 110 / AusKidTalk 136 h / MyST 470 h (paid) |
| E | speaker diversity | **REMOVABLE (research)** | OCSC 303, JIBO 110, AusKidTalk 620 |
| F | age domain | **PARTIALLY REMOVABLE (research)** | OCSC 4–9, JIBO 4–7, CAPIL 5–6, LWE 4.9; commercial 4–6 still missing |
| G | phone labels | **NOT REMOVED (main gap)** | no large child corpus with human phone labels; generated+verified strategy defined |
| H | license | **PARTIALLY REMOVABLE** | MyST paid commercial route verified; speechocean762 CC BY 4.0 clear; SIAK ND review open |
| I | Vietnamese-L1 | **NOT REMOVED (long-term)** | no public corpus; 2 research datasets identified; collection protocol designed |
| J | B2 readiness | **PILOT-READY (design)** | pilot design + frozen success criteria + encoder comparison (609 tokens) |

## What would move the gate

1. Two reviewers complete the pack → A/B/C resolved; then the local pilot decides the encoder
   question.
2. A signed MyST commercial license (or a Vietnamese-L1 partnership) → data gate for the intended
   scope.
3. Counsel answers on CC-BY-ND/derived weights → SIAK unblocked or definitively excluded.

## Success assessment for this WP (Part 32)

- ✅ Established a concrete, legal, realistic route to obtain labels (pipeline + pack + protocol).
- ✅ Defined a scientifically defensible small pilot that can determine whether full B2 is worth
  pursuing.
- ✅ Found legally usable child-speech resources at scale for research use (OCSC/JIBO) and a paid
  commercial route (MyST), substantially stronger evidence than WP-1.9.25.
- ❌ Did not obtain the labels (no reviewer available) and did not remove the phone-label/commercial
  license blocker.
