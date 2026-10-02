# Candidate 28-gop-improved

## Source
sweekarsud/Goodness-of-Pronunciation (Interspeech 2019 improved-GOP with HMM transition probabilities). Clone: D:\speech-lab\src-gopimp

## Version
commit ff1d3f1a1fa875e87534d3cde960d24e7c3525fb (2025-07-19, "Delete file."), --depth 1

## License
NO LICENSE FILE in repo. README has citation request only (Sudhakara et al., Interspeech 2019, pp.954-958). No grant text -> treat as research-only / all-rights-reserved until clarified.

## Model License
Requires caller-supplied Kaldi nnet2/nnet3 native AM (show-transitions -> lookup_table; align.sh -> alignment; nnet_am_compute.cc -> posterior ark). No model shipped.

## Dependency License
numpy/pandas + Kaldi ASR toolkit (binaries, Linux recipes) + bash. Python script itself is dependency-light (prop_gop_eqn.py: sys/math/os/subprocess/pandas/numpy).

## Intended Role
phone-level GOP scoring given Kaldi alignment + senone posteriors

## Installation
PARTIAL-RUN. Python scorer needs only numpy/pandas (present in p0: numpy 2.5.3, pandas 3.0.6) + bash for 2 trivial .sh preprocessors (modify_post.sh drops ark header; extract_from_alignments.sh splits alignment line into tmp_t_ids/tmp_phones/tmp_segments). Git Bash present (C:\Program Files\Git\bin\bash.exe 5.3.15, not on PATH by default) -> ran with Git\bin prepended to PATH, cwd=src-gopimp, output to Temp file (shipped gop_outfile.txt untouched).

## Runtime
Windows ASUS, CPU-only, p0 (py3.14, torch CPU - torch not needed for this script)

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
VERIFIED on repo-bundled sample (NOT our corpus): `python prop_gop_eqn.py posterior_infile.ark alignment_infile.txt <tmp_out>` reproduced shipped gop_outfile.txt BYTE-IDENTICAL (diff empty): 15 phones SIL JH_B OW_I N_I IH_I L_E B_B IY_E HH_B IY_I R_E S_B UW_I N_E SIL, e.g. SIL 6.918000, JH_B 3.898403, R_E -4.743207, N_I -2.823758. NO score for sapi_red_apple.wav: generating its posterior_infile.ark + alignment_infile.txt needs the Kaldi nnet2 AM + align.sh + nnet_am_compute (unavailable on Windows) -> BLOCKED, no fabrication.

## Baseline Comparison
Posterior/GOP math (prop_gop_eqn.py:52-67, paper eq.): per phone with D frames, transition ids req_t_id and pdf map from lookup_table (tid -> pdf, trans_prob): `score = (sum_{y=1}^{D-1} [log P_trans(tid_y) + log post(frame_y, pdf_y)] + log post(last) + (D-1)*log(N_senones)) / D`. Delta vs classical GOP (cand. 27: pure mean log-posterior): adds HMM transition-prob term + N_senones normalizer; note scores are log-domain (positive values possible, e.g. HH_B 7.22) -> NOT a 0-100 scale like 19-openpronounce; thresholds must be recalibrated per AM.

## Improvements
None measured on LWE audio. Verified artifact: single-file scorer core runs on CPU with zero ML deps beyond numpy/pandas.

## Problems
No license file; corpus scoring blocked (no Kaldi AM/posts/align on Windows); bundled lookup_table.txt (74194 lines) + sample ark are toy-scale (U_ID, ~186 frames) tied to the authors' AM, not reusable for our vocab; gen_lookup_table.sh hardcodes Kaldi exp paths (../exp/nnet2_online/nnet_ms_a_online + ../../../../src/bin/show-transitions).

## Unique Capability
RETAIN (FORMULA + MICRO-SCORER: dependency-light improved-GOP core; candidate Phase-1.2 phone scorer IF posteriors come from a Windows-runnable AM + own forced alignment; transition term needs re-derivation for non-Kaldi AMs)

## Retention Decision
Keep prop_gop_eqn.py logic as reference implementation; do not vendor AM-specific tables. Next step on Linux/Kaldi runner: regenerate lookup_table + posteriors for LWE vocab, then score sapi_red_apple.wav for real.

## Future Combination
CLEAR (formula, after license clarified) + COMPLEMENTARY to 19 (utterance 0-100) and 27 (classical baseline); needs calibration mapping log-GOP -> kid-facing score
