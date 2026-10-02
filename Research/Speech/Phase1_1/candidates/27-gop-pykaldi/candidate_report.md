# Candidate 27-gop-pykaldi

## Source
JazminVidal/gop-pykaldi (classical DNN-GOP on EpaDB via PyKaldi). Clone: D:\speech-lab\src-gopkaldi

## Version
commit c34952ebbfc492226900a619c6e72e3d1a7daa67 (2022-06-12, "deleted and added files, fully working gop"), --depth 1

## License
NO LICENSE FILE in repo. README Copyright identical to gop-ft: code + EpaDB "freely available for research purposes", cite vidal2019epadb + sancinetti2021transfer. Research-only, no OSS grant.

## Model License
Same Kaldi Librispeech TDNN-F chain AM via OpenSLR download, ported to PyTorch (src/dataprep/convert_chain_to_pytorch.py). Terms unverified.

## Dependency License
torch/torchaudio + REAL PyKaldi + Kaldi C++ binaries + EpaDB (gated) + same malformed requirements line 18 as gop-ft.

## Intended Role
phone-level GOP pronunciation scoring (no fine-tuning; this repo = classical branch only; gop-ft adds train/, FTDNNPronscorer, finetuning_utils, ~2279-line delta)

## Installation
BLOCKED, evidence: same as 26-gop-ft (shared pipeline files): (1) `import kaldi` / `import kaldiio` -> ModuleNotFoundError in p0; PyPI pykaldi stub verified bogus + uninstalled. (2) Upstream: no Windows build ("no idea what is needed"), whl Linux-only py3.7-3.11. (3) align.py:1-5 `from kaldi.matrix/util.table/alignment/fstext/lat.align import ...` + MappedAligner.from_files + DoubleMatrixWriter; FeatureManager.py:3 kaldi.util.table + compute-mfcc-feats/ivector-extract-online2 via os.system; gop_utils.py:19 show-transitions binary. (4) src/gop/gop.py:8 IPython import fails in p0. (5) EpaDB gated; snapshot configs/ holds only dataprep.yaml (README's configs/gop.yaml absent).

## Runtime
n/a (Windows ASUS, CPU-only)

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
NOT RUN on corpus. No GOP score for sapi_red_apple.wav (no fabrication; needs Kaldi AM posteriors + MappedAligner forced alignment).

## Baseline Comparison
Cleanest statement of the DNN-GOP baseline (README:6-10 + src/gop/gop.py:52-81 + calculate_gop.py:38-62): `GOP(p) = -(1/D) * sum_{t=T}^{T+D-1} log P_t(p|O)`, P_t = softmaxed senone loglikes summed over all pdfs of phone p (mask matmul, 6024 senones), segment [T,T+D) from forced aligner on word transcript. Reference formula for any Phase 1.2 scorer (compare vs 19-openpronounce DTW score, 28 improved-GOP).

## Improvements
None measured (no run).

## Problems
No license; gated EpaDB; PyKaldi/Kaldi unbuildable on Windows/py3.14; Unix-only dataprep (wget/tar/cp/ln -s); missing gop.yaml; IPython top-level import.

## Unique Capability
HOLD (FORMULA REFERENCE: minimal canonical DNN-GOP definition + PyTorch-ported TDNN-F align-then-score pattern)

## Retention Decision
Keep report only. If a Linux+Kaldi runner ever appears, this (not gop-ft) is the smaller recipe to revive for baseline GOP numbers.

## Future Combination
BLOCKED (license + Kaldi/PyKaldi Windows + gated data)
