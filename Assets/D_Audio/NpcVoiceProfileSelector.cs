// D_Audio/NpcVoiceProfileSelector.cs — Agent D (W0-T1). Deterministic NPC voice pool.
// Fixed identities (never via pool): milo -> milo_v1, mia -> mia_v1.
// (learning_v1 is a content voice, not an NPC — never assigned here.)
// Pool NPCs: balanced F/M by live population (fewer members wins, hash breaks ties),
// least-used pool voice first, stable FNV-1a hash of npcId|worldSeed for determinism.
// Persisted via ISaveService (save.NpcVoices read first, written on new assignment).
// W0 note: AUDIO_DESIGN §4 "stable-shuffle the whole NPC set" assumes the full set is
// known; at spawn time only arrivals are known, so balance is by arrival population.
// Same save => same assignment, always (CT-A04). No UnityEngine.Random, no GameObject.
using System;
using System.Collections.Generic;

public sealed class NpcVoiceProfileSelector : INpcVoiceSelector {
  public static readonly VoiceProfileId MiloVoice = new VoiceProfileId("milo_v1");
  public static readonly VoiceProfileId MiaVoice = new VoiceProfileId("mia_v1");

  public static readonly VoiceProfileId[] FemalePool = {
    new VoiceProfileId("npc_female_01"),
    new VoiceProfileId("npc_female_02"),
    new VoiceProfileId("npc_female_03"),
  };

  public static readonly VoiceProfileId[] MalePool = {
    new VoiceProfileId("npc_male_01"),
    new VoiceProfileId("npc_male_02"),
    new VoiceProfileId("npc_male_03"),
  };

  readonly ISaveService _save;

  // Dependency in via ctor only (GameInstaller news this). Never `new`s other services.
  public NpcVoiceProfileSelector(ISaveService save) {
    _save = save ?? throw new ArgumentNullException(nameof(save));
  }

  public VoiceProfileId Assign(NpcId npc, NpcArchetype archetype) {
    string id = (npc.Value ?? "").ToLowerInvariant().Trim();
    if (id == "milo") return MiloVoice;
    if (id == "mia") return MiaVoice;

    PlayerProgress progress = null;
    try { progress = _save.Load(); } catch (Exception) { }
    if (progress == null) progress = new PlayerProgress();
    if (progress.NpcVoices == null) progress.NpcVoices = new Dictionary<string, string>();

    if (progress.NpcVoices.TryGetValue(id, out string existing)
        && !string.IsNullOrEmpty(existing)) {
      return new VoiceProfileId(existing);
    }
    if (string.IsNullOrEmpty(progress.WorldSeed)) {
      progress.WorldSeed = Guid.NewGuid().ToString("N");
    }

    uint hash = Fnv1a32(id + "|" + progress.WorldSeed);
    int females = CountPrefix(progress.NpcVoices, "npc_female_");
    int males = CountPrefix(progress.NpcVoices, "npc_male_");
    bool wantFemale = females < males ? true : males < females ? false : (hash & 1u) == 0u;
    VoiceProfileId[] pool = wantFemale ? FemalePool : MalePool;

    // Least-used pool voice; ties broken by hash-rotated order (deterministic).
    string picked = pool[(int)(hash % (uint)pool.Length)].Value;
    int best = int.MaxValue;
    int start = (int)(hash % (uint)pool.Length);
    for (int k = 0; k < pool.Length; k++) {
      string candidate = pool[(start + k) % pool.Length].Value;
      int used = CountExact(progress.NpcVoices, candidate);
      if (used < best) {
        best = used;
        picked = candidate;
        if (best == 0) break;
      }
    }

    progress.NpcVoices[id] = picked;
    try { _save.Save(progress); } catch (Exception) { }
    return new VoiceProfileId(picked);
  }

  // Stable FNV-1a 32-bit (spec: deterministic across sessions/platforms).
  public static uint Fnv1a32(string s) {
    const uint offset = 2166136261u;
    const uint prime = 16777619u;
    uint h = offset;
    string t = s ?? "";
    for (int i = 0; i < t.Length; i++) {
      h ^= t[i];
      h *= prime;
    }
    return h;
  }

  static int CountPrefix(Dictionary<string, string> map, string prefix) {
    int n = 0;
    foreach (var kv in map) {
      if (kv.Value != null && kv.Value.StartsWith(prefix)) n++;
    }
    return n;
  }

  static int CountExact(Dictionary<string, string> map, string voice) {
    int n = 0;
    foreach (var kv in map) {
      if (kv.Value == voice) n++;
    }
    return n;
  }
}
