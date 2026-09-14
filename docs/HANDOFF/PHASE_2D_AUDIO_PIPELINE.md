# Phase 2D Audio Pipeline

## Status

PASS — mapping → binary → resolution → playback-request is now a proven,
validated, shippable pipeline for all runtime-consumed audio. No frozen
system modified. One honest boundary held throughout (§30 six levels):
AUDIBLE-by-ear and GOOD QUALITY are NOT PROVEN (no listening apparatus in
this environment); everything below that line is proven with evidence.

## Scope

Audio pipeline + asset delivery only. No speech recognition, no scoring,
no dialogue rewrite, no Ball staging, no Stager, no NPC/camera work, no
bulk content (4 new binaries only: the exact 2D gap), no QuestManager /
LearningService changes (none needed — verified, not assumed).

## Baseline

- Phase 1 LOCKED; 2A (75/75) / 2B (PASS WITH OPEN ITEMS) / 2C (86/86 PASS).
- Git was CLEAN at start (57892b7). Prior state: 8 shipped binaries,
  mapping≠binary for ball + 2 dialogue lines, silent-fallback known issue.

## Audio Inventory (re-read live)

- VOCABULARY: 16 actives mapped normal+slow. Binaries shipped: apple,
  ball (2D). Mapping-only: 14 actives × 2 (validator lists each; --ship
  fails on them — correctly).
- DIALOGUE: 38 manifest lines mapped. Binaries shipped: 6 Milo lines
  (greet/instruct_find/instruct_bring/praise_found/celebrate/encourage) +
  inst_07 + ok_05 (2D). 30 lines mapping-only (no runtime consumer yet).
- EXISTING BINARY: 8 (9/11 Worker TTS). 2D ADDED 4 (ball_normal 6336B,
  ball_slow 7104B, inst_07 10176B, ok_05 11904B). Total 12/12.
- No duplicates, no orphans (either direction), no missing-in-manifest —
  all three enforced by validator + CT-P08F.
- Reproducibility: re-fetching the 8 old files reproduced byte-identical
  output for apple_slow + milo_praise_found and same-size output for the
  rest — source pipeline deterministic.

## Canonical Audio Identity

TEXT-based cache key: `SHA256(text|voice|lang|rate|pitch|style|format)`
(AudioCache.CacheKey; priority excluded by design). Vocab text IS the
stable id (lowercased WordId value — "apple", "ball"); dialogue text is
the exact manifest string. Manifest `id` fields are mapping labels, never
key inputs. Convention kept (no enum/struct refactor — §4). Consequence
2F must obey: narration calls MUST reuse exact manifest text + the §2F
call contract below, or keys miss.

## File / Directory Contract

- Ship list: `Content/audio_manifest.json` (top-level list, 12 entries).
- Binaries: `Assets/StreamingAssets/audio/<file>` (plain filename, .mp3).
- Player mirror: same dir `manifest.json` (written by pregen_w1.py;
  PregenSeeder reads the MIRROR, never Content/ — verified by read).
- Vocab JSON `audio.normal/slow` paths (`audio/<x>.mp3`, Content-relative)
  do NOT resolve to files anywhere — metadata labels only. Runtime
  resolution is cache-key, never these paths (MAPPING-ORPHAN documented,
  harmless: CT-008 + P08C use them as labels, nothing opens them).
- Every binary locatable + validatable + mappable + loadable (L2 copy) +
  playable (decode→AudioSource) — each link proven.

## Audio Format Contract (DESIGN DECISION)

MP3 (MPEG Layer III, Worker audio/mpeg, AudioFormat.Mp3_44100) — the only
format in the repo, matching the Worker contract + existing 8 files. All
12 verified MPEG frame-sync (FF Ex). Decoded runtime facts: mono,
24 kHz, 0.84–2.16 s for the proof set. StreamingAssets bypass Unity
import (raw ship + runtime decode via UnityWebRequestMultimedia) — so
there are NO import settings to contract (§14 answered: N/A by design,
not an omission). No quality optimization attempted (no evidence of need;
129 600 bytes total — NO ISSUE OBSERVED, §21).

## Source Audio

Cloudflare Worker Google-Translate TTS source via tools/pregen_w1.py
(reproducible: fetch→verify 200/audio-mpeg/size→write→mirror; exit
nonzero on any failure, nothing faked). Worker probed live 200/audio-mpeg
before minting. No copyrighted/supplied/recorded sources involved.
Normal/slow are distinct syntheses (slow bytes > normal bytes for both
pairs: 7488>6720, 7104>6336 — file-level support for the semantic claim;
pedagogical effectiveness NOT claimed, §9).

## Vocabulary Audio

apple + ball complete (normal 0.85 / slow 0.70, learning_v1, en-US —
runtime PlayVocabularyAsync params match manifest params EXACTLY,
key-equality pinned P08B). 14 actives mapping-only (backlog, listed by
validator). No normal=slow duplicates (sizes differ per pair).

