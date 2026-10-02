# SpeechResearchRegistry — Phase 1.1

Status vocabulary ONLY: RESEARCH_PENDING / TESTED / RETAINED / HOLD / BLOCKED / RESEARCH_ONLY.
No BEST/WORST/WINNER/LOSER. One real advantage retains the candidate.

## Baseline (measured 2026-10-02, ASUS, repo HEAD cc4a259)

- Capture: PC `Microphone.Start(16kHz)` slice 0.1s + energy VAD 0.004
  (`UnityMicrophoneCapture`); phone `getUserMedia→16k PCM16→WSS→bridge→TCP 127.0.0.1:8451`
  (`NetworkMicrophoneCapture`, cap 10s, foreign-serial drop). Both yield `CapturedSpeech`.
- Pipeline: `SpeechRecognizer.StartAttemptAsync` (15s timeout, stale-gen guard) →
  provider → `SpeakingPassPolicy.Decide` → `SpeakingExerciseRunner` → `WordSpokenEvent`.
- Recognition: NO real transcript engine shipped. `LocalAcousticProvider` (offline VAD +
  DTW-vs-synth acoustic, `Transcript=""`) is the only honest production provider.
  Azure STT/assessment = `not-configured` stubs. `FakeSpeechProvider` = scripted mock.
- Matching: `LexicalMatcher` (tokenize + Levenshtein whole-word) + thresholds
  PassLexical 0.80 / Strong 0.95+conf 0.75 / PartialFloor 0.40; acoustic Pass 0.60.
- Phonemes: 6 vocab words with V1 data (ball/apple/red/one/please/teddy),
  ARPAbet table, DTW calibrated on SYNTHETIC fixtures — child validity NOT PROVEN.
- External network for voice: TTS only —
  `https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/` (no auth). No cloud STT.
- Wiring gap (proven by grep): recognizer/captures/transports are `new`-ed only in
  CT-P12/P13/P14/P15 tests + fakes; production game path not wired
  (`SpeechMic is future recognizer input`).

## Candidates (measured 2026-10-02, ASUS CPU-only, corpus 27 WAV 16k mono)

