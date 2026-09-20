// CT-P28: premium nature decor pinning (Phase 2.4 world upgrade 2026-09-18).
// Pins: all 17 Quaternius prefabs load, Spawn strips colliders + normalizes
// height, missing-prefab null-safety, LwGrass tuft validity (real normals,
// no collider, deterministic), gameplay clear-zone contract (solid decor
// stays off path/anchors/corridors), NatureSway API sanity. C# 9.0 only.
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

public class CT_P28_NatureDecor {
  static readonly string[] AllModels = {
    "CommonTree_1", "CommonTree_3", "CommonTree_5", "PineTree_2", "Willow_2",
    "Bush_1", "Bush_2", "BushBerries_1", "Flowers",
    "Rock_2", "Rock_4", "Rock_6", "Rock_Moss_2",
    "Plant_2", "Plant_4", "TreeStump", "WoodLog",
  };

  static float DeployedHeight(GameObject go) {
    Bounds b = new Bounds(go.transform.position, Vector3.zero);
    bool any = false;
    foreach (Renderer r in go.GetComponentsInChildren<Renderer>()) {
      if (r == null) continue;
      if (any) b.Encapsulate(r.bounds);
      else { b = r.bounds; any = true; }
    }
    return any ? b.size.y : 0f;
  }

  static bool InClearZone(Vector3 pos) {
    MethodInfo m = typeof(MarketBuilder).GetMethod("IsInGameplayClearZone",
      BindingFlags.NonPublic | BindingFlags.Static);
    Assert.IsNotNull(m, "clear-zone predicate must exist (contract seam)");
    return (bool)m.Invoke(null, new object[] { pos });
  }

  [Test] public void P28A_AllDecorPrefabsLoad() {
    foreach (string name in AllModels) {
      GameObject prefab = NatureLibrary.Load(name);
      Assert.IsNotNull(prefab, "Nature prefab must import clean: " + name);
    }
  }

  [Test] public void P28B_SpawnStripsCollidersAndNormalizesHeight() {
    GameObject root = new GameObject("P28B");
    try {
      GameObject grown = NatureLibrary.Spawn(root.transform, "CommonTree_1",
        new Vector3(20f, 0f, 20f), 0f, 1.6f,
        new Color(0.25f, 0.58f, 0.28f), Color.white, Color.white);
      Assert.IsNotNull(grown, "known prefab must spawn (null = silent fallback, not this path)");
      Collider[] colliders = grown.GetComponentsInChildren<Collider>(true);
      Assert.AreEqual(0, colliders.Length, "decor never touches NavMesh/clicks");
      float h = DeployedHeight(grown);
      Assert.Greater(h, 1.6f * 0.9f, "height normalization lands near target (low)");
      Assert.Less(h, 1.6f * 1.1f, "height normalization lands near target (high)");
    } finally { Object.DestroyImmediate(root); }
  }

  [Test] public void P28C_SpawnMissingPrefabIsNullSafe() {
    GameObject root = new GameObject("P28C");
    try {
      int failBefore = NatureLibrary.SpawnFail;
      GameObject grown = null;
      Assert.DoesNotThrow(() => {
        grown = NatureLibrary.Spawn(root.transform, "NoSuchModel_ZZ",
          Vector3.zero, 0f, 1f, Color.green, Color.white, Color.white);
      });
      Assert.IsNull(grown, "missing prefab = null so callers fall back to primitives");
      Assert.Greater(NatureLibrary.SpawnFail, failBefore, "failure counter moves (diagnostics)");
    } finally { Object.DestroyImmediate(root); }
  }

  [Test] public void P28D_LwGrassTuftValid() {
    GameObject root = new GameObject("P28D");
    try {
      Vector3 pos = new Vector3(-4f, 0f, 5.2f); // legacy tuft anchor
      GameObject tuft = LwGrass.SpawnTuft(root.transform, pos, 0f, 0.30f,
        new Color(0.30f, 0.62f, 0.30f));
      Assert.IsNotNull(tuft, "tuft must generate (no external mesh dependency)");
      Assert.AreEqual(0, tuft.GetComponentsInChildren<Collider>(true).Length, "walkable, never blocks");
      MeshFilter filter = tuft.GetComponent<MeshFilter>();
      Assert.IsNotNull(filter, "tuft carries its own mesh");
      Assert.IsNotNull(filter.sharedMesh, "mesh assigned");
      // 6 tapered blades x 4 verts (star clump, BuildVariant contract).
      Assert.AreEqual(24, filter.sharedMesh.vertexCount, "6 blades x 4 verts");
      Assert.AreEqual(24, filter.sharedMesh.normals.Length, "explicit up-biased normals (no black blades)");
      // Deterministic: same anchor reuses the same cached variant mesh.
      GameObject tuft2 = LwGrass.SpawnTuft(root.transform, pos, 90f, 0.30f,
        new Color(0.30f, 0.62f, 0.30f));
      Assert.AreSame(filter.sharedMesh, tuft2.GetComponent<MeshFilter>().sharedMesh,
        "same anchor position = same variant (deterministic look)");
    } finally { Object.DestroyImmediate(root); }
  }

  [Test] public void P28E_ClearZoneContract() {
    // Anchors + path corridor are inside the zone (solid decor kept out).
    Assert.IsTrue(InClearZone(new Vector3(0f, 0f, -0.8f)), "Milo anchor clear");
    Assert.IsTrue(InClearZone(new Vector3(-3.5f, 0f, -2.5f)), "Mia anchor clear");
    Assert.IsTrue(InClearZone(new Vector3(0f, 0f, 4.5f)), "player spawn clear");
    Assert.IsTrue(InClearZone(new Vector3(0f, 0f, 2f)), "path corridor clear");
    Assert.IsTrue(InClearZone(new Vector3(3.5f, 0f, -2.0f)), "apple crate clear");
    // Milo->Mia walking corridor midpoint is guarded.
    Assert.IsTrue(InClearZone(new Vector3(-1.75f, 0f, -1.65f)), "Milo->Mia corridor clear");
    // Authored solid-decor positions sit outside the zone (or Add* skips them).
    Assert.IsFalse(InClearZone(new Vector3(-7.3f, 0f, -2.0f)), "boundary bush anchor is plantable");
    Assert.IsFalse(InClearZone(new Vector3(6.8f, 0f, -3.2f)), "pine anchor is plantable");
  }

  [Test] public void P28F_SwayApiSane() {
    GameObject root = new GameObject("P28F");
    try {
      NatureSway sway = null;
      Assert.DoesNotThrow(() => { sway = root.AddComponent<NatureSway>(); });
      Assert.IsNotNull(sway);
      Assert.Greater(sway.amplitudeDeg, 0f, "micro-motion alive by default");
      Assert.LessOrEqual(sway.amplitudeDeg, 2f, "readable life, zero distraction");
      Assert.AreEqual(0, root.GetComponentsInChildren<Collider>(true).Length, "sway adds no physics");
    } finally { Object.DestroyImmediate(root); }
  }
}
