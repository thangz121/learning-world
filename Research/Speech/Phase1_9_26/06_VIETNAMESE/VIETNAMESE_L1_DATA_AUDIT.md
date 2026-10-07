# VIETNAMESE-L1 DATA AUDIT — WP-1.9.26 Part 14

> **Tóm tắt (VI):** Tìm kiếm rộng (Anh + Việt) cho dữ liệu trẻ em Việt-L1 nói tiếng Anh hoặc tiếng
> Việt có annotation ngữ âm. Kết quả: **không có corpus công khai**; có 2 dataset nghiên cứu
> (VietSpeech — 69 trẻ 2;0–8;10 tại Úc; Phạm 2019/Lee 2024 — trẻ Việt 2;2–7 tuổi) nhưng không phân
> phối công khai. Toàn bộ corpus tiếng Việt tải được đều là người lớn.

## 1. Categories (strictly separated)

| category | examples found | public? | usable for B2? |
|---|---|---|---|
| English spoken by Vietnamese-L1 children | VietSpeech Study 2 (69 children 2;0–8;10, Australia; Vietnamese Speech Assessment + DEAP English) | no (research collaboration only) | no public route |
| Vietnamese speech by Vietnamese children | Phạm 2019 (195 children 2;2–5;11, Northern Vietnam, single words); Lee et al. 2024 (80 children 3–7, Central Vietnam, stops) | no | no public route |
| adult Vietnamese speech | VIVOS (15 h), Bud500 (500 h), ViMD (102 h), VinBigData-VLSP2020-100h, FOSD, VietSpeech social voice (1100 h) | some | NOT_SUITABLE (adult) |
| adult Vietnamese-accented English | L2-ARCTIC-style resources; no child counterpart identified | – | NOT_SUITABLE (adult) |
| child English from other L1s | speechocean762 (Mandarin-L1, 122 children); SIAK (mixed L1); MyST/OCSC/JIBO (US English) | yes/partial | usable with explicit domain limitation |

## 2. Findings

- No public Vietnamese-L1 child English pronunciation corpus with phone-level labels was identified.
- The VietSpeech project (Charles Sturt University) built a Vietnamese-English child speech database
  as part of its research; it is not a public download (contact the project).
- Phạm 2019 and Lee et al. 2024 are single-word articulation datasets of Vietnamese (not English),
  held by institutions; no download.
- The Vietnamese child speech literature consistently reports cross-linguistic differences relevant
  to the product (final consonant weakening, /θ/→/t/, /ð/→/z/, /r/ realization differences), which
  motivates the domain but does not provide data.

## 3. Consequence

A Vietnamese-L1 domain B2 is not executable from public data. Options: (a) partnership with one of
the research groups; (b) a prospective local collection under the protocol in
`VIETNAMESE_CHILD_SPEECH_COLLECTION_PROTOCOL.md`; (c) proceed with English child speech and state
the L1-domain limitation explicitly.
