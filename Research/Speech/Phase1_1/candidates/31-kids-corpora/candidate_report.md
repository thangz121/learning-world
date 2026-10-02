# Candidate 31-kids-corpora

## Source
Access/license research (no download) for the two restricted child corpora behind
candidates 29–30: OGI/CSLU Kids (LDC2007S18) + CMU Kids (LDC97S63). Primary
sources: LDC catalog pages + LDC readme/docs fetched 2026-10-02, plus
peer-reviewed corpus descriptions (Shobaki et al. 2007; Mostow 1996; survey rows
in ChildMandarin arXiv:2409.18584 and slate-lab papers). Nothing downloaded
beyond public catalog metadata — per policy: no restricted child dataset is
fetched without a signed agreement.

## Version
Catalog snapshots 2026-10-02 (LDC pages live; fee amounts login-gated, see
Problems). OGI/CSLU Kids v1.1 (release 2007-11-20). CMU Kids (release 1997, no
updates). This candidate is paperwork, not software — no commit/tag applies.

## License
Data licenses (code: n/a):
- OGI/CSLU Kids → "CSLU Agreement" (LDC-hosted PDF:
  `cslu-corpora-non-commercial-research-only.pdf`): NON-COMMERCIAL RESEARCH ONLY,
  signed agreement required. Copyright: portions © 2001–2002 CSLU/OHSU, © 2007
  Trustees of the University of Pennsylvania.
- CMU Kids → TWO signed agreements: Individual + Organization (`cmu-kids-
  individual-agreement.pdf`, `cmu-kids-organization-agreement.pdf`). Org must
  control/retain per-person applications, post access lists, and prohibit display/
  reproduction/transmission/distribution/publication outside terms. Prompt texts
  © 1994/1995 Weekly Reader (special reprint permission).
Status: RESTRICTED (both). Commercial use is out (CSLU forbids it; CMU limits to
linguistic R&D under agreement).

## Model License
NOT_AVAILABLE — no models are evaluated here. Any model TRAINED on these corpora
(e.g. candidate 30's recipe, candidate 29's EN aligner claim) inherits the
corpus restriction: research-only, non-redistributable, non-commercial.

## Dependency License
n/a (no software installed for this candidate).

## Intended Role
Validation-data sourcing ONLY. Standing principle: child speech validates, it
never defines "correct" — our V1 phoneme table stays calibrated on synthetic
fixtures until proven otherwise, and any future corpus use feeds
ASR_RECOGNITION vs PRONUNCIATION_ASSESSMENT as separate fields.

## Installation
None. Access path documented (not executed): LDC account → sign the applicable
agreement (CSLU non-commercial-research-only for OGI; Individual and/or
Organization for CMU Kids) → pay fee (non-members; members via subscription) →
Web Download. No account created, no agreement signed, no fee paid, no audio
fetched in this session.

## Runtime
n/a (no data, no runs).

## Tests
Documentary audit only, 2026-10-02: fetched both LDC catalog pages (full text),
the LDC2007S18 readme (2002-03-29, v1.1), the CMU Kids organization agreement,
and cross-checked age/speech-type facts against three independent papers. No
corpus audio touched; our local corpus (27 WAV, 16k mono) is NOT child speech
(SAPI/pregen synth + stress probes) and is never presented as such.

## Results
### A. OGI Kids == CSLU Kids' Speech v1.1 (LDC2007S18)
Identity: "OGI kids", "CSLU Kids", "CSLU: Kids' Speech" are the SAME corpus
(Shobaki, Hosom, Cole; OGI School/CSLU at OHSU; distributed by LDC).
- Speakers/ages: ~1,100 children, Kindergarten–Grade 10, ~100 per grade, Forest
  Grove School District (Oregon). Ages ≈ 5–16 (K≈5–6 … G10≈15–16).
- Speech types: SCRIPTED (prompted: LDC intro says ~60 items/child from a
  319-item phonetically-balanced list of simple words/sentences/digit strings;
  Data section says 200 isolated words + 10 numeric strings via animated
  character + text prompt — both wordings kept as published) + SPONTANEOUS
  (alphabet recitation + ~1-minute monologue, e.g. favorite movie).
- Recording: CSLU Speech Toolkit, Windows NT 4.0, SoundBlaster 16 PnP,
  head-mounted mics, 16-bit/16 kHz, ~20 min session → ~8–10 min speech/speaker.
  This release: 1,017 spontaneous files + word-level transcriptions + scripted
  verify/quality marks; ~12 GB total.