## Dialogue Audio

6 Milo lines complete (params match Milo.Say call sites exactly — text,
milo_v1, en-US, 1.0/1.0, manifest style — P08B pins all six). inst_07 +
ok_05 minted for 2F with the documented 2F CALL CONTRACT: SpeakAsync
with EXACT manifest text, mia_v1, en-US, rate 1.0, pitch 1.0, Clear,
Mp3_44100 — any deviation misses L2 (P08B pins the contract side).
30 further lines mapping-only (no consumer; not minted — §19).

## Pregen Pipeline

Dynamic end-to-end, property preserved: Content manifest (list, grew 8→12
with zero code changes) → pregen_w1.py fetch → StreamingAssets + mirror →
PregenSeeder manifest-driven copy to L2 (no hardcoded ids — verified by
read; seeder keys computed ONLY in C#) → L2 keyed lookup. New vocab/words
flow through BuildPreGenList with no special-casing (P08C full-pack +
P08D synthetic "zebra" proof). Seeder steady-state observed: "Seeded 0"
on the clean-build boot (L2 populated), L2HIT=True on all proof probes.

## Runtime Resolution

Request → CacheKey → L1 memory → L2 disk (seeded) → Worker synthesize →
store → file:// decode → single voice AudioSource. Priority gate
(P0>…>P7), 2s text+voice dedupe, 3-deep FIFO, 5s queue timeout — all
pre-existing, untouched, unit-covered (CT-A02). No mega-service created;
one small additive index only (below).

## Silent Fallback Audit (§12 — the important one)

Missing/undecodable audio path, traced (AudioDirector.cs:267-296):
L2-miss → TTS synthesize → on TtsException → pregen re-check → on total
failure: `Debug.LogWarning("No decodable audio...")` + quest continues +
VocabularyPlayed STILL published (with fromCache flag). Verdict:
- NOT silent-swallow: warnings at every failure point (no-clip, decode
  fail, TTS fail, fallback fail) + observable bus events. Development and
  production share the path (no dual-fallback confusion).
- BUT: no build/production gate previously detected missing assets, and
  VocabularyPlayed fires even when nothing was audible (consumer could
  misread Played as Audible — no consumers exist today; documented for 2F).
- 2D closes the gate half: validator FAILS authoring on any manifest entry
  without a valid binary (missing/empty/corrupt) + mirror mismatch +
  orphans; PregenManifestIndex gives runtime/diagnostics an observable
  lookup (HasBinary false, never throws). Production never marks missing
  audio as success at the CONTENT layer anymore — the remaining Played-
  without-clip nuance is queued behind real consumers (P2, Phase 4).
- With 12/12 manifest binaries shipped + key-equality pinned, the staged
  quest's full audio path (6 Milo lines + apple/ball vocab) has no
  fallback leg left to take while L2 seeds (boot log proves seeding).

## Validator

validate_content.py gained the audio-manifest section (§13): required
keys, unique ids/files, text 1..200, plain filename, .mp3/.wav, voice
pool, en-US freeze, binary exists + non-empty + MPEG sync, mirror-id
equality, orphan binaries fail, actives-without-binaries NOTE in
authoring / FAIL in --ship. Authoring result: PASS (12/12 + 28 honest
NOTEs). --ship: fails (14 actives mapping-only + unapproved flags) —
correctly, that's the remaining-asset backlog made machine-readable.

## Unity Import Settings

N/A by architecture (StreamingAssets ship raw; decode at runtime).
Pinned implicitly: P2DAUDIO DECODED markers prove the runtime decode path
per file. If a future phase moves audio into imported AudioClips, THAT
phase owns the import contract.

## Runtime Audio Proof (§16 — real standalone, real L2 bytes)

Temp driver (-p2daudio), foregrounded player, proof build Succeeded:
- apple_normal: L2HIT True 6720B → DECODED mono 24kHz 0.89s → DONE,
  Played(apple|Normal|True), no fault.
- ball_normal: L2HIT True 6336B → 0.84s → DONE + Played, no fault.
- ball_slow: L2HIT True 7104B → 0.94s (longer than normal — slow real).
- milo_greet: L2HIT True 16704B → 2.16s → DONE + DialogueRequested.
- Player.log: zero exceptions, zero "No decodable audio", zero decode
  failures. Levels reached: MAPPED → GENERATED → IMPORTED(n/a-raw) →
  RESOLVED → PLAYBACK REQUESTED. AUDIBLE/GOOD QUALITY: NOT PROVEN
  (no listening apparatus; stated, not smuggled).
- Followed by: temp deleted, FINAL clean build Succeeded + boot
  3×FACE_OK + 0 exceptions (survey-free final state, lock protocol).

## Manual Listening Evidence

NONE — no human ear or audio output analysis exists in this environment.
Integrity proxies instead: MPEG sync on all 12, Worker-source success
lines (12/12 200/audio-mpeg), size floor (all > 6KB vs 2KB suspicion
floor), runtime decode to expected sample counts, slow>normal durations.
Honest label: integrity + decodability proven; audibility unproven.

## Tests (CT-P08, 6 tests — matrix §22)

A inventory (12, sync bytes, 2KB floor) · B key-equality manifest≡runtime
(4 vocab + 6 Milo + 2 2F-contract) · C full-pack dynamic collection ·
D synthetic-word no-coupling · E missing observable (null/ghost/
traversal/bad-style) · F duplicates + orphans. Full suite: BEFORE 86/86
→ AFTER 92/92 PASS (86 frozen + 6 new).

## Files Changed

Binaries (shipped content, reproducible via pregen_w1.py):
- ADD StreamingAssets/audio/{ball_normal,ball_slow,inst_07,ok_05}.mp3
  (+ Unity .metas)
- MOD StreamingAssets/audio/manifest.json (mirror 8→12)
- MOD 7 old mp3s (re-fetch bytes; validity re-verified — apple_slow +
  milo_praise_found byte-identical, rest same-size)
- MOD Content/audio_manifest.json (ship list 8→12)
Pipeline/validation (additive + one tool section):
- ADD Assets/D_Audio/PregenManifestIndex.cs (read-only mirror index)
- tools/validate_content.py (audio-manifest section)
Tests: ADD CT-P08_AudioPipeline.cs (+.meta). Docs: this file.
Frozen systems touched: NONE (no Quest/Learning/NPC/camera/audio-core
diff — verified by status: only listed files).

## Known Limitations

- Audibility/quality by ear: NOT PROVEN. No clipping/corruption listening.
- 14 actives + 30 dialogue lines mapping-only (binaries = future 2D+
  batches; validator NOTEs each; --ship fails — by design).
- Vocab JSON audio paths are unresolvable labels (documented orphan).
- VocabularyPlayed-without-clip nuance (P2, Phase 4).
- Threshold drifts from 2C (24h-vs-7d etc.) untouched (P2-7 stands).
- seed_content.py drift untouched (P2-1 stands).

## Deferred Items

- Phase 4: audibility/quality program, Played-vs-Audible consumer
  semantics, retention audio, adaptive audio, bulk binary batches.
- 2E: bubble SetIcon (needs no audio), NPC roster (voice fields exist).
- 2F: entry-driven narration calls (MUST use the §2F call contract),
  provider switch, ball staging + full-quest audio run (Milo lines +
  ball vocab now WILL resolve — proven above).

## Git Evidence

- Baseline CLEAN (57892b7). Commit `phase2d: audio pipeline foundation`
  (binaries + manifest + index + validator + tests + doc). No tag (no
  phase lock claimed). Post-commit CLEAN.

## Final Verdict

A. Mapping→binary pipeline complete? YES for all runtime-consumed audio
   (12/12); mapping-only remainder explicitly inventoried, not hidden.
B. Mappings without real binaries? YES — 14 actives + 30 dialogue lines
   (listed by validator, none runtime-consumed today).
C. Normal/slow pairs? Complete for shipped vocab (apple, ball); file
   sizes support distinct syntheses; pedagogy unclaimed.
D. Playable dialogue? 6 Milo lines + 2 2F-ready Mia lines (keys pinned);
   rest mapping-only.
E. Pregen dynamic? YES — manifest grew 8→12 with zero code changes;
   seeder manifest-driven; collection full-pack (P08C) + synthetic (P08D).
F. Hard-coded apple/ball audio deps? NONE in the pipeline path (params
   flow; zebra proof). (Milo/Mia voice TIMBRE coupling is narration
   content, staged in 2F — not pipeline hardcode.)
G. Missing audio swallowed? NO — warnings at every failure point +
   validator authoring-FAIL + observable index lookup. (Played-without-
   clip nuance documented, consumerless today.)
H. Runtime resolves AudioId→AudioClip? YES — 4/4 probes L2-hit + decoded
   to real sample counts on standalone.
I. Playback proof? YES to PLAYBACK REQUESTED (DONE + bus events, no
   fault); AUDIBLE not machine-provable here.
J. Manual listening? NONE — stated NOT PROVEN.
K. QuestManager changes? NONE. L. LearningService changes? NONE.
M. 2E blocked by audio? NO. N. 2F blocked by audio? NO — staged quest
   audio fully resolves; narration must follow the call contract.
O. Unproven: ear-audibility/quality, 44 mapping-only binaries, Played
   semantics with future consumers, in-quest full-audio run (2F).
P. Deferred to Phase 4: quality program, bulk batches, adaptive/retention
   audio, consumer semantics.
