# DATASET DISCOVERY REPORT — WP-1.9.26 Part 8

> **Tóm tắt (VI):** Mở rộng audit từ 18 → 28 nguồn. Phát hiện quan trọng: **có corpus trẻ 4–6 tuổi
> công khai quy mô lớn** — Ohio Child Speech Corpus (303 trẻ 4–9, TalkBank-CHILDES), JIBO Kids
> (110 trẻ 4–7, 21h, GitHub/Zenodo), CAPIL (30 trẻ 5–6). Blocker được tinh chỉnh: không phải thiếu
> giọng trẻ tuổi 4–6, mà thiếu **phone-level labels + license thương mại** trên cùng một corpus.
> Không tải nguồn nào.

## 1. New finds (vs WP-1.9.25)

| corpus | age | speakers | hours | annotations | access | status |
|---|---|---:|---:|---|---|---|
| Ohio Child Speech Corpus (OCSC) | 4–9 | 303 | not verified (7-task protocol) | CHILDES orthographic; lapel + table mic | free TalkBank account | RESTRICTED (research terms) |
| JIBO Kids Corpus | 4–7 | 110 | 21 | word-level transcripts; 383 wav | public GitHub + Zenodo mirror | LICENSE_UNVERIFIED (no LICENSE file) |
| AusKidTalk | 3–12 | 620 single-word task | 136.6 annotated | orthographic (corrected) | project registration | REQUIRES_LEGAL_REVIEW |
| CAPIL (CHILDES) | 5;1–6;10 | 30 children (+30 adults) | ~18 | transcribed spontaneous | TalkBank account | RESTRICTED (research) |
| Storiza Corpus (CoNLL 2026) | ~6–9 | 63 | ~9 | IPA phonemic + reading errors | release not stated | LICENSE_UNVERIFIED |
| SingaKids-Mandarin | 7–12 | 255 | 125 | word + phone-level + proficiency | unknown | LICENSE_UNVERIFIED (wrong language) |
| CFSC (Filipino) | 6–11 | 57 | ~8 | partial word/phoneme | unknown | LICENSE_UNVERIFIED |

## 2. What this changes

- **Age-domain blocker is partially removable**: OCSC (303 speakers, 4–9) and JIBO (110, 4–7)
  provide public child speech in the target age band at scale for research use. The earlier
  statement "no dataset" was too coarse; the refined blocker is **phone-level labels + commercial
  rights**, not raw child speech.
- **Phone-label blocker persists**: none of the new corpora has human phone-level labels; the only
  phone-labeled child resources found are speechocean762 (scores, Mandarin-L1, 2.4 h child),
  PERCEPT-R/GFTA (/r/-specific, non-commercial), SingaKids (Mandarin), CFSC (partial, Filipino).
- **Commercial blocker persists**: the large age-appropriate corpora are research-only
  (TalkBank/CHILDES terms) or license-unverified (JIBO); the only clear commercial-scale route is
  MyST (paid, word-level, ages 8–11).

## 3. Search coverage

Queries covered: children speech phone labels; child speech phoneme annotations; children English
pronunciation corpus; child speech articulation; children phoneme recognition; 4-6 year old speech
corpus; Vietnamese children English; Vietnamese L1 child speech; young learner English phoneme;
children pronunciation assessment; child speech phone-level annotation. Sources: academic
repositories (arXiv/ACL/ISCA/PMC/NSF PAR), TalkBank/CHILDES/PhonBank, LDC, ELRA, OpenSLR, Zenodo,
Hugging Face, Kaggle (provenance-checked), project pages (CSU VietSpeech, Storiza, Boulder
Learning), and the Wikipedia list of children's speech corpora. No dataset was downloaded.

## 4. Ranked recommendation

1. **Local pilot + label round** (no new licensing): existing LWE/SO762/SIAK + the 276-candidate
   review pack (see `09_B2_DESIGN/B2_PILOT_DESIGN.md`).
2. **OCSC/JIBO research-only diagnostic** (register + research terms): age-4–7 speaker diversity for
   encoder diagnostics; phone labels generated + spot-verified (research-only).
3. **MyST commercial license** (paid): the only legal route to 400+ h for a commercial model, with
   generated phone alignments and human verification (see `05_LEGAL/MYST_LICENSE_REVIEW.md`).
4. **SIAK legal review** (pending): could unlock 16K utterances locally, mostly ages 8–10.
5. **PERCEPT-R research-only /r/ benchmark** (non-commercial).
