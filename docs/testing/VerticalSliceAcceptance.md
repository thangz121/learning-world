# testing/VerticalSliceAcceptance.md — v6.1 (Part I standalone + Part J pillar criteria)

## I. Standalone Windows product checklist (ship gate)

* [ ] Phát hành dạng Windows 64-bit standalone EXE, launch trực tiếp, không browser/localhost/web server
* [ ] Core game chạy khi Internet disabled: world, gameplay, quest, 15 Active pre-gen, dialogue pack pre-gen
* [ ] Quest không softlock khi Worker/Azure unreachable (fallback + offline intent)
* [ ] First-run không bắt buộc login/tài khoản
* [ ] TTS/STT online chỉ là enhancement qua HTTPS background, fail êm

## J. Pillar acceptance (playtest build, không placeholder)

**Visual:** lighting coherent; NPC animation không robotic; camera transition smooth; interaction readability (biết bấm vào đâu); không placeholder visual.
**Constrained 3D (v6.4, chi tiết CONSTRAINED_3D.md §3):** di chuyển trong vùng gameplay + đổi góc tiếp cận; depth + occlusion đúng; NPC di chuyển/animation; tương tác object thật (không hotspot-UI); spatial audio theo position; camera context-driven mượt. FAIL khi là hotspot-2D trá hình / billboard / camera phẳng toàn bộ.
**Audio:** pre-gen zero-latency cảm nhận được; không overlap sai priority (đúng bảng Part H); không clipping; ducking đúng; pronunciation nhất quán 1 voice; offline phát đủ.
**Learning:** objective rõ mỗi quest; vocab đúng ngữ cảnh; repetition có chủ đích (spaced ẩn); transfer test không visual prompt (CT-010).
**UX/UI (Foundation, không làm sau):** bé 4 tuổi biết action tiếp theo mà không đọc text; không dashboard/panel text cho trẻ; target ≥120px; menu không overload; pause/settings cho phụ huynh nhưng không clutter game UI.
