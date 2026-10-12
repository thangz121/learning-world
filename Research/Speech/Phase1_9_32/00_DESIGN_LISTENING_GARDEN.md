# 00 — DESIGN: VƯỜN TAI THÍNH (English → Listening foundation)

> **Tóm tắt (VI):** Game NGHE đầu tiên của môn Tiếng Anh: nghe một từ tiếng Anh → chạm đúng
> hình. Voice-first (không chữ cho trẻ), có chuông nghe-lại, trả lời sai thì tự đọc lại CHẬM.
> Một lượt chơi đi LV1→LV4 (12 vòng), hoàn thành thì lưu lifecycle như các arena khác.
> Đây là FOUNDATION: 8 từ khởi điểm + pipeline audio tự sinh/tự kiểm (offline, miễn phí).

- **Game id:** `listen_garden` · **Tên:** "Vườn Tai Thính" · **Skill:** `english_listening`
- **Scene:** `ListeningGardenPlayScene` (root `ListeningGardenWorld`), Build Settings #12
- **Trạng thái:** `Implemented` (KHÔNG human-accepted; không tự chốt visual)

## 1. Gameplay (trẻ 4–7, chưa biết đọc)

| vòng | trẻ làm gì | hệ thống làm gì |
|---|---|---|
| vào sân | (phiên đầu) nghe "Con ra vườn nghe nhé!" + đi vào đĩa vàng | camera follow, HUD objective "Nghe và tìm hình nhé." |
| mỗi vòng | nghe từ tiếng Anh, chạm 1 trong 2–4 hình | đọc từ (P1) sau nhịp ngắn; đĩa tiến độ |
| nghe lại | chạm CHUÔNG → phát lại từ | âm "ding" + phát lại (normal) |
| chạm sai | nghe "Chưa đúng. Nghe lại nhé!" rồi tự phát lại từ ở tốc độ CHẬM | không fail, không trừ gì |
| chạm đúng | nghe khen: "Đúng rồi! Quả táo!" + "Yes! Apple!" | nhạc thắng, sao lấp lánh, sang vòng mới |
| hết lượt | "Giỏi lắm!" | lifecycle `Completed`; lần vào sau adopt (không lặp phiên) |

- **Cấp độ (1 lượt = 12 vòng):** LV1 2 hình {apple, ball} ×2 vòng · LV2 3 hình {…+banana} ×3 ·
  LV3 4 hình {…+orange} ×4 · LV4 4 hình từ 8 từ ×4.
- Đáp án **xoay vị trí** theo vòng (không thiên vị bên trái/phải); distractor không trùng nhau
  (`ListeningRounds`, thuần C#, test khoá tính xác định).

## 2. Bộ từ khởi điểm (8) — khớp PropKit có sẵn

| id | nghe | nghĩa (VI) | hình |
|---|---|---|---|
| apple | "apple" | quả táo | PropKit/apple |
| ball | "ball" | quả bóng | primitive (cam + đai trắng) |
| banana | "banana" | quả chuối | PropKit/banana |
| orange | "orange" | quả cam | PropKit/orange |
| carrot | "carrot" | củ cà rốt | PropKit/carrot |
| tomato | "tomato" | quả cà chua | PropKit/tomato |
| watermelon | "watermelon" | quả dưa hấu | PropKit/watermelon |
| grape | "grapes" | chùm nho | PropKit/grapes |

"grape" được đọc là **"grapes"**: bản số ít một-mình bị voice local đọc mờ đuôi (ASR nghe
"rape/fape"); "grapes" qua 100% self-check và khớp hình chùm nho (chi tiết WP-1.9.32 §01).

## 3. Kiến trúc & điểm nối (theo đúng pattern 4 arena hiện có)

- `Assets/A_World/ListeningGarden/` — `ListeningGardenBuilder` (sân khấu, pod hình, chuông,
  entry/exit, NavMesh bake), `ListeningGarden` (vòng chơi/lifecycle), `ListeningGardenChoice`
  + `ListeningGardenBell` (IClickTarget), `ListeningWords` + `ListeningRounds` (logic thuần).
- `LearningMap`: `listen_garden` → `english_listening`, scene const, `Implemented`.
- `GameInstaller`: nhánh dispatch `ListeningGardenBuilder.SceneName` + `BuildListeningGardenPlayScene`
  (SetPlay, ExitPortal.YardArea, lifecycle bind — như market/city).
- `SelectionYardArea`: `ListeningLifecycle` + `ListeningGarden ListeningGame` + `BindListeningGame`.
- `SelectionYardBuilder`: diorama không chữ cho cổng (chuông vàng + 2 thẻ hình).
- Ngôn ngữ: câu hướng dẫn/khen theo `DialogueLang` (vi/en); **audio TỪ luôn en-US** (frozen).

## 4. Audio (offline, miễn phí — xem §01/§02)

- 16 master (8 từ × normal/slow) trong `Assets/StreamingAssets/audio/listen_*.mp3`;
  manifest id `listen_<word>_<mode>`, voice `listen_v1` (thêm vào pool validator).
- Từ phát qua `IAudioDirector.SpeakAsync` với đúng tuple đã khoá → **pregen hit, không cần mạng**
  (khoá bởi CT-S21F + CT-P08B).

## 5. Giới hạn đã biết (trung thực)

- Visual/feel chưa được người duyệt (quy tắc dự án: không tự chốt visual).
- 1 giọng đọc; slow là bản render riêng (không time-stretch) — nhất quán với pregen hiện có.
- Journey driver cho arena này chưa thêm (các driver TempUx* vẫn chỉ đi chuỗi Toán); nên bổ sung
  ở giai đoạn sau khi có human pass sơ bộ.
- Mở rộng nội dung = thêm từ vào `Content/listening/listen_garden.json` + chạy lại
  `tools/pregen_listen.py` (tự sinh + tự kiểm ASR + cập nhật manifest).
