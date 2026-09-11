# testing/ContractTests.md — Suite freeze v6 (CT-001..CT-012 + CT-A01..A04, Lead chạy mỗi PR)

> Mọi doc/workflow chỉ gọi ID CT-xxx, không gọi “test số 8”. Mapping CT ↔ file Unity: `TestManifest.md`. Spec-check python: `contract-tests.yml`.

```
CT-001 WordSeen_IncreasesExposure:
Given mastery apple=Unknown When publish WordSeenEvent(apple,Object) x3 Then stage=Exposed
CT-002 SpeakApple_CompletesQuest:
Given Q1(QuestId market_help_mia) ở bước speak_apple When Policy Great + ReportAction(PlayerAction.Speak, apple) Then objective done → publish QuestCompletedEvent
CT-003 QuestComplete_ChangesWorld:
Given QuestCompletedEvent When handler chạy Then flower_pot + friendship_mia +10
CT-004 HintEscalation_TimerFreeze:
Given idle total 8.0s→L1 visual, 15.0s→L2 point, stuck→L3 demo→L4 simplify; wrong 3→L1,5→L2,7→L3,≥9→L4
CT-005 HintReset_NoCarry:
Given Q1 Level=3 When StartQuest(Q2)+Reset(Q2) Then GetState(Q2).Level==0, WrongCount==0
CT-006 NoActionString_NoNewService:
Given grep ngoài test When thấy Action<string>/string questId/string action/new QuestManager ngoài Installer Then fail (architecture-lint.yml)
CT-007 IdsTyped_PlayerAction:
Given ReportAction("find") string thô When lint Then fail; phải PlayerAction.Find + WordId/QuestId/NpcId
CT-008 VocabActive15_HasSemantic:
Given Content/vocab/*.json (validator thật) When chạy validate_content.py Then Active==15, Passive==35, active có tags+expectedForms, quest target tồn tại
CT-009 MeaningfulSpeech_DedupSpam:
Given "apple"x10/5s cùng interaction When đếm Meaningful (expected+initiated+usable voice) Then ==1
CT-010 Transfer_WithoutVisual:
Given apple Produced Q1 When Q4 không visual + đúng Then ContextUse+1
CT-011 EventSubscription_DisposeStopsDelivery:
Given Subscribe(handler) rồi Dispose ở OnDisable When Publish Then handler KHÔNG chạy; Publish khi subscriber destroyed KHÔNG throw
CT-012 DuplicateSubscription_DefinedBehavior:
Given Subscribe cùng handler 2 lần When Publish 1 event Then chỉ nhận 1 lần (idempotent) hoặc lần 2 bị reject rõ ràng — cấm gọi 2 lần lặng lẽ
CT-A01 CacheKey_UniquePerVoice:
Given cùng text "Apple" khác VoiceProfile (milo_v1 vs learning_v1) hoặc rate khác When hash cache key Then key khác nhau (SHA256 text+voice+locale+rate+pitch+style+format)
CT-A02 Focus_DucksMusicAmbient:
Given P1 pronunciation đang phát When đo bus Then music duck 20%, ambient duck 40%, không voice thứ 2 chen vào
CT-A03 OfflineFallback_QuestContinues:
Given mất mạng (Worker fail) When quest cần audio/dialogue Then pre-gen local phát đầy đủ + FallbackSpeechProvider tiếp tục quest (Mock chỉ test)
CT-A04 NpcVoice_IdentityPersist:
Given NpcId shopkeeper_01 assigned npc_female_02 When spawn lại session sau Then cùng VoiceProfile; Milo/Learning/Mia luôn fixed
```
Runtime offline: FallbackSpeechProvider (Offline Intent Mode). MockSpeechProvider chỉ test/CI. Google voice mapping chỉ ở Worker/config, gameplay/content chỉ VoiceProfileId.
