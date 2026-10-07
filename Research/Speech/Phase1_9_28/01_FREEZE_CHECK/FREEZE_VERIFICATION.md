# FREEZE VERIFICATION - WP-1.9.28 Part 12

> **Tóm tắt (VI):** Xác minh cấu hình đóng băng: hash SHA256 của 19 file khóa (thiết kế, tiêu chí,
> split, features, packs) được ghi lại; `git diff dfdb82d` trên các đường dẫn khóa = rỗng; cây sạch.
> Kết quả: PASS - không có thay đổi nào sau WP-1.9.27, trước khi có bất kỳ nhãn nào.

## Method

1. Hash every frozen configuration/input file (SHA256) into `FROZEN_CONFIG_HASHES.txt`
   (19 files: preregistration, success/failure/kill, B2-D/B2-E design + ablations + evaluation
   protocols, label protocol, label schema, pilot manifest, Pack P, pilot split, pilot features
   prod/alt/merged, Pack R).
2. Verify no tracked frozen file changed since the WP-1.9.27 commit:
   `git diff --stat dfdb82d -- Research/Speech/Phase1_9_27 Research/Speech/Phase1_9_26/01_HUMAN_LABEL_ACQUISITION/REVIEW_CANDIDATES.csv`
   -> empty.
3. `git status` -> clean at the start of this WP.

## Results

| check | result |
|---|---|
| frozen file hashes recorded | PASS (19 entries) |
| git diff vs `dfdb82d` on frozen paths | empty (no change) |
| Pack R sha256 vs 1.9.27 record | match: `3D127711...FAF5A0675`, 126339 bytes |
| config changed after seeing labels | N/A - no labels exist |
| evaluation run under changed criteria | NONE - no evaluation was run |

## Statement

The frozen configs are the WP-1.9.27 versions. No criterion, threshold, ablation, split, or feature
definition was modified in WP-1.9.28. Every future evaluation must either (a) reproduce these hashes,
or (b) be explicitly declared a new preregistration with a new version tag and be excluded from the
frozen-pilot conclusion.

If reviewer labels arrive, run the frozen pilot under exactly these hashes.
