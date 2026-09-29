# COUNTING GARDEN — GAMEPLAY #6/#7: "HÁI ĐÚNG SỐ DÂU" (ZONE 1) + "BẺ ĐÚNG SỐ NGÔ LÊN XE" (ZONE 3) — BLUEPRINT

Phase: S3-P2Z16 (2026-09-25, maynode). Reference: gameplay #1-#5
(`COUNTING_GAME*_BLUEPRINT.md` / `..._REPORT.md`) — reused as QUALITY/
ARCHITECTURE/PRESENTATION reference, never copied as a gameplay.

## 1. Experience (what the child lives)

- **#6 Vườn Dâu (zone 1):** NHÌN SỐ → NGHE CÔ HỎI → XEM BẠN HÁI MẪU → TRẺ HÁI
  TỪNG QUẢ DÂU (cầm trong tay thật) → BỎ VÀO RỔ → ĐẾM CÙNG CÔ → ĐỦ THÌ ĐƯỢC
  XÁC NHẬN. Pattern: **PICK → CARRY → FILL (basket)**.
- **#7 Vườn Ngô (zone 3):** NHÌN SỐ → NGHE → XEM BẠN BẺ NGÔ MẪU → TRẺ BẺ TỪNG
  BẮP → MANG LÊN XE → XẾP VÀO XE → ĐỦ THÌ XE ĐƯỢC CẮM CỜ. Pattern: **PICK →
  CARRY → LOAD (cart)**.

Cả hai dùng CHUNG kit `HarvestArena` (item/state/game) + 2 builder riêng cho
cây trồng và vật chứa. Một arena phục vụ mọi target 1..9; target chỉ quyết
định số cây cần hái.

## 2. Garden mini lesson = tutorial thật (NPC hỏi – trò đáp)

User order: "mọi khu có game đều phải có demo; demo dùng gameplay giản lược,
coi như hướng dẫn chơi; NPC phải hỏi như game thật."

`HarvestLessonDemo` (chạy tại chính luống dâu/ngô trong Vườn Đếm, scale 0.62):

1. "Look at the board!" → "This is number three."
2. **Cô hỏi:** "How many strawberries?" / "Có mấy quả dâu?" (corn: "How many
   corn cobs?" / "Có mấy bắp ngô?")
3. **Trò đáp:** "Three!" / "Ba ạ!" + gật đầu.
4. Cô giao bài ("Pick three strawberries!" / "Hái ba quả dâu nhé!").
5. Trò diễn LẠI GAMEPLAY THẬT thu nhỏ: đi tới bụi, hái từng quả (bay vào tay),
   mang tới rổ, bỏ vào, cô đếm "One strawberry… Three strawberries!".
6. "Yes! Three strawberries!" + cả hai celebrate → "Now it's your turn!" →
   `PassDone`, panel mở sau 1 lượt xem (audience gate như khu 2/5).

## 3. Arena (real gameplay)

Layout dùng chung (child scale, entry z=-3):

    ENTRY (0,-3) → LISTEN CIRCLE (0,2.6) → ORIENTATION: board "N" (0,7.6)
      → PRODUCE west (-2.4,3.0): berry bush / corn stalks
      → BIN east (2.4,3.0): berry basket / wooden cart + cờ thưởng
      → RESULT (3.4,-0.6) → EXIT (0,-11.5)

- Trẻ vào arena → đi vào vòng nghe → cô đọc đề (board → số → việc cần làm) →
  trẻ tự do hái. Đây là điểm khác #3 (bài học đầy đủ nằm ở demo vườn).
- Hái: cúi nhặt (PickUp clip), cây bay vào nắm tay ĐỘNG (`PlayerVisual.HandBone`);
  mang tới rổ/xe; dừng lại mới đặt (stop-gated), cây bay theo cung vào slot.
- Rổ/xe đầy dần = con số nhìn thấy được (slot 3 ngang × 3 tầng).
- Thiếu: cô nhắc "Còn N quả dâu nữa nhé!" (dwell 2s, cooldown 8s, chỉ khi ở
  gần khu làm việc).
- Thừa: cây thứ N+1 VẪN hạ xuống trước, cô đếm lại 1..N → "Bảng ghi số N." →
  "Đủ N quả dâu rồi." → cây dư bay về bụi → success trở lại. Không bao giờ fail.
- Success: câu xác nhận + recap 1..N + closer "Con hái ba quả dâu!" + result
  board + spotlight/reward (dâu: vòng sáng vàng dưới rổ; ngô: xe hiện cờ đỏ) +
  cả hai NPC celebrate + nhân vật trẻ Victory + lifecycle Completed.
- Exit: cổng luôn mở; rời khu mới lên rung kế tiếp (advance-on-leave như
  #2/#3/#4/#5), re-entry = bài mới cho rung kế; re-entry giữa bài = học lại
  cùng target.

## 4. Target ladder + CLI

- Một arena, mọi target 1..9: `HarvestProgression = {3,5,7,9,1}`.
- CLI chẩn đoán: `-strawberry-target N`, `-corn-target N` (inert mặc định).
- 10 cây mỗi arena (target + 1 dự phòng cho bài sửa thừa).

## 5. Camera-first shots

| shot | purpose | framing |
|---|---|---|
| teaching | cô đọc đề | board + cô + trò + khu hái/rổ trong 1 khung |
| success | payoff | trẻ + rổ/xe đầy + result |
| follow | trẻ làm việc | sau lưng trẻ, nhìn vào khu làm việc |

## 6. GitHub-first research (ADOPT/ADAPT/REJECT)

Kế thừa nguyên kết luận research của #4/#5: ghost + explicit confirm, hand
socket (BossRoom/PositionConstraint pattern), next-step cue
(UnityTutorialSystem NextEventSelector), tick-driven phase machine (reject FSM
package), open-arena camera (reject Cinemachine). Không thêm package nào.

## 7. Reuse vs local

- **REUSED:** CountingGardenArea travel/zone flow, WorldTransition micro slot,
  SmartCamera beats, MarketHUD tunnel/objective, DemoJuice, MicroWorldPortal,
  ActivityLifecycle, LessonActors + PacedVoice, CharacterPresentation,
  DialogueLang, digit/check-mark builders, `pickup`/`basket`/`block`/`success`
  SFX.
- **NEW SHARED:** `HarvestArena.cs` (HarvestKind/HarvestItem/HarvestBinZone/
  HarvestGame), `HarvestPlayBuilder.cs` (abstract arena shell),
  `HarvestLessonDemo.cs` (garden tutorial with NPC Q&A).
- **LOCAL:** `StrawberryPlayBuilder` (bush/basket/reward glow),
  `CornPlayBuilder` (stalks/cart/reward flag).

## 8. Tests → CT-P58 (14 tests)

plot staging + scene/demoGate; both arenas structure; targets 1..9; full flow
strawberry@3; full flow corn@5; undershoot; overshoot correction; spam + hint;
re-entry adopt; lazy scenes + Build Settings; ladder + CLI; line safety @9;
carry-on-fist; garden mini lessons (teacher asks, student answers).

## 9. Verification (maynode, batch)

- EditMode suite: **694 total / 689 passed / 0 failed / 5 skipped**
  (baseline 680 + 14 CT-P58; P50A/G/H re-pinned deliberately for the two new
  staged beds).
- Production + journey build: **Succeeded errors=0** (11 scenes incl.
  StrawberryPlayScene + CornPlayScene).
- Standalone journey of the two new beds: pending user approval (full-journey
  driver now covers zones 1 and 3 + their garden mini lessons).
