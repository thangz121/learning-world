# FULL B2 DATA ROADMAP — WP-1.9.27 Part 32

> **Tóm tắt (VI):** Lộ trình 6 route xếp hạng theo hiệu quả-loại-blocker: (1) nhãn người trên dữ
> liệu local; (2) pilot local; (3) OCSC/JIBO/AusKidTalk/CAPIL sau đăng ký (research); (4) MyST
> thương mại; (5) SIAK sau pháp lý; (6) thu thập Việt-L1. Không route nào giải quyết đồng thời
> scale + phone labels + thương mại ngay hôm nay.

| rank | route | cost | time | license | annotation burden | scientific value | speaker diversity | age relevance | phone-label quality |
|---:|---|---|---|---|---|---|---|---|---|
| 1 | Human labels on existing local data (Pack R + Pack P) | none | days (human) | local; no licence | none (listening labels) | high: unblocks TYPE-B, /r/, pilot | 49 + 10 | 4–15 | human listening (authority) |
| 2 | Local pilot (B2-D/B2-E, CPU) | none | days after labels | local; no licence | minimal | high: GO/NO-GO for full B2 | 10 | 6–7 | human listening |
| 3 | OCSC/JIBO/AusKidTalk/CAPIL (research registration) | free—low | weeks | research; needs registration/confirmation | generated + human verification | high for diagnostics/age | 303/110/620/30 | 4–9/4–7/3–12/5–6 | generated + verified |
| 4 | MyST commercial license | paid (quote; ~USD 10K secondary, unverified) | weeks–months | paid commercial | forced alignment + verification | high for adaptation | 1,371 | 8–11 | generated + verified |
| 5 | SIAK (after legal review) | low | months (counsel) | CC-BY-ND unresolved | word scores only | medium | 172 | 4–12 (thin 4–6) | score-derived |
| 6 | Vietnamese-L1 local collection | high (ethics, partnership, recording) | months+ | own consent | human phone labels possible | highest domain relevance | 30–50 target | 4–6 | human phone labels |

## Route logic

- Route 1 is executable today with no licence; it unblocks the label blocker and enables the pilot.
- Routes 3–4 split the data blocker into a research track (non-commercial) and a commercial track
  (paid, no phone labels, wrong age band). Neither alone suffices for shipping.
- Route 6 is the only route that solves age + L1 + labels + licensing together; it is a collection
  project, not a download.
- Do not start a 10–30 h collection (or pay MyST) before the pilot answers the encoder question.

## Parallel no-cost actions (this WP)

- TalkBank registration (OCSC/CAPIL/Providence access); PERCEPT-R research request for /r/.
- MyST commercial quote request; counsel questions (`LEGAL_QUESTIONS_FOR_COUNSEL.md`); JIBO author
  license confirmation; AusKidTalk terms inquiry.

Until the pilot and/or legal answers: all restricted corpora remain REQUIRES_LEGAL_REVIEW or
LICENSE_UNVERIFIED; nothing was downloaded.
