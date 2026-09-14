# Phase 2.1 Speech Recognition + Speaking Assessment

## Status

PASS WITH OPEN ITEMS — the speech foundation is real, tested, and honest:
capture → VAD → recognition seam → pronunciation-evidence model → lexical
matching → intelligibility proxy → assessment → quest policy all exist as
production code behind a provider-agnostic abstraction, proven by 155/155
EditMode tests and a real-microphone run. The open items are explicit and
non-blocking for Phase 3 prep: live transcript requires the Azure key/SDK
wiring (documented point, not bundled), so phoneme-level scoring and
human-validated intelligibility remain NOT PROVEN by design (§87).

## Why This Phase Exists

Gameplay must ask "Can you say BALL?" and judge a 4-year-old's attempt on
evidence — attempt vs resemblance vs understandability — without becoming an
exam, and without blaming the child for missing microphones, timeouts, or
noise. Transcript-equals-target auto-PASS was forbidden and is not present
anywhere: transcript is one signal among VAD, energy, confidence,
pronunciation evidence, and policy.

## Baseline

- Phase 1 LOCKED; 2A/2C/2D PASS; 2B PASS WITH OPEN ITEMS; 2E PASS; 2F PASS.
- Git CLEAN at start (`a50e0a7`). EditMode **110/110** at baseline
  (107 from 2F + 3 cursor follow-ups; verified arithmetically: first 2.1 run
  totaled 155 with only the 45 new P12 tests failing, zero baseline failures).
- Inputs consumed: `ISpeechProvider` Router + Azure/Fallback/Mock stubs,
  `SpeechAssessmentPolicy` (PronScore thresholds), `QuestManager.AdvanceOnSpoken`
  (Speak advances on Great+), `WordSpokenEvent` → MarketBootstrap glue,
  `ball.json` (`speech.expectedForms` + `phonetic`), 2D audio contract.

## Requirements

Implemented (§86 checklist): SpeechRecognizer abstraction, mic detection,
graceful no-mic, disconnect/reconnect, capture, recognition seam, no SDK leak,
SpeechAttempt + SpeakingAssessment models, confidence separated from
pronunciation, honest pronunciation evidence, lexical-vs-pronunciation split,
proxy-labeled intelligibility, no false human-intelligibility claim,
configurable pass policy, bounded retry, speaking-only data-driven exercise,
real-mic + real-capture proof, wrong/partial/silence/quiet/timeout/stale
coverage, no raw-audio storage, no committed secrets, learning untouched,
Ball/Apple regression-safe, suite green, docs + limitations.

## Provider Selection

Researched 2026-09-14 (Unity 6, Windows, child English, phoneme need):

| Option | Transcript | Phoneme/pronunciation | Offline | Verdict |
|---|---|---|---|---|
| Azure AI Speech (PronunciationAssessmentConfig, HundredMark, Granularity.Phoneme, miscue, NBest, IPA/SAPI) | yes | YES (accuracy/fluency/completeness/prosody, word+syllable+phoneme) | no (key, cost, latency, child audio leaves device) | CHOSEN pronunciation backend |
| Vosk / Whisper-Unity / Wav2Vec2 / TEN-VAD | yes | NO | yes (model MBs + CPU) | fallback-grade only |
| Windows built-in / System.Speech | n/a in Unity player | NO | — | rejected |

Known Azure model limits (documented, designed around): substitution of
phonetically-close words is not always flagged (assessment scores acoustic
similarity, NOT lexical correctness — hence our separate transcript-vs-target
lexical guard); single-phoneme scores are heuristic (hence downgrade-only use
+ NBest inspection point). Unity quirk recorded: phoneme NAMES require the
recognizer locale pinned to `en-US`.

## Architecture

