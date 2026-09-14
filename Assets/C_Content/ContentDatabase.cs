using System;
using System.Collections.Generic;
using UnityEngine;

// Agent C — Learning Content database (text/metadata only).
// Owns text + approval intent; NEVER fetches/plays audio (Agent D owns generation/files, Part K).
// No network, no UnityWebRequest, no gameplay logic, no `new` services (only DTOs/lists).
// Voice fields carry VoiceProfileId values only (e.g. "learning_v1"), never Google voice names.
// Text QA (flags only, Content/ untouched): manifest "Milk and bread here!" (mia_02)
// is borderline unnatural outside a shop shelf context; "Thank you! Yay!" (ok_03)
// pairs formal + cheer interjection — both left as-is for Content/ review.
public static class ContentDatabase
{
    // ---- Raw JSON shapes (private, JsonUtility-matched, field names mirror JSON exactly) ----

    [Serializable] private class RawDisplay { public string en; public string vi; }
    [Serializable] private class RawSemantic { public string category; public List<string> tags; }
    [Serializable] private class RawSpeech { public List<string> expectedForms; public string phonetic; }
    [Serializable] private class RawAssets { public string prefab; public string image; }
    [Serializable] private class RawVocabAudio
    {
        public string normal; public string slow; public string syllable;
        public string voice; public string lang; public bool generated; public bool approved;
    }
    // Phase 2C: optional authoring block. Absent entirely in older files
    // (backward compatible: defaults below). Present-but-malformed values
    // fall back to defaults too — the python validator (not the parser)
    // is where authoring mistakes fail loudly.
    [Serializable] private class RawProgression
    {
        public int introOrder; public List<string> prerequisites;
    }
    [Serializable] private class RawVocab
    {
        public string id; public bool active; public RawDisplay display;
        public RawSemantic semantic; public RawSpeech speech; public RawAssets assets; public RawVocabAudio audio;
        public RawProgression progression;
    }

    [Serializable] private class RawObjective { public string id; public string action; public string target; }
    [Serializable] private class RawSimplify { public int reduce_choices_to; public bool demo_one_step; }
    [Serializable] private class RawReward { public int friendship_mia; public string world_change; }
    [Serializable] private class RawQuest
    {
        public string id; public string npc; public bool one_objective_at_a_time;
        public List<RawObjective> objectives; public List<string> hint_levels;
        public RawSimplify simplify_path; public RawReward reward;
    }

    [Serializable] private class RawLine
    {
        public string id; public string npc; public string voice; public string text; public string audio;
    }
    [Serializable] private class RawManifest
    {
        public int count; public string lang; public bool approved; public List<RawLine> lines;
    }

    // ---- Boundary parsers ----

    public static PlayerAction ParsePlayerAction(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            throw new ArgumentException("Unknown PlayerAction: <null/empty>");
        string v = raw.Trim().ToLowerInvariant();
        if (v == "find") return PlayerAction.Find;
        if (v == "bring") return PlayerAction.Bring;
        if (v == "speak") return PlayerAction.Speak;
        if (v == "give") return PlayerAction.Give;
        if (v == "select") return PlayerAction.Select;
        throw new ArgumentException("Unknown PlayerAction: '" + raw + "'");
    }

    public static VocabEntry ParseVocab(string json)
    {
        RawVocab r = JsonUtility.FromJson<RawVocab>(json);
        if (r == null) throw new ArgumentException("ParseVocab: invalid JSON");
        VocabEntry e = new VocabEntry();
        e.id = r.id;
        e.active = r.active;
        e.displayEn = r.display != null ? r.display.en : null;
        e.displayVi = r.display != null ? r.display.vi : null;
        e.category = r.semantic != null ? r.semantic.category : null;
        e.tags = r.semantic != null && r.semantic.tags != null ? r.semantic.tags : new List<string>();
        e.expectedForms = r.speech != null && r.speech.expectedForms != null ? r.speech.expectedForms : new List<string>();
        e.phonetic = r.speech != null ? r.speech.phonetic : null;
        e.prefab = r.assets != null ? r.assets.prefab : null;
        e.image = r.assets != null ? r.assets.image : null;
        e.audioNormal = r.audio != null ? r.audio.normal : null;
        e.audioSlow = r.audio != null ? r.audio.slow : null;
        e.audioVoice = r.audio != null ? r.audio.voice : null;
        e.audioLang = r.audio != null ? r.audio.lang : null;
        e.audioGenerated = r.audio != null && r.audio.generated;
        e.audioApproved = r.audio != null && r.audio.approved;
        // Phase 2C progression block (optional): JsonUtility auto-instantiates
        // a missing nested block (all-default), so absence is detected by
        // CONTENT, not nullness: a block carrying no information
        // (order < 1 AND no prerequisites) means "unordered, no
        // prerequisites". Convention (validator-enforced): real orders start
        // at 1; 0 is reserved = unordered. An explicit block must therefore
        // always set introOrder >= 1.
        e.introOrder = 999;
        e.prerequisites = new List<string>();
        if (r.progression != null
            && (r.progression.introOrder >= 1
                || (r.progression.prerequisites != null && r.progression.prerequisites.Count > 0)))
        {
            if (r.progression.introOrder >= 1) e.introOrder = r.progression.introOrder;
            if (r.progression.prerequisites != null)
            {
                foreach (string p in r.progression.prerequisites)
                {
                    if (!string.IsNullOrWhiteSpace(p) && !e.prerequisites.Contains(p.Trim()))
                        e.prerequisites.Add(p.Trim());
                }
            }
        }
        return e;
    }

