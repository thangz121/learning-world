# NO-DATA DEAD-END TEST — WP-1.9.26 Part 24

> **Tóm tắt (VI):** Trả lời 10 câu hỏi trước khi kết luận "blocked": (1) local data đủ cho ~276 nhãn
> — có; (2) dữ liệu công khai đủ speaker trẻ — có (OCSC/JIBO, research); (3) restricted request được
> — có (LDC/TalkBank); (4) SIAK clear được — chưa, cần counsel; (5) MyST dùng được — có, trả phí;
> (6) PERCEPT-R làm benchmark /r/ — có, non-commercial; (7) thu thập Việt-L1 — có thể, protocol sẵn;
> (8) pilot nhỏ trả lời encoder hypothesis — có; (9) thí nghiệm nhỏ nhất — pilot 8–10 speakers;
> (10) bằng chứng nào đáng chi 10–30h — pilot dương tính + license rõ.

| # | question | answer | evidence |
|---|---|---|---|
| 1 | Can existing local data provide enough human labels? | **YES** | 276-candidate pack from 609 local tokens / 49 speakers; pipeline validated |
| 2 | Can existing public data provide enough child speakers? | **YES (research)** | OCSC 303 (4–9), JIBO 110 (4–7), AusKidTalk 620 (3–12), CAPIL 30 (5–6) |
| 3 | Can restricted data be legitimately requested? | **YES** | LDC agreements (CSLU/CMU), TalkBank account (OCSC/CAPIL/Providence), PhonBank (PERCEPT) |
| 4 | Can SIAK be legally cleared? | **NOT YET** | CC-BY-ND derivative question open; counsel list prepared |
| 5 | Can MyST be used meaningfully? | **YES, with paid license** | LDC2021S05: 470 h, lexicon, train/dev/test; commercial contact verified |
| 6 | Can PERCEPT-R provide a research-only diagnostic benchmark? | **YES** | 280 speakers, rhotic/derhotic labels, non-commercial |
| 7 | Can a local Vietnamese-L1 study solve the domain gap? | **YES, long-term** | collection protocol designed; ethics/partnership required |
| 8 | Can a smaller B2 pilot answer the encoder hypothesis? | **YES** | B2_PILOT_DESIGN.md: 8–10 speakers, 2–4 h, 60P+60A labels |
| 9 | What is the smallest defensible experiment? | **Local pilot + label round** | no licensing, no GPU, speaker-disjoint, FRR-first |
| 10 | What evidence would justify a 10–30 h corpus spend? | **Pilot success** | FRR-first pass + missing-evidence improvement on the pilot test split + a signed license route |

**Conclusion:** the project is **not at a data dead end**. It is at a *label-execution* point: the
cheapest unblock (human review) has not yet happened, and the pilot is designed and unblocked.
