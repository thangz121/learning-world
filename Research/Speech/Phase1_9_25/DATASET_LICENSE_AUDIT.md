# DATASET & LICENSE AUDIT — WP-1.9.25 Phase 5

> **Tóm tắt (VI):** 18 nguồn dữ liệu được audit (không tải nguồn nào). Kết quả chính: chỉ 2 nguồn
> CLEAR/CLEAR_WITH_CONDITIONS có phone-level labels hoặc license thương mại — **speechocean762**
> (CC BY 4.0, nhưng 2,41 h child, Mandarin-L1, tuổi 6–15) và **MyST** (route thương mại có phí, 393 h
> nhưng chỉ word-level, tuổi 8–11). /r/ tốt nhất (**PERCEPT-R**, 32,5 h, 280 người) là
> **non-commercial**. SIAK CC-BY-ND cần legal review. Không nguồn nào đồng thời đạt quy mô + nhãn
> phone + tuổi 4–6 + license thương mại.

Full matrix: `DATASET_LICENSE_AUDIT.csv` (28 fields per dataset). No dataset was downloaded.
Statuses: CLEAR · CLEAR_WITH_CONDITIONS · REQUIRES_LEGAL_REVIEW · RESTRICTED · NOT_SUITABLE ·
LICENSE_UNVERIFIED.

## 1. Status summary

| status | datasets |
|---|---|
| CLEAR | speechocean762 (local; CC BY 4.0, free commercial) |
| CLEAR_WITH_CONDITIONS | LWE Zenodo 200495 (CC BY 4.0; project rule = test-only), MyST (non-commercial CC BY-NC-SA / paid commercial license via Boulder Learning) |
| REQUIRES_LEGAL_REVIEW | SIAK (CC-BY-ND-4.0; local note claims model building not prohibited, ND clause needs counsel) |
| RESTRICTED | CSLU Kids/OGI (LDC2007S18; research-only, commercial via OHSU), CMU Kids (LDC97S63), PERCEPT-R, PERCEPT-GFTA, Providence (all PhonBank, non-commercial) |
| LICENSE_UNVERIFIED | PF-STAR, TBALL, CID Children's Speech, UltraSuite, Storiza (2026), NCTE Dataset |
| NOT_SUITABLE | VietSpeech Multilingual Children database (no public download), Vietnamese children stop-consonant dataset (Lee et al. 2024, not distributed), all Vietnamese ASR corpora audited (adult) |

## 2. Verified license facts (with sources)

- **speechocean762**: OpenSLR SLR101, "Attribution 4.0 International (CC BY 4.0)"; paper states free
  for commercial and non-commercial use; 5000 utterances, 250 speakers (half children), ~6 h,
  phoneme-level scores by 5 experts (https://www.openslr.org/101, arXiv 2104.01378). Local child
  subset measured: 122 speakers / 2440 utts / 2.41 h.
- **MyST**: 393–448 h, 1,371 students (grades 3–5), 228,874 utterances; research use CC BY-NC-SA 4.0
  with a signed agreement; commercial licensing via Boulder Learning (LDC2021S05; the corpus paper
  notes ten commercial licensees; the Wikipedia corpus list records a flat USD 10K commercial fee).
  Word-level transcripts for ~45–100K utterances; no phone-level labels.
- **PERCEPT-R / PERCEPT-GFTA**: PhonBank, "distributed for noncommercial use"; 280/350 participants
  ages 6–17 (some adults); 32.5–36 h; rhotic/derhotic /r/ labels (PERCEPT-R) and broader phone
  sampling (PERCEPT-GFTA) (PMC12510240; talkbank.org pages).
- **CSLU Kids/OGI**: LDC2007S18; LDC non-commercial research agreement; commercial license via OHSU
  tech transfer; 1100 children K–G10; ~75–150 h; word-level transcriptions.
- **CMU Kids**: LDC97S63; individual + organization agreements; 76 children ages 6–11; ~9 h;
  word-level.
- **Providence**: PhonBank; 6 children ages 1–4; 364 h; broad phonemic (SAMPA) transcription of child
  utterances; research terms with citation requirement.
- **SIAK**: local Kaggle mirror; CC-BY-ND-4.0; 172 speakers, 16,308 utterances, ages 4–12 (594
  utterances age 4–6); single expert word score 0–100; no phone-level labels.
- **LWE Zenodo 200495**: CC BY 4.0 open; 11 children (mean age 4.9), 3 microphones; numbers and
  sentences; no phone labels; project rule = test-only.

## 3. Vietnamese-L1 findings

See `VIETNAMESE_CHILD_SPEECH_GAP.md`. No publicly accessible Vietnamese-L1 child speech corpus with
phone-level labels was identified. Research datasets exist (VietSpeech database; Lee et al. 2024) but
are not publicly distributed.

## 4. Consequences

1. The only license-clear dataset with phone-level scores (speechocean762) is ~2.4 h of child speech
   (Mandarin-L1, ages 6+): insufficient for the 10–30 h / 4–6 age / Vietnamese-L1 B2 target.
2. The only large-scale commercial child-speech route (MyST) has no phone-level labels and ages
   8–11; phone labels would have to be generated/annotated (Option B).
3. The best /r/ resource (PERCEPT-R) is non-commercial and ages 6–17.
4. SIAK remains the only locally available child corpus with age-4–6 data at any scale, but its
   CC-BY-ND status is unresolved for derived weights.
5. No dataset was downloaded; no license-unclear data was used.
