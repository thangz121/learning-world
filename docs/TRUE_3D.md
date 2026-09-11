# TRUE_3D.md — SUPERSEDED by CONSTRAINED_3D.md (v6.4)

> File này không còn hiệu lực. Scope full-free (§2 camera 360, §3 unrestricted, camera collision) bị loại ở v6.4 để giảm complexity. Source of truth hiện tại: `CONSTRAINED_3D.md`. Giữ file để tham khảo lịch sử.

> Game 3D thực sự, không phải 2D/2.5D render bằng Unity 3D. 3D không chỉ là định dạng asset — 3D phải được cảm nhận trong movement, camera, interaction, NPC behavior, environment depth và spatial audio. The game must feel like the child is inside a real explorable three-dimensional world.

## 1. Core requirement

Player phải có cảm giác tồn tại trong thế giới 3 chiều có chiều sâu thực sự. World bắt buộc có: X/Y/Z spatial coordinates, 3D collision, 3D navigation, 3D NPC movement, 3D objects, chiều sâu không gian thực, camera perspective, occlusion, distance, scale, spatial relationships, player movement trong world.

Cấm thiết kế gameplay chính kiểu: side-scroller, fixed 2D plane, billboard world, static diorama, point-and-click scene disguised as 3D, 2.5D gameplay chỉ có background 3D.

## 2. Camera requirement

Perspective camera, smooth movement, follow player/NPC theo context, depth-aware framing, collision/obstruction handling. Không khóa toàn bộ trải nghiệm vào camera cố định. Cinematic moment có script được phép, gameplay chính không bị khóa góc nhìn phẳng.

## 3. Player movement requirement

Di chuyển thực sự: tiến/lùi, trái/phải, xoay hướng, đi quanh object, tiếp cận NPC từ nhiều góc, đổi khoảng cách với object/NPC. Không giới hạn trên đường ray/mặt phẳng 2D trừ cinematic có chủ đích.

## 4. NPC requirement

Mỗi NPC: 3D model, skeleton/rig, animation, navigation, orientation, spatial position, interaction distance, animation state, look-at/attention khi phù hợp. Cấm: sprite billboard, static mesh không animation, pre-rendered video, object đứng yên đổi ảnh phẳng.

## 5. Environment requirement

Player đi vòng quanh object, quan sát nhiều góc, cảm nhận foreground/midground/background, khoảng cách, occlusion, scale variation, chiều cao + chiều sâu. Supermarket: đi giữa aisle, tiếp cận shelf, nhìn object góc khác, quay lại nhìn NPC, đi quanh display stand — không chỉ click hotspot trên background.

## 6. Interaction requirement

Tương tác với 3D objects trong world (approach → nhìn → tương tác → object animation/physics/response). Cấm core interaction kiểu click hotspot → popup UI → lesson. World là một phần gameplay/learning context, không chỉ decoration.

## 7. Spatial audio integration

NPC/object audio hỗ trợ world position, listener distance, attenuation, directional perception. Pronunciation core ưu tiên clarity (focused audio). Ambient/NPC/world audio góp phần tạo cảm giác 3D thực. (Chi tiết: AUDIO_DESIGN.md.)

## 8. Visual acceptance test — True 3D Test

Slice không pass nếu chỉ có "3D assets". Người chơi phải có thể: (1) đi quanh NPC, (2) đi quanh object, (3) đổi camera angle, (4) thấy foreground/background depth, (5) thấy occlusion theo position, (6) thấy NPC di chuyển trong không gian, (7) tương tác object trong world (không chỉ UI hotspot), (8) nghe world/NPC audio theo spatial position. Nếu flatten thành 2D mà chơi y hệt → FAIL.

## 9. Architecture rule

* Agent A: world topology, true 3D scene composition, camera, lighting, depth, NPC visual embodiment, animation, spatial layout.
* Agent B: player navigation, 3D interaction, NPC movement/gameplay spatial behavior.
* Agent D: spatial audio integration.
* Không agent nào biến world thành 2.5D để giảm implementation complexity.

## 10. Product invariant

> 3D không chỉ là định dạng asset. 3D phải được cảm nhận trong movement, camera, interaction, NPC behavior, environment depth và spatial audio.
