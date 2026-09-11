using System;
using System.Collections.Generic;

// C_Content DTOs — flat, JsonUtility-compatible ([Serializable] fields only, no dicts).
// Raw JSON shapes live in ContentDatabase.cs as private Raw* wrappers; these are the typed outputs.
// Text QA (flags only, Content/ untouched per ownership rule):
// - manifest inst_03 "Apple please!" reads telegraphic, kept as-is (beginner-directed speech by design).
// - manifest ok_03 "Thank you! Yay!" pairing is slightly unnatural, kept as-is for Agent C review in Content/.
[Serializable]
public class VocabEntry
{
    public string id;
    public bool active;
    public string displayEn;
    public string displayVi;
    public string category;
    public List<string> tags;
    public List<string> expectedForms;
    public string phonetic;
    public string prefab;
    public string image;
    public string audioNormal;
    public string audioSlow;
    public string audioVoice; // VoiceProfileId only, never a Google voice name.
    public string audioLang; // Freeze "en-US".
    public bool audioGenerated;
    public bool audioApproved;
}

[Serializable]
public class QuestObjective
{
    public string id;
    public PlayerAction action; // Parsed from string at JSON boundary (see ContentDatabase.ParsePlayerAction).
    public string target; // WordId value (raw string; parse to WordId at gameplay boundary).
}

[Serializable]
public class QuestEntry
{
    public string id;
    public string npc;
    public List<QuestObjective> objectives;
    public List<string> hintLevels;
    public int simplifyReduceChoices;
    public bool simplifyDemo;
    public bool oneObjectiveAtATime; // Freeze true (validator: one_objective_at_a_time).
    public int rewardFriendship;
    public string rewardWorldChange;
}

[Serializable]
public class DialogueLine
{
    public string id;
    public string npc;
    public string voice; // VoiceProfileId only.
    public string text;
    public string audio;
}

// Dialogue manifest pack header + lines (mirrors dialogues/manifest.json shape
// checked by tools/validate_content.py: count/lang/approved + lines).
[Serializable]
public class DialoguePack
{
    public int count;
    public string lang; // Freeze "en-US".
    public bool approved;
    public List<DialogueLine> lines;
}

[Serializable]
public class PreGenItem
{
    public string id;
    public string text;
    public string voiceProfile; // VoiceProfileId only.
    public string lang; // Freeze "en-US".
    public float rate;
}
