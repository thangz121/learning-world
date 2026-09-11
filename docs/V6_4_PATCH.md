# v6.4 Patch — license-free CI + constrained 3D + Worker base (quyết định kiến trúc mới)

## 1. Bỏ Unity license khỏi CI
* Xóa `.github/workflows/unity-build.yml` (game-ci). GitHub CI chỉ chạy kiểm tra không cần Editor: validator, suite IDs, mapping 2 chiều, lint, schema.
* `contract-tests.yml`: step Unity chuyển thành NOT RUN / LOCAL REQUIRED (không PASS giả).
* `release-gate.yml`: chia NON-UNITY CI GATE (luôn chạy) + UNITY LOCAL GATE (manual: compile → EditMode → PlayMode → Windows build) + check đủ 16 file test.
* ARCHITECTURE §10, AGENTS checklist/Lead, W0_T0_LOG: bỏ UNITY_LICENSE/EMAIL/PASSWORD, bỏ pending license. Windows EXE build local; automated cloud build để sau, không blocker.

## 2. Constrained 3D (thay full-free)
* Mới `CONSTRAINED_3D.md` (source of truth): giữ 3D env/NPC/animation/perspective/lighting/depth/movement vùng gameplay/spatial audio/interaction 3D; bỏ 360 full-time, open-world, exploration vô hạn, camera-collision phức tạp; camera constrained/cinematic/context-driven.
* `TRUE_3D.md` superseded (giữ tham khảo). Sync: GAME_DESIGN pointer, VerticalSliceAcceptance test, AGENTS A/B/D, PRODUCT mini-world wording.

## 3. Worker base freeze
* `WorkerTtsContract.md`: base `https://round-mud-63dd.hoaithuong1995cdmna.workers.dev/` (user-provided); path/auth vẫn REQUIRES USER CONFIG, không suy đoán.

## READY: W0-T1 được phép tiếp tục, không chờ license. Plan version: v6.4.
