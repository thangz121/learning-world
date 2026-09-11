# ADR-003 — Typed EventBus, subscriber owns subscription

Decision: `IGameEventBus { Subscribe<T>, Publish<T> }` với typed record (`WordSeenEvent`, `WordSpokenEvent`, `QuestCompletedEvent`) + `WordId` chuẩn hóa lowercase. Không weak-ref trong core.
Context: string event gây bug Apple/apple/appple, không biết Source học.
Alternatives: Action<string> (nhanh prototype, khó scale), weak-ref auto-clean (tiện nhưng 3 agent implement 3 kiểu + khó debug).
Why: predictable + testable. Quên dispose = leak thấy ngay khi review. Biết Source (Object/NPC/Quest/Repetition/Milo) để tính mastery + retention.
Consequences: mọi event mới phải là record typed. BusBehaviour là helper dispose ở OnDisable, không phải core magic.
