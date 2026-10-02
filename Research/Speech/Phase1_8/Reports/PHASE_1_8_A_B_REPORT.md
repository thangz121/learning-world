# Phase 1.8 A/B Report — IMG_0639 vs 1790932799243…mp3

Date: 2026-10-02. ASUS CPU-only.  
Baseline pipeline: Phase 1.6/1.7 components, **identical** Silero thr=**0.5**.  
Originals **untouched**, not committed to git.

---

## 1. Scope
A/B real-audio comparison to isolate why IMG_0639 VAD failed. No threshold retune before baseline A/B. No pronunciation scores (no authorized targets). No age inference as fact.

## 2. Files analyzed

| ID | Path | SHA-256 | Size |
|---|---|---|---|
| IMG_0639 | `IMG_0639.mp3` | `E4CDF24D…A6F7` | 5,264,528 |
| NEW | `1790932799243_8856108714107255767_8856108714107255767.mp3` | `17E3DA86…33FF` | 1,505,682 |

External copies: `D:\speech-lab\data\real-audio\`  
Derived 16 kHz mono: `Research/Speech/Phase1_8/AudioDerived/` (gitignored)

## 3. File metadata

| | IMG_0639 | NEW |
|---|---|---|
| duration | **219.28 s** | **94.02 s** |
| container | mp3 | mp3 |
| sample_rate (orig) | 48000 | 44100 |
| channels | 2 | 2 |
| bit_rate | 192 kbps | 128 kbps |
| derived | 16k mono PCM | 16k mono PCM |

## 4–5. Audio quality & energy structure

| Metric | IMG_0639 | NEW | Δ (NEW−IMG) | Interpretation |
|---|---:|---:|---:|---|
| RMS | 0.094 | 0.031 | −0.063 | NEW quieter overall |
| peak | 0.790 | 0.578 | −0.212 | |
| noise_floor p10 RMS | 0.030 | **0.0032** | −0.027 | NEW much lower floor |
| SNR p90/p10 dB | 13.3 | **23.6** | **+10.4** | NEW higher speech/noise contrast |
| silence ratio \|x\|<0.01 | 0.129 | **0.632** | +0.504 | NEW has real quiet gaps |
| continuous energy (1s rms>0.03) | **0.973** | **0.234** | **−0.739** | IMG nearly always “on” |
| energy contrast std/mean | 0.308 | **0.877** | **+0.569** | NEW clear peaks/valleys |
| spectral centroid Hz | 2202 | 1561 | −641 | |
| spectral flatness | 0.490 | 0.279 | −0.211 | IMG flatter/noisier spectrum |

**Energy structure answer:** YES — NEW has clear speech/silence contrast; IMG has nearly continuous energy (~97% of 1s windows above 0.03 RMS).

## 6. VAD baseline (Silero, thr=0.5, identical)

| Metric | IMG_0639 | NEW | Δ |
|---|---:|---:|---:|
| n_segments | **0** | **38** | +38 |
| speech_ratio | **0.0** | **0.598** | +0.598 |
| speech_duration_s | 0 | 56.2 | +56.2 |
| first_onset | null | ~present | |
| avg_seg_dur | — | measured | |

### Outcome code: **A_IMG_FAILS_NEW_WORKS**

Same Silero model/threshold: IMG → 0 segments; NEW → 38 segments (~60% speech ratio).

**Conclusion (precise):** Under this configuration, Silero VAD produces no segments when **continuous-energy ratio ≈ 0.97** and **energy contrast ≈ 0.31** (IMG), but succeeds when continuous-energy ratio ≈ 0.23 and energy contrast ≈ 0.88 with SNR(p90/p10) ≈ 24 dB (NEW).

This is **file-characteristic difference**, not “Silero always broken.”

## 7. Language / content
Both: **UNKNOWN** definitive LID (EN-only probes).  
NEW fixed-window Moonshine produced English-like strings (e.g. greetings/counting-like text) on some windows — **ASR hypothesis only**, not verified transcript, **not** age proof.  
One ASR window text contained a “4 yea…” fragment — **not** accepted as speaker-age metadata.

## 8. Speaker-age status
Both: **UNKNOWN** (no authorized metadata).

## 9. Pipeline behavior
Identical stages: quality → Silero thr0.5 → Moonshine 8s windows → whole-file acoustics.  
Pronunciation scoring: **not run** (no authorized English target).

## 10. Pronunciation eligibility
Both: **FALSE** without known target.

## 11. Root cause analysis

| Hypothesis | Evidence |
|---|---|
| Continuous background energy on IMG | cont_energy 0.97 vs 0.23 |
| Low speech/silence contrast on IMG | contrast 0.31 vs 0.88 |
| Lower SNR on IMG | 13.3 vs 23.6 dB |
| One-file Silero bug | **Rejected** — NEW works same config |
| VAD always fails on real mic | **Rejected** — NEW works |

**Primary associated properties with VAD failure on IMG:** high continuous-energy ratio + low energy contrast + lower SNR proxy — not merely “has noise.”

## 12. Controlled noise test (on NEW only)
White noise added to NEW; Silero thr=0.5:

| condition | n_seg | speech_ratio |
|---|---:|---:|
| clean NEW | 38 | 0.598 |
| SNR 20 | 38 | 0.528 |
| SNR 10 | 29 | 0.459 |
| SNR 5 | 26 | 0.418 |
| SNR 0 | 29 | 0.395 |
| SNR −5 | 10 | 0.140 |
| IMG as-is | **0** | **0** |

**Finding:** Additive white noise alone at moderate SNR does **not** reproduce IMG’s total VAD collapse. IMG failure is closer to **lack of speech/silence structure / continuous energy**, not simple broadband SNR drop to 0 dB.

(Synthetic noise ≠ real continuous ambience; labeled SYNTHETIC.)

## 13. VAD sensitivity findings
- Baseline thr=0.5 is adequate for NEW-like contrastive real speech.
- IMG requires different front-end (noise floor tracking, spectral gate, hybrid energy+Silero) — **not** blind threshold lowering (Phase 1.7 showed thr 0.1 still ~1 tiny segment).

## 14. Real-mic failure modes
1. Continuous-energy recordings without pause structure → Silero 0 seg  
2. Contrastive speech with quiet gaps → Silero OK  
3. EN-only ASR on real audio → sparse/hallucinated text; not LID  

## 15. Remaining gaps
- Human listen labels for content/language  
- Consent/age metadata  
- Hybrid VAD prototype (next phase)  
- Real-child consented pilot still pending  

## 16. Phase 1.9 recommendation
1. Implement hybrid VAD: energy contrast / noise-floor gate **before** Silero, validate on IMG vs NEW A/B.  
2. Do **not** globally lower Silero threshold.  
3. If authorized EN targets appear on NEW segments → optional pronunciation run.  
4. Real-child pilot when consented data exists.

---

## Acceptance checklist

| ID | Status |
|---|---|
| A Both hashed | **PASS** |
| B Originals untouched | **PASS** |
| C Identical baseline settings | **PASS** thr=0.5 |
| D VAD both measured | **PASS** |
| E Energy compared quantitatively | **PASS** |
| F No age/language guess as fact | **PASS** |
| G Pron only if target known | **PASS** (not scored) |
| H Difference quantitative | **PASS** |
| I VAD conclusion from BOTH files | **PASS** outcome A |
| J Controlled noise curve | **PASS** |
| K No ASR score boost | **PASS** |
| L No child claim | **PASS** |

## Artifacts
- `Results/IMG_0639_baseline.json`
- `Results/new_audio_baseline.json`
- `Results/real_audio_ab.json`
- `Results/vad_sensitivity_curve.json`
