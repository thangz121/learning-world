# B2 READINESS — WP-1.9.22

This is NOT permission to train. No encoder fine-tuning, no head training, no B2 run was performed.

## Checklist (from the WP-1.9.22 mission)

| # | criterion | status | evidence |
|---|---|---|---|
| 1 | production decision reconstructed exactly | **PASS** | 609/609 recompute vs cache; 65/65 vs WP-1.9.12 archived |
| 2 | identity contribution quantified | **PASS** | 501/527 accepts identity (95.1%); 104 rank>0; 26 similarity; posterior path 0 |
| 3 | false acceptance mechanism quantified | **PASS** | 14/14 decomposed (7 IDENTITY_WEAK, 4 IDENTITY_STRONG, 3 SOFT_SIMILARITY) |
| 4 | removing identity does not destroy FRR-first | **FAIL** | variant B: 446 true presents rejected vs 11 false accepts removed; LWE recall 0.9375→0.0; dev 0.9088→0.0632 |
| 5 | remaining errors plausibly encoder/acoustic | **PARTIAL** | 4/14 false accepts are strong-support (0.63–0.96) encoder disagreements; weak true presents (`child_06_six` 0.0007, `child_07_one` 0.013) are no-evidence — but 10/14 false accepts are an acceptance-logic defect, not encoder |
| 6 | enough trusted labels for relevant phone classes | **FAIL** | 28 LWE blind labels; only 16 present; rank-2–5 identity accepts (104) largely unlabelled |
| 7 | /r/ no longer critically data-limited | **FAIL** | 1 present (LOW) / 5 absent; candidate pack prepared, not reviewed |
| 8 | no unresolved scoring/acceptance defect | **FAIL** | identity/similarity acceptance fires in blank-dominated (12/14), competitor-conflict (10/14) frames; 20.8% of identity accepts are not top-1; window-dependent identity 11.5% |

## Decision

**B2 = NOT READY.**

Both halves are required first:
1. **Acceptance-rule redesign (research-only, FRR-first):** rank-aware identity, margin/blank
   conditions, explicit UNCERTAIN; evaluated on the existing cache and labels before any encoder work.
2. **Label expansion:** the 20-item WP-1.9.21 candidate pack (esp. /r/ and weak present finals).

Only if (1) is resolved or proven to require encoder evidence, and (2) provides enough trusted
labels, may B2 be reconsidered. "DESIGN-READY" is not reached in this package.
