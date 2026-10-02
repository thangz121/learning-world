# Candidate 14-nemo

## Source
NVIDIA-NeMo/Speech

## Version
3.0.0, parakeet-tdt-0.6b-v2 (CC-BY-4.0)

## License
Apache-2.0

## Model License
CC-BY-4.0 (commercial-ready with attribution)

## Dependency License
torch/lightning(2.2.5 pin; 2.6 breaks NeptuneLogger export)/lhotse/einops/kaldialign/webdataset/datasets; nv_one_logger wheel ABSENT on PyPI -> no-op stub used (documented)

## Intended Role
toolkit ASR

## Installation
pip + dep chase; telemetry stub; manual env; from_pretrained parakeet

## Runtime
CPU ASUS

## Tests
Corpus: sapi_red/blue/cat/red_apple + pregen_apple_normal + stress_silence(/noise/tone) 16kHz mono. Same audio for all STT candidates.

## Results
6/6 PERFECT + hypothesis scores (Red -0.31, Apple -0.06, silence empty score 0.0). Batch 0.3s/file, load 232s.

## Baseline Comparison
Only candidate emitting usable sequence-level confidence out of the box.

## Improvements
Confidence-scored ASR for SpeakingPassPolicy Strong gating.

## Problems
Heavy env (lightning pin, stub); 0.6B model size for tiny-vocab task.

## Unique Capability
RETAIN

## Retention Decision
Confidence + accuracy path; ONNX export evaluated in Phase 1.2.

## Future Combination
CLEAR (code+this checkpoint with attribution)
