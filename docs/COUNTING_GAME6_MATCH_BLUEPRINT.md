# COUNTING GARDEN — GAMEPLAY #6: "GHÉP ĐÚNG CẶP" (MATCH THE PAIR) — BLUEPRINT

> **REJECTED (product decision 2026-09-29):** this gameplay was removed from the
> product (scenes, areas, games, builders, drivers, tests, Build Settings, SFX
> wiring). Counting Garden keeps only Rabbit Feeding (#3) + Number Stairs (#2);
> the Math Hub gate of this world remains a LANDMARK SKELETON. Kept as a
> historical round record only.

Phase: S3-P2Z17 (2026-09-25, maynode). The FINAL game of the Counting Garden
chain. Reference: gameplay #1-#5 + the two harvest beds (#6/#7 internal) —
reused as QUALITY/ARCHITECTURE/PRESENTATION reference, never copied as a
gameplay. Full vertical slice: Math Hub gate → transition → independent
Micro-World → arena → demo → player gameplay → completion → reward → exit →
return → re-entry.

## 1. Experience

NHÌN VẬT MẪU → HIỂU "GIỐNG NHAU" → ĐI TÌM → NHẶT → MANG VỀ → ĐẶT CẠNH MẪU →
GHÉP THÀNH CẶP → ĐƯỢC XÁC NHẬN → CẶP TIẾP THEO.

Pattern: **LOOK → IDENTIFY → SEARCH → CARRY → PAIR**. World-based matching:
objects really exist, the child really walks/picks/carries/places. NEVER a
memory-card UI, never click-A-then-click-B.

## 2. Gate (Math Hub) — finished, not redesigned

- The reviewed `match_meadow` gate ("Đồng Ghép Cặp", two matching halves,
  aqua) gains a walk-in portal (whole-arch coverage 0.7m hub-side, fireRadius
  1.8, cold-start latch) + the shared `MicroGateHint` approach glow.
- No hub redesign, no other gate touched.

## 3. Micro-World: MATCH MEADOW (independent scene)

- `MatchMeadowScene` (lazy, `WorldTransition.EnterMicroAsync` only, one micro
  slot) at island +600x. `MatchArea` (scene-local module in MathScene) owns the
  travel beats, the `ActivityLifecycle("match_pairs")` and the pair ladder.
- Layout (child scale): ENTRY (0,-3) → ORIENTATION board "N" (0,7.6) + teacher
  → PAIRING MAT (z≈4.5) with one REFERENCE pedestal + one pair slot per pair →
  SEARCH FIELD (z≈0.5) with six spaced candidate spots → reward arch + bloom →
  EXIT (0,-11.5).
- Spatial hierarchy (brief §16): clean reference area, uncluttered search
  field, obvious pairing mat; no decoration on any sightline.

## 4. Rounds (one meadow, 1..3 pairs)

| pairs | family | reference colours | distractor |
|---|---|---|---|
| 1 | balls | blue | green |
| 2 | flowers | blue, red | yellow |
| 3 | blocks | blue, red, yellow | green |

- Candidates = pairs + one distractor (never crowded); identity = family +
  colour (no sub-pixel differences, brief §18).
- Ladder 1 → 2 → 3 → 1 (`-match-pairs N` CLI); advance-on-leave, mid-round
  re-entry keeps the count, completed re-entry stages the next round.

## 5. Demo (real, in-arena, two NPCs)

- Teacher at the board: "Look at the board!" → "This is number one." →
  "Find the same!" pointing at the reference.
- Student: looks at the reference → walks the field → picks the matching object
  (it rides his real fist) → carries it to the pairing pad → places it beside
  the reference (real states, real landing) → teacher confirms
  ("Two blue balls!") → the demo pair tidies home → handoff ("Now it's your
  turn!").
- Demo and player use the SAME components/calls; a wrong object in the demo
  would never complete (brief §14).

## 6. Player gameplay + feedback

- Pick any candidate (walk → bend → carry in the fist) → place on the pairing
  pad (click or stop beside it). The pair lands, THEN the teacher praises and
  the pair counts (brief §20).
- Wrong object: gentle line ("Not that one. Find the same!" /
  "Chưa đúng rồi. Tìm lại nhé!"), the object walks itself home, no fail, no
  reset (brief §10).
- All pairs matched: "You found all the pairs!" + celebration + reward bloom +
  result board (N + tick) + lifecycle Completed + area notification.
- Exit portal always live; completion never auto-returns (brief §23).

## 7. Camera (brief §15)

| shot | framing |
|---|---|
| teaching | board + teacher + student + reference row |
| demo/search | student + candidate field, references behind |
| success | the pairing mat with all pairs standing |
| follow | behind the child; reference row + field in view |

## 8. Architecture + tests

- Reuses: WorldTransition micro slot, SmartCamera beats, MarketHUD tunnel,
  DemoJuice, MicroWorldPortal + IMicroWorldArea, ActivityLifecycle,
  LessonActors + PacedVoice, CharacterPresentation, DialogueLang, digit/check
  builders, `pickup`/`give`/`success` SFX. No manager/singleton/service, no new
  package, no Core change.
- New local files: `MatchArea`, `MatchMeadowBuilder`, `MatchGame`
  (+`MatchItem`/`MatchPadZone`), the scene shell. Shared generalization: the
  Build Yard's gate hint became the reusable `MicroGateHint`.
- CT-P59 ×12: gate portal + hint, arena structure, round composition, full flow
  at 1 pair, flow at 3 pairs, gentle wrong match, spam safety, re-entry adopt,
  lazy contract + Build Settings, ladder + CLI, line safety at 3 pairs,
  carry-on-fist.
- Verification (maynode): EditMode **706/701/0/5**; production + journey build
  **Succeeded errors=0** (12 scenes). Standalone journey + evidence: see
  `COUNTING_GARDEN_REFERENCE_REVIEW.md`.