| ID | Candidate | Role | Ver | License | Words 6-file | T/file | Status | Unique capability |
|----|-----------|------|-----|---------|--------------|--------|--------|-------------------|
| 01 | whisper | ASR baseline | tiny 20250625 | CLEAR | 6/6 | 0.4s | RETAINED | fp32 accuracy anchor |
| 02 | whisper.cpp | native runtime | tiny ggml | CLEAR | 6/6, silence->[BLANK_AUDIO] | 0.33s | RETAINED | native CPU, no torch |
| 03 | faster-whisper | fast inference | tiny int8 | REVIEW | 6/6 + conf 1.00 | 0.28s | RETAINED | confidence + speed |
| 04 | sherpa-onnx | local runtime | tiny.en int8 | REVIEW | words ok, repeats on silence | 0.1s | RETAINED | fastest; multi-model box |
| 05 | vosk-api | streaming ASR | small-en-0.15 | CLEAR* | 5.5/6 (red->read) | 0.4s | RETAINED | streaming + 40MB |
| 06 | silero-vad | VAD | 6.2.3 | REVIEW | seg 6/6, noise/tone empty | 0.02s | RETAINED | robust VAD gate |
| 07 | py-webrtcvad | VAD baseline | fork 2.0.14 | CLEAR | speech ok; noise/tone 100% FP | 0ms | RETAINED | zero-cost baseline |
| 08 | whisperX | alignment | 3.8.6 | REVIEW | words + timestamps (Red 0.15-0.32) | 1.7s | RETAINED | word timing |
| 09 | MFA | align tool | 3.4.2 | REVIEW | BLOCKED (_kalpy) | - | HOLD | offline phone bounds |
| 10 | allophant | phoneme rec | 1.0.0 | RESTRICTED | BLOCKED (Rust build) | - | HOLD | unseen inventory design |
| 11 | pron-bench | scoring method | scripts | REVIEW | features+embeddings run | 0.1s | RETAINED | scorer recipe |
| 12 | speechbrain | toolkit | 1.1.1 w2v2-en | CLEAR | words ok, silence garbage | 0.5s | RETAINED | recipe breadth |
| 13 | espnet | toolkit | conformer5 | CLEAR | 6/6 PERFECT | 0.35s | RETAINED | accuracy + ARPAbet G2P |
| 14 | nemo | toolkit | parakeet-tdt-0.6b | CLEAR | 6/6 + hyp scores | 0.3s | RETAINED | sequence confidence |
| 15 | kaldi | primitives | sparse src | REVIEW | kaldialign sub=1 proven | - | HOLD | algorithm source |
| 16 | phonemizer | text->phone | 3.4.0+espeak1.52 | RESTRICTED | IPA all 7 probes | - | RESEARCH_ONLY | design-time G2P |
| 17 | moonshine | small STT | tiny-int8 27M | CLEAR | 6/6 PERFECT, Red exact | 0.03s | RETAINED | 10x speed, smallest |
| 18 | kid-whisper | child tiny | gated | REVIEW | HOLD (gated DL) | - | HOLD | MyST WER 15.9% publ. |
| 19 | openpronounce | pron score | 0.3.0 | CLEAR | 98.86 vs 6.79 discrim. | - | RETAINED | 0-100 + phones + F0 |
| 20 | cupe-2i | phoneme rec | english 30M | RESTRICTED | phonemes correct | 3s | RESEARCH_ONLY | contextless frames |
| 21 | babar | toddler phon | paper | RESTRICTED | NOT RUN | - | HOLD | key-child concept |
| 22 | if-mdd | MDD | paper | BLOCKED | NOT RUN | - | HOLD | F1 57.52 target |
| 23 | mdd-quang | MDD arch | AGPL | BLOCKED | no weights | - | HOLD | cross-attn design |
| 24 | mdd-grad | MDD+Lev | no LICENSE | BLOCKED | no weights | - | HOLD | sub/del/ins spec |
| 25 | modelz-mdd | CTC GOP | no LICENSE | BLOCKED | host dead | - | HOLD | E2E GOP JSON spec |
| 26 | gop-ft | GOP-FT | research-only | BLOCKED | no PyKaldi Win | - | HOLD | LayO/BCE design |
| 27 | gop-pykaldi | classic GOP | research-only | BLOCKED | no PyKaldi Win | - | HOLD | baseline formula |
| 28 | gop-improved | GOP+trans | no LICENSE | PARTIAL | sample OK | - | RETAINED | formula verified |
| 29 | kid-align | child MFA | no LICENSE | BLOCKED | no MFA v1 bin | - | HOLD | child AM concept |
| 30 | ogi-kids-phon | child CRDNN | no LICENSE | BLOCKED | no ckpt | - | HOLD | 39-phone inventory |
| 31 | kids-corpora | OGI+CMU LDC | LDC agreements | HOLD | not downloaded | - | HOLD | validation sources |
| 32 | w2v2-phoneme | phone CTC | Apache | RUN | red ɹɛd conf.91 | 0.4s | RETAINED | IPA+frame conf |
| 33 | parselmouth | F0/F1/F2 | GPL-ish Praat | RUN | 440Hz PASS | - | RETAINED | acoustic evidence |
| 34 | cmudict | ARPAbet lex | BSD | RUN | full LWE vocab | <1ms | RETAINED | SpeakingTarget |
| 35 | elevenlabs | ref TTS only | SaaS | NOT RUN | no API key | - | HOLD | cache-once ref |
| 36 | score-conf-v1 | score+conf | composed | RUN | red 91.1/0.91; wrong 1.8 | 0.4s | RETAINED | SCORE≠CONF proven |

License status values: CLEAR / REVIEW_REQUIRED->REVIEW / RESTRICTED / RESEARCH_ONLY / BLOCKED.
Full per-candidate evidence: `candidates/<ID>/candidate_report.md` (+ `licenses.md` where present).
Phase 1.1.2 final: `PHASE_1_1_2_FINAL_REPORT.md`.
Principle: child data = validation only; CMUdict = canonical target.
