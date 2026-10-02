# Phase 1.4 — Phone evidence hardening

Baseline freeze: Phase 1.3 `00dca92`.

## Run
```
# LWE forensic soft vs hard
D:\speech-lab\venvs\p0\Scripts\python Research\Speech\Phase1_4\LWE\run_forensic_v2.py

# Protocol v2
D:\speech-lab\venvs\p0\Scripts\python Research\Speech\Phase1_4\Pipeline\speaking_protocol_v2.py --audio ... --target red --out out.json

# Non-ceiling SO762 cal
D:\speech-lab\venvs\p0\Scripts\python Research\Speech\Phase1_4\Calibration\run_nonceiling_cal.py 10
```

Principles: no ASR→score boost; child never canonical; SCORE≠CONFIDENCE.
