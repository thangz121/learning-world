# GAME_DESIGN.md — Game Design Bible (v6 audio-first)

> Luật sắt cho 4 agent. Audio voice/priority/focus chi tiết xem AUDIO_DESIGN.md. Scope 3D: CONSTRAINED_3D.md (v6.4, TRUE_3D.md đã superseded).

> Luật sắt cho 3 agent. Nếu conflict, file này thắng. Mọi “child-friendly” phải hiểu giống nhau.

## 1. Child Interaction Rules (4 tuổi)

* 1 mục tiêu tại 1 thời điểm. Không quest log dài.
* Tối đa 3 lựa chọn trên màn hình cùng lúc.
* Tối đa 1 nguồn audio nói cùng lúc. Cấm 2 NPC nói đè.
* Icon cực lớn (≥120px), voice instruction luôn kèm icon + animation dẫn đường.
* Control: **Click-to-Move + Smart Camera** là default. Fallback ←→↑↓. Cấm WASD + free mouse camera.
* Inactivity → Hint Escalation (§5), không để trẻ đứng yên quá 8s mà không giúp.
* Quest instruction diegetic, không panel text: Milo nói + icon bay + mũi tên môi trường + vật phát sáng. Cấm Quest Panel/Objective#/XP cho trẻ.
* Audio là Tier 1: 1 voice tại 1 thời điểm; pronunciation (P1) luôn thắng music/ambient (ducking); xem AUDIO_DESIGN §6.

## 2. NPC Language Rules (v5 — hết conflict ≤6 vs ≤8)

* General NPC dialogue (Mia, Mom, Boy...): **≤6 từ**. Ví dụ: “Apple please!”, “Help me please!”, “Here you are!”
* Milo encouragement / fixed feedback: **≤8 từ**. Ví dụ: “Great! Let's try together!” (7), “Almost! Listen again!” Validator đếm từ trước TTS, vượt = reject.
* Level 1 vocab only, cấm idiom/sarcasm/trừu tượng. Luôn kèm visual+object+action. TTS chậm từ mới: “Aaa...pple.”

## 3. Camera Rules (ưu tiên)

1. NPC speaking → camera Interaction (zoom nhẹ vào mặt NPC + object).
2. Object learning → object priority (frame vật + NPC tay cầm).
3. Child exploring → follow mode.
* Cấm cho trẻ tự xoay camera lung tung. Camera tự framing khi phải tìm đồ.

## 4. Reward & Failure Rules

* Never reward only with currency. Luôn ưu tiên world change: friendship +10, chậu hoa mọc, pet lớn, sticker, khu mới hé mở.
* Never say “Wrong.” / “Score 62/100.” Chỉ dùng: Perfect / Great / Almost / Let's try together / Let's listen again.
* Never remove progress, never punishment loop. Sai → gợi ý dễ hơn, không trừ điểm.
* Social-emotional: dạy Please / Thank you / Sorry / Are you okay? qua tình huống (Mia buồn → hug / bring flower / hỏi thăm), không trắc nghiệm khô.

## 5. Hint / Frustration Escalation System — timer freeze v4 (không tự suy diễn)

Bắt buộc implement ở QuestManager + Milo (HintService Session + QuestHintState per-quest, Reset mỗi quest mới):

```
Idle total (tính từ action đúng cuối cùng, reset khi trẻ làm đúng):
0–7.9s    → No hint
8.0s      → L1 visual hint (kệ/vật phát sáng + icon nhấp nháy)
15.0s     → L2 Milo point (chạy tới + camera dẫn + "Come with me!")
15s+ vẫn stuck / im lặng / nói TV / linh tinh → L3 Milo demo 1 bước mẫu
Vẫn stuck → L4 auto-simplify (còn 2 lựa chọn: 1 đúng + 1 sai, hoặc auto-complete 1 sub-step)

Wrong count (per-quest, reset khi qua level? giữ cộng dồn trong quest):
3  → L1
5  → L2
7  → L3
≥9 → L4
```

Mọi quest phải có `hint_levels` + `simplify_path` trong JSON. Không quest nào được kẹt cứng. L2/L3 dùng total inactivity (8.0s/15.0s total), không phải 8s rồi +15s nữa.

Ví dụ Q1 Help Mia:
* L1: Mia chỉ vào kệ táo phát sáng
* L2: Milo chạy tới kệ, camera follow
* L3: Milo nhặt 1 quả mẫu, đưa cho trẻ bắt chước
* L4: chỉ còn 2 quả trên kệ (1 táo đỏ đúng + 1 bóng sai)

## 6. Quest Template (2–5 phút)

```json
{
  "id": "market_help_mia",
  "npc": "shopkeeper_mia",
  "one_objective_at_a_time": true,
  "objectives": [
    {"id":"find_apple","action":"find","target":"apple"},
    {"id":"bring_apple","action":"bring","target":"apple"},
    {"id":"say_apple","action":"speak","target":"apple"}
  ],
  "hint_levels": ["visual_glow","milo_point","milo_demo","auto_simplify"],
  "reward": {"friendship_mia": 10, "world_change": "flower_pot"}
}
```

## 7. Content Slice — Active 15 vs Passive 35 (v5: JSON là source of truth)

Source of truth là `Content/vocab/*.json` (`"active": true/false`), Markdown chỉ mô tả. `python tools/validate_content.py` in Active/Passive/Total và fail nếu sai count. Không sửa count tay trong MD.

Slice đầu KHÔNG học 50 từ cùng lúc (loãng, khó đo). Chia:

### Active Vocabulary (16 từ — học thật, đo recall, lên MasteryFSM)
*apple, banana, milk, bread, basket, bag, red, blue, one, two, find, bring, help, please, thank you*
+ *ball* (Phase 2A: quest target mới, progression order + prerequisites xem `docs/HANDOFF/PHASE_2C_VOCABULARY_PROGRESSION.md`)

Mỗi từ Active phải có đủ: prefab interactable + audio + quest objective + speech assessment + NextReview. Playtest hỏi: “Bé có nhớ 15 từ này không?” (recognition + transfer không visual prompt), không phải “Bé đã chạm vào bao nhiêu vật?”

### Passive World Vocabulary (35 từ — có trong world cho sống động, không phải learning objective)
*egg rice fish chicken cookie cake candy orange grape watermelon carrot potato tomato cheese yogurt juice cart box bottle cup plate spoon money teddy open close sit stand eat sleep thank sorry are you okay? yellow green*

Passive chỉ cần visual + label khi chạm, không bắt buộc speech, không tính vào KPI Go/No-Go. Khi scale lên 1000 từ, Active mới được promote từ Passive qua Content JSON (`"active": true/false`).

## 8. Graphics & Audio

* Pixar-like stylized: màu dịu, khối rõ, facial exaggerated, animation rõ ràng. Không photorealistic.
* Audio: 1 voice / lần, hiệu ứng vui, không ồn.
