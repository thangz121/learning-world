# MyST B2 DECISION — WP-1.9.27 Part 21

> **Tóm tắt (VI):** MyST (LDC2021S05): 470 h, 1.371 học sinh lớp 3–5, lexicon + train/dev/test;
> research CC BY-NC-SA + DUA; thương mại trả phí qua Boulder Learning. KHÔNG có phone-level labels
> → cần forced alignment + hiệu chuẩn người. Quyết định hiện tại: **DO_NOT_BUY_NOW /
> REQUIRES_ANNOTATION** — chỉ mua nếu pilot dương tính và quote xác nhận quyền derived weights.

## Decision

**Current: DO_NOT_BUY** (no purchase now, no download beyond what terms allow) combined with
**REQUIRES_ANNOTATION** for any future use. This is the only verified commercial-scale route, but
it cannot answer the current hypothesis alone.

## Facts (verified in WP-1.9.26, unchanged)

- LDC2021S05: ~470 h English, 1,371 students grades 3–5 (~8–11 years), ~102K transcribed utterances
  (~45%), pronunciation dictionary, official train/dev/test, FLAC 16 kHz.
- Research route: CC BY-NC-SA 4.0 + Boulder Learning research agreement (non-commercial only; no
  redistribution).
- Commercial route: paid license via Boulder Learning; secondary source records a flat USD 10K fee —
  must be confirmed by quote, not budgeted from hearsay.
- No phone-level labels; age band 8–11 (outside the 4–6 target).

## Movement conditions (all required before BUY)

1. Local pilot is SUCCESS (or at least PARTIAL with a clear mechanism) per `09_SUCCESS_CRITERIA/`.
2. Signed/confirmed quote answers: (a) fee, (b) model training permitted, (c) derived weights may be
   distributed/embedded in the product, (d) derived phone-level annotations permitted,
   (e) term/renewal/contractor access.
3. A phone-label generation + human-verification plan exists (MFA + stratified spot-checks; see
   `07_ANNOTATION` in WP-1.9.26).

If the pilot fails: DO_NOT_BUY; the corpus cannot fix an acceptance/representation problem it was
not designed to measure.

## Cost sketch (to confirm, not commit)

- License: quote-dependent (secondary source ~USD 10K one-off; unverified).
- Annotation: forced alignment CPU-only; human verification of a 10–30 h subset (word-level checks +
  500–2,000 phone spot-labels).
- Compute for adaptation: GPU required (not ASUS).
