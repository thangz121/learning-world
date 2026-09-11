# PRODUCT.md — Little World English (v6 audio-first)

> Source of truth về sản phẩm. Thêm Product Quality Hierarchy + online-optional rule.

## 0. Product Quality Hierarchy (v6 — freeze)

TIER 1 — NON-NEGOTIABLE: 1) Visual Quality, 2) Audio Quality, 3) Learning Content Quality. Ba pillar này định nghĩa perceived quality.
TIER 2: 4) Gameplay, 5) Story.
FOUNDATION: UX/UI là force multiplier + sàn nhà — không phải "làm sau". UI phải invisible/diegetic cho bé 4 tuổi (icon, mũi tên môi trường, vật phát sáng), không dashboard/panel text. Nếu UX tệ thì Visual/Audio/Lesson 10/10 vẫn thành experience 4/10.
Architecture và agent workflow tồn tại để bảo vệ Tier 1, không bao giờ ưu tiên trên child experience.

## 0b. Platform (v6 — freeze)

Standalone Windows EXE, online-optional. Cài đặt/chạy trực tiếp, không web/không trình duyệt. Core offline (15 Active, lesson, common dialogue pre-gen, world, quest). Online enhancement (Cloud STT, Assessment, Dynamic AI/TTS). Mất mạng không kẹt quest.

> Source of truth về sản phẩm. Mọi quyết định Game Design / Architecture phải trace về file này.

## 1. Vision

Không phải “app học tiếng Anh 3D”.
Mà là: **“A living 3D world where a child naturally needs English to play.”**

Trẻ cảm thấy: “Con đang đi chơi với Milo.”
Không cảm thấy: “Con đang học tiếng Anh.”

## 2. User

* Primary: trẻ 4 tuổi, chạy Windows, chưa đọc rành, nói nhỏ / ngọng / trộn Việt-Anh.
* Secondary: phụ huynh (xem tiến độ, giới hạn giờ, bật/tắt mic).
* Non-user: không quảng cáo, không stranger chat, không public profile.

Ràng buộc y tế: WHO 3–4 tuổi sedentary screen time ≤60 phút/ngày. Thiết kế session **10–15 phút / Adventure**, quest **2–5 phút**.

## 3. Học từ sản phẩm khác

* **Lingokids (Playlearning):** không Lesson 1→2→3. Mà `World → Activity → Interaction → Learning → Reward → New World State`.
* **Khan Kids (Companion):** 1 companion xuyên suốt (Milo). Là bạn, không phải teacher. Nhớ tiến trình, khuyến khích, phản ứng.
* **Minecraft Education (Immersive):** từ vựng gắn vật thể + hành động + ngữ cảnh. Thấy táo → NPC cầm táo nói “Apple!” → nhặt → “What is this?” → nói → NPC phản ứng.

## 4. Hub World Architecture

```
        HOME (Hub)
            |
  SCHOOL-MARKET-FARM-ZOO-PARK
            |
  Airport-Hospital-Beach-Restaurant-Space
```

Mỗi khu là mini world sống động trong vùng gameplay giới hạn (constrained, không GTA, không open-world tự do). Vertical Slice chỉ làm **SUPERMARKET**.

## 5. Core Loop

```
EXPLORE → SEE → NPC INTERACT → UNDERSTAND EN → ACT → SPEAK → WORLD REACTS → REWARD → NEW DISCOVERY
```

Reward = world state change (NPC thành bạn, cây mọc, nhà trang trí, pet lớn, khu mới mở, NPC nhớ việc tốt). Không chỉ coin.

## 6. Scope Vertical Slice — Current Freeze

* 1 map Supermarket 3D, 6 nhân vật: Milo, Shopkeeper Mia, Mom, Boy, Girl, Cleaner
* Source of truth là JSON (`Content/vocab/*.json` với `"active": true/false`), Markdown chỉ mô tả. Validator in: Active 15 / Passive 35 / Total 50. Active học thật: apple banana milk bread basket bag red blue one two find bring help please thank_you.
* 5 quest, 100+ speech interaction, game loop hoàn chỉnh
* Chơi được bằng chuột + Fallback offline khi chưa có mic (Mock chỉ ở test)

## 7. Không làm ở Slice (dời Phase 2)

* Parent Dashboard web đầy đủ → Slice chỉ làm Debug Panel
* Backend cloud / sync đa thiết bị → Slice chỉ local save
* Advanced LLM NPC / personalization sâu → Slice chỉ script + slot memory

## 8. KPI Go/No-Go (Current Freeze — chống farm)

* `Parent Intervention Count / 10-min session`: target ≤2, excellent ≤1
* `Time to first action` <60s, `Quest completion` ≥80%, `Voluntary replay` = trẻ đòi chơi lại
* `Meaningful Speech Attempt` (thay speech attempts thô): chỉ tính khi đủ 3 điều kiện — (1) đúng expected interaction, (2) child-initiated (không phải bấm spam), (3) provider detect được usable voice (confidence/VAD pass). Nói “apple x10” liên tục chỉ tính 1.
* `Learning Transfer`: Apple dạy ở Q1 có visual → Q4 hỏi lại KHÔNG visual prompt (“What fruit is red?”) → bé chọn/nói đúng mới tính Retained. Đây là KPI quan trọng nhất.
* Playtest: 3 bé internal + 5–8 bé beta. 1 bé chỉ smoke test, không quyết định Go.

## 9. Roadmap

* Phase 0 Research, Phase 1 Vertical Slice (8 tuần chi tiết trong plan v2), Phase 2 Core Systems, Phase 3 World Expansion, Phase 4 Smart NPC, Phase 5 Personalization.