- Access: signed CSLU non-commercial-research-only agreement + LDC fee/account.
  Publicly obtainable WITHOUT agreement: catalog metadata, readme/docs, ONE
  sample spontaneous wav on the LDC page. Everything else: restricted.
### B. CMU Kids (LDC97S63)
- Speakers/ages: 76 children, ages 6–11 (grades 1–3; one 11-year-old in grade 6),
  24 male / 52 female, 5,180 utterances of READ sentences (Weekly Reader texts).
- Two populations: SUM95 (44 speakers, 3,333 utt — summer 1995 day-camp programs,
  good readers, on-site) + FP/Fort Pitt (32 speakers, 1,847 utt — April 1996
  school, at-risk/poor readers, dialectal "Pittsburghese" variants). Built as
  SPHINX-II training for the LISTEN reading tutor (reads incl. errorful reading).
- Format 16 kHz 1-channel PCM. Public sample: one audio + one transcript on LDC.
- Access: signed Individual and/or Organization agreement + LDC fee/account.
  Same split: metadata/docs/samples public; corpus restricted.
### C. What was / was not obtained
OBTAINED (public, no agreement): both catalog records, both LDC readme/docs,
agreement TEXTS (to read terms), survey cross-checks, sample-file existence.
NOT obtained (restricted): all corpus audio, all transcriptions, verify files,
speaker tables. LDC fee AMOUNTS not verified — both pages show "Login for the
applicable fee"; no figure is quoted here because none is visible without an
account. No LDC membership purchased, no application filed.

## Baseline Comparison
No audio → no baseline numbers. Qualitative fit vs our need (preschool 3–6,
short prompted words/phrases, validation-only): OGI K–G2 bands (≈ ages 5–8,
prompted words/sentences/digits, 16 kHz) are the CLOSEST match in kind (read
prompts, head-mounted mic, word transcripts) but still OLDER than our 3–5 band;
CMU Kids (6–11, read sentences, errorful-reading subset) is further in age yet
uniquely relevant to mispronunciation/errorful-reading validation (FP subset).
Neither covers ages 3–5; both are US English only (no Vietnamese-accented child
English). Any future validation subset must be age-banded and labeled as such.

## Improvements
None measured (no data in hand). Potential (unproven): age-graded prompted-word
validation for candidates 01/03/13/17 + error-pattern checks for scorer 11/19 —
ONLY after lawful access.

## Problems
1. Both corpora RESTRICTED: signed agreement(s) + LDC account + fee. No lawful
   shortcut; fee amounts unverifiable without login (do not quote estimates).
2. Age mismatch: neither corpus covers 3–5; OGI K-band starts ≈5. Preschool
   validation would be partial at best — documented limitation, not a reason to
   overclaim.
3. OGI doc wording discrepancy (~60 items of 319 vs 200 words + 10 strings) —
   reconcile against `docs/` AFTER access, not from memory.
4. Consent/provenance sensitivity: 1990s–2000s school recordings; any use must
   stay inside agreement terms (no redistribution, org access lists for CMU).
5. Open alternatives noted but NOT evaluated here (MyST — see candidate 18;
   CU Kids, PF-STAR, TBALL per surveys): different licenses/fees, separate audit
   required before any fetch.

## Unique Capability
The ONLY lawful path in scope to real child validation audio with age/grade
structure: OGI for prompted-word + spontaneous coverage K–G10 with transcripts;
CMU Kids for read-sentence + errorful-reading coverage 6–11. Everything else in
Phase 1.1 is adult/synth/SURROGATE.

## Retention Decision
HOLD (access-gated; data status RESTRICTED/RESEARCH_ONLY). No code, no weights,
no audio held. Do NOT mark BLOCKED-permanent — the path (LDC agreement + fee)
is real, just not taken in Phase 1.1.

## Future Combination
If Phase 1.2 budgets LDC access: (1) file ONE organization application covering
both corpora; (2) pull ONLY the age bands needed (OGI grades 00–02 first; CMU
FP subset for errorful-reading checks); (3) store OUTSIDE git (12 GB scale),
hash-manifest like current corpus; (4) run the standard harness (same short-word
sets + silence/noise controls) with results labeled child-VALIDATION, never
ground truth; (5) feed outcomes to scorer design (11/19) and kid-model A/B
(18/30) — recognition and assessment fields kept separate throughout.
