# ASSESSABILITY_MATRIX — "refuse to guess" behavior across systems

| System | silence | noise/bad audio | wrong word | partial | Vietnamese/other language | Mechanism | Evidence |
|---|---|---|---|---|---|---|---|
| Speechace | NO_SPEECH | score reduction | FREE_SPEAK (off-script) | INCOMPLETE | detect_dialect warn/error option | `fidelity_class` + score_issue_list | E1 |
| Microsoft PA | no text / zero completeness | low accuracy | miscue Insertion/Substitution | Completeness ratio | n/a | Completeness + miscue | E1 |
| Chivox | `post proc failed` → re-record | audio-quality gate (clipping/background/missing) | no valid voice check | incomplete-response check | UNKNOWN | quality gate before feedback | E1 |
| SpeechSuper | UNKNOWN | UNKNOWN | UNKNOWN | UNKNOWN | UNKNOWN | not documented | E1 |
| SpeechStep | "declined rather than guessed at" (profile it cannot judge) | — | — | — | — | refusal rule (product claim) | E0/E1 |
| Speech Blubs | no score (attempt reward) | — | — | — | — | voice detection only | E0/E1 |
| SayBananas | excluded in study | **<50% unanalyzable** (audio quality) | excluded (study) | excluded | — | template match + manual review | E2 |
| SIAK | rating 0 excluded | rating 0 excluded | rating 0 excluded | interrupted excluded | — | rejected class in annotation | E2 |
| NOCASA | rating 0 removed | rating 0 removed | — | — | — | zero-rating removal | E2 |
| PER-MDD | retrieval threshold → blank | — | — | — | — | FRR-first; no assessment refusal | E2 |
| OpenPronounce | score 0 | low score | very low score | low score | low score | none (implicit) | E4 |
| speak-better-than-ai | low score (deletion sim=0) | low score | low score | low score | low score | none (implicit) | E3 |
| slip | lowConfidence if <3 frames | — | — | — | — | low-confidence flag | E3 |

**Pattern:** mature systems put an explicit assessability/fidelity decision BEFORE pronunciation
feedback; open-source systems mostly do not (they return low scores instead).