```
Microphone
    ↓
MicrophoneDeviceService (IMicrophoneDevice: enumerate/select/poll/permission)
    ↓
UnityMicrophoneCapture (ISpeechAudioCapture: 16kHz, silence-aware, no storage)
    ↓
SpeechRecognizer (ISpeechRecognizer: capability gate, attempt ids, timeout,
                   cancel, stale guard, events, dev logging)
    ↓
ISpeechAssessmentProvider → LocalSpeechProvider (VAD-only, honest)
                          → AzureSpeechAssessmentProvider (wiring point, not configured)
                          → FakeSpeechProvider (scripted tests)
    ↓  normalized SpeechRecognitionResult (no SDK types escape)
SpeakingPassPolicy.Decide (pure: lexical + confidence + phoneme evidence)
    ↓
SpeakingAssessment → SpeechAssessedEvent → SpeakingExerciseRunner
    ↓
WordSpokenEvent → (existing frozen glue) → QuestManager.AdvanceOnSpoken + Learning
```

Quest/presenters touch ONLY `ISpeechRecognizer`. Learning sees ONLY
`WordSpokenEvent` levels. Audio-out (2D) and audio-in (2.1) share no code
beyond `WordId` (§80).

## SpeechRecognizer Contract

`Capability` / `CapabilityChanged`; `StartAttemptAsync(WordId, CancellationToken)`
→ `SpeakingAssessment`; `Cancel()`; `SwitchProvider()` (router pattern: consumers
keep the instance); `RefreshCapability()`. Cancellation throws
`OperationCanceledException` (no phantom assessments); stale generations return
dropped results that never publish (§48, pinned P12E).

## Microphone Device Detection

`MicrophoneDeviceService`: `Microphone.devices` enumeration, default selection,
`SelectDevice` pinning, `MicStatus { Unknown, Ready, NoDevice, PermissionDenied,
Error }`. `devices.Length > 0` never equated with usable: failed capture flips
to `Error`. Permission probe seam (Web auth future). Pinned: startup-empty,
multi-device select, capture-failure→Error, denial distinct (P12D).

## Speech Capture

`UnityMicrophoneCapture`: 16 kHz mono default, 8 s max / 3 s post-voice silence
timeout, 100 ms energy polling, cancel/timeout flags, per-window VAD estimate,
buffer destroyed after recognition. Quiet-start tolerant (silence timeout only
counts after first voice, §33). No file, no upload except to the configured
provider (§20/§21).

## Recognition

Normalized `SpeechRecognitionResult`: transcript + recognition confidence +
VAD + durations + energy + provider id + latency + error + pronunciation
evidence. Local provider returns VAD truth with EMPTY transcript (never faked).
Azure provider returns `not-configured` error until SDK+key land (never a fake
score). Fake provider scripts all 8 outcomes incl. timeout/error.

## Pronunciation Assessment

`PronunciationEvidence { HasPhonemeData, Source, Accuracy/Fluency/Completeness,
WordAccuracy, WordErrorType, PhonemeCount }`. Default = NOT AVAILABLE (NaN).
With evidence: accuracy feeds decision + downgrade path. Without: NO code path
treats string distance as pronunciation (§28/§29/§69). Per-word canonical
reference is content-owned (`vocab/*/speech.expectedForms` + `phonetic`);
the speech layer consumes `WordId` only (§30). No accent penalty: matching is
whole-word + similarity floors, no accent model anywhere (§31).

## Lexical Matching

`LexicalMatcher`: whole-word containment = 1.0 ("the ball", "ball please",
repeats all pass, §44); otherwise best-token normalized Levenshtein
("ba" 0.50 / "bal" 0.75 / "bol" 0.50 / "apple"→ball 0.20). Unicode-safe, never
throws (Vietnamese "quả bóng" → 0.25 WrongWord, no semantic claim, §32).

## Intelligibility Proxy

`IntelligibilityScore = 0.5·lexical + 0.3·confidence + 0.2·(pronunciation or
lexical fallback)`; `IntelligibilityIsProxy` is ALWAYS true in 2.1.
HUMAN INTELLIGIBILITY: NOT PROVEN. No foreign-listener claim anywhere (§26/§27).

## Scoring

