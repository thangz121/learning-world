# MIGRATION — SANG MÁY MỚI (CLONE-FIRST)

> **Tóm tắt (VI):** Repo chứa đủ code + nhãn review + báo cáo + pack + requirements.
> Ba thứ KHÔNG nằm trong git (chính sách + bản quyền + dung lượng): audio gốc, model weights, venv.
> Làm theo 6 bước dưới đây là máy mới chạy được toàn bộ pipeline speech 1.9.27–1.9.30.

## 0. Yêu cầu máy mới

- Git; **Python 3.14.x cài vào đúng `C:\Python314`** (venv p0 build từ đường dẫn này)
- Ổ **D:** tồn tại (mọi script hardcode `D:\speech-lab` và `D:\Vscode`)
- Internet (tải dataset/model công khai). Tailscale tùy chọn (copy dữ liệu local-only từ máy cũ).
- Không cần GPU cho pilot (CPU đủ); GPU chỉ cần cho B2-C tương lai.

## 1. Clone

```powershell
git clone https://github.com/thangz121/learning-world.git D:\Vscode\little-world-english
cd D:\Vscode\little-world-english
git checkout main   # main == ux/math-arenas-hotfix-20260930
```

## 2. Venv + packages

```powershell
C:\Python314\python.exe -m venv D:\speech-lab\venvs\p0
D:\speech-lab\venvs\p0\Scripts\python.exe -m pip install -r D:\Vscode\little-world-english\Research\Speech\requirements-p0.txt
```

## 3. Audio (bắt buộc cho review + pilot)

| đích trên máy mới | nguồn | ghi chú |
|---|---|---|
| `D:\speech-lab\data\speechocean762` | https://www.openslr.org/101 → `speechocean762.tar.gz` | giữ nguyên cây `WAVE\SPEAKER####` — Pack P + phần lớn Pack R trỏ vào đây |
| `D:\Vscode\little-world-english\Research\Speech\ExternalData\zenodo_200495` | https://zenodo.org/records/200495 (LWE, CC BY 4.0) | giữ đúng cây `extracted\...` vì CSV trỏ path tuyệt đối |
| `…\ExternalData\SIAK` | **KHÔNG công khai** (CC-BY-ND, bản local) | chỉ cần nếu muốn tiếp tục 30 ca Pool O; copy riêng từ máy cũ, đừng public |
| `D:\speech-lab\data\so762_16k` (tùy chọn) | chuyển từ speechocean762 | chỉ dùng cho các phase legacy 1.2–1.4 |

## 4. Models (một lần, đều Apache-2.0 trên HuggingFace)

```powershell
D:\speech-lab\venvs\p0\Scripts\python.exe -c "from huggingface_hub import snapshot_download as d; d('facebook/wav2vec2-lv-60-espeak-cv-ft')"
D:\speech-lab\venvs\p0\Scripts\python.exe -c "from huggingface_hub import snapshot_download as d; d('facebook/wav2vec2-xlsr-53-espeak-cv-ft', cache_dir=r'D:\speech-lab\models')"
```

- `lv-60` → cache mặc định `C:\Users\<user>\.cache\huggingface` (pilot_runner dùng `local_files_only=True`).
- `xlsr-53` → cache riêng `D:\speech-lab\models` (PhoneEvidenceV2: `HF_CACHE = D:\speech-lab\models`).
- `cmudict.dict` (3.45MB): đã kèm trong repo, chạy script mục 4b hoặc copy tay:
  `copy Research\Speech\Phase1_9_30\MIGRATION\assets\cmudict.dict D:\speech-lab\models\`

### 4b. Script gộp (chạy sau bước 2)

```powershell
powershell -ExecutionPolicy Bypass -File D:\Vscode\little-world-english\Research\Speech\Phase1_9_30\MIGRATION\setup_new_machine.ps1
```

## 5. Verify

```powershell
D:\speech-lab\venvs\p0\Scripts\python.exe D:\Vscode\little-world-english\Research\Speech\Phase1_9_27\experiments\pilot_runner.py --stage validate
# kỳ vọng: rows 200 / pass 200 / overlaps 0 / leakage_pass true

D:\speech-lab\venvs\p0\Scripts\python.exe D:\Vscode\little-world-english\Research\Speech\Phase1_9_29\experiments\infra_health_1929.py
# kỳ vọng: hashes_all_match true, server_pass true, mapping_pass true, REVIEW_INFRASTRUCTURE PASS
```

## 6. Tiếp tục phiên review (khi cần)

```powershell
# Pack R (276 ca)
D:\speech-lab\venvs\p0\Scripts\python.exe D:\Vscode\little-world-english\Research\Speech\Phase1_9_27\experiments\serve_review_1927.py 8791

# Pack P — test split 99 / dev+train 447
...serve_review_1927.py 8791 --pack D:\Vscode\little-world-english\Research\Speech\Phase1_9_30\artifacts\review_packs\PILOT_TEST_SPLIT_99.csv --reviews D:\Vscode\little-world-english\Research\Speech\Phase1_9_30\artifacts\reviews_s2
```
Mở UI: `http://<IP>:8791` (cùng Wi-Fi hoặc qua Tailscale). Nhãn raw lưu vào `--reviews` dir (JSONL) và snapshot tự động khi bấm Export.

## Những gì clone ĐÃ có sẵn

- Toàn bộ code experiments (Phase1_9_*.27/28/29/30), server review (bản có auto-delivery), import validator, agreement pipeline, pilot runner.
- Nhãn review thật đã thu: `Phase1_9_27/artifacts/reviews/` (REV-A), `Phase1_9_30/artifacts/reviews_s2/` (REV-A-S2) + export snapshots + import/agreement outputs.
- Pack R/P + pack con (test 99, dev+train 447), frozen configs + hashes, toàn bộ báo cáo và session reports.
- `requirements-p0.txt` + `cmudict.dict` + setup script.

## KHÔNG làm

- Không commit audio/dataset/archive/model (đã chặn trong `.gitignore`).
- Không dùng MAYNODE cho nghiên cứu.
- Không đổi path `D:\speech-lab` (hoặc set env `LWE_SPEECH_LAB` nếu buộc phải khác).
