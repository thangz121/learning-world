# REMAINING_GAPS — problems still unsolved after external audit

1. **Child pronunciation scoring with human-parity calibration.**
   No inspected system provides a child-specific, human-calibrated scorer that we can adopt.
   SIAK: 0.59–0.61 correlation, single annotator, no released model. OpenPronounce: failed on
   our human-correct child token. Commercial APIs: adult-oriented, cloud-only.
   → LWE must keep building/validating child scoring itself (with SIAK data as calibration).

2. **Fidelity for child speakers.**
   Speechace's taxonomy is adult-designed; child silence/partial/noise behavior is untested.
   SIAK's rejected class exists only as training labels. → research needed.

3. **Window policy (our 1.9.11/1.9.12 open question).**
   No external system exposed window/padding policy evidence; their EOS/attempt layers make the
   question less central. → keep our decision C/B, revisit only with perceptual data.

4. **Vietnamese-L1 child phonology.**
   SIAK is Finnish-L1 (+UK native). Our LWE learners are Vietnamese-L1. No external data
   covers this population.

5. **Local + free + child-usable end-to-end.**
   OpenPronounce/speak-better/slip are local but adult-trained; commercial APIs are good but
   cloud/paid. No drop-in local child-safe system exists.

6. **Deletion-aware evidence in OUR pipeline.**
   The external fix (GOP/GOP-AF/deletion states) is understood but not implemented in LWE.