`OverallScore` = intelligibility proxy (decision-oriented aggregation, 0..1).
Bands: StrongPass (contains + lexical≥0.95 + conf≥0.75), Pass (contains +
lexical≥0.80 + conf≥0.35), Partial (lexical≥0.40 — "ba"/"bal"/"bol" live here),
Unclear (low-confidence garble — never auto-Wrong), WrongWord, TooWeak
(energy<0.005), NoSpeech, PossibleAttempt (speech, no transcript engine).
Phoneme evidence below 0.40 caps a lexical pass at Partial. `StrongPass→Perfect,
Pass→Great, Partial/Possible→Almost, else TryTogether` — Partial never advances
the frozen Speak gate (encourages retry, never fails).

## Pass Policy

`SpeakingPassPolicy`: all thresholds named/configurable/tested (§70), zero
word-specific branches (§72). Defaults encode attempt > perfection (§71).

## Retry Policy

`attemptsAllowed` default 3, hard-bounded 1..5 (§35): attempt loop replays the
canonical vocab prompt (listen-again ladder) then `PartialComplete` /
`NotPassed` (quest stays OPEN — speech failure never fails a quest, §36).

## No Microphone Policy

Option B (defer): capability unavailable → `SkippedNoMic` + child line
"Microphone not connected." + transient `SpeechQuestDeferral` mark; zero
provider calls, zero learning events, zero mastery delta (§13/§14/§57).
Reconnect restores eligibility only — the player still initiates (§58).
Mid-capture disconnect → capture error → `device_error` env assessment,
current attempt cancelled, capability refreshed (§12).

## Microphone Hot-Plug

Poll-based (Unity exposes no event): `Refresh()` diffs device lists, fires
`StatusChanged` + `SpeechCapabilityChangedEvent`. Unplug → NoDevice (speaking
disabled, no soft-lock); replug → Ready (no restart, no progress reset, §15).
Pinned without hardware via injected device lists (P12D).

## Speaking-Only Exercise

`SpeakingExerciseRunner` + `SpeakingExerciseConfig` (JSON, `Content/speaking/
say_ball.json`): target/attempts/thresholds/replay/all child lines are DATA —
`say_red` parses and runs with zero code changes (P12F). Flow: capability →
NPC vocab prompt (canonical 2D audio reused, §54) → your-turn capture →
assess → child feedback → pass/retry. Silence publishes nothing (not an
attempt); env errors publish nothing (not child performance).

## Quest Integration

Runner → `WordSpokenEvent` → existing MarketBootstrap glue →
`QuestManager.AdvanceOnSpoken` (frozen thresholds honored). Proven live-path:
`market_help_mia` find→bring→spoken-apple(Pass) completes; WrongWord leaves
the quest OPEN at the same objective (P12G). `QuestManager`, `LearningService`,
`HintService` diffs: NONE. `Ball`/`Apple` quests untouched (§64/§65).

## Learning Integration

No new mastery system (§37): real attempts publish `WordSpokenEvent`
(levels via `ToSpeechLevel()`); silence/env publish nothing. Attempt-vs-success
split preserved by the frozen service (misses raise totals, never hits).
`ReportSpoken`/`AdvanceOnSpoken` double-report avoided: single bus path only.

## Privacy

Minimum-necessary audio, in-memory only, destroyed post-recognition; no raw
voice stored, logged, or committed (§21/§52/§60). Transcript never shown to the
child (§53). Local path: nothing leaves the machine. Azure path (when keyed):
audio leaves the device — documented here, key via `LWE_AZURE_SPEECH_KEY` env
only, never in repo/build (§61). No identity/biometrics/storage (§59 held).

## Security

No secrets in repo (verified `git status`: code + 1 content JSON + tests +
this doc). Provider fail-closed (`not-configured`, never fake data).

## Performance

No main-thread inference (provider is `Task`-based; capture polls 100 ms
slices). Measured real hardware: mic startup **355 ms**, capture 1.96 s for a
2 s window, local provider **1 ms**. P50/P95 across users: NOT ENOUGH DATA
(single ambient run — stated, not faked, §47/§81). No leaked recordings
(capture `Cancel` + `Microphone.End` on every exit path, §82).

## Automated Tests

