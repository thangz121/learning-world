# PERCEPT-R / GFTA LICENSE REVIEW — WP-1.9.26 Part 12

> **Tóm tắt (VI):** PERCEPT-R/GFTA (PhonBank): 280/350 người, 6–17 tuổi + một ít người lớn,
> 32,5–36h, nhãn rhotic/derhotic cho /r/ (R) và mẫu phone rộng hơn (GFTA). **Phân phối
> non-commercial** (PMC12510240). Giá trị nghiên cứu cao cho /r/; **không dùng được cho sản phẩm
> thương mại** nếu không có thỏa thuận riêng.

## Facts (verified)

- PhonBank pages: PERCEPT-R 280 participants ages 6–17 (some adults); PERCEPT-GFTA 350 participants;
  combined >36 h and >125,000 syllable/word/phrase utterances; formatted for Phon; citation
  required (TalkBank rules).
- PMC12510240: "The PERCEPT project is distributed for **noncommercial use** through PhonBank".
- PERCEPT-R labels: perceptual rhotic/derhotic /r/ judgments (word level), not full phone labels;
  PERCEPT-GFTA samples a broader range of phonemes from the GFTA-3 articulation test.
- Ages skew toward the younger limit of 6–24 (mean ≈ 11.3 y in v2.2.1p); no 4–6 band.

## Value separation

| aspect | assessment |
|---|---|
| RESEARCH-ONLY VALUE | high: the best available /r/ benchmark (rhotic vs derhotic), 280 speakers, aligned with the project's weakest phone class |
| COMMERCIAL PRODUCT VALUE | none under the current non-commercial distribution; a separate agreement with the authors/institutions would be required |

## Recommended use

Use PERCEPT-R as a **research-only /r/ diagnostic benchmark** (encoder evidence on rhotic vs
derhotic tokens; not for training a distributed model). Cite Benway et al. 2022 and the TalkBank
rules. Do not ship any weights derived from it without a separate commercial agreement.
