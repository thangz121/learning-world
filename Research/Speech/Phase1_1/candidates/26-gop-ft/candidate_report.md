# Candidate 26-gop-ft

## Source
JazminVidal/gop-ft (transfer-learning GOP on EpaDB, ICASSP 2022: Sancinetti/Vidal/Bonomi/Ferrer). Clone: D:\speech-lab\src-gopft

## Version
commit 0a0abc30d6dde454cf7ab563bc07107b885580fa (2022-06-14, "fully working per-phone and per-frame gop-ft"), --depth 1

## License
NO LICENSE FILE in repo (checked root + tree). README Copyright: code + EpaDB "freely available for research purposes", cite vidal2019epadb + sancinetti2021transfer. = research-only grant, no OSS license -> cannot ship product code until clarified.

## Model License
Librispeech chain TDNN-F AM downloaded at dataprep from kaldi-asr.org/models/13 (0013_librispeech_v1_chain/lm/extractor.tar.gz), converted Kaldi->PyTorch (src/dataprep/convert_chain_to_pytorch*.py). Terms = OpenSLR/Kaldi model terms (unverified here).

## Dependency License
torch/torchaudio + REAL PyKaldi (pykaldi/pykaldi, Apache-2.0 but source-build only) + Kaldi C++ binaries + EpaDB (gated, email jvidal@dc.uba.ar) + wandb + malformed requirements line 18 (`+git+https://github.com/Legisign/Praat-textgrids`, invalid pip syntax).

## Intended Role
pronunciation scoring (phone-level GOP + fine-tuned scorer)

## Installation
BLOCKED, evidence: (1) `import kaldi` -> ModuleNotFoundError in p0; PyPI `pykaldi 0.0.1` is a stub ("A demo package", Alienmaster, 1.2kB, provides NO module; verified + uninstalled, p0 clean). (2) Upstream PyKaldi FAQ: "How do I build PyKaldi on Windows? We have no idea... probably lots of changes"; official whl = Linux only, py3.7-3.11 (+experim. Mac M1/M2) -> no Windows, no py3.14. (3) Pipeline is Unix-only: prepare_data.py uses wget/tar/cp/ln -s/mkdir -p + Kaldi bins (nnet3-copy, compute-mfcc-feats, ivector-extract-online2, show-transitions); FeatureManager.py:3 `from kaldi.util.table import RandomAccessMatrixReader`; align.py:1-5 imports kaldi.matrix/util.table/alignment/fstext/lat.align. (4) Even pure file src/gop/gop.py:8 `from IPython import embed` fails in p0 (no IPython; not installed to protect shared env). (5) EpaDB gated; README configs/gop.yaml absent from snapshot (only dataprep.yaml present).

## Runtime
n/a (Windows ASUS, CPU-only lab; repo additionally hardcodes CUDA in FTDNNPronscorer.py:10-26 `summarize_outputs_per_phone` -> `.to(cuda0)` with cuda:0, so loss_per_phone path crashes on CPU-only even if deps existed)

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
NOT RUN on corpus. No GOP score for sapi_red_apple.wav (refused to fabricate; posteriors + forced alignment require Kaldi AM + EpaDB pipeline above).

## Baseline Comparison
Posterior/GOP math (classical branch, src/gop/gop.py + calculate_gop.py:46-57): senone loglikes -> softmax -> phone posteriors via pdf-to-phone mask matmul (`matrix_gop_robust`, 6024 senones) -> `GOP(p) = -mean(log P_t(p|O))` over forced-aligned frames (Witt & Young 2000, DNN variant). GOP-FT branch: replace TDNN-F last layer with OutputLayer(256->phone_count, Sigmoid) (FTDNNPronscorer.py:64-75); LayO (output layer only) vs LayO+1 (last hidden unfrozen stage 2); per-phone BCEWithLogitsLoss with phone weights (train.py:123-188, criterion_fast). Documented as Phase 1.2 scorer design reference.

## Improvements
None measured (no run).

## Problems
No license file; gated EpaDB; Kaldi+PyKaldi unbuildable on Windows/py3.14; Unix-only dataprep; missing gop.yaml; CUDA-hardcoded scorer path; IPython import at module top.

## Unique Capability
HOLD (DESIGN REFERENCE: LayO/LayO+1 + per-phone weighted BCE scorer head on frozen ASR backbone)

## Retention Decision
Keep report + formula; no code vendored. Revisit only if Linux runner + EpaDB access exist.

## Future Combination
BLOCKED (license + Kaldi/PyKaldi Windows + gated data + CUDA hardcode)