CT-P12, 45 tests, all green: matcher matrix (exact/phrase/partial/wrong/
Vietnamese/empty/case) · 11 policy decisions · honesty trio (NaN pron,
phoneme flow, downgrade) · mic lifecycle (5) · recognizer (short-circuit,
events, stale, timeout, cancel, evidence line) · exercise (skip/pass/retry/
partial/data-driven/JSON/messages/deferral) · quest+learning integration (3) ·
provider honesty (4). Full suite: **110 → 155/155**, zero regressions.
`validate_content.py`: PASS (authoring). New `Content/speaking/` dir is outside
validator scope by design (counts untouched).

## Real Microphone Tests

Device: `Microphone Array (Realtek(R) Audio)` — 1 device, `Ready`, poll-stable
across refreshes, capture OK (`-executeMethod P12MicProbe.Run`, temp script,
deleted after). Ambient room: mean energy 0.0259/peak 0.31 → VAD speech,
local provider → `PossibleAttempt/transcript_unavailable`, intellig 0.33
(proxy), pron NOT_AVAILABLE — the honest no-STT verdict on real hardware.
No private recordings kept (buffer destroyed; only scalar stats logged, §67).

## Real Speech Tests

Developer-voice transcript matrix is provider-SIMULATED (scripted transcripts
through the real policy path — labeled as such, §51): ball/the-ball/
ball-please → StrongPass; ba/bal/bol/babababall → Partial; apple/quả-bóng →
WrongWord; silence → NoSpeech; "buh"-low-conf → Unclear; quiet → TooWeak;
timeout/error → environment. Real human speech with live transcript: NOT
PROVEN (no speaker session + no STT key in 2.1 — open item, not a silent gap).
Nothing cherry-picked: the full matrix output is in §Real-World Example.

## Known Limitations

1. Live transcript needs Azure SDK + key (wiring point ready, fail-closed).
2. Phoneme-level scoring: NOT AVAILABLE until (1).
3. Human intelligibility: NOT PROVEN (proxy only; protocol for Phase 4 below).
4. VAD floors tuned on one ambient room (0.026 mean fired speech) — retune
   with real child sessions before production thresholds lock.
5. No speaking UI scene yet (runner exposes `ChildMessage` + capability;
   visual acceptance §79 pending first staged speaking beat).
6. Multi-word targets match on joined form (single-word targets unaffected).
7. Exercise→quest binding for NEW speak quests awaits the stager-era binder
   (proven pattern: `market_help_mia` speak leg).

## Human Intelligibility Limitations

`IntelligibilityScore` is a recognizer-as-listener proxy. Phase 4 protocol
(drafted, not run): ages 4–6, quiet + typical-home noise, same USB mic class,
5 attempts × 6 target words, blind adult-listener transcription vs system
decision; acceptance = proxy/decision agreement ≥ thresholds set THEN (no
numbers invented now, §46).

## Deferred Work

Phase 3: stage first speaking beat (UI + NPC prompt visuals) on this runner;
third-quest stager trigger (2F) unchanged. Phase 4: retention/persistence,
listener study, threshold lock, effectiveness. Never: conversation/LLM,
emotion/identity/biometrics, stored voice, adaptive AI (§59 held).

## Files Changed

ADD (no frozen-file edits, `git status` verified):
- `Assets/_SharedKernel/SpeechFoundation.cs` (domain: MicStatus, decisions,
  evidence structs, matcher, policy, ports)
- `Assets/_SharedKernel/SpeechEvents.cs` (Attempted/Recognized/Assessed/
  CapabilityChanged — no audio payloads)
- `Assets/D_Audio/MicrophoneDeviceService.cs` + `SpeechCapture.cs`
  (Unity mic service + capture + fakes + VAD helpers)
- `Assets/D_Audio/SpeechAssessmentProviders.cs` (local/azure/fake)
- `Assets/D_Audio/SpeechRecognizer.cs` (gameplay service)
- `Assets/B_Brain/SpeakingExercise.cs` (config + runner + deferral)
- `Assets/Tests/EditMode/CT-P12_SpeechFoundation.cs` (45 tests)
- `Content/speaking/say_ball.json` (data-driven exercise)
- This doc. Temp probe created, run, DELETED (zero temp files remain).

## Git Evidence

