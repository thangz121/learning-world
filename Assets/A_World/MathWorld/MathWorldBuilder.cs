// A_World/MathWorld/MathWorldBuilder.cs — Agent A (World & Visual), P3.0.1 S2 + S3.
// Code-builds the Math world: ground, boundary, entry/lobby/zone pads,
// return arch visual, sun, NavMesh bake, return-gate wiring (S2: ENTERABLE,
// TRAVERSABLE, RETURNABLE) plus the S3 playable skeleton: lobby abacus,
// Counting Garden (beds + 1/2/3 cube pedestals + the "one" quest target),
// Number Bridge (stream + planks + rails + number blocks), warm-tan paths
// entry->lobby->areas->return. Deterministic (no Random): every build
// identical. Same protocol as MarketBuilder: bake AFTER all geometry,
// visual-only bits stripped + ignored so feet pass.
// S3 adds identity/content; Phase 3.1 opens learning via MathLearningEntries
// (stable IDs — never coordinates). C# 9.0 only.
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class MathWorldBuilder : MonoBehaviour {
  // Pilot spatial offset (firewall §3: implementation detail, NOT a contract —
  // gameplay resolves markers/positions dynamically, never this constant).
  public static readonly Vector3 WorldOffset = new Vector3(60f, 0f, 0f);

  // S3: Tess lobby anchor (local). The presenter resolves it through its
  // SpawnPosition (Lead sets world = offset + this); tests/world code never
  // hard-code the constant outside this file + GameInstaller wiring.
  public static readonly Vector3 HostAnchorLocal = new Vector3(2.2f, 0f, 0.8f);

  // 3.0.1.1 shared travel framing (Lead contract): world-space entry/host
  // anchors for the arrival beat (camera frames the host on warp-in, then
  // Follow resumes). Strings/coordinates live here (world-owned), Bootstrap
  // only reads them — no Math literals leak into shared contracts.
  // 3.0.2: warp lands at the LOBBY CENTER (user round: spawn mid-world), so
  // EntryWorldPos IS the center; the entry board/path at z=-8 stays the
  // "door you came through" backdrop.
  public static Vector3 EntryWorldPos {
    get { return WorldOffset + new Vector3(0f, 0f, 0f); }
  }
  public static Vector3 HostWorldPos {
    get { return WorldOffset + HostAnchorLocal; }
  }
  // Click bounds for the enlarged world (38m ground vs 28m Main): Bootstrap
  // widens the router on travel-in and restores Main values on return.
  public const float BoundX = 20f;
  public const float BoundZ = 20f;

  // S3: quest-target Interactables created during Build (Lead binds the bus;
  // Playable skeleton: the "one" cube in Counting Garden).
  public readonly List<Interactable> CountingObjects = new List<Interactable>();

  // S3B: bloom root for the math_bloom consumer (children start HIDDEN;
  // MathBloomDisplay flips them on math_counting completion).
  public Transform BloomRoot { get; private set; }

  // Injection boundary (GameInstaller calls this on MathScene load; services
  // come from the shared Core, never newed here).
  public void Build(IWorldNavService nav, Transform playerT) {
    BuildContent(transform);
    BuildNavMesh(transform);
    BindReturnGate(transform, nav, playerT);
  }

  // S3B: content-only entry (geometry + markers + Interactables, NO NavMesh
  // bake, NO gate binding) so EditMode can functionally review world
  // composition headlessly (CT-P41). Production behavior unchanged: Build
  // calls this in the same order as before.
  public void BuildContent(Transform root) {
    BuildGround(root);
    BuildBoundary(root);
    BuildPads(root);
    BuildLobby(root);          // S3: abacus (arrival zone stays open)
    BuildCountingGarden(root); // S3: Area A (west)
    BuildNumberBridge(root);   // S3: Area B (east)
    BuildPaths(root);          // S3: entry->lobby->areas->return readability
    BuildDecor(root);          // S4: playground dressing (collider-free, deterministic)
    BuildEntryBoard(root);     // 3.0.1.1: entry identity (ignored bake, above headroom)
    BuildNumberRow(root);      // 3.0.1.1: counting pads 1..5 (Number Hunt motif, walkable)
    BuildShapeTrio(root);      // 3.0.1.1: Blocks shape language at the garden mouth
    BuildBridgeDiscs(root);    // 3.0.1.1: gold rhythm discs over the bridge (visual-only)
    BuildNature(root);         // 3.0.2: Quaternius CC0 nature (GitHub decor round)
    BuildReturnArch(root);
    BuildSun(root);
  }

  void BuildGround(Transform parent) {
    // 3.0.2: main-yard scale (user round: 28m felt tiny next to Main) — 38m
    // across, and a true grass green (the pale mint read as aqua/skeleton).
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ground.name = "MathGround";
    ground.transform.SetParent(parent);
    ground.transform.localPosition = new Vector3(0f, -0.05f, 0f);
    ground.transform.localScale = new Vector3(19f, 0.05f, 19f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.36f, 0.62f, 0.34f));
    // Meadow value-breakup (walkable flats, tops above ground, below paths).
    Flat(parent, "MathMeadowW", new Vector3(-6f, 0.02f, 6f), new Vector3(8f, 0.02f, 5f));
    GameObject meadowW = parent.Find("MathMeadowW").gameObject;
    meadowW.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.30f, 0.55f, 0.30f));
    Flat(parent, "MathMeadowE", new Vector3(7f, 0.02f, 5f), new Vector3(7f, 0.02f, 4.5f));
    GameObject meadowE = parent.Find("MathMeadowE").gameObject;
    meadowE.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.45f, 0.62f, 0.30f));
  }

  void BuildBoundary(Transform parent) {
    // Hedge ring r=18 (feet stay in; trees live inside now, backdrops outside).
    for (int i = 0; i < 16; i++) {
      float ang = i * 22.5f * Mathf.Deg2Rad;
      Vector3 p = new Vector3(Mathf.Cos(ang) * 18f, 0.3f, Mathf.Sin(ang) * 18f);
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "MathHedge";
      bush.transform.SetParent(parent);
      bush.transform.localPosition = p;
      bush.transform.localScale = new Vector3(2.2f, 1.2f, 2.2f);
      bush.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.24f, 0.55f, 0.30f));
    }
  }

  void BuildPads(Transform parent) {
    // 3.0.1.1 z-layering (user round: medallion/paths flickered — pad tops and
    // path tops were coplanar at 0.03): pads settle at top 0.015, paths ride at
    // top 0.047 (32mm apart, never coplanar, both still step-free for feet).
    // 3.0.2: main-yard scale — garden/bridge ride out to their new districts.
    Pad(parent, "MathLobbyPad", new Vector3(0f, -0.005f, 0f), 4f, new Color(0.55f, 0.68f, 0.88f));
    Pad(parent, "MathEntryPad", new Vector3(0f, -0.005f, -8f), 1.5f, new Color(0.80f, 0.68f, 0.48f));
    Pad(parent, "CountingGardenPad", new Vector3(-10.5f, -0.005f, 3f), 3f, new Color(0.55f, 0.75f, 0.55f));
    Pad(parent, "NumberBridgePad", new Vector3(10.5f, -0.005f, -3f), 3f, new Color(0.90f, 0.80f, 0.55f));
    // Stable learning-entry markers (Phase 3.1 resolves these by ID, never by
    // coordinates — see MathLearningEntries). Parented under the shell's
    // LearningEntryRoot when present, else the world root.
    Transform entryRoot = parent.Find("LearningEntryRoot");
    if (entryRoot == null) entryRoot = parent;
    Mark(entryRoot, MathLearningEntries.Lobby, new Vector3(0f, 0f, 0f));
    Mark(entryRoot, MathLearningEntries.CountingGarden, new Vector3(-10.5f, 0f, 3f));
    Mark(entryRoot, MathLearningEntries.NumberBridge, new Vector3(10.5f, 0f, -3f));
  }

  // ---- S3 playable skeleton ---------------------------------------------------
  // Lobby identity: abacus on the SOUTH side (arrival corridor entry->lobby
  // runs up x=0 and must stay furniture-free; Tess anchors EAST at
  // HostAnchorLocal). Counting Garden (west) + Number Bridge (east) carry the
  // 1/2/3 cube shape language; the gold "One" cube is the quest target
  // (Interactable "one" — Lead binds the bus, quest stages via director).

  static readonly Color AbacusBlue = new Color(0.25f, 0.45f, 0.85f);
  static readonly Color QuestGold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color SoilBrown = new Color(0.45f, 0.30f, 0.16f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);

  void BuildLobby(Transform parent) {
    Vector3 p = new Vector3(-2.6f, 0f, -1.2f);
    Box(parent, "MathAbacusPostL", p + new Vector3(-0.7f, 0.6f, 0f),
      new Vector3(0.14f, 1.2f, 0.14f), AbacusBlue);
    Box(parent, "MathAbacusPostR", p + new Vector3(0.7f, 0.6f, 0f),
      new Vector3(0.14f, 1.2f, 0.14f), AbacusBlue);
    Box(parent, "MathAbacusBarT", p + new Vector3(0f, 1.15f, 0f),
      new Vector3(1.55f, 0.12f, 0.12f), AbacusBlue);
    Box(parent, "MathAbacusBarB", p + new Vector3(0f, 0.15f, 0f),
      new Vector3(1.55f, 0.12f, 0.12f), AbacusBlue);
    for (int r = 0; r < 2; r++) {
      float y = 0.55f + r * 0.35f;
      GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      rod.name = "MathAbacusRod" + r;
      rod.transform.SetParent(parent);
      rod.transform.localPosition = p + new Vector3(0f, y, 0f);
      rod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
      rod.transform.localScale = new Vector3(0.06f, 1.4f, 0.06f);
      rod.GetComponent<Renderer>().sharedMaterial = Lit(SoilBrown);
      for (int i = 0; i < 3; i++) {
        GameObject bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bead.name = "MathBead" + r + "_" + i;
        bead.transform.SetParent(parent);
        bead.transform.localPosition = p + new Vector3(-0.35f + i * 0.35f, y, 0f);
        bead.transform.localScale = new Vector3(0.20f, 0.20f, 0.20f);
        bead.GetComponent<Renderer>().sharedMaterial =
          Lit(i % 2 == 0 ? QuestGold : AbacusBlue);
        StripCollider(bead);
      }
    }
  }

  void BuildCountingGarden(Transform parent) {
    // Soil beds (baked obstacles; pads stay walkable around them).
    // 3.0.2: garden district rides out to (-10.5, 3) — same layout, shifted.
    Box(parent, "GardenBedW", new Vector3(-11.3f, 0.15f, 3.8f),
      new Vector3(1.2f, 0.3f, 0.6f), SoilBrown);
    Box(parent, "GardenBedE", new Vector3(-9.7f, 0.15f, 3.8f),
      new Vector3(1.2f, 0.3f, 0.6f), SoilBrown);
    // Counting pedestals: 2-cube and 3-cube stacks (decor) + the gold One
    // (quest target) on the center pedestal.
    Pedestal(parent, "GardenPedestal2", new Vector3(-11.4f, 0f, 2.0f), 2, AbacusBlue);
    Pedestal(parent, "GardenPedestal3", new Vector3(-9.6f, 0f, 2.0f), 3, AbacusBlue);
    Box(parent, "GardenPedestal1", new Vector3(-10.5f, 0.25f, 2.0f),
      new Vector3(0.5f, 0.5f, 0.5f), SoilBrown);
    MakeCountable(parent, "MathOneCube", new Vector3(-10.5f, 0.7f, 2.0f), 0.4f, QuestGold, "one");
    BuildBloomRoot(parent);
  }

  // S3B math_bloom visual: three gold blooms around the garden pad, hidden
  // until the quest completes (consumer flips them; deterministic placement).
  void BuildBloomRoot(Transform parent) {
    GameObject root = new GameObject("MathBloomRoot");
    root.transform.SetParent(parent);
    root.transform.localPosition = new Vector3(-10.5f, 0f, 3f);
    for (int i = 0; i < 3; i++) {
      float ang = (30f + i * 120f) * Mathf.Deg2Rad;
      Vector3 p = new Vector3(Mathf.Cos(ang) * 2.2f, 0.35f, Mathf.Sin(ang) * 2.2f);
      GameObject bloom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bloom.name = "MathBloom" + i;
      bloom.transform.SetParent(root.transform, false);
      bloom.transform.localPosition = p;
      bloom.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
      bloom.GetComponent<Renderer>().sharedMaterial = Lit(QuestGold);
      StripCollider(bloom);
      bloom.SetActive(false);
    }
    root.SetActive(true); // root stays active (children carry the hidden state)
    BloomRoot = root.transform;
  }

  void BuildNumberBridge(Transform parent) {
    // Pebble stream (flat visual, walkable) + plank crossing + side rails
    // (rails bake; 1.8m corridor stays open) + number blocks (decor).
    // 3.0.2: bridge district rides out to (10.5, -3) + stream reads WATER.
    GameObject stream = GameObject.CreatePrimitive(PrimitiveType.Cube);
    stream.name = "BridgeStream";
    stream.transform.SetParent(parent);
    stream.transform.localPosition = new Vector3(10.5f, 0.012f, -3f);
    stream.transform.localScale = new Vector3(5f, 0.024f, 1.4f);
    stream.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.30f, 0.56f, 0.86f));
    StripCollider(stream);
    for (int i = 0; i < 5; i++) {
      Box(parent, "BridgePlank" + i, new Vector3(10.5f, 0.02f, -3.5f + i * 0.25f),
        new Vector3(1.6f, 0.04f, 0.2f), SoilBrown);
    }
    Box(parent, "BridgeRailW", new Vector3(9.6f, 0.35f, -3f),
      new Vector3(0.12f, 0.7f, 1.6f), AbacusBlue);
    Box(parent, "BridgeRailE", new Vector3(11.4f, 0.35f, -3f),
      new Vector3(0.12f, 0.7f, 1.6f), AbacusBlue);
    Box(parent, "BridgeBlockA", new Vector3(11.7f, 0.13f, -1.8f),
      new Vector3(0.26f, 0.26f, 0.26f), AbacusBlue);
    Box(parent, "BridgeBlockB", new Vector3(11.7f, 0.39f, -1.8f),
      new Vector3(0.26f, 0.26f, 0.26f), QuestGold);
    Box(parent, "BridgeBlockC", new Vector3(9.3f, 0.13f, -4.2f),
      new Vector3(0.26f, 0.26f, 0.26f), QuestGold);
  }

  void BuildPaths(Transform parent) {
    // Warm-tan readability strips (walkable, like S2 roads): entry->lobby,
    // lobby->garden, lobby->bridge, lobby->return. Arrival corridor up x=0
    // never narrows below 1.6m. Tops ride 32mm above the pads (z-fight fix).
    // 3.0.2: district-scale lengths (entry z=-8, garden (-10.5,3),
    // bridge (10.5,-3), return z=8).
    Flat(parent, "MathPathEntry", new Vector3(0f, 0.032f, -4f), new Vector3(1.6f, 0.03f, 8.4f));
    Flat(parent, "MathPathReturn", new Vector3(0f, 0.032f, 4f), new Vector3(1.6f, 0.03f, 8.4f));
    YawFlat(parent, "MathPathGarden", new Vector3(-5.25f, 0.032f, 1.5f), 1.6f, 11f);
    YawFlat(parent, "MathPathBridge", new Vector3(5.25f, 0.032f, -1.5f), 1.6f, 11f);
  }

  void Pedestal(Transform parent, string name, Vector3 basePos, int cubes, Color color) {
    Box(parent, name, basePos + new Vector3(0f, 0.25f, 0f),
      new Vector3(0.5f, 0.5f, 0.5f), SoilBrown);
    for (int i = 0; i < cubes; i++) {
      GameObject cube = Box(parent, name + "Cube" + i,
        basePos + new Vector3(0f, 0.63f + i * 0.26f, 0f),
        new Vector3(0.26f, 0.26f, 0.26f), color);
      StripCollider(cube);
    }
  }

  // Quest-target Interactable (Lead binds the bus after Build; ParseIds runs
  // here because Awake already fired before wordId was assigned).
  void MakeCountable(Transform parent, string name, Vector3 localPos, float size, Color color, string word) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent);
    go.transform.localPosition = localPos;
    go.transform.localScale = new Vector3(size, size, size);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    Interactable inter = go.AddComponent<Interactable>();
    inter.wordId = word;
    inter.interactionDistance = 2.5f;
    inter.ParseIds();
    go.AddComponent<MathBeacon>(); // S4: readability motion (dies with the cube)
    CountingObjects.Add(inter);
  }

  static void Flat(Transform parent, string name, Vector3 localPos, Vector3 scale) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent);
    go.transform.localPosition = localPos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(PathTan);
  }

  static void YawFlat(Transform parent, string name, Vector3 localPos, float width, float len) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent);
    go.transform.localPosition = localPos;
    go.transform.localScale = new Vector3(width, 0.03f, len);
    Vector3 dir = new Vector3(localPos.x, 0f, localPos.z);
    if (dir.sqrMagnitude > 0.001f)
      go.transform.localRotation = Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, 0f);
    go.GetComponent<Renderer>().sharedMaterial = Lit(PathTan);
  }

  // ---- S4 playground dressing ---------------------------------------------------
  // Deterministic hand placements (no Random): lobby pots, entry tufts,
  // pebbles, garden mini-blooms, bridge reeds, backdrop blobs, return flowers,
  // bridge chevrons. ALL collider-free (visual-only dressing can never block
  // feet or eat clicks); corridor clearance verified by CT-P42. Bakes as
  // harmless static geometry (pre-bake like everything else in this builder).
  static readonly Color LeafGreen = new Color(0.28f, 0.60f, 0.30f);
  static readonly Color PebbleGrey = new Color(0.60f, 0.60f, 0.62f);
  static readonly Color BloomPink = new Color(0.95f, 0.55f, 0.65f);
  static readonly Color BloomWhite = new Color(0.96f, 0.95f, 0.90f);
  static readonly Color ReedGreen = new Color(0.22f, 0.52f, 0.28f);

  void BuildDecor(Transform parent) {
    // Lobby pots (clear of the x=0 return corridor and Tess at (2.2,0.8)).
    DressPot(parent, "MathPotW", new Vector3(-1.8f, 0f, 3.2f));
    DressPot(parent, "MathPotE", new Vector3(1.8f, 0f, 3.4f));
    // Entry tufts (clear of the x=0 entry corridor).
    DressTuft(parent, "MathTuftW", new Vector3(-1.5f, 0f, -4.2f));
    DressTuft(parent, "MathTuftE", new Vector3(1.5f, 0f, -4.5f));
    // Pebbles (clear of all four corridors — see CT-P42).
    DressPebble(parent, "MathPebble0", new Vector3(-3f, 0f, -3f));
    DressPebble(parent, "MathPebble1", new Vector3(3f, 0f, -3f));
    DressPebble(parent, "MathPebble2", new Vector3(-4f, 0f, 4f));
    DressPebble(parent, "MathPebble3", new Vector3(4f, 0f, 4.5f));
    // Garden mini-blooms (inside the pad rim, off beds and pedestals).
    DressBloom(parent, "MathBloomFB0", new Vector3(-12f, 0f, 4.5f), BloomPink);
    DressBloom(parent, "MathBloomFB1", new Vector3(-9f, 0f, 4.5f), BloomWhite);
    DressBloom(parent, "MathBloomFB2", new Vector3(-12f, 0f, 1.5f), BloomWhite);
    DressBloom(parent, "MathBloomFB3", new Vector3(-9f, 0f, 1.5f), BloomPink);
    // Bridge reeds (off the planks and rails).
    DressReeds(parent, "MathReedsW", new Vector3(4.6f, 0f, -0.2f));
    DressReeds(parent, "MathReedsE", new Vector3(9.4f, 0f, -3.9f));
    // Bridge chevrons: dotted guide onto the planks from the lobby side.
    Flat(parent, "MathChevron0", new Vector3(2.6f, 0.032f, -0.75f), new Vector3(0.3f, 0.03f, 0.3f));
    Flat(parent, "MathChevron1", new Vector3(3.2f, 0.032f, -0.92f), new Vector3(0.3f, 0.03f, 0.3f));
    Flat(parent, "MathChevron2", new Vector3(3.8f, 0.032f, -1.09f), new Vector3(0.3f, 0.03f, 0.3f));
    // Return flowers (collider-free, clear of the 1.6m trigger).
    DressBloom(parent, "MathReturnFlowerL", new Vector3(-1.9f, 0f, 8.6f), BloomPink);
    DressBloom(parent, "MathReturnFlowerR", new Vector3(1.9f, 0f, 8.6f), BloomWhite);
    // Backdrop blobs outside the hedge ring (depth behind the boundary).
    DressBackdrop(parent, "MathBackdropN", new Vector3(-5f, 0f, 21f), 2.4f);
    DressBackdrop(parent, "MathBackdropS", new Vector3(6f, 0f, -21f), 2.6f);
    DressBackdrop(parent, "MathBackdropE", new Vector3(24f, 0f, 4f), 2.2f);
  }

  void DressPot(Transform parent, string name, Vector3 pos) {
    GameObject pot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pot.name = name;
    pot.transform.SetParent(parent);
    pot.transform.localPosition = pos + new Vector3(0f, 0.2f, 0f);
    pot.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
    pot.GetComponent<Renderer>().sharedMaterial = Lit(SoilBrown);
    StripCollider(pot);
    GameObject bloom = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    bloom.name = name + "Bloom";
    bloom.transform.SetParent(parent);
    bloom.transform.localPosition = pos + new Vector3(0f, 0.55f, 0f);
    bloom.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
    bloom.GetComponent<Renderer>().sharedMaterial = Lit(BloomPink);
    StripCollider(bloom);
  }

  void DressTuft(Transform parent, string name, Vector3 pos) {
    GameObject tuft = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    tuft.name = name;
    tuft.transform.SetParent(parent);
    tuft.transform.localPosition = pos + new Vector3(0f, 0.12f, 0f);
    tuft.transform.localScale = new Vector3(0.7f, 0.25f, 0.7f);
    tuft.GetComponent<Renderer>().sharedMaterial = Lit(LeafGreen);
    StripCollider(tuft);
  }

  void DressPebble(Transform parent, string name, Vector3 pos) {
    GameObject pebble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    pebble.name = name;
    pebble.transform.SetParent(parent);
    pebble.transform.localPosition = pos + new Vector3(0f, 0.06f, 0f);
    pebble.transform.localScale = new Vector3(0.22f, 0.12f, 0.26f);
    pebble.GetComponent<Renderer>().sharedMaterial = Lit(PebbleGrey);
    StripCollider(pebble);
  }

  void DressBloom(Transform parent, string name, Vector3 pos, Color color) {
    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = name + "Stem";
    stem.transform.SetParent(parent);
    stem.transform.localPosition = pos + new Vector3(0f, 0.10f, 0f);
    stem.transform.localScale = new Vector3(0.03f, 0.10f, 0.03f);
    stem.GetComponent<Renderer>().sharedMaterial = Lit(LeafGreen);
    StripCollider(stem);
    GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    head.name = name + "Head";
    head.transform.SetParent(parent);
    head.transform.localPosition = pos + new Vector3(0f, 0.19f, 0f);
    head.transform.localScale = new Vector3(0.09f, 0.09f, 0.09f);
    head.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(head);
  }

  void DressReeds(Transform parent, string name, Vector3 pos) {
    for (int i = 0; i < 2; i++) {
      GameObject reed = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      reed.name = name + i;
      reed.transform.SetParent(parent);
      reed.transform.localPosition = pos + new Vector3(i * 0.25f, 0.35f, 0f);
      reed.transform.localScale = new Vector3(0.06f, 0.35f, 0.06f);
      reed.GetComponent<Renderer>().sharedMaterial = Lit(ReedGreen);
      StripCollider(reed);
    }
  }

  void DressBackdrop(Transform parent, string name, Vector3 pos, float s) {
    GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    blob.name = name;
    blob.transform.SetParent(parent);
    blob.transform.localPosition = pos + new Vector3(0f, 0.35f * s, 0f);
    blob.transform.localScale = new Vector3(2.0f * s, 0.7f * s, 2.0f * s);
    blob.GetComponent<Renderer>().sharedMaterial = Lit(ReedGreen);
    StripCollider(blob);
  }

  // ---- 3.0.1.1 Math identity (skeleton-complete: the world must READ math) --
  // All pieces are deterministic + corridor-safe (CT-P42 pins): baked pieces
  // sit off the walking lines with colliders (feet go around); overhead/flat
  // pieces are collider-free + ignored by the bake (feet pass under/over).

  void BuildEntryBoard(Transform parent) {
    // Entry identity gate: two blue posts flanking the arrival corridor +
    // gold beam with abacus-bead cubes (same language as the hub Math gate).
    // Posts stand at +-1.1 (baked corridor stays 1m+); beam + beads ride at
    // 2m+, ignored by the bake; all pieces are click-through (no colliders)
    // so entry taps always reach the corridor. 3.0.2: rides at the new
    // entry plaza (z=-7, entry pad z=-8).
    GameObject postL = Box(parent, "MathEntryPostL", new Vector3(-1.1f, 1.0f, -7.0f),
      new Vector3(0.14f, 2.0f, 0.14f), AbacusBlue);
    GameObject postR = Box(parent, "MathEntryPostR", new Vector3(1.1f, 1.0f, -7.0f),
      new Vector3(0.14f, 2.0f, 0.14f), AbacusBlue);
    GameObject beamTop = Box(parent, "MathEntryBeam", new Vector3(0f, 2.06f, -7.0f),
      new Vector3(2.4f, 0.12f, 0.12f), AbacusBlue);
    StripCollider(beamTop);
    IgnoreFromBuild(beamTop);
    for (int i = 0; i < 3; i++) {
      GameObject bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bead.name = "MathEntryBead" + i;
      bead.transform.SetParent(parent);
      bead.transform.localPosition = new Vector3(-0.35f + i * 0.35f, 2.2f, -7.0f);
      bead.transform.localScale = new Vector3(0.20f, 0.20f, 0.20f);
      bead.GetComponent<Renderer>().sharedMaterial = Lit(i % 2 == 0 ? QuestGold : AbacusBlue);
      StripCollider(bead);
      IgnoreFromBuild(bead);
    }
  }

  void BuildNumberRow(Transform parent) {
    // Counting pads 1..5 (Number Hunt motif, GitHub ADAPT: ynsemre1/learn-math
    // + Frog-Adventure — find-the-number play, 3D embodied): flat walkable
    // pads north of the garden path, each carrying its count in gold pips.
    // Pads are flat (bake-neutral); pips are flat + collider-free. Computed
    // from the lobby->garden axis (no magic numbers): 1m steps along the
    // axis, 1.6m north of it — 1.25m+ off the walking line (CT-P43 pins).
    Vector3 axis = new Vector3(-10.5f, 0f, 3f);
    Vector3 dir = axis.normalized;
    Vector3 north = new Vector3(dir.z, 0f, -dir.x);
    for (int i = 0; i < 5; i++) {
      Vector3 padPos = dir * (2f + i * 1f) + north * 1.6f;
      padPos.y = 0.032f;
      Flat(parent, "MathNumPad" + (i + 1), padPos, new Vector3(0.7f, 0.03f, 0.7f));
      for (int p = 0; p <= i; p++) {
        float px = padPos.x - 0.18f + (p % 3) * 0.18f;
        float pz = padPos.z - 0.12f + (p / 3) * 0.24f;
        GameObject pip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pip.name = "MathNumPip" + i + "_" + p;
        pip.transform.SetParent(parent);
        pip.transform.localPosition = new Vector3(px, 0.055f, pz);
        pip.transform.localScale = new Vector3(0.11f, 0.02f, 0.11f);
        pip.GetComponent<Renderer>().sharedMaterial = Lit(QuestGold);
        StripCollider(pip);
      }
    }
  }

  void BuildShapeTrio(Transform parent) {
    // Blocks shape language at the garden mouth: cube + sphere + cylinder —
    // the Math gate motif repeated at child scale. Click-through visuals (no
    // colliders, like all Math dressing); the bake still routes feet around
    // the solid meshes. Positions clear the garden walking line (CT-P42).
    // 3.0.2: rides out with the garden district.
    GameObject cube = Box(parent, "MathShapeCube", new Vector3(-8.7f, 0.25f, 0.6f),
      new Vector3(0.5f, 0.5f, 0.5f), AbacusBlue);
    GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    ball.name = "MathShapeBall";
    ball.transform.SetParent(parent);
    ball.transform.localPosition = new Vector3(-7.9f, 0.25f, 0.5f);
    ball.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
    ball.GetComponent<Renderer>().sharedMaterial = Lit(QuestGold);
    StripCollider(ball);
    GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pillar.name = "MathShapePillar";
    pillar.transform.SetParent(parent);
    pillar.transform.localPosition = new Vector3(-8.3f, 0.25f, 1.2f);
    pillar.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
    pillar.GetComponent<Renderer>().sharedMaterial = Lit(SoilBrown);
    StripCollider(pillar);
  }

  void BuildBridgeDiscs(Transform parent) {
    // Gold rhythm discs floating over the bridge walk (Star-Race rhythm motif,
    // visual-only): above headroom, ignored by the bake, no colliders.
    // 3.0.2: rides out with the bridge district.
    for (int i = 0; i < 3; i++) {
      GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      disc.name = "MathBridgeDisc" + i;
      disc.transform.SetParent(parent);
      disc.transform.localPosition = new Vector3(10.5f, 2.4f, -3.6f + i * 0.6f);
      disc.transform.localScale = new Vector3(0.5f, 0.03f, 0.5f);
      disc.GetComponent<Renderer>().sharedMaterial = Lit(QuestGold);
      StripCollider(disc);
      IgnoreFromBuild(disc);
    }
  }

  // ---- 3.0.2 GitHub nature (Quaternius CC0, provenance in NatureKit README)
  // Real low-poly trees/bushes/rocks/grass replace the skeleton blobs.
  // Imported meshes carry NO colliders (click-through); trunks/rocks still
  // route feet through the BAKED meshes. All placements deterministic and
  // 1m+ off every walking line (CT-P43 pins). Names carry the MathQ prefix.
  void BuildNature(Transform parent) {
    PlaceQ(parent, "Tree1", "MathQTreeN", new Vector3(-14f, 0f, 7f), 20f, 1.2f);
    PlaceQ(parent, "Tree2", "MathQTreeE", new Vector3(14f, 0f, 6f), 140f, 1.3f);
    PlaceQ(parent, "Tree3", "MathQTreeW", new Vector3(-15f, 0f, -6f), 75f, 1.1f);
    PlaceQ(parent, "Tree4", "MathQTreeS", new Vector3(15f, 0f, -7f), 200f, 1.2f);
    PlaceQ(parent, "Tree2", "MathQTreeL", new Vector3(-3f, 0f, 12f), 300f, 1.0f);
    PlaceQ(parent, "Tree1", "MathQTreeR", new Vector3(5f, 0f, 13f), 10f, 1.1f);
    PlaceQ(parent, "Bush1", "MathQBushA", new Vector3(-12.5f, 0f, 5.5f), 0f, 1.0f);
    PlaceQ(parent, "Bush2", "MathQBushB", new Vector3(12f, 0f, 4.5f), 90f, 1.0f);
    PlaceQ(parent, "Bush3", "MathQBushC", new Vector3(-13f, 0f, -3f), 45f, 1.0f);
    PlaceQ(parent, "Bush1", "MathQBushD", new Vector3(12.5f, 0f, -5.5f), 180f, 1.0f);
    PlaceQ(parent, "Rock1", "MathQRockA", new Vector3(-9f, 0f, -4.5f), 30f, 1.0f);
    PlaceQ(parent, "Rock2", "MathQRockB", new Vector3(9.5f, 0f, 4.8f), 120f, 1.0f);
    PlaceQ(parent, "Rock3", "MathQRockC", new Vector3(2f, 0f, -9.5f), 210f, 1.0f);
    PlaceQ(parent, "Grass1", "MathQGrassA", new Vector3(-2f, 0f, -3.5f), 0f, 1.0f);
    PlaceQ(parent, "Grass2", "MathQGrassB", new Vector3(3f, 0f, 3.8f), 60f, 1.0f);
    PlaceQ(parent, "Grass3", "MathQGrassC", new Vector3(-6.5f, 0f, -1.5f), 120f, 1.0f);
    PlaceQ(parent, "Grass1", "MathQGrassD", new Vector3(6.8f, 0f, 0.8f), 200f, 1.0f);
    PlaceQ(parent, "Grass2", "MathQGrassE", new Vector3(-11f, 0f, -0.5f), 280f, 1.0f);
    PlaceQ(parent, "Grass3", "MathQGrassF", new Vector3(11.5f, 0f, 0.5f), 340f, 1.0f);
  }

  static void PlaceQ(Transform parent, string fbx, string goName, Vector3 pos, float yaw, float s) {
    GameObject go = NatureKit.Place(parent, fbx, pos, yaw, s);
    if (go != null) go.name = goName;
  }

  void BuildReturnArch(Transform parent) {
    // S4: gold disc under the arch (walkable) so the return reads as a place.
    // 3.0.1.1: disc rides ABOVE the return path (no coplanar flicker).
    Flat(parent, "MathReturnDisc", new Vector3(0f, 0.05f, 8f), new Vector3(3f, 0.024f, 3f));
    // 3.0.1.1 passability (user round: the return door blocked like the old
    // Thinking gate — the 1.5m pillar gap baked down to a ~0.5m slot after
    // agent erosion): pillars move to +-1.25 (2.2m clear, ~1.2m after erosion)
    // so feet walk through to the trigger; beam widens to match.
    Color gold = new Color(0.98f, 0.78f, 0.25f);
    GameObject a = Box(parent, "MathReturnA", new Vector3(-1.25f, 0.9f, 8f),
      new Vector3(0.3f, 1.8f, 0.3f), gold);
    GameObject b = Box(parent, "MathReturnB", new Vector3(1.25f, 0.9f, 8f),
      new Vector3(0.3f, 1.8f, 0.3f), gold);
    GameObject beam = Box(parent, "MathReturnBeam", new Vector3(0f, 1.95f, 8f),
      new Vector3(2.8f, 0.3f, 0.3f), gold);
    // Pillars stay baked (foot-level: feet walk AROUND them through the 2.2m
    // gap); only the overhead beam is ignored (headroom rule).
    IgnoreFromBuild(beam);
  }

  void BuildSun(Transform parent) {
    // Math owns its sun (Main sun deactivates with Main; one sun per active
    // world, no duplicate lighting, same warm universe).
    GameObject sun = new GameObject("MathSun");
    sun.transform.SetParent(parent);
    sun.transform.rotation = Quaternion.Euler(68f, -35f, 0f);
    Light light = sun.AddComponent<Light>();
    light.type = LightType.Directional;
    light.color = new Color(1f, 0.96f, 0.88f);
    light.intensity = 0.95f;
    light.shadows = LightShadows.Soft;
    light.shadowStrength = 0.65f;
  }

  void BuildNavMesh(Transform parent) {
    GameObject navGo = new GameObject("MathNavMesh");
    navGo.transform.SetParent(parent);
    NavMeshSurface surface = navGo.AddComponent<NavMeshSurface>();
    surface.collectObjects = CollectObjects.All;
    surface.BuildNavMesh();
  }

  void BindReturnGate(Transform parent, IWorldNavService nav, Transform playerT) {
    GameObject gateGo = new GameObject("MathReturnGate");
    gateGo.transform.SetParent(parent);
    gateGo.transform.position = parent.TransformPoint(new Vector3(0f, 0f, 8f));
    SubjectGate gate = gateGo.AddComponent<SubjectGate>();
    // 3.0.1.1: fire earlier (1.6m) so the trigger lands while feet are still
    // threading the widened arch — no exact-center arrival required.
    gate.fireRadius = 1.6f;
    gate.Bind(nav, SubjectIds.Main, true, playerT);
  }

  static void Pad(Transform parent, string name, Vector3 localPos, float r, Color color) {
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = name;
    pad.transform.SetParent(parent);
    pad.transform.localPosition = localPos;
    pad.transform.localScale = new Vector3(r, 0.02f, r);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(color);
  }

  static void Mark(Transform parent, string id, Vector3 localPos) {
    GameObject m = new GameObject(id);
    m.transform.SetParent(parent);
    m.transform.localPosition = localPos;
  }

  static GameObject Box(Transform parent, string goName, Vector3 localPos, Vector3 scale, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = goName;
    go.transform.SetParent(parent);
    go.transform.localPosition = localPos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(go);
    return go;
  }

  static void StripCollider(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
      // CursorPresenter pattern: DestroyNow is edit-mode safe (CT-P41 builds
      // content headlessly) and a plain destroy in players.
      if (c != null) CharacterPresentation.DestroyNow(c);
    } catch (System.Exception) { }
  }

  static void IgnoreFromBuild(GameObject go) {
    if (go == null) return;
    try {
      NavMeshModifier mod = go.AddComponent<NavMeshModifier>();
      mod.ignoreFromBuild = true;
    } catch (System.Exception) { }
  }

  static readonly System.Collections.Generic.Dictionary<string, Material> _litCache =
    new System.Collections.Generic.Dictionary<string, Material>();

  static Material Lit(Color color) {
    string key = color.r.ToString("F2") + "," + color.g.ToString("F2") + "," + color.b.ToString("F2");
    Material cached;
    if (_litCache.TryGetValue(key, out cached) && cached != null) return cached;
    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
    mat.SetColor("_BaseColor", color);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    mat.enableInstancing = true;
    _litCache[key] = mat;
    return mat;
  }
}
