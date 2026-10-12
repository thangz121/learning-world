# SESSION REPORT - WP-1.9.32

> **Tóm tắt (VI):** Nghiên cứu công cụ MIỄN PHÍ (Kokoro loại vì Python 3.14; chọn **Piper** với
> voice **LibriTTS-R, CC BY 4.0**), xây pipeline audio tự sinh + tự kiểm ASR (16/16), và dựng
> **FOUNDATION game NGHE đầu tiên của môn Tiếng Anh — "Vườn Tai Thính" (`listen_garden`)**:
> scene riêng, luồng nghe→chạm hình, chuông nghe-lại, sai→đọc chậm, LV1→LV4/12 vòng, hoàn thành
> lưu lifecycle; nối đủ LearningMap/GameInstaller/SelectionYard; 9 test mới. Suite **739/734/0/5**,
> build **Succeeded errors=0**. Audio phát OFFLINE 100% (pregen, khoá bằng test). Chưa human/visual QA.

- **DATE:** 2026-10-12
- **MACHINE:** TECHVKITCHEN01 (Unity 6000.6.0f1 tại `D:\Unity\Hub\Editor`)
- **BRANCH:** `main` (= `ux/math-arenas-hotfix-20260930`)
- **BASE COMMIT:** `fcd4725` (WP-1.9.31 external child standard)
- **FINAL COMMIT:** this commit
- **SCOPE:** speech/content tooling + English→Listening game foundation (IMPLEMENTED, not accepted)
- **PRODUCTION FLAGS:** untouched; no training; approved math games untouched.

## Mission

Per the PO directive: use free-only tooling (full permission to download/research/try) and deliver
an operable FOUNDATION of the English *Listening* skill game, ready for the next phase.

## Work performed

1. **Engine research:** Kokoro rejected (pip requires Python <3.13; venv is 3.14.8). Chose **Piper
   1.8.0 (MIT code)** + voice `en_US-libritts_r-medium` (**LibriTTS-R dataset, CC BY 4.0**), CPU.
   Speaker picked by F0 scan (id 100, ~214 Hz); model lives outside the repo.
2. **Self-checking audio pipeline** (`tools/pregen_listen.py`): synthesize N takes per word/mode →
   faster-whisper (`base.en`) picks the first exact match → trim pad + normalize + 24 kHz mono MP3 →
   idempotent manifest update (Content + StreamingAssets mirror) → convergence cache. Findings:
   aggressive silence-trim clips initial stops ("banana" heard "and Anna"); isolated "grape" renders
   an unclear onset in this voice (5/5 takes failed) → the pack teaches **"grapes"** (matches the
   bunch prop) with `id=grape`. Final: **16/16 exact ASR matches**, stable across reruns.
3. **Listening pack:** 16 masters `listen_<word>_{normal,slow}.mp3`, voice `listen_v1`, rates
   0.85/0.70 — pregen keys pinned == runtime keys (offline guaranteed). Validator PASS (pool
   extended with `listen_v1`).
4. **Game foundation "Vườn Tai Thính"** (`listen_garden`, English→Listening): new arena folder
   (builder/game/choice/bell/pure logic), bare-root scene + Build Settings, LearningMap entry
   (`Implemented`), GameInstaller dispatch + build method, SelectionYardArea lifecycle,
   wordless door preview. 8 starter words (PropKit foods + primitive ball), LV1→LV4 (2/3/4/4
   choices; 12 rounds per visit), answer position rotates, wrong→slow replay, bell replays,
   voice-first (no text for the child; guidance follows DialogueLang).
5. **Tests:** CT-S21 A→I (9), CT-S10 updated (6 games + asserts + skip-list), CT-P08A updated
   (mirror 12→28). All green.

## Metrics

| item | value |
|---|---|
| EditMode suite | total 739 · passed **734** · failed **0** · skipped 5 (exit 0) |
| Production build | **Succeeded errors=0** (warnings 190, all pre-existing; 0 from new files) |
| ASR self-check | **16/16** exact (faster-whisper base.en), rerun = cached |
| Audio masters | 16 × ~4.2–7.2 KB, mono 24 kHz MP3, MPEG frame-sync valid |
| Offline path | 16/16 manifest keys == runtime keys (CT-S21F); seeder copies at boot |

## Decisions / gates

- Engine: Piper + LibriTTS-R (free, offline, commercially usable with attribution) — recorded.
- Word shaping: teach "grapes" (plural) for the bunch; `TextEn` mapping pinned by test.
- Status: `Implemented` (NOT human-accepted; no self visual pass — per project rules).
- No paid licenses touched; no new cloud dependency (item audio is local; guidance lines behave
  like every other in-game line).
- Foundation is data-first: extending = edit `Content/listening/listen_garden.json` + rerun
  `tools/pregen_listen.py`.

## Files created (key)

- `Research/Speech/Phase1_9_32/00_DESIGN_LISTENING_GARDEN.md`, `01_TTS_PIPELINE.md`,
  `02_AUDIO_PACK.md`, `03_IMPLEMENTATION.md`, `04_VERIFICATION.md`
- `Assets/A_World/ListeningGarden/` (7 files + scene + metas)
- `Assets/Tests/EditMode/CT-S21_ListeningGarden.cs`
- `Content/listening/listen_garden.json`; `tools/pregen_listen.py`
- 16 masters `Assets/StreamingAssets/audio/listen_*.mp3` (+ metas)

## Files modified

- `LearningMap.cs`, `GameInstaller.cs`, `SelectionYardArea.cs`, `SelectionYardBuilder.cs`,
  `EditorBuildSettings.asset`, `CT-S10`, `CT-P08`, `Content/audio_manifest.json`,
  `StreamingAssets/audio/manifest.json`, `tools/validate_content.py`.
- NOT touched: the two approved games; the 3 pre-existing Unity working-tree edits
  (MicSetupMonitor/Phone watchers) left as-is — outside this WP.

## Git actions

- This commit: stage listening foundation + reports + index update; push to `main` (fast-forward)
  per the standing instruction.
- MP3 masters force-added (repo `.gitignore` blanket `*.mp3`, same as the existing 12 masters).

## Open items / next steps

1. **Human/visual QA** of the listening arena (play it on the node/machine; bell position, pod
   sizes, prompt pacing, praise lines).
2. Journey driver for the English→Listening path (TempUx journeys still walk the math chain only).
3. Content phase 2: more words (e.g., colors via shaped cards, numbers), sentences level (LV5+),
   and possibly a "listen and move" variant — all on the same pack + pipeline.
4. Speech track unaffected: WP-1.9.31 readout remains the current gate for pronunciation
   (`B2_PILOT_PROMISING_DATA_REQUIRED`); this WP is orthogonal and touches no scorer code.
