# W0-T0 Log — Unity scaffold + test-first (Phase 0 approved)

## Done
* `ProjectSettings/ProjectVersion.txt`: `6000.6.0f1` (v6.5 upgrade: Hub Supported+Recommended trên máy dev; trước đó `6000.0.60f1` ở scaffold).
* `Packages/manifest.json`: versions hiện tại là scaffold theo 6000.0.x — KHÔNG pin theo phỏng đoán. Lần mở đầu bằng 6000.6.0f1 để UPM resolve thực tế rồi cập nhật manifest + packages-lock (xem checkpoint W1-Unity6000.6-Upgrade).
* `Assets/` structure: `_SharedKernel` (+2 asmdef), `A_World`, `B_Brain`, `C_Content`, `D_Audio` (.gitkeep), `Tests/EditMode` (+asmdef Editor-only).
* 16 CT EditMode tests: 4 GREEN-capable (CT-006 reflection, CT-007 IDs, CT-011/012 bus) + 12 RED pending có owner W0-T1 rõ ràng. Test-first: EditMode đỏ là đúng cho tới khi W0-T1 implement.
* Không có Unity local ở máy này → compile/build/test Unity chạy local trên máy dev (v6.4 license-free CI, không game-ci). Preflight scaffold check nằm trong `architecture-lint`/PR gate dạng file-exists.

## Pending inputs (cần user, không tự bịa)
1. Cloudflare Worker: endpoint path + auth mechanism (base URL đã freeze ở WorkerTtsContract, còn path/auth REQUIRES USER CONFIG).
2. ~~`UNITY_LICENSE` vào secrets~~ — BỎ (v6.4 license-free, không cần, không chờ).
3. Mở project lần đầu bằng Hub 6000.6.0f1 (checkpoint W1-Unity6000.6-Upgrade): để UPM resolve → verify manifest/URP → commit `ProjectSettings/` đầy đủ + `Packages/packages-lock.json`.

## Next (W0-T1, 4 agent song song)
A greybox+ClickToMove+camera; B Quest+Hint+Tier1/2+Milo request; C shootout QA list; D Director+cache+Mixer+providers+Selector+Fallback. Lead integrate, test đỏ chuyển xanh dần theo từng owner.
