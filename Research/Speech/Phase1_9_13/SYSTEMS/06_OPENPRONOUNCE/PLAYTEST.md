# OpenPronounce — Playtest (local run)

STATUS: **RAN** (not a consumer product; local CLI/library).

Environment: Windows, venv `D:\speech-lab\venvs\op13`, torch 2.14.1+cpu, transformers 5.17.0.
Runner: `RUN_LOGS/run_openpronounce.py` (env: espeakng-loader; monkeypatched gTTS → local SAPI ref).
Raw outputs: `Research/Speech/Phase1_9_13/RAW_OUTPUT/openpronounce/*.json`.

Run log summary (16 cases):
```
phone model enabled: True
adult_red            score=100.0  per=0.0    errors=0
adult_blue           score=23.23  per=0.6667 errors=1
adult_cat            score=100.0  per=0.0    errors=0
adult_apple          score=53.34  per=1.0    errors=1
adult_big            score=65.62  per=0.6667 errors=0
adult_book           score=25.49  per=0.6667 errors=1
adult_dog            score=80.7   per=0.3333 errors=0
adult_red_apple      score=100.0  per=0.0    errors=0
adult_red_vs_blue    score=6.77   per=1.0    errors=1   (wrong-word control)
child_07_four        score=20.48  per=0.5    errors=1   (human: final /r/ absent)
child_06_four        score=24.69  per=0.5    errors=1   (human: final /r/ absent)
child_09_four        score=12.19  per=1.5    errors=1   (human: final /r/ absent)
child_05_four        score=3.05   per=1.0    errors=1   (unlabeled)
child_07_one         score=2.46   per=1.3333 errors=1   (human: final present) ← FALSE LOW
child_02_eight       score=20.0   per=0.5    errors=0   (human: final present)
silence_1s_vs_red    score=0.0    per=1.0    errors=1
```
