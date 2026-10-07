# FALSIFICATION RESULTS — WP-1.9.23

Candidate rule under test: **`D2_identity_blank_occ1.0`** (identity credit AND blank occupancy
≤ 1.0 — the best available dev trade-off; selected on dev, LWE external, test held out). Production
baseline and the conservative `F_main_sup0.1_mar0.01_blank0.95` rule are reported where relevant.
All counts are on the labeled pool (LWE 28 + SO762 dev 302 + SO762 test 227 = 557 tokens).

| # | test | production | best rule | verdict |
|---|---|---|---|---|
| 1 | Can a blank-dominated target still be accepted? (blank mean ≥ 0.90) | 279 | **262** | **FAIL** — 262 blank-dominated accepts remain |
| 2 | Can rank-2..5 identity with almost zero support still be accepted? (rank > 0, max < 0.05) | 34 | **34** | **FAIL** — unchanged |
| 3 | Can a competitor stronger than target still be accepted? (margin < 0) | 128 | **104** | **FAIL** — 104 remain |
| 4 | Can a one-frame spike still be accepted? (longest run ≤ 1 at p ≥ 0.05) | 271 | **248** | accepted by design — one-frame finals are real (203/330 labeled finals); the rule cannot separate true one-frame finals from false spikes |
| 5 | Can a strong encoder false peak still be accepted? (absent, max ≥ 0.30) | 4 | **4** | **FAIL** — irreducible by acceptance logic |
| 6 | Does the rule destroy known true weak-present cases? (production-accepted present, max < 0.15) | 87 accepted | **72 rejected** | **FAIL** — the best rule rejects 83% of weak true presents |
| 7 | Does the rule behave differently between LWE and SO762? | dev recall 0.9088 / FAR 0.5294 | LWE 0.9375 / 0.3333 vs dev 0.8632 / 0.4118 | transfer asymmetry: the rule is nearly free on LWE (drops the similarity-path accept) but costs 4.6 recall points on dev |
| 8 | Does the rule collapse on /r/? | n=21, recall 1.000 | recall 1.000 | PASS (but LWE /r/ present n=1 LOW; not conclusive) |
| 9 | Does the rule become overly conservative when blank is high? (blank mean ≥ 0.90) | n=339 | recall 0.8057, UNCERTAIN 0% | recall loss without an UNCERTAIN escape |
| 10 | Does the rule create excessive UNCERTAIN outcomes? | 0% | D2 0%; F rule 22.2% (dev) / 35.7% (LWE) | F family is over-conservative; D2 uses no UNCERTAIN at all |

## Interpretation

- The best rule reduces false PRESENT only by dropping the **similarity-path** accepts
  (`child_01_four`, `014350017_18`, `014470149_5`) and some all-blank-span identity accepts; it
  cannot touch the blank-dominated, competitor-conflict, rank-2..5 or strong-support cases without
  destroying weak true-present recall (test 6).
- Tests 1–3 fail because the weak true-present and weak false-accept feature distributions overlap:
  the same identity-in-blank-dominated-frames pattern produces both.
- Test 5 fails by construction: TYPE B false accepts are encoder-side evidence and must not be
  counted as solved by acceptance redesign.
- Test 4 is intentionally not a "pass": one-frame (20 ms) support is legitimate for child final
  consonants (WP-1.9.21), so a rule that rejects one-frame spikes would be worse; the rule therefore
  cannot use brevity as a separator.
- Test 10 shows the only family with explicit UNCERTAIN (F) buys its FAR reduction by deferring
  22–36% of decisions and still loses 28.4 dev recall points.

**Falsification outcome:** the redesign does not survive the FRR-first requirement; the evidence
supports `ACCEPTANCE_RULE_AND_ENCODER_BOTH_LIMITING`.
