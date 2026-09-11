# contracts/ContentSchemas.md — Vocab / Quest / Dialogue (v6 audio)

Vocab Active (15 Active + 35 Passive, JSON là source of truth):
```json
{
  "id": "apple", "active": true, "language": "en",
  "display": {"en": "apple", "vi": "quả táo"},
  "learning": {"minAge": 4, "difficulty": 1, "skills": ["listen","recognize","speak","use"]},
  "semantic": {"category": "food", "tags": ["fruit","red","round","sweet","market"]},
  "speech": {"expectedForms": ["apple"], "phonetic": "ˈæpəl"},
  "assets": {"prefab": "Addressables/Food/Apple", "image": "apple.png"},
  "audio": {
    "normal": "audio/apple_normal.mp3",
    "slow": "audio/apple_slow.mp3",
    "syllable": null,
    "voice": "learning_v1",
    "lang": "en-US",
    "approved": true
  }
}
```
Rules: Active bắt buộc `audio.normal + audio.slow + voice + lang + approved=true`. `syllable` chỉ từ khó cần. Passive `audio` có thể null. Không để tên Google voice trong JSON gameplay — chỉ `VoiceProfileId`.

Quest bắt buộc `hint_levels` 4 + `simplify_path` + `one_objective_at_a_time`. Dialogue: từng response có `voice` + word-count (NPC ≤6, Milo encouragement ≤8); pack Slice 30–40 câu xem `Content/dialogues/manifest.json`.
