# Candidate 29-kid-align

## Source
https://github.com/megseekosh/kid_align — "Forced aligners, trained on largescale
child speech corpora, that are more successful than adult models." Cloned to
`D:\speech-lab\src-kidalign`.

## Version
HEAD `ff9dba2` dated 2020-03-11 ("Update README.md"). Unmaintained since 2020.
Repo contents (total 3 versioned files + git history): `README.md`,
`Quechua_dic.txt` (Quechua word-list dictionary), `aligners/child_english.zip`
(9,979,530 bytes), `aligners/quechua_model_2.zip` (9,836,327 bytes). Zip
internals (MFA v1 acoustic-model layout): `final.mdl` (~10.8 MB Kaldi GMM),
`tree`, `final.occs`, `meta.yaml`.

## License
NO LICENSE FILE in repo (checked root + full recursive listing). GitHub default
applies: all rights reserved, no reuse grant. Status: BLOCKED (same treatment as
candidate 22-ifmdd: cannot use code until clarified).

## Model License
NOT_AVAILABLE as a license grant (no license/stated terms for either zip).
`child_english.zip` meta.yaml declares: `architecture: gmm-hmm`, MFCC+LDA+fMLLR
(`ivectors: false, pitch: false`), `version: 1.0.0`, 47-phone ARPAbet-style
inventory with stress digits
(AE1/AE2/AH1/AH2/AO1/AW1/AW2/AY1/AY2/B/CH/D/EH0/EH1/EH2/ER1/ER2/EY1/EY2/F/G/
IH1/IH2/IY1/IY2/J/K/L/M/N/NG/OW1/OW2/OY1/OY2/P/R/S/SH/T/TH/UH1/UH2/UW1/UW2/V/W/Y).
Provenance per README only: "trained on controlled child speech in English and
Quechua (word list readings)" — no paper, no speaker/consent documentation, no
training script in repo. Child-data principle: even if runnable, this would be
VALIDATION evidence only, never the definition of correct.

## Dependency License
Requires the Montreal Forced Aligner v1-era binary (`bin/mfa_align`, McAuliffe
et al. 2020) + a per-language dictionary. That binary is NOT in the repo and MFA
v1 is obsolete. The MFA installed in our venv is 3.4.2 (MIT, pip-installed) but
its native dependency `_kalpy` has no Windows wheel, so the whole MFA CLI is
dead on this machine (same root cause as candidate 09-MFA, verified again below).
Kaldi model blobs inside the zips carry no shipped license text.

## Intended Role
Alignment (offline forced-alignment acoustic models, child-tuned; never runtime).

## Installation
1. `git clone https://github.com/megseekosh/kid_align D:\speech-lab\src-kidalign`
   — OK (HEAD ff9dba2, 2020-03-11).
2. Listed zip contents via `System.IO.Compression.ZipFile` — OK; read
   `meta.yaml` (gmm-hmm / mfcc / v1.0.0) — OK.
3. MFA availability check in venv `D:\speech-lab\venvs\p0` (torch 2.14.1+cpu,
   speechbrain 1.1.1, montreal-forced-aligner 3.4.2 pip): `mfa.exe align --help`
   and `python -m montreal_forced_aligner.command_line.align --help` both die at
   `from _kalpy.gmm import AccumAmDiagGmm` → `ModuleNotFoundError: No module
   named '_kalpy'`. No `bin/mfa_align` (v1 binary) exists anywhere in the clone.
4. Dictionary check: repo ships ONLY `Quechua_dic.txt`; NO English dictionary
   file, so even a working MFA v1 could not align our English corpus from this
   repo alone.

## Runtime
ASUS Windows, CPU-only, venv `D:\speech-lab\venvs\p0` (Python 3.14.7,
torch 2.14.1+cpu). No alignment process could be launched (see evidence above).

## Tests
Corpus standard would have been: sapi_red/blue/cat/red_apple +
pregen_apple_normal + stress_silence(/noise/tone), 16kHz mono (probed
sapi_red.wav = 19511 samples @16kHz = 1.22 s — fixture itself is fine).
Actual tests run 2026-10-02: (a) repo inventory + git history audit; (b) zip
asset inspection (both models present, EN meta.yaml read); (c) MFA CLI import
probe (two entry points, both fail on `_kalpy`); (d) dictionary coverage check
(English dict absent). No audio was fed to any aligner — there is no working
aligner entry point.

## Results
NOT RUN / BLOCKED before first audio. No timestamps, no TextGrids, no phone
boundaries produced. Nothing measured beyond the probes above.

## Baseline Comparison
Baseline is candidate 08-whisperX: word timestamps proven on the SAME corpus
(Red 0.15–0.32 s, Apple 0.42–0.66 s on sapi_red_apple.wav). kid_align produced
no output on the same files, so no numeric comparison exists. Conceptually:
whisperX needs no transcript; kid_align (MFA-style) REQUIRES transcript + dictionary
+ working MFA binary — a strictly heavier offline setup for the same timing goal.

## Improvements
None measured.

## Problems
1. No repo license → legally unusable as-is (BLOCKED, not just REVIEW).
2. Hard-blocked technically on this machine: MFA v1 binary absent AND MFA 3.4.2
   dead without `_kalpy` (Windows, CPU-only) — identical to candidate 09
   evidence, re-verified this session.
3. Format gap: v1.0.0 GMM models cannot be consumed by MFA 3.x even where MFA 3.x
   works (different acoustic-model stack + required dict/G2P pipeline missing).
4. Missing English dictionary in repo; README's "stay tuned for child Spanish
   aligner" never materialized (last commit 2020).
5. Undocumented training provenance (no paper/speakers/consent info) — fails the
   child-data audit bar regardless of engineering.

## Unique Capability
Concept value only: child-specific GMM-HMM aligners (EN + Quechua) trained on
controlled child word-list readings, ~10 MB each — tiny offline phone-bound
models IF resurrected. No other candidate offers a child-tuned classical
aligner; but concept alone retains nothing without license + runnable path.

## Retention Decision
BLOCKED (license status BLOCKED: no license file; technical status BLOCKED:
`_kalpy`/v1-binary gap on Windows CPU). Not HOLD — HOLD implies a visible path;
here both law and toolchain say no.

## Future Combination
Revisit ONLY if ALL clear: (a) upstream adds an explicit license (or ownership
confirms terms); (b) models are ported to a runnable stack (MFA 3.x acoustic
model or Kaldi recipe with English lexicon) or a conda/WSL `_kalpy` path is
proven per candidate 09; (c) training-data provenance is documented. Role would
remain offline phone-boundary tool feeding the scorer — never runtime, never
ground truth (child data = validation only).
