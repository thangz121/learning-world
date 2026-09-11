# CONSTRAINED_3D.md — 3D Constrained World / 2.5D-style Experience (v6.4, thay TRUE_3D full-free)

> Quyết định v6.4: bỏ yêu cầu free exploration / camera 360 / open-world / camera-collision phức tạp để giảm implementation complexity. Giữ visual quality cao và cảm giác 3D thật. File này thay thế `TRUE_3D.md` làm source of truth (TRUE_3D.md giữ lại để tham khảo, không còn hiệu lực).

## 1. Bắt buộc giữ (non-negotiable)

* 3D environment thật (X/Y/Z, scale, occlusion, foreground/midground/background)
* 3D NPC models + skeleton/rig + animation + orientation + look-at khi phù hợp
* Perspective camera + real lighting + depth + spatial relationships
* Player movement trong vùng gameplay (tiến/lùi, trái/phải, đổi khoảng cách/góc tiếp cận NPC/object)
* Interaction với 3D objects thật (approach → nhìn → tương tác → response), không hotspot→popup UI
* Spatial audio (position, distance attenuation, directional) cho NPC/object/ambient; pronunciation giữ focused clarity

## 2. Không bắt buộc ở Slice

* Free 360-degree camera mọi lúc, full open-world navigation, unrestricted exploration, complex camera collision system
* Camera được phép constrained, cinematic hoặc context-driven (follow / interaction zoom / cinematic theo quest)

## 3. Acceptance test — Constrained 3D Test

Slice pass khi người chơi có thể: (1) di chuyển trong vùng gameplay và đổi góc tiếp cận NPC/object, (2) thấy depth foreground/background + occlusion theo position, (3) thấy NPC di chuyển + animation trong không gian, (4) tương tác object trong world (không chỉ UI hotspot), (5) nghe spatial audio theo position, (6) camera chuyển mượt theo context (follow/interaction/cinematic). FAIL khi: gameplay là hotspot2D trá hình, NPC là billboard/static, camera khóa phẳng toàn bộ, hoặc flatten thành 2D mà chơi y hệt.

## 4. Ownership

* Agent A: topology vùng gameplay, constrained camera, lighting/depth, NPC embodiment/animation, spatial layout.
* Agent B: navigation trong vùng cho phép, 3D interaction, NPC movement/orientation/look-at.
* Agent D: spatial audio integration.
* Không agent nào được hạ world xuống 2D-hotspot để giảm complexity.
