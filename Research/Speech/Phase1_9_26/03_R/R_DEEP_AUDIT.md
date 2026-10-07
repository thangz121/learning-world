# /r/ DEEP AUDIT — WP-1.9.26 Part 7

> **Tóm tắt (VI):** 24 ca /r/ (9 LWE + 15 SO762) với acoustic features mới (duration, RMS, voiced
> fraction, F1/F2/F3 qua parselmouth). Kết quả quan sát: span median 487 ms; RMS trong span ~1,13×
> toàn utterance; F3 median 2812 Hz, F3/F2 median 1,84 (gợi ý rhoticity yếu — chỉ là giả thuyết,
> n=24, formant trẻ em nhiễu); 20/24 isolated peak; 0/9 LWE có max_A ≥ 0,3. Không kết luận tổng quát.

Full data: `R_CASES.csv` (per-case context, duration, RMS, voiced fraction, F1/F2/F3, model
evidence, label). No new encoder runs; parselmouth local.

## 1. Inventory and context

| group | n | speakers | label status | median max_A | alt median | isolated peaks |
|---|---:|---:|---|---:|---:|---:|
| LWE | 9 | 9 | 1 PRESENT LOW / 5 ABSENT HIGH / 3 unlabelled | 0.041 | 0.015 (6 covered) | 8 |
| SO762 | 15 | 10 | 11 score 2.0, 1 score 0.0, 3 intermediate | 0.206 | n/a | 12 |

Context: all 24 are word-final; 15/24 are also utterance-final (following silence). Preceding
phones are vowels (ɔ, ɝ, ɑ) except a few clusters. Child ages 6–15 (so762; age not joined into
this artifact).

## 2. Acoustic observations (OBSERVED)

- Production span median 487 ms (includes the preceding vowel when the forced span is wide).
- RMS in span / RMS utterance median 1.13 (final /r/ is not systematically low-energy in the
  span — but the span often covers the vowel).
- Voiced fraction and F1/F2/F3 computed per case (parselmouth Burg). Median F3 2812 Hz,
  median F3/F2 ratio 1.84.
- 20/24 isolated one-frame model peaks; 8/9 LWE cases < 0.10 evidence.
- Alternative encoder gains evidence for `child_04_four` (0.09 → 0.41) and `child_01_seven`
  (0.06 → 0.47) but loses it for other tokens (see encoder comparison).

## 3. Hypotheses (HYPOTHESIZED, not established)

| hypothesis | evidence for | evidence against / gap |
|---|---|---|
| child /r/ is often short and weakly rhotic, under-resolved by the 20 ms CTC frame | 20/24 isolated peaks; median max_A 0.041 in LWE | no human labels on the peaks; formant estimation noisy in children |
| F3 lowering (rhoticity) is weak or absent in these productions | median F3 2812 Hz is not low | no adult/child normative baseline in this artifact; span includes vowel |
| Vietnamese-L1 /r/ realization differs (trill/tap/approximant) | literature (VietSpeech: Vietnamese /r/ is a trill/uvular variant) | no Vietnamese-L1 child recordings in the audit |

## 4. What is NOT claimed

- No general /r/ conclusion from n=1 PRESENT (LOW).
- No causal claim about short duration, weak rhoticity, or L1 transfer.
- No claim that the encoder is the sole limiter for /r/.

## 5. Next steps

15 PRESENT + 15 ABSENT /r/ listening labels across ≥15 speakers (pack pools A–C, plus new
candidates), then re-run this audit with labels attached. The best external /r/ resource
(PERCEPT-R) is non-commercial (see `05_LEGAL/PERCEPT_R_LICENSE_REVIEW.md`).