    public static QuestEntry ParseQuest(string json)
    {
        RawQuest r = JsonUtility.FromJson<RawQuest>(json);
        if (r == null) throw new ArgumentException("ParseQuest: invalid JSON");
        QuestEntry e = new QuestEntry();
        e.id = r.id;
        e.npc = r.npc;
        e.objectives = new List<QuestObjective>();
        if (r.objectives != null)
        {
            for (int i = 0; i < r.objectives.Count; i++)
            {
                RawObjective o = r.objectives[i];
                QuestObjective q = new QuestObjective();
                q.id = o.id;
                q.action = ParsePlayerAction(o.action); // string -> enum at boundary; throws on unknown.
                q.target = o.target;
                e.objectives.Add(q);
            }
        }
        e.hintLevels = r.hint_levels != null ? r.hint_levels : new List<string>();
        e.simplifyReduceChoices = r.simplify_path != null ? r.simplify_path.reduce_choices_to : 0;
        e.simplifyDemo = r.simplify_path != null && r.simplify_path.demo_one_step;
        e.oneObjectiveAtATime = r.one_objective_at_a_time;
        e.rewardFriendship = r.reward != null ? r.reward.friendship_mia : 0;
        e.rewardWorldChange = r.reward != null ? r.reward.world_change : null;
        return e;
    }

    public static DialoguePack ParseDialoguePack(string json)
    {
        RawManifest r = JsonUtility.FromJson<RawManifest>(json);
        if (r == null) throw new ArgumentException("ParseDialoguePack: invalid JSON");
        DialoguePack pack = new DialoguePack();
        pack.count = r.count;
        pack.lang = r.lang;
        pack.approved = r.approved;
        pack.lines = new List<DialogueLine>();
        if (r.lines != null)
        {
            for (int i = 0; i < r.lines.Count; i++)
            {
                RawLine l = r.lines[i];
                DialogueLine d = new DialogueLine();
                d.id = l.id; d.npc = l.npc; d.voice = l.voice; d.text = l.text; d.audio = l.audio;
                pack.lines.Add(d);
            }
        }
        return pack;
    }

    public static List<DialogueLine> ParseDialogueManifest(string json)
    {
        return ParseDialoguePack(json).lines;
    }

    // Mirrors tools/validate_content.py word-count rule: NPC lines <= 6 words, Milo (milo_v1) <= 8.
    // Pass isMilo=true for Milo voice lines, false otherwise.
    public static bool ValidateWordCount(string text, bool isMilo)
    {
        if (string.IsNullOrEmpty(text)) return true;
        string[] parts = text.Split(new char[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
        int limit = isMilo ? 8 : 6;
        return parts.Length <= limit;
    }

    // Builds the pre-gen request list for Agent D. Hardcodes NOTHING about content:
    // ids/text/voices come from the passed entries. Only contract constants are fixed:
    // vocab normal rate 0.85, vocab slow rate 0.70, dialogue rate 0.85, dialogue lang "en-US" (manifest freeze).
    // Emits metadata only — no audio fetching, no network.
    public static List<PreGenItem> BuildPreGenList(List<VocabEntry> vocabs, List<DialogueLine> lines)
    {
        List<PreGenItem> out_ = new List<PreGenItem>();
        if (vocabs != null)
        {
            for (int i = 0; i < vocabs.Count; i++)
            {
                VocabEntry v = vocabs[i];
                if (v == null || !v.active) continue;
                PreGenItem normal = new PreGenItem();
                normal.id = v.id + "_normal"; normal.text = v.displayEn;
                normal.voiceProfile = v.audioVoice; normal.lang = v.audioLang; normal.rate = 0.85f;
                out_.Add(normal);
                PreGenItem slow = new PreGenItem();
                slow.id = v.id + "_slow"; slow.text = v.displayEn;
                slow.voiceProfile = v.audioVoice; slow.lang = v.audioLang; slow.rate = 0.70f;
                out_.Add(slow);
            }
        }
        if (lines != null)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                DialogueLine l = lines[i];
                if (l == null) continue;
                PreGenItem p = new PreGenItem();
                p.id = l.id; p.text = l.text; p.voiceProfile = l.voice; p.lang = "en-US"; p.rate = 0.85f;
                out_.Add(p);
            }
        }
        return out_;
    }
}
