# ADR-002 — Manual DI (GameInstaller), không framework ở W0

Decision: Manual Composition Root duy nhất `GameInstaller` ở BootstrapScene. Không VContainer/Zenject ở Slice.
Context: 3 AI agent song song, cần 1 hướng duy nhất, dễ review, tránh magic.
Alternatives: VContainer (gọn nhưng AI agent chưa quen, dễ inject sai scope), Static ServiceLocator (nhanh nhưng thành global soup + leak).
Why: explicit `new` 1 chỗ + constructor inject = AI khó phát minh lại kiến trúc, Lead dễ grep `new QuestManager` ngoài Installer là reject.
Consequences: chỉ GameInstaller được `new` service. Lifetime theo bảng ARCHITECTURE §3 (EventBus/Speech/Save=Application, Learning/Hint=Session, Quest=Scene/Session, NPC/UI=Scene).
