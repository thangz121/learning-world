# Child Speech Benchmarks — Reuse Analysis

| Item | Type | Status | Reason |
|---|---|---|---|
| speechocean762 corpus | DIRECT_DATASET | **ADAPT (legal: commercial allowed)** | English child+L2 + phoneme-level expert labels |
| NOCASA / TeflonNorL2 | DIRECT_DATASET | ADAPT (EULA required) | Norwegian; methodology + child speech |
| UAR + bootstrap CI + zero-rating removal | EVALUATION_METHOD | **DIRECT_REUSE** | Methodology for our child scoring research |
| Multi-task wav2vec2 baseline | ALGORITHM | REFERENCE_ONLY | Baseline code MIT? (check repo license) |
| SVM+ComParE_16 interpretability | EVALUATION_METHOD | REFERENCE_ONLY | Explainability angle |
| Baseline ceiling 36.37% UAR | RESEARCH_REFERENCE | — | Sets realistic expectations for child APA |

REUSE_COST: LOW (methodology) / MEDIUM (datasets)
EXPECTED_VALUE: **HIGH — calibration data + evaluation methodology**
