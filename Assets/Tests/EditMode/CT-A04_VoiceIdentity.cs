// CT-A04: same NpcId across sessions -> same VoiceProfile; Milo/Mia/Learning fixed. Owner: Agent D (W0-T1 GREEN).
// Real: NpcVoiceProfileSelector over an in-memory ISaveService (save read first, write-back
// on new assignment, stable FNV-1a hash + balanced F/M population).
using System;
using System.Collections.Generic;
using NUnit.Framework;

public class CT_A04_VoiceIdentity {
  sealed class CTA04_MemSave : ISaveService {
    public PlayerProgress Progress = new PlayerProgress {
      WorldSeed = "w0t1-test-seed",
      NpcVoices = new Dictionary<string, string>(),
    };
    public void Save(PlayerProgress p) { Progress = p; }
    public PlayerProgress Load() { return Progress; }
  }

  static void AssignTen(ISaveService save, out Dictionary<string, string> map) {
    var sel = new NpcVoiceProfileSelector(save);
    for (int i = 0; i < 10; i++) {
      sel.Assign(new NpcId("npc_" + i), NpcArchetype.Worker);
    }
    map = new Dictionary<string, string>(save.Load().NpcVoices);
  }

  [Test] public void CT_A04() {
    // Fixed identities never go through the pool.
    var save = new CTA04_MemSave();
    var session1 = new NpcVoiceProfileSelector(save);
    Assert.AreEqual("milo_v1",
      session1.Assign(new NpcId("milo"), NpcArchetype.Child).Value, "milo fixed");
    Assert.AreEqual("mia_v1",
      session1.Assign(new NpcId("mia"), NpcArchetype.Shopkeeper).Value, "mia fixed");

    // Same NpcId, later session, same save -> same voice (persisted, not re-rolled).
    VoiceProfileId first = session1.Assign(new NpcId("shopkeeper_01"), NpcArchetype.Shopkeeper);
    Assert.IsTrue(first.Value.StartsWith("npc_"), "pool NPC uses pool voice, got " + first.Value);
    var session2 = new NpcVoiceProfileSelector(save);
    Assert.AreEqual(first.Value,
      session2.Assign(new NpcId("shopkeeper_01"), NpcArchetype.Shopkeeper).Value,
      "identity must persist across sessions");

    // Balanced population: 10 NPCs -> F/M differ by <= 1.
    var popSave = new CTA04_MemSave();
    AssignTen(popSave, out var pop);
    int f = 0, m = 0;
    foreach (var kv in pop) {
      if (kv.Value.StartsWith("npc_female_")) f++;
      else if (kv.Value.StartsWith("npc_male_")) m++;
      else Assert.Fail("pool voice expected, got " + kv.Value);
    }
    Assert.AreEqual(10, f + m);
    Assert.LessOrEqual(Math.Abs(f - m), 1, "population must stay balanced");

    // Deterministic: same seed + same arrival order -> identical map.
    var seedA = new CTA04_MemSave();
    var seedB = new CTA04_MemSave();
    AssignTen(seedA, out var mapA);
    AssignTen(seedB, out var mapB);
    CollectionAssert.AreEquivalent(mapA, mapB, "same seed must assign identically");
  }
}