- Baseline: CLEAN (`a50e0a7`).
- Commit: `phase21: speech recognition + speaking assessment foundation`
  (additive only; no tag — no phase lock claimed). Post-commit: CLEAN.

## Real-World Example

Target: ball. Real hardware, ambient room (no speaker present):

```
device=Microphone Array (Realtek(R) Audio) capture=2.0s speech=Y
heard="<empty>" conf=0.00 lexical=n/a pron=NOT_AVAILABLE
intellig=0.33(proxy) overall=0.33
Decision: PossibleAttempt  Reason: transcript_unavailable
```

Simulated-transcript matrix through the same policy (labeled, not real speech):
ball→StrongPass · the-ball→StrongPass · ball-please→StrongPass · ba→Partial
(0.50) · bal→Partial (0.75) · bol→Partial (0.50) · apple→WrongWord (0.20) ·
quả-bóng→WrongWord (0.25) · silence→NoSpeech · buh-low-conf→Unclear ·
quiet→TooWeak · timeout→env-error (never WrongWord). No numbers invented:
every figure above is observed output.

## Final Questions (A–Z)

A. Mic detected? YES (enumerate+select+Ready, real hardware proven).
B. Hot-plug? YES (poll diff, disconnect→NoDevice, replug→Ready, no restart).
C. No mic at quest? Explain once, skip/defer, no failure, no retry loop.
D. Mic lost mid-speech? Attempt cancelled, `device_error` env verdict, no blame.
E. Recognizer decoupled? YES (3 provider impls behind one interface; SDK leak: none).
F. Transcript vs pronunciation confidence? Separate fields; transcript-only
   backends report pron NaN/NOT_AVAILABLE, never conflated.
G. Attempt judged? YES (VAD+energy → NoSpeech vs TooWeak vs real attempt).
H. Partial judged? YES ("ba"/"bal"/"bol" → Partial, never auto-fail).
I. Phoneme evidence? Model exists; live data NOT AVAILABLE (no SDK/key).
J. Pronunciation score measures? Phoneme-aggregated acoustic closeness — ONLY
   when a phoneme backend provides it; otherwise honestly absent.
K. Intelligibility measures? Recognizer-as-listener PROXY (weighted blend).
L. "Foreigner understands?" NOT PROVEN (proxy only; Phase 4 protocol drafted).
M. Human validation? None in 2.1 (explicit future work).
N. Quiet child? TooWeak (still an attempt) vs silence (NoSpeech); no harsh floor.
O. Vietnamese? No crash; lexically WrongWord with encouraging retry (no
   semantic claim).
P. "the ball"? PASS (contains-target, phrase-aware).
Q. "ba"? PARTIAL (0.50, attempt honored).
R. Wrong word? WrongWord, quest stays open, retry bounded.
S. Silence? NoSpeech, no learning event, "Can you say it?".
T. Provider timeout? Environment error, "Let's try again.", never Wrong.
U. Infinite retry? NO (1..5 bound, default 3).
V. Speech failure → mastery down? NEVER (no decrements exist; env writes nothing).
W. Speech failure → quest fail? NEVER (open + retry, or skip/defer).
X. Speaking-only lesson standalone? YES (runner + JSON, no world deps).
Y. Ball/Apple regression? NONE (untouched; suite 155/155; validator PASS).
Z. Ready for Phase 3 speech content? YES as foundation (runner+policy+seams),
   with live-transcript wiring as the explicit gating item for perfection
   claims — safe to stage prompts/skip-paths, not yet to claim scoring.

## Final Verdict

**PASS WITH OPEN ITEMS**

Open, each with Phase 3 blocking status:
1. Azure SDK + key wiring for live transcript/phonemes — NON-BLOCKING for
   staging speak prompts, skip/defer paths, and attempt-level gameplay;
   BLOCKING only for shipping pronunciation-score claims (correctly gated by
   fail-closed errors until then).
2. Speaking-beat UI (child visuals for mic/listening/feedback) — NON-BLOCKING
   foundation-wise; required in the first staged beat (runner contract ready).
3. VAD-floor retune on real child sessions + listener study — Phase 4 work,
   NON-BLOCKING now (floors are config, not code).
