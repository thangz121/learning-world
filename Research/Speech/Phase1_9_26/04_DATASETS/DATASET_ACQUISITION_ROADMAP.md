# DATASET ACQUISITION ROADMAP — WP-1.9.26 Parts 22/23

> **Tóm tắt (VI):** Lộ trình theo thứ tự hiệu quả-loại blocker: (1) thu nhãn trên pack 276 (không
> cần license mới); (2) OCSC/JIBO cho diagnostic tuổi 4–7 (research-only); (3) MyST license thương
> mại (paid) cho quy mô; (4) SIAK legal review; (5) PERCEPT-R cho /r/ (research-only). Không route
> nào đồng thời đạt scale + phone labels + thương mại ngay hôm nay.

| rank | route | probability of access | effort | legal risk | scientific value | label quality | speaker diversity | age relevance | removes which blocker |
|---:|---|---|---|---|---|---|---|---|---|
| 1 | Human label round on the 276-candidate pack (pipeline built, 0 labels so far) | high (needs 2 reviewers) | low-medium | none (local recordings) | high (settles TYPE-B, /r/, weak-present) | high (listening) | 49 in pack | 4–15 | human-label + TYPE-B + /r/ |
| 2 | OCSC + JIBO research-only registration (303 + 110 speakers, 4–9 / 4–7) | high (free account) | low | medium (research terms; no commercial) | high for encoder diagnostics and age-domain | generated + verified (research) | high | high (4–7) | data-volume + age + speaker diversity (research-only) |
| 3 | MyST commercial license (Boulder Learning; 393–470 h, 1371 speakers, lexicon) | medium (paid, contact) | medium | low-medium (explicit commercial route) | high for adaptation | generated phone alignments + human verification | high | medium (8–11) | data-volume + commercial (phone labels still generated) |
| 4 | SIAK CC-BY-ND legal review (172 speakers, local) | medium (counsel needed) | low | high (ND interpretation) | medium | word scores | high | medium (4–6 thin) | local age-4–6 data if cleared |
| 5 | PERCEPT-R research-only /r/ benchmark (280 speakers, 32.5 h) | high (PhonBank) | low | medium (non-commercial) | high for /r/ | high (rhotic labels) | high | low-medium (6–17) | /r/ diagnostics (research) |
| 6 | PF-STAR / CSLU Kids / CMU Kids license inquiries | medium | medium | medium | medium | word-level | high | medium-high (4–14 / K–G10) | data-volume (research/commercial unclear) |
| 7 | Local Vietnamese-L1 collection (protocol designed) | medium (partnership) | high | low with consent | highest domain relevance | human phone labels possible | 30–50 target | 4–6 | all blockers (long-term) |

## Route logic

- Route 1 is the only one executable *today* with no licensing: it removes the label blocker and
  enables the local pilot.
- Routes 2–3 split the data blocker into a **research-only track** (OCSC/JIBO, free, non-commercial)
  and a **commercial track** (MyST, paid). Neither alone is sufficient: the research track cannot
  ship in the product; the commercial track lacks phone labels and the 4–6 age band.
- Route 7 is the only route that solves age + L1 + labels + licensing simultaneously; it is a
  collection project, not a download.
