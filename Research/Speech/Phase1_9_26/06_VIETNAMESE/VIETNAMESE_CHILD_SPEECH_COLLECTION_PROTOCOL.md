# VIETNAMESE CHILD SPEECH COLLECTION PROTOCOL (DESIGN ONLY — NO DATA COLLECTED)

> **Tóm tắt (VI):** Thiết kế corpus tương lai: 30–50 trẻ 4–6 tuổi Việt-L1, 10–30h, từ đơn/câu ngắn,
> oversample phụ âm cuối và /r/, thu 2 micro, consent + assent, ẩn danh, double-review, chia
> speaker-disjoint. WP này KHÔNG thu dữ liệu, KHÔNG liên hệ ai, KHÔNG tạo bản ghi giả.

## 1. Participants

- 30–50 children, ages 4;0–6;11, Vietnamese-L1, typically developing, no known hearing loss.
- Balanced gender; mixed dialects (Northern/Central/Southern) recorded as metadata.
- Parental written consent + child verbal assent; withdrawal any time; no coercion.

## 2. Recording protocol

- Environment: quiet room (<45 dBA noise floor), no other speakers.
- Microphones: primary headset/lapel (16 kHz+) + secondary table mic (recording-condition test).
- Tasks: (a) word imitation (target-phone balanced), (b) sentence repetition, (c) picture naming,
  (d) short free description (optional). 2–3 repetitions per target.
- Session length: 15–20 min per child; total target 10–30 h usable speech.
- Metadata: age, gender, dialect, L1 exposure, session id, device, task id, prompt id.

## 3. Elicitation lists (design)

- **Final-consonant list** (oversampled): /t, d, k, p, n, m, ŋ, s, z, f, v, l, ɹ, θ, ð/ in word-final
  position; 10–15 words per phone; familiar vocabulary for 4–6-year-olds.
- **/r/ list**: word-final and syllable-final /r/ after different vowels (ɑ, ɔ, ɝ, ɪ); 15–20 items;
  include minimal pairs (e.g., "four/fought", "car/caught").
- **Sentence list**: 20 short sentences embedding the same targets.
- **Control list**: 10 words without the target phones.

## 4. Annotation

1. Orthographic transcription of each utterance (double-checked).
2. Phone-level annotation of the target phones by two trained annotators independently
   (PRESENT/ABSENT/UNCERTAIN + confidence), disagreements preserved.
3. Forced alignment (MFA) of all utterances as a separate AUTOMATIC evidence layer — never merged
   with human labels.
4. Quality control: 10% re-annotation; inter-rater kappa reported; a third adjudicator only for
   frozen disagreements.

## 5. Storage / privacy

- Encrypted at rest; pseudonymized IDs; no names; consent forms stored separately.
- Retention policy defined by the ethics approval; deletion on withdrawal.
- No redistribution without explicit consent and a data-sharing agreement.

## 6. Splits and sufficiency

- Speaker-disjoint train/dev/test (e.g., 60/20/20% speakers).
- Minimum viable pilot: 8–10 speakers with high-quality labels (see B2_PILOT_DESIGN.md).
- Full B2: 30–50 speakers, 10–30 h, balanced targets as in `08_B2_DATA/B2_DATA_REQUIREMENTS.csv`.

## 7. Compliance

- Ethics approval required before any recording (institutional IRB/ethics committee).
- This document is a design; no personal data was collected, no recordings exist.
