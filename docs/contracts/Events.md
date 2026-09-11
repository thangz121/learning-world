# contracts/Events.md — Typed Events (freeze v6)

> C# POLICY (verified 2026-09-11, Unity 6000.6.0f1): project compiles at
> LangVersion 9.0 (`-langversion:9.0` in `Library/Bee` rsp, no `csc.rsp`
> override). `record struct` is C# 10 → CS8773, BANNED. Events are
> `readonly struct` with record-equivalent value semantics
> (`IEquatable<T>`, `==`/`!=`, `GetHashCode`, `ToString`, `Deconstruct`),
> public readonly FIELDS (Unity/JsonUtility-serializable). No `with`
> expressions (C# 10). Implementation: `Assets/_SharedKernel/Events.cs`.

IDs freeze: `WordId, QuestId, NpcId, InteractionId, VoiceProfileId, LanguageCode` (xem `contracts/Ids.md`).
`LearnSource {Object,NPC,Quest,Repetition,Milo}`. `PlayerAction {Find,Bring,Speak,Give,Select}`.

```csharp
// C# 9 form (canonical). Positional ctor + field access preserved, e.g.
// _bus.Publish(new WordSeenEvent(wordId, LearnSource.Object, DateTime.UtcNow));
readonly struct WordSeenEvent { WordId WordId; LearnSource Source; DateTime At; }
readonly struct WordSpokenEvent { WordId WordId; SpeechResult Result; LearnSource Source; }
readonly struct QuestCompletedEvent { QuestId QuestId; DateTime At; }
readonly struct QuestStartedEvent { QuestId QuestId; DateTime At; }
readonly struct HintLevelChanged { QuestId QuestId; int Level; } // 0 none,1 visual,2 point,3 demo,4 simplify
readonly struct NavigationCompleted { InteractionId Target; NpcId ByNpc; }
readonly struct DialogueRequested { VoiceProfileId Voice; LanguageCode Lang; AudioPriority Priority; string Text; }
readonly struct VocabularyPlayed { WordId WordId; VocabularyAudioMode Mode; bool FromCache; }
```

Quy tắc: thêm event = thêm readonly struct + PR Lead duyệt. Cấm `Action<string>` / `string questId` trôi nổi — dùng struct typed. Cấm `record struct` / `with` (C# 10, CS8773 ở LangVersion 9.0).
