# testing/TestManifest.md — v6.1 Part G (CT ↔ Unity test mapping, machine-readable)

> Quy tắc: `ContractTests.md` có CT nào thì file test tương ứng phải tồn tại và ngược lại. CI check 2 chiều (`contract-tests.yml`); thiếu 1 chiều = fail. Convention: `Assets/Tests/EditMode/<ID>_<Name>.cs`, 1 public test method tên `<ID>`.

| CT | Unity test file | Method |
|----|-----------------|--------|
| CT-001 | Assets/Tests/EditMode/CT-001_WordSeenExposure.cs | CT-001 |
| CT-002 | Assets/Tests/EditMode/CT-002_SpeakCompletesQuest.cs | CT-002 |
| CT-003 | Assets/Tests/EditMode/CT-003_QuestChangesWorld.cs | CT-003 |
| CT-004 | Assets/Tests/EditMode/CT-004_HintTimerFreeze.cs | CT-004 |
| CT-005 | Assets/Tests/EditMode/CT-005_HintResetNoCarry.cs | CT-005 |
| CT-006 | Assets/Tests/EditMode/CT-006_NoActionString.cs | CT-006 |
| CT-007 | Assets/Tests/EditMode/CT-007_IdsTyped.cs | CT-007 |
| CT-008 | Assets/Tests/EditMode/CT-008_VocabAudioApproved.cs | CT-008 |
| CT-009 | Assets/Tests/EditMode/CT-009_MeaningfulDedup.cs | CT-009 |
| CT-010 | Assets/Tests/EditMode/CT-010_TransferNoVisual.cs | CT-010 |
| CT-011 | Assets/Tests/EditMode/CT-011_DisposeStopsDelivery.cs | CT-011 |
| CT-012 | Assets/Tests/EditMode/CT-012_DuplicateIdempotent.cs | CT-012 |
| CT-A01 | Assets/Tests/EditMode/CT-A01_CacheKeyUnique.cs | CT-A01 |
| CT-A02 | Assets/Tests/EditMode/CT-A02_FocusDucking.cs | CT-A02 |
| CT-A03 | Assets/Tests/EditMode/CT-A03_OfflineFallback.cs | CT-A03 |
| CT-A04 | Assets/Tests/EditMode/CT-A04_VoiceIdentity.cs | CT-A04 |

*Ghi chú: Unity tests được tạo ở W0 (chưa tồn tại trong patch plan-only). CI khi chưa có `Assets/Tests/EditMode` báo SKIP rõ ràng + fail gate release, không echo xanh giả.*
