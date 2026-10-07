# B2 READINESS — WP-1.9.23

NOT permission to train. No encoder fine-tuning, no head training, no B2 run was performed.

## Checklist (WP-1.9.23 rule)

| # | criterion | status | evidence |
|---|---|---|---|
| 1 | production baseline remains exactly reproducible | **PASS** | WP-1.9.22: 609/609 cache, 65/65 archived; feature matrix rebuilt from those artifacts |
| 2 | acceptance defect has a measured research-only solution | **FAIL** | 0/68 rules FRR-first safe; best rule removes only 3/10 TYPE A at a 4.6-point dev recall loss |
| 3 | weak identity false accepts materially reduced | **FAIL** | 7/10 TYPE A remain under every recall-preserving rule; rank/margin/blank rules cost 21.8–51.6 recall points |
| 4 | true-present recall remains FRR-first acceptable | **FAIL** | best rule rejects 72/87 production-accepted weak true presents; LWE recall preserved only because the rule drops the similarity-path accept |
| 5 | rank-2..5 behavior is understood | **PASS (understood, unresolved)** | rank-2..5 subgroup FAR 1.0 (n=63); rank restriction removes 97 rank>0 true presents (WP-1.9.22) |
| 6 | blank-dominated acceptance is controlled | **FAIL** | 262/279 blank-dominated accepts remain; blank gating costs 51.6 recall points |
| 7 | strong encoder false accepts isolated rather than hidden | **PASS** | TYPE B = 4/14 false accepts, unchanged by every rule; reported separately |
| 8 | /r/ label evidence is adequate | **FAIL** | /r/ present n=1 LOW (LWE); pack has 3 unlabelled LWE /r/ tokens, all with negative word verdicts |
| 9 | 10–20 new confident PRESENT labels exist OR the report proves they are unavailable and labels are the remaining blocker | **PARTIAL** | 0 collected; 23-item READY_FOR_HUMAN_REVIEW pack; labels are the external-validation blocker |
| 10 | no unresolved acceptance contradiction remains | **FAIL** | weak true-present vs weak false-accept feature distributions overlap; no safe operating point found |

## Decision

**B2 = NOT READY.**

The acceptance redesign did not find a safe operating point (0/68), so — per the WP rule — B2 remains
blocked by decision-layer uncertainty even though the residual strong false accepts (4/14) are
encoder-side. The next step is not training but (a) obtaining the human labels that validate the
gray zone and (b) a design-only encoder-evidence study for weak finals and strong false peaks.
"DESIGN-READY" is not reached in this package.
