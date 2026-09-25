// A_World/MathWorld/MathWorldBuilder.cs — Agent A (World & Visual), P3.0.1 S2/S3
// + P3.0.1.1 B1/B1R composition.
// Code-builds the Math world: ground + boundary, entry/return arches, hub
// courtyard with the Counting Frame landmark and Tess nook, the fenced
// Counting Garden (Kenney CC0 fence + crops), the Number Bridge (Kenney CC0
// bridge modules over a carved brook) with a far-bank stone clearing, the
// meadow loop path, and a composed rim. Deterministic (no Random): every
// build identical. Same protocol as MarketBuilder: bake AFTER all geometry,
// visual-only bits stripped/ignored so feet pass; brook carves run after the
// bake. C# 9.0 only.
//
// B1R notes (user round: floating trees, cluttered hub, world too small):
//   - ground r19 -> r26 + dark skirt; every object lives inside r25;
//   - hub decluttered (no pot cluster, no mouth stones, no number-row pads);
//   - Kenney Food/Nature kits (CC0) replace primitive crops/fence/bridge;
//   - Quaternius Math trees removed (white-canopy bug); Kenney props carry
//     the palette through PropKit.
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
public class MathWorldBuilder : MonoBehaviour {
  // Pilot spatial offset (firewall §3: implementation detail, NOT a contract —
  // gameplay resolves markers/positions dynamically, never this constant).
  public static readonly Vector3 WorldOffset = new Vector3(60f, 0f, 0f);

  // Tess lobby anchor (local). Lead sets world = offset + this.
  public static readonly Vector3 HostAnchorLocal = new Vector3(3.4f, 0f, 1.2f);

  // Shared travel framing (Lead contract): world-space entry/host anchors for
  // the arrival beat (camera frames the host on warp-in, then Follow resumes).
  public static Vector3 EntryWorldPos {
    get { return WorldOffset + new Vector3(0f, 0f, 0f); }
  }
  public static Vector3 HostWorldPos {
    get { return WorldOffset + HostAnchorLocal; }
  }
  // Click bounds for the enlarged world (52m ground vs 38m Main): Bootstrap
  // widens the router on travel-in and restores Main values on return.
  public const float BoundX = 27f;
  public const float BoundZ = 27f;

  // B1R3: higher spawn framing (user round: "camera cao hơn một chút để nhìn
  // toàn thể") + the world-name column anchor for the arrival beat.
  // S1 review (+30% height, same view direction): (0,4.6,6.4) -> (0,6.0,8.3).
  public static readonly Vector3 FollowOffset = new Vector3(0f, 6.0f, 8.3f);
  public static readonly Vector3 SignLocal = new Vector3(3.6f, 0f, -10.6f);
  public static Vector3 SignWorldPos {
    get { return WorldOffset + SignLocal; }
  }

  // Quest-target Interactables created during Build (Lead binds the bus).
  public readonly List<Interactable> CountingObjects = new List<Interactable>();

  // P3.0.1 Hub: 10 micro-world gate skeletons (walk-to destinations, NO
  // gameplay yet). Registry + lookup; Micro-World 1 wires travel on top.
  public readonly List<MicroWorldGate> MicroGates = new List<MicroWorldGate>();

  public MicroWorldGate FindMicroGate(string gateId) {
    if (string.IsNullOrEmpty(gateId)) return null;
    foreach (MicroWorldGate g in MicroGates) {
      if (g != null && string.Equals(g.gateId, gateId, System.StringComparison.OrdinalIgnoreCase))
        return g;
    }
    return null;
  }

  // math_bloom consumer root (children start HIDDEN; MathBloomDisplay flips).
  public Transform BloomRoot { get; private set; }

  // P1-2 presentation registry (scene-authored anchors, NOT magic vectors).
  // Initial staging mirrors the former inline constants 1:1 (no visual
  // change); human review re-stages NODES in the hierarchy from here on.
  public ActivityAnchors Anchors { get; private set; }

  // Injection boundary (GameInstaller calls this on MathScene load).
  public void Build(IWorldNavService nav, Transform playerT) {
    BuildContent(transform);
    BuildNavMesh(transform);
    BuildCarves(transform); // brook denies feet except the boardwalk corridor
    BindReturnGate(transform, nav, playerT);
  }

  // Content-only entry (geometry + markers + Interactables, NO NavMesh bake,
  // NO gate binding) so EditMode can review world composition headlessly.
  public void BuildContent(Transform root) {
    BuildGround(root);
    BuildBoundary(root);
    BuildPads(root);
    BuildHubCourtyard(root);
    BuildLobby(root);          // Counting Frame landmark
    BuildMathIdentity(root);   // domino line + number tower + signs
    BuildHostNook(root);
    BuildCountingGarden(root); // plot: fence, gate, beds, crops, tree, stones
    BuildNumberBridge(root);   // brook + Kenney boardwalk + far-bank clearing
    BuildPaths(root);          // spokes + meadow loop + garden inner path
    BuildDecor(root);
    BuildEntryBoard(root);
    BuildWorldSign(root);
    BuildShapeTrio(root);
    BuildBridgeDiscs(root);
    BuildNature(root);         // Kenney props (trees/bushes/rocks/grass)
    BuildReturnArch(root);
    BuildSun(root);
    BuildPresentationAnchors(root);
    BuildHubLandmark(root);      // P3.0.1 Hub: central orientation landmark
    BuildMicroWorldGates(root);  // P3.0.1 Hub: 10 gate skeletons + ring
    BuildHubDressing(root);      // S1: compass + zone tints + accent flowers
  }

  // S1 §4/§8: small composed dressing (all flat/walkable, collider-free).
  // Compass medallion on the lobby pad (orientation heart), three restrained
  // zone tints (N warm / W leaf / E pale) to break green monotony, three
  // accent flower clusters on the ring. MathGate* names (count-safe).
  void BuildHubDressing(Transform parent) {
    Pad(parent, "MathGateCompass", new Vector3(0f, 0.02f, 1f), 2.0f, CourtyardSand);
    Ball(parent, "MathGateCompassDot", new Vector3(0f, 0.06f, 1f), 0.4f, AbacusBlue, false);
    Pad(parent, "MathGateZoneN", new Vector3(0f, 0.008f, 14.3f), 10f,
      new Color(0.84f, 0.76f, 0.60f));
    Pad(parent, "MathGateZoneW", new Vector3(-10.5f, 0.008f, -2f), 9f,
      new Color(0.78f, 0.76f, 0.58f));
    Pad(parent, "MathGateZoneE", new Vector3(11f, 0.008f, -2f), 10f,
      new Color(0.86f, 0.78f, 0.60f));
    DressBloom(parent, "MathGateAccent0", new Vector3(9f, 0f, 8f), BerryRed);
    DressBloom(parent, "MathGateAccent1", new Vector3(-7f, 0f, 11f), BloomPink);
    DressBloom(parent, "MathGateAccent2", new Vector3(3f, 0f, -12f), BloomWhite);
    // S1-final depth: background silhouettes (distant, quiet, shared green).
    DressBackdrop(parent, "MathGateBackdropN1", new Vector3(-6f, 0f, 21.5f), 2.2f);
    DressBackdrop(parent, "MathGateBackdropN2", new Vector3(7f, 0f, 21f), 2.0f);
    DressBackdrop(parent, "MathGateBackdropE", new Vector3(23f, 0f, 2f), 2.0f);
    // S1-final foreground: entry-path framing bushes (low, flanking, never
    // competing — camera passes over them).
    PlaceProp(parent, "plant_bushSmall", "MathGateFrameL", new Vector3(-2.5f, 0f, -11f), 15f, 1.4f);
    PlaceProp(parent, "plant_bushSmall", "MathGateFrameR", new Vector3(2.5f, 0f, -11f), 195f, 1.4f);
    // S4 declutter (user round "rối mắt"): entry pebbles + spoke grass tufts
    // removed — the domino number-walk + chevrons already guide the eye, and
    // the small scatter read as noise around the gates.
  }

  // ---- micro-world hub (P3.0.1: selection hub, NO gameplay) --------------------
  // The pilot quest zones (lobby/garden/bridge/host) stay EXACTLY where they
  // are (journey + P40-P43 pins). The 10 gates occupy the surveyed free zones
  // A-E, each >=2m off every walking spoke, inside the r25.5 island box.
  // Shared contract: base disc + gate root (faces approach) + MicroWorldGate
  // (id/name/pattern/accents + Entry/Exit/Label anchors) + locked-style pill
  // label + motif geometry. Unique silhouette per gate, same art direction
  // (shared Lit palette + PropKit + Kenney kits). ALL gate geometry is
  // collider-free (P42 dressing rule: clicks fall through, child walks to the
  // mouth); solid posts bake as NavMesh obstacles off-path (no through-walk).
  //
  // S2 GATE-SHAPE PASS: every gate became a real gate silhouette (frame +
  // motif) and the body moved off the ring. S3 CARTOON PASS (user round:
  // "các cổng giống nhau quá… cartoon hơn và đúng bản chất từng micro world"):
  // the shared plain frame is replaced by the cartoon kit below — each gate
  // gets its OWN chunky silhouette (see the kit + per-gate builders).
  // The gate BODY stands GateBodyZ behind the ring waypoint so the 1.5m
  // circulation ring keeps its full width clear while every gate fronts the
  // hub. Arches are bake-ignored (headroom rule, P3 survey lesson); legs stay
  // collider-free and bake as off-path obstacles like every other post.

  // Gate body offset (local -Z = away from the hub, i.e. behind the ring).
  public const float GateBodyZ = -2.0f;
  // Pill floats above every S3 crown/topper (tallest ring/ball ~3.67).
  const float GateLabelHeight = 3.95f;

  MicroWorldGate GateRoot(Transform parent, MicroWorldCatalog.Entry def,
      Vector3 pos, Vector3 facePos) {
    GameObject root = new GameObject("MathGate_" + def.Id);
    root.transform.SetParent(parent);
    root.transform.localPosition = pos;
    Vector3 d = facePos - pos;
    d.y = 0f;
    if (d.sqrMagnitude > 0.001f)
      root.transform.localRotation = Quaternion.LookRotation(d.normalized);
    MicroWorldGate gate = root.AddComponent<MicroWorldGate>();
    gate.Wire(def.Id, def.VnName, def.Pattern, def.Accent);
    Pad(root.transform, "MathGateBase", new Vector3(0f, 0f, GateBodyZ), 3.4f, CourtyardSand);
    // S2 GATE IDENTITY: tinted threshold ring under the arch — the shared
    // "this is a doorway" grammar for all 10 entrances (accent ring + sand
    // inner, walkable ground treatment, above the base pad).
    GateThreshold(root.transform, def.Accent);
    // Name pill rides above the frame (anchored to the body, not the ring).
    gate.LabelAnchor.localPosition = new Vector3(0f, 0f, GateBodyZ);
    GameObject labelGo = new GameObject("MathGateLabel");
    labelGo.transform.SetParent(root.transform, false);
    WorldNameLabel label = labelGo.AddComponent<WorldNameLabel>();
    label.Setup(def.VnName, gate.LabelAnchor, GateLabelHeight);
    label.Show();
    MicroGates.Add(gate);
    return gate;
  }

  // S2 GATE IDENTITY: threshold ring (accent) + sand inner under the arch.
  // Same grammar on all 10 entrances; collider-free ground treatment.
  static void GateThreshold(Transform root, Color accent) {
    GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    ring.name = "MathGateThreshold";
    ring.transform.SetParent(root);
    ring.transform.localPosition = new Vector3(0f, 0.035f, GateBodyZ);
    ring.transform.localScale = new Vector3(3.0f, 0.014f, 3.0f);
    ring.GetComponent<Renderer>().sharedMaterial = Lit(accent);
    StripCollider(ring);
    GameObject inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    inner.name = "MathGateThresholdInner";
    inner.transform.SetParent(root);
    inner.transform.localPosition = new Vector3(0f, 0.05f, GateBodyZ);
    inner.transform.localScale = new Vector3(2.3f, 0.014f, 2.3f);
    inner.GetComponent<Renderer>().sharedMaterial = Lit(CourtyardSand);
    StripCollider(inner);
  }

  // The gate body (frame + motif anchor). Builders compose in body-local
  // coords: +Z faces the ring/hub, -Z faces the micro-world.
  static Transform GateBody(MicroWorldGate gate) {
    Transform t = gate.transform.Find("MathGateBody");
    if (t != null) return t;
    GameObject go = new GameObject("MathGateBody");
    go.transform.SetParent(gate.transform, false);
    go.transform.localPosition = new Vector3(0f, 0f, GateBodyZ);
    return go.transform;
  }

  // ---- S3 cartoon gate kit (shared rounded language + per-gate silhouette) --
  // User round: "các cổng giống nhau quá… muốn cartoon và đúng bản chất từng
  // micro world". The plain post-and-lintel frame is gone. Every gate now
  // stands on two cartoon legs at ±1.6 (rounded post + ball cap, toy blocks,
  // striped site post, mushroom, tree, stone…) and carries a UNIQUE chunky
  // arch/topper that says what the micro-world is about (beads, magnifier,
  // fruit branch, mirrored halves, rainbow sort, jigsaw, cottage roof, crane,
  // stone bridge, mushroom + moon). Arches are bake-ignored (headroom rule);
  // all gate geometry stays collider-free (P42/P45 dressing rules).

  // Rounded cartoon leg: chunky cylinder + ball cap (the family signature).
  static GameObject CartoonPost(Transform t, string name, float x, float h, float radius,
      Color body, Color cap) {
    GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    post.name = name;
    post.transform.SetParent(t);
    post.transform.localPosition = new Vector3(x, h * 0.5f, 0f);
    post.transform.localScale = new Vector3(radius * 2f, h * 0.5f, radius * 2f);
    post.GetComponent<Renderer>().sharedMaterial = Lit(body);
    StripCollider(post);
    Ball(t, name + "Cap", new Vector3(x, h + radius * 0.9f, 0f), radius * 2.3f, cap, false);
    return post;
  }

  // Toy counting-block leg (stack 1-2-3, ball cap): the block IS the motif.
  static void BlockPost(Transform t, string name, float x, Color a, Color b) {
    Box(t, name, new Vector3(x, 0.45f, 0f), new Vector3(0.9f, 0.9f, 0.9f), a);
    Box(t, name + "B", new Vector3(x, 1.25f, 0f), new Vector3(0.7f, 0.7f, 0.7f), b);
    Box(t, name + "C", new Vector3(x, 1.9f, 0f), new Vector3(0.5f, 0.5f, 0.5f), a);
    Ball(t, name + "Cap", new Vector3(x, 2.3f, 0f), 0.44f, BloomWhite, false);
  }

  // Hazard-striped site leg (gold/soil bands + cap): Build Yard signature.
  static void StripedPost(Transform t, string name, float x) {
    Box(t, name, new Vector3(x, 0.3f, 0f), new Vector3(0.6f, 0.6f, 0.6f), QuestGold);
    Box(t, name + "B", new Vector3(x, 0.9f, 0f), new Vector3(0.6f, 0.6f, 0.6f), SoilBrown);
    Box(t, name + "C", new Vector3(x, 1.5f, 0f), new Vector3(0.6f, 0.6f, 0.6f), QuestGold);
    Ball(t, name + "Cap", new Vector3(x, 2.05f, 0f), 0.5f, BloomWhite, false);
  }

  // Chunky cartoon arch: segments along a half-ellipse landing at (±radiusX,
  // baseY); per-segment colour cycling reads "rainbow"/"mirrored" motifs.
  static void CartoonArch(Transform t, string name, float radiusX, float rise, float baseY,
      int segments, float thick, Color[] colors) {
    for (int i = 0; i < segments; i++) {
      float a = Mathf.PI * (i + 0.5f) / segments;
      float x = -Mathf.Cos(a) * radiusX;
      float y = baseY + Mathf.Sin(a) * rise;
      float x0 = -Mathf.Cos(Mathf.PI * i / segments) * radiusX;
      float y0 = baseY + Mathf.Sin(Mathf.PI * i / segments) * rise;
      float x1 = -Mathf.Cos(Mathf.PI * (i + 1) / segments) * radiusX;
      float y1 = baseY + Mathf.Sin(Mathf.PI * (i + 1) / segments) * rise;
      float len = Mathf.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0)) * 1.06f;
      GameObject seg = Box(t, name + i, new Vector3(x, y, 0f),
        new Vector3(len, thick, thick), colors[i % colors.Length]);
      seg.transform.localRotation = Quaternion.Euler(0f, 0f,
        Mathf.Atan2(Mathf.Cos(a) * rise, Mathf.Sin(a) * radiusX) * Mathf.Rad2Deg);
      IgnoreFromBuild(seg);
    }
  }

  // Chunky cartoon ring (the Discovery magnifier head).
  static void CartoonRing(Transform t, string name, float cx, float cy, float r,
      int segments, float thick, Color color) {
    for (int i = 0; i < segments; i++) {
      float a = 2f * Mathf.PI * (i + 0.5f) / segments;
      float len = 2f * Mathf.PI * r / segments * 1.25f;
      GameObject seg = Box(t, name + i, new Vector3(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, 0f),
        new Vector3(len, thick, thick), color);
      seg.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg + 90f);
      IgnoreFromBuild(seg);
    }
  }

  // Mushroom leg (Memory Grove signature): cream stalk + spotted red cap +
  // accent base ring.
  static void MushroomPost(Transform t, string name, float x, float stalkH, Color accent, Color capColor) {
    GameObject baseRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    baseRing.name = name + "Base";
    baseRing.transform.SetParent(t);
    baseRing.transform.localPosition = new Vector3(x, 0.06f, 0f);
    baseRing.transform.localScale = new Vector3(0.7f, 0.06f, 0.7f);
    baseRing.GetComponent<Renderer>().sharedMaterial = Lit(accent);
    StripCollider(baseRing);
    GameObject stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stalk.name = name;
    stalk.transform.SetParent(t);
    stalk.transform.localPosition = new Vector3(x, stalkH * 0.5f, 0f);
    stalk.transform.localScale = new Vector3(0.4f, stalkH * 0.5f, 0.4f);
    stalk.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.97f, 0.94f, 0.86f));
    StripCollider(stalk);
    GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    cap.name = name + "Cap";
    cap.transform.SetParent(t);
    cap.transform.localPosition = new Vector3(x, stalkH + 0.18f, 0f);
    cap.transform.localScale = new Vector3(1.0f, 0.55f, 1.0f);
    cap.GetComponent<Renderer>().sharedMaterial = Lit(capColor);
    StripCollider(cap);
    Ball(t, name + "SpotA", new Vector3(x - 0.2f, stalkH + 0.42f, 0.12f), 0.16f, BloomWhite, false);
    Ball(t, name + "SpotB", new Vector3(x + 0.22f, stalkH + 0.42f, -0.1f), 0.14f, BloomWhite, false);
  }

  static void MushroomMini(Transform t, string name, float x, Color capColor) {
    GameObject stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stalk.name = name;
    stalk.transform.SetParent(t);
    stalk.transform.localPosition = new Vector3(x, 0.22f, 0.6f);
    stalk.transform.localScale = new Vector3(0.2f, 0.22f, 0.2f);
    stalk.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.97f, 0.94f, 0.86f));
    StripCollider(stalk);
    GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    cap.name = name + "Cap";
    cap.transform.SetParent(t);
    cap.transform.localPosition = new Vector3(x, 0.5f, 0.6f);
    cap.transform.localScale = new Vector3(0.5f, 0.26f, 0.5f);
    cap.GetComponent<Renderer>().sharedMaterial = Lit(capColor);
    StripCollider(cap);
  }

  static void GateBasket(Transform parent, string name, Vector3 pos) {
    GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    body.name = name;
    body.transform.SetParent(parent);
    body.transform.localPosition = pos;
    body.transform.localScale = new Vector3(0.5f, 0.4f, 0.5f);
    body.GetComponent<Renderer>().sharedMaterial = Lit(BasketTan);
    StripCollider(body);
    GameObject soil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    soil.name = name + "Soil";
    soil.transform.SetParent(parent);
    soil.transform.localPosition = pos + new Vector3(0f, 0.18f, 0f);
    soil.transform.localScale = new Vector3(0.42f, 0.06f, 0.42f);
    soil.GetComponent<Renderer>().sharedMaterial = Lit(SoilBrown);
    StripCollider(soil);
  }

  void BuildHubLandmark(Transform parent) {
    // Great Abacus (5.5, 9.5): the hub's north star — tall bead frame visible
    // from spawn over the lobby, staging the return marker + north gates behind.
    Transform root = new GameObject("MathGateLandmark").transform;
    root.SetParent(parent);
    root.localPosition = new Vector3(5.5f, 0f, 9.5f);
    Box(root, "MathGateLandmarkPostL", new Vector3(-1.2f, 1.6f, 0f),
      new Vector3(0.26f, 3.2f, 0.26f), AbacusBlue);
    Box(root, "MathGateLandmarkPostR", new Vector3(1.2f, 1.6f, 0f),
      new Vector3(0.26f, 3.2f, 0.26f), AbacusBlue);
    for (int r = 0; r < 3; r++) {
      float y = 0.9f + r * 0.8f;
      Box(root, "MathGateLandmarkRod" + r, new Vector3(0f, y, 0f),
        new Vector3(2.4f, 0.1f, 0.1f), FenceWood);
      for (int i = 0; i < 3; i++) {
        GameObject bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bead.name = "MathGateLandmarkBead" + r + "_" + i;
        bead.transform.SetParent(root);
        bead.transform.localPosition = new Vector3(-0.5f + i * 0.5f, y + 0.14f, 0f);
        bead.transform.localScale = new Vector3(0.3f, 0.3f, 0.3f);
        bead.GetComponent<Renderer>().sharedMaterial =
          Lit((i + r) % 2 == 0 ? QuestGold : BerryRed);
        StripCollider(bead);
      }
    }
    Pad(root, "MathGateLandmarkBase", Vector3.zero, 3.0f, CourtyardSand);
  }

  void BuildMicroWorldGates(Transform parent) {
    // S1 arc/court: 10 gates on a ring around the hub (r~13-16), ALL facing
    // the center so the child reads fronts from the lobby. Slots sit between
    // the 4 walking spokes (>=2m, pinned CT-P45C) inside the r25.5 box.
    Vector3 hub = new Vector3(0f, 0f, 0f);
    MicroWorldCatalog.Entry[] all = MicroWorldCatalog.All;
    BuildGateCounting(parent, all[0], new Vector3(-11f, 0f, -3f), hub);
    BuildGateDiscovery(parent, all[1], new Vector3(-9.6f, 0f, 12.5f), hub);
    BuildGateOrchard(parent, all[2], new Vector3(14.5f, 0f, 4.9f), hub);
    BuildGateMatch(parent, all[3], new Vector3(11.5f, 0f, 10.6f), hub);
    BuildGateSorting(parent, all[4], new Vector3(6.3f, 0f, 14.6f), hub);
    BuildGateWorkshop(parent, all[5], new Vector3(-5.1f, 0f, 15.1f), hub);
    BuildGateVillage(parent, all[6], new Vector3(-10.6f, 0f, -10.6f), hub);
    BuildGateBuild(parent, all[7], new Vector3(6.3f, 0f, -12.6f), hub);
    BuildGateBridge(parent, all[8], new Vector3(13.2f, 0f, -8.6f), hub);
    BuildGateMemory(parent, all[9], new Vector3(-6.3f, 0f, -12.6f), hub);
    BuildRingCirculation(parent);
    // S2 PIONEER: the counting_garden gate is the first live micro-world door —
    // a walk-in portal at its hub-side mouth fires the CountingGardenArea beat.
    MicroWorldGate counting = MicroGates.Count > 0 ? MicroGates[0] : null;
    if (counting != null && counting.EntryAnchor != null) {
      // User report: walking INTO the gate did not warp when the child stood
      // under the arch (the trigger sat at the mouth, 1.8m hub-side of the
      // centre — stepping through from inside never crossed it). The trigger
      // now covers the WHOLE arch: 0.7m hub-side of the gate centre with a
      // 1.8m radius (covers mouth + interior + a step behind).
      Vector3 toHub = hub - counting.transform.position;
      toHub.y = 0f;
      if (toHub.sqrMagnitude < 0.001f) toHub = new Vector3(0f, 0f, 1f);
      toHub.Normalize();
      GameObject portalGo = new GameObject("CountingGardenPortal");
      portalGo.transform.SetParent(parent);
      portalGo.transform.position = counting.transform.position + toHub * 0.7f;
      MicroWorldPortal portal = portalGo.AddComponent<MicroWorldPortal>();
      portal.ExitMode = false;
      portal.fireRadius = 1.8f;
      portal.areaId = CountingGardenArea.AreaId;
      CountingGardenPortal = portal;
      Pad(parent, "CountingGardenPortalDisc", portalGo.transform.localPosition, 3.0f, CourtyardSand);
    }
    // S3-P2Z14 GAMEPLAY #4: the build_yard gate opens its OWN micro-world
    // (BuildTowerScene) through the same shared slot — identical walk-in
    // portal contract (whole-arch coverage, cold-start latch). The mini-tower
    // landmark beside the gate reflects the last completed target.
    MicroWorldGate build = FindMicroGate("build_yard");
    if (build != null && build.EntryAnchor != null) {
      Vector3 toHub = hub - build.transform.position;
      toHub.y = 0f;
      if (toHub.sqrMagnitude < 0.001f) toHub = new Vector3(0f, 0f, 1f);
      toHub.Normalize();
      GameObject buildPortalGo = new GameObject("BuildTowerPortal");
      buildPortalGo.transform.SetParent(parent);
      buildPortalGo.transform.position = build.transform.position + toHub * 0.7f;
      MicroWorldPortal buildPortal = buildPortalGo.AddComponent<MicroWorldPortal>();
      buildPortal.ExitMode = false;
      buildPortal.fireRadius = 1.8f;
      buildPortal.areaId = BuildTowerArea.AreaId;
      BuildTowerPortal = buildPortal;
      Pad(parent, "BuildTowerPortalDisc", buildPortalGo.transform.localPosition, 3.0f, CourtyardSand);
    }
  }

  // S1 ring: one circulation loop through every gate mouth (lighter sand than
  // the spokes so the loop reads). The east chord jogs via the real bridge
  // deck (the ring USES the Number Bridge to cross the brook); the west chord
  // midpoints around the garden fence. Waypoints are exposed for CT-P45's
  // carve-avoidance pin.
  public readonly System.Collections.Generic.List<Vector3> RingWaypoints =
    new System.Collections.Generic.List<Vector3>();

  void BuildRingCirculation(Transform parent) {
    RingWaypoints.Clear();
    Vector3[] pts = {
      new Vector3(6.3f, 0f, 14.6f),    // sorting mouth
      new Vector3(11.5f, 0f, 10.6f),   // match mouth
      new Vector3(14.5f, 0f, 4.9f),    // orchard mouth
      new Vector3(15.5f, 0f, -5f),     // bridge deck (ford the brook)
      new Vector3(13.2f, 0f, -8.6f),   // number-bridge mouth
      new Vector3(6.3f, 0f, -12.6f),   // build mouth
      new Vector3(-6.3f, 0f, -12.6f),  // memory mouth
      new Vector3(-10.6f, 0f, -10.6f), // village mouth
      new Vector3(-11f, 0f, -3f),      // counting mouth
      new Vector3(-8f, 0f, 4f),        // fence midpoint (around the garden)
      new Vector3(-9.6f, 0f, 12.5f),   // discovery mouth
      new Vector3(-5.1f, 0f, 15.1f),   // workshop mouth
    };
    foreach (Vector3 p in pts) RingWaypoints.Add(p);
    for (int i = 0; i < pts.Length; i++) {
      Vector3 a = pts[i];
      Vector3 b = pts[(i + 1) % pts.Length];
      Seg(parent, "MathRing" + i.ToString("00"),
        a + new Vector3(0f, 0.032f, 0f), b + new Vector3(0f, 0.032f, 0f), 1.5f);
      // Ring tint: lighter wash over the spoke tan (circulation reads).
      // Applied by re-tinting the seg just built (shared Lit cache, no new material).
    }
    TintRing(parent);
  }

  // S1 §8: restrained value variation — the ring reads lighter than spokes.
  static void TintRing(Transform parent) {
    for (int i = 0; i < 12; i++) {
      Transform seg = parent.Find("MathRing" + i.ToString("00"));
      if (seg == null) continue;
      Renderer r = seg.GetComponent<Renderer>();
      if (r != null) r.sharedMaterial = Lit(CourtyardSand);
    }
  }

  // 01 Counting Garden (COUNT/CHOOSE/COLLECT): toy 1-2-3 block legs + chunky
  // green arch with a bead count row hanging under it.
  void BuildGateCounting(Transform parent, MicroWorldCatalog.Entry def, Vector3 pos, Vector3 face) {
    MicroWorldGate gate = GateRoot(parent, def, pos, face);
    Transform t = GateBody(gate);
    BlockPost(t, "MathGatePostL", -1.6f, AbacusBlue, QuestGold);
    BlockPost(t, "MathGatePostR", 1.6f, QuestGold, AbacusBlue);
    CartoonArch(t, "MathGateArch", 1.6f, 1.0f, 2.15f, 5, 0.36f,
      new[] { CropGreen, LeafGreen, CropGreen, LeafGreen, CropGreen });
    // Count row: beads hang just under the arch segments.
    for (int i = 0; i < 5; i++) {
      float a = Mathf.PI * (i + 0.5f) / 5f;
      Ball(t, "MathGateBead" + i,
        new Vector3(-Mathf.Cos(a) * 1.6f, 2.15f + Mathf.Sin(a) * 1.0f - 0.42f, 0f),
        0.3f, i % 2 == 0 ? QuestGold : BerryRed, false);
    }
    // S2 crown: a chunky row of three counting beads above the arch (the
    // "count" promise reads at courtyard distance, no text needed).
    for (int i = 0; i < 3; i++)
      Ball(t, "MathGateCountCrown" + i, new Vector3(-0.5f + i * 0.5f, 3.42f, 0f),
        0.5f, i == 1 ? BerryRed : QuestGold, false);
  }

  // 02 Discovery Garden (FIND/SEARCH/DISCOVER): the gate IS a giant cartoon
  // magnifier — mint ring + pale glass + handle into the right post.
  void BuildGateDiscovery(Transform parent, MicroWorldCatalog.Entry def, Vector3 pos, Vector3 face) {
    MicroWorldGate gate = GateRoot(parent, def, pos, face);
    Transform t = GateBody(gate);
    CartoonPost(t, "MathGatePostL", -1.6f, 2.05f, 0.22f, def.Accent, BloomWhite);
    CartoonPost(t, "MathGatePostR", 1.6f, 2.05f, 0.22f, def.Accent, BloomWhite);
    CartoonRing(t, "MathGateArch", 0f, 2.75f, 0.7f, 8, 0.3f, def.Accent);
    GameObject glass = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    glass.name = "MathGateGlass";
    glass.transform.SetParent(t);
    glass.transform.localPosition = new Vector3(0f, 2.75f, 0f);
    glass.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
    glass.transform.localScale = new Vector3(1.2f, 0.05f, 1.2f);
    glass.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.78f, 0.90f, 0.96f));
    StripCollider(glass);
    IgnoreFromBuild(glass);
    GameObject handle = Box(t, "MathGateHandle", new Vector3(0.62f, 2.15f, 0f),
      new Vector3(0.22f, 0.95f, 0.22f), def.Accent);
    handle.transform.localRotation = Quaternion.Euler(0f, 0f, -38f);
    IgnoreFromBuild(handle);
  }

  // 03 Fruit Orchard (COLLECT/GATHER): the legs ARE fruit trees (trunk +
  // canopy) and a leafy branch arches over with ripe apples hanging.
  void BuildGateOrchard(Transform parent, MicroWorldCatalog.Entry def, Vector3 pos, Vector3 face) {
    MicroWorldGate gate = GateRoot(parent, def, pos, face);
    Transform t = GateBody(gate);
    CartoonPost(t, "MathGatePostL", -1.6f, 1.95f, 0.34f, SoilBrown, LeafGreen);
    CartoonPost(t, "MathGatePostR", 1.6f, 1.95f, 0.34f, SoilBrown, LeafGreen);
    CartoonArch(t, "MathGateArch", 1.6f, 0.9f, 2.3f, 5, 0.4f,
      new[] { LeafGreen, CropGreen, LeafGreen, CropGreen, LeafGreen });
    for (int i = 0; i < 3; i++) {
      float a = Mathf.PI * (i + 1f) / 4f;
      Ball(t, "MathGateApple" + i,
        new Vector3(-Mathf.Cos(a) * 1.6f, 2.3f + Mathf.Sin(a) * 0.9f - 0.35f, 0f),
        0.34f, BerryRed, false);
    }
    GateBasket(t, "MathGateBasket", new Vector3(1.35f, 0.2f, 0.85f));
    // S2 crown: one giant apple + leaf above the leafy arch (orchard promise).
    Ball(t, "MathGateGiantApple", new Vector3(0f, 3.34f, 0f), 0.78f, BerryRed, false);
    Ball(t, "MathGateGiantAppleLeaf", new Vector3(0.3f, 3.62f, 0f), 0.28f, LeafGreen, false);
  }

  // 04 Match Meadow (MATCH): two mirrored halves (aqua | gold) meeting at a
  // pink crown — the whole arch is a "pair", with mirrored pairs below.
  void BuildGateMatch(Transform parent, MicroWorldCatalog.Entry def, Vector3 pos, Vector3 face) {
    MicroWorldGate gate = GateRoot(parent, def, pos, face);
    Transform t = GateBody(gate);
    CartoonPost(t, "MathGatePostL", -1.6f, 2.05f, 0.22f, def.Accent, BloomWhite);
    CartoonPost(t, "MathGatePostR", 1.6f, 2.05f, 0.22f, def.Accent, BloomWhite);
    CartoonArch(t, "MathGateArch", 1.6f, 1.0f, 2.2f, 6, 0.36f,
      new[] { def.Accent, QuestGold, def.Accent, QuestGold, def.Accent, QuestGold });
    Ball(t, "MathGateCrown", new Vector3(0f, 3.42f, 0f), 0.5f, BloomPink, false);
    Ball(t, "MathGatePairBallL", new Vector3(-0.85f, 0.2f, 0.8f), 0.4f, def.Accent, false);
    Ball(t, "MathGatePairBallR", new Vector3(0.85f, 0.2f, 0.8f), 0.4f, def.Accent, false);
    Box(t, "MathGatePairCubeL", new Vector3(-0.85f, 0.2f, -0.6f),
      new Vector3(0.4f, 0.4f, 0.4f), QuestGold);
    Box(t, "MathGatePairCubeR", new Vector3(0.85f, 0.2f, -0.6f),
      new Vector3(0.4f, 0.4f, 0.4f), QuestGold);
  }

  // 05 Sorting Park (SORT/CATEGORIZE): a rainbow arch over three colour bins
  // with shape toppers riding the crown.
  void BuildGateSorting(Transform parent, MicroWorldCatalog.Entry def, Vector3 pos, Vector3 face) {
    MicroWorldGate gate = GateRoot(parent, def, pos, face);
    Transform t = GateBody(gate);
    CartoonPost(t, "MathGatePostL", -1.6f, 2.05f, 0.22f, AbacusBlue, BloomWhite);
    CartoonPost(t, "MathGatePostR", 1.6f, 2.05f, 0.22f, AbacusBlue, BloomWhite);
    CartoonArch(t, "MathGateArch", 1.6f, 0.95f, 2.2f, 5, 0.38f,
      new[] { AbacusBlue, CropGreen, BerryRed, QuestGold, AbacusBlue });
    Box(t, "MathGateTopCube", new Vector3(-0.6f, 3.35f, 0f),
      new Vector3(0.36f, 0.36f, 0.36f), AbacusBlue);
    Ball(t, "MathGateTopBall", new Vector3(0f, 3.42f, 0f), 0.36f, BerryRed, false);
    GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    disc.name = "MathGateTopDisc";
    disc.transform.SetParent(t);
    disc.transform.localPosition = new Vector3(0.6f, 3.35f, 0f);
    disc.transform.localScale = new Vector3(0.36f, 0.18f, 0.36f);
    disc.GetComponent<Renderer>().sharedMaterial = Lit(CropGreen);
    StripCollider(disc);
    Color[] bin = { AbacusBlue, BerryRed, CropGreen };
    for (int i = 0; i < 3; i++) {
      GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      body.name = "MathGateBin" + i;
      body.transform.SetParent(t);
      body.transform.localPosition = new Vector3(-1.2f + i * 1.2f, 0.45f, -1.0f);
      body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
      body.GetComponent<Renderer>().sharedMaterial = Lit(bin[i]);
      StripCollider(body);
    }
  }

  // 06 Puzzle Workshop (DRAG/DROP/PLACE/ORDER): the top IS two stepped,
  // interlocking jigsaw planks over a wooden workbench.
  void BuildGateWorkshop(Transform parent, MicroWorldCatalog.Entry def, Vector3 pos, Vector3 face) {
    MicroWorldGate gate = GateRoot(parent, def, pos, face);
    Transform t = GateBody(gate);
    Box(t, "MathGatePostL", new Vector3(-1.6f, 1.0f, 0f),
      new Vector3(0.44f, 2.0f, 0.44f), FenceWood);
    Ball(t, "MathGatePostLCap", new Vector3(-1.6f, 2.18f, 0f), 0.5f, BasketTan, false);
    Box(t, "MathGatePostR", new Vector3(1.6f, 1.0f, 0f),
      new Vector3(0.44f, 2.0f, 0.44f), FenceWood);
    Ball(t, "MathGatePostRCap", new Vector3(1.6f, 2.18f, 0f), 0.5f, BasketTan, false);
    // Jigsaw step: two planks at different heights locked by a center tab.
    GameObject plankL = Box(t, "MathGateArch0", new Vector3(-0.8f, 2.55f, 0f),
      new Vector3(1.7f, 0.42f, 0.5f), FenceWood);
    IgnoreFromBuild(plankL);
    GameObject plankR = Box(t, "MathGateArch1", new Vector3(0.8f, 2.95f, 0f),
      new Vector3(1.7f, 0.42f, 0.5f), BasketTan);
    IgnoreFromBuild(plankR);
    GameObject tab = Box(t, "MathGateArch2", new Vector3(0f, 2.75f, 0f),
      new Vector3(0.5f, 0.5f, 0.5f), SoilBrown);
    IgnoreFromBuild(tab);
    Ball(t, "MathGatePeg", new Vector3(0f, 3.28f, 0f), 0.36f, QuestGold, false);
    // S2 crown: a giant jigsaw piece locked on top (workshop promise).
    GameObject piece = Box(t, "MathGateJigsawCrown", new Vector3(0f, 3.5f, 0f),
      new Vector3(0.72f, 0.42f, 0.5f), BasketTan);
    IgnoreFromBuild(piece);
    GameObject pieceTab = Box(t, "MathGateJigsawTab", new Vector3(0.26f, 3.5f, 0f),
      new Vector3(0.24f, 0.62f, 0.5f), BasketTan);
    IgnoreFromBuild(pieceTab);
    Box(t, "MathGateBench", new Vector3(0f, 0.75f, -1.9f),
      new Vector3(1.8f, 0.14f, 0.7f), FenceWood);
    Box(t, "MathGateBenchLegL", new Vector3(-0.7f, 0.35f, -1.9f),
      new Vector3(0.14f, 0.7f, 0.5f), SoilBrown);
    Box(t, "MathGateBenchLegR", new Vector3(0.7f, 0.35f, -1.9f),
      new Vector3(0.14f, 0.7f, 0.5f), SoilBrown);
    Box(t, "MathGateBlockCube", new Vector3(-0.5f, 1.05f, -1.9f),
      new Vector3(0.4f, 0.4f, 0.4f), AbacusBlue);
    Ball(t, "MathGateBlockBall", new Vector3(0.4f, 1.0f, -1.9f), 0.4f, QuestGold, false);
  }

  // 07 Delivery Village (DELIVER/GIVE/BRING): a little cottage gate — the
  // legs carry a pitched roof + chimney, with the mailbox and parcels.
  void BuildGateVillage(Transform parent, MicroWorldCatalog.Entry def, Vector3 pos, Vector3 face) {
    MicroWorldGate gate = GateRoot(parent, def, pos, face);
    Transform t = GateBody(gate);
    CartoonPost(t, "MathGatePostL", -1.6f, 2.05f, 0.22f, def.Accent, BloomWhite);
    CartoonPost(t, "MathGatePostR", 1.6f, 2.05f, 0.22f, def.Accent, BloomWhite);
    GameObject roofL = Box(t, "MathGateArch0", new Vector3(-0.75f, 2.52f, 0f),
      new Vector3(1.9f, 0.26f, 1.0f), def.Accent);
    roofL.transform.localRotation = Quaternion.Euler(0f, 0f, 26f);
    IgnoreFromBuild(roofL);
    GameObject roofR = Box(t, "MathGateArch1", new Vector3(0.75f, 2.52f, 0f),
      new Vector3(1.9f, 0.26f, 1.0f), BerryRed);
    roofR.transform.localRotation = Quaternion.Euler(0f, 0f, -26f);
    IgnoreFromBuild(roofR);
    Ball(t, "MathGateRidge", new Vector3(0f, 2.98f, 0f), 0.42f, QuestGold, false);
    // S2 crown: a delivery parcel stack on the ridge (village promise).
    Box(t, "MathGateParcelCrown", new Vector3(0f, 3.3f, 0f),
      new Vector3(0.6f, 0.44f, 0.6f), BasketTan);
    Box(t, "MathGateParcelCrownB", new Vector3(-0.1f, 3.64f, 0.05f),
      new Vector3(0.42f, 0.28f, 0.42f), FenceWood);
    Box(t, "MathGateChimney", new Vector3(1.05f, 2.78f, -0.2f),
      new Vector3(0.3f, 0.66f, 0.3f), BerryRed);
    Box(t, "MathGateMailPost", new Vector3(1.7f, 0.5f, 0.75f),
      new Vector3(0.12f, 1.0f, 0.12f), FenceWood);
    Box(t, "MathGateMailBox", new Vector3(1.7f, 1.1f, 0.75f),
      new Vector3(0.4f, 0.3f, 0.5f), AbacusBlue);
    Box(t, "MathGateParcelA", new Vector3(-1.35f, 0.25f, 0.8f),
      new Vector3(0.5f, 0.5f, 0.5f), BasketTan);
    Box(t, "MathGateParcelB", new Vector3(-1.35f, 0.65f, 0.8f),
      new Vector3(0.38f, 0.3f, 0.38f), FenceWood);
  }

  // 08 Build Yard (BUILD/CONSTRUCT): hazard-striped site legs under a beam,
  // with a little crane arm + hook and material stacks.
  void BuildGateBuild(Transform parent, MicroWorldCatalog.Entry def, Vector3 pos, Vector3 face) {
    MicroWorldGate gate = GateRoot(parent, def, pos, face);
    Transform t = GateBody(gate);
    StripedPost(t, "MathGatePostL", -1.6f);
    StripedPost(t, "MathGatePostR", 1.6f);
    GameObject beam = Box(t, "MathGateArch0", new Vector3(0f, 2.3f, 0f),
      new Vector3(3.8f, 0.26f, 0.3f), FenceWood);
    IgnoreFromBuild(beam);
    // Gantry crane (S4 fix: the old thin diagonal arm read as a broken
    // scaffold). Chunky gold mast + symmetric jib + two cables: a load block
    // left, a hook right — reads as BUILD/CONSTRUCT from the lobby.
    GameObject mast = Box(t, "MathGateCraneMast", new Vector3(0f, 2.85f, 0f),
      new Vector3(0.3f, 1.1f, 0.3f), QuestGold);
    IgnoreFromBuild(mast);
    GameObject jib = Box(t, "MathGateCraneArm", new Vector3(0f, 3.45f, 0f),
      new Vector3(3.4f, 0.26f, 0.26f), QuestGold);
    IgnoreFromBuild(jib);
    Box(t, "MathGateCraneCable", new Vector3(1.15f, 3.12f, 0f),
      new Vector3(0.08f, 0.42f, 0.08f), SoilBrown);
    Ball(t, "MathGateCraneHook", new Vector3(1.15f, 2.75f, 0f), 0.3f, StoneGrey, false);
    Box(t, "MathGateCraneCableL", new Vector3(-1.15f, 3.15f, 0f),
      new Vector3(0.08f, 0.4f, 0.08f), SoilBrown);
    Box(t, "MathGateCraneLoad", new Vector3(-1.15f, 2.72f, 0f),
      new Vector3(0.44f, 0.44f, 0.44f), BasketTan);
    Box(t, "MathGateBlockA", new Vector3(-0.6f, 0.25f, -1.2f),
      new Vector3(0.5f, 0.5f, 0.5f), StoneGrey);
    Box(t, "MathGateBlockB", new Vector3(-0.6f, 0.7f, -1.2f),
      new Vector3(0.4f, 0.4f, 0.4f), QuestGold);
    GameObject barrel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    barrel.name = "MathGateBarrel";
    barrel.transform.SetParent(t);
    barrel.transform.localPosition = new Vector3(1.2f, 0.4f, -1.2f);
    barrel.transform.localScale = new Vector3(0.5f, 0.8f, 0.5f);
    barrel.GetComponent<Renderer>().sharedMaterial = Lit(SoilBrown);
    StripCollider(barrel);
    // S3-P2Z14 gameplay #4: the progress landmark — a mini block tower beside
    // the arch (local +x of the body, clear of the label, the portal and the
    // walkway) whose height reflects the last completed target.
    GameObject landmarkGo = new GameObject("MathBuildYardLandmark");
    BuildYardLandmark landmark = landmarkGo.AddComponent<BuildYardLandmark>();
    landmark.Build(gate.transform, new Vector3(2.9f, 0f, GateBodyZ + 0.15f));
    BuildTowerLandmark = landmark;
  }

  // 09 Number Bridge (PATH/SEQUENCE/ORDER): a chunky stone arch bridge with a
  // wooden deck + rails across the crown and a rill below.
  void BuildGateBridge(Transform parent, MicroWorldCatalog.Entry def, Vector3 pos, Vector3 face) {
    MicroWorldGate gate = GateRoot(parent, def, pos, face);
    Transform t = GateBody(gate);
    CartoonPost(t, "MathGatePostL", -1.6f, 1.9f, 0.24f, StoneGrey, BloomWhite);
    CartoonPost(t, "MathGatePostR", 1.6f, 1.9f, 0.24f, StoneGrey, BloomWhite);
    CartoonArch(t, "MathGateArch", 1.6f, 0.9f, 2.3f, 5, 0.42f,
      new[] { StoneGrey, StoneGrey, StoneGrey, StoneGrey, StoneGrey });
    // S8 fix (user: "còn 1 cái thanh ở trên, Failed"): the top plank + tiny
    // rails read as a board stuck on the arch. The bridge is now just the
    // clean stone arch with a keystone, plus the ground boardwalk behind.
    GameObject keystone = Box(t, "MathGateKeystone", new Vector3(0f, 3.28f, 0f),
      new Vector3(0.56f, 0.5f, 0.5f), StoneGrey);
    IgnoreFromBuild(keystone);
    // S2 crown: a 1-2-3 ascending bead sequence hanging under the keystone
    // (sequence/path promise, still above head clearance).
    Ball(t, "MathGateSeqCrown0", new Vector3(-0.45f, 3.0f, 0f), 0.26f, Sky, false);
    Ball(t, "MathGateSeqCrown1", new Vector3(0f, 2.92f, 0f), 0.34f, QuestGold, false);
    Ball(t, "MathGateSeqCrown2", new Vector3(0.45f, 2.84f, 0f), 0.42f, Sky, false);
    Box(t, "MathGateDeck", new Vector3(0f, 0.12f, -0.7f),
      new Vector3(1.7f, 0.12f, 2.4f), FenceWood);
  }

  // 10 Memory Grove (MEMORY/RECALL): mushroom legs (red dotted caps) under a
  // dusk-plum arch with the moon, paired mini mushrooms at the base.
  void BuildGateMemory(Transform parent, MicroWorldCatalog.Entry def, Vector3 pos, Vector3 face) {
    MicroWorldGate gate = GateRoot(parent, def, pos, face);
    Transform t = GateBody(gate);
    MushroomPost(t, "MathGatePostL", -1.6f, 1.7f, def.Accent, BerryRed);
    MushroomPost(t, "MathGatePostR", 1.6f, 1.7f, def.Accent, BerryRed);
    CartoonArch(t, "MathGateArch", 1.6f, 0.95f, 2.15f, 5, 0.4f,
      new[] { def.Accent, Plum, def.Accent, Plum, def.Accent });
    Ball(t, "MathGateMoon", new Vector3(0f, 3.35f, 0f), 0.6f, BloomWhite, false);
    MushroomMini(t, "MathGateMiniL", -1.15f, BerryRed);
    MushroomMini(t, "MathGateMiniR", 1.15f, BerryRed);
  }
  // P1-2 presentation registry (scene-authored anchors, NOT magic vectors).
  // S1 re-stage: the arrival beat is a south-high wide shot INTO the hub
  // (player + lobby + Tess + abacus + return marker + north gate row) instead
  // of the old sign-column close-up. Follow framing untouched.
  // Entry = warp landing (EntryWorldPos); Npc = Tess lobby anchor; Focus =
  // garden (where the find happens);
  // Prompt = bubble anchor beside Tess; Feedback = praise point before Tess;
  // Reward = far-bank clearing (completion moment); Exit = return marker.
  void BuildPresentationAnchors(Transform root) {
    ActivityAnchors a = ActivityAnchors.Ensure(root, "PresentationRoot");
    Anchors = a;
    if (a == null) return;
    a.Entry = a.EnsureSlot("EntryAnchor", new Vector3(0f, 0f, 0f));
    a.GameplayFocus = a.EnsureSlot("GameplayFocusAnchor", new Vector3(-16f, 0f, 5f));
    a.Npc = a.EnsureSlot("NpcAnchor", HostAnchorLocal);
    a.Camera = a.EnsureSlot("CameraAnchor", new Vector3(0f, 7.2f, -11f));
    a.CameraLook = a.EnsureSlot("CameraLookAnchor", new Vector3(0f, 1.5f, 5f));
    a.Prompt = a.EnsureSlot("PromptAnchor", HostAnchorLocal + new Vector3(1.45f, 1.78f, 0.55f));
    a.Feedback = a.EnsureSlot("FeedbackAnchor", HostAnchorLocal + new Vector3(1.0f, 1.2f, 1.4f));
    a.Reward = a.EnsureSlot("RewardAnchor", new Vector3(15.5f, 0f, -8.5f));
    a.Exit = a.EnsureSlot("ExitAnchor", new Vector3(0f, 0f, 12f));
  }

  // ---- palette (Math world grammar v2) ---------------------------------------
  static readonly Color AbacusBlue = new Color(0.25f, 0.45f, 0.85f);
  static readonly Color QuestGold = new Color(0.98f, 0.78f, 0.25f);
  static readonly Color SoilBrown = new Color(0.45f, 0.30f, 0.16f);
  static readonly Color PathTan = new Color(0.76f, 0.60f, 0.40f);
  static readonly Color CourtyardSand = new Color(0.86f, 0.78f, 0.62f);
  static readonly Color StoneGrey = new Color(0.62f, 0.60f, 0.55f);
  static readonly Color BrookWater = new Color(0.24f, 0.48f, 0.80f);
  static readonly Color CropGreen = new Color(0.30f, 0.58f, 0.28f);
  static readonly Color SunflowerYellow = new Color(0.98f, 0.78f, 0.20f);
  static readonly Color BerryRed = new Color(0.80f, 0.20f, 0.22f);
  static readonly Color FenceWood = new Color(0.58f, 0.42f, 0.24f);
  static readonly Color BasketTan = new Color(0.72f, 0.56f, 0.32f);
  static readonly Color LeafGreen = new Color(0.28f, 0.60f, 0.30f);
  static readonly Color PebbleGrey = new Color(0.60f, 0.60f, 0.62f);
  static readonly Color BloomPink = new Color(0.95f, 0.55f, 0.65f);
  static readonly Color BloomWhite = new Color(0.96f, 0.95f, 0.90f);
  static readonly Color ReedGreen = new Color(0.22f, 0.52f, 0.28f);
  static readonly Color SkirtGreen = new Color(0.24f, 0.47f, 0.33f);
  // B1R3: red/pink accent family (user round: world read too yellow/green).
  static readonly Color PinkAccent = new Color(0.93f, 0.45f, 0.62f);
  static readonly Color RedAccent = new Color(0.85f, 0.25f, 0.28f);
  // Hub phase: gate accent colors (shared with MicroWorldCatalog accents).
  static readonly Color Sky = new Color(0.45f, 0.70f, 0.92f);
  static readonly Color Plum = new Color(0.55f, 0.30f, 0.55f);

  // ---- ground + boundary ------------------------------------------------------

  void BuildGround(Transform parent) {
    // B1R3 (user round: "ngoài sân thành bầu trời, đỡ bí bách"): the Math
    // world is a floating island in the sky. A soil/rock rim under the lawn,
    // a cloud sea far below and cloud blobs around the rim replace the old
    // endless green field. All sky pieces are bake-ignored + collider-free.
    GameObject rim = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    rim.name = "MathSkyRim";
    rim.transform.SetParent(parent);
    // B1R4: the rim top sat EXACTLY at y=0 (coplanar with the ground plane) —
    // the brown/grass z-fight read as a disco floor while walking. Top must
    // ride just below the lawn. Cylinder primitive height = 2 * scaleY:
    // scaleY 1.4 -> height 2.8, center -1.5 -> top -0.10 (B1R5 fixed an
    // over-tall 2.8 scaleY that buried the whole island under the rim).
    rim.transform.localPosition = new Vector3(0f, -1.5f, 0f);
    rim.transform.localScale = new Vector3(52.6f, 1.4f, 52.6f);
    rim.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.42f, 0.33f, 0.24f));
    StripCollider(rim);
    IgnoreFromBuild(rim);

    // B1R7 (user round): the "cloud sea" plane at y=-14 filled the horizon
    // with white. Removed — beyond the island the camera background is SKY
    // blue; only floating cloud blobs remain (island in the sky, open view).
    float[] cloudAng = { 0f, 40f, 85f, 130f, 175f, 215f, 260f, 300f, 335f };
    for (int i = 0; i < cloudAng.Length; i++) {
      float rad = cloudAng[i] * Mathf.Deg2Rad;
      float dist = 34f + (i % 3) * 10f;
      Vector3 p = new Vector3(Mathf.Sin(rad) * dist, -4f - (i % 4) * 2.2f, Mathf.Cos(rad) * dist);
      GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      cloud.name = "MathSkyCloud" + i;
      cloud.transform.SetParent(parent);
      cloud.transform.localPosition = p;
      cloud.transform.localScale = new Vector3(8f + (i % 3) * 3f, 2.8f, 7f + (i % 2) * 2.4f);
      cloud.GetComponent<Renderer>().sharedMaterial = Lit(WorldBeauty.CloudPink);
      StripCollider(cloud);
      IgnoreFromBuild(cloud);
    }
    // A few lower clouds directly under the island edge (depth below the
    // cliff so the island visibly floats).
    float[] underAng = { 20f, 100f, 200f, 280f };
    for (int i = 0; i < underAng.Length; i++) {
      float rad = underAng[i] * Mathf.Deg2Rad;
      Vector3 p = new Vector3(Mathf.Sin(rad) * 20f, -9f - i * 2.2f, Mathf.Cos(rad) * 20f);
      GameObject cloud = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      cloud.name = "MathSkyCloudUnder" + i;
      cloud.transform.SetParent(parent);
      cloud.transform.localPosition = p;
      cloud.transform.localScale = new Vector3(9f, 2.6f, 8f);
      cloud.GetComponent<Renderer>().sharedMaterial = Lit(WorldBeauty.CloudPink);
      StripCollider(cloud);
      IgnoreFromBuild(cloud);
    }
    for (int i = 0; i < 3; i++) {
      GameObject high = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      high.name = "MathSkyCloudHigh" + i;
      high.transform.SetParent(parent);
      high.transform.localPosition = new Vector3(-30f + i * 30f, 16f + i * 3f, -38f + i * 8f);
      high.transform.localScale = new Vector3(12f, 3.4f, 9f);
      high.GetComponent<Renderer>().sharedMaterial = Lit(WorldBeauty.CloudPink);
      StripCollider(high);
      IgnoreFromBuild(high);
    }

    // B1R2 ROOT FIX (kept): Unity's cylinder mesh has radius 0.5 AND its
    // primitive collider is a CapsuleCollider — "scale 26" was r13, and
    // scaling a capsule non-uniformly produced a giant sphere (click rays
    // missed the ground, the agent could not path past r13). The ground is a
    // Plane now (10m at scale 1 -> 52x52m, flat MeshCollider like Main).
    GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
    ground.name = "MathGround";
    ground.transform.SetParent(parent);
    ground.transform.localPosition = Vector3.zero;
    ground.transform.localScale = new Vector3(5.2f, 1f, 5.2f);
    ground.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.36f, 0.62f, 0.34f));
    Flat(parent, "MathMeadowW", new Vector3(-10f, 0.02f, 7f), new Vector3(11f, 0.02f, 6f),
      new Color(0.30f, 0.55f, 0.30f));
    Flat(parent, "MathMeadowE", new Vector3(10f, 0.02f, 6f), new Vector3(10f, 0.02f, 5f),
      new Color(0.45f, 0.62f, 0.30f));
  }

  void BuildBoundary(Transform parent) {
    // Hedge ring r25: 20 base bushes + Kenney trees/bushes between them +
    // backdrop blobs INSIDE the island. Nothing floats beyond the ground.
    for (int i = 0; i < 20; i++) {
      float ang = i * 18f * Mathf.Deg2Rad;
      Vector3 p = new Vector3(Mathf.Sin(ang) * 24f, 0.3f, Mathf.Cos(ang) * 24f);
      GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bush.name = "MathHedge";
      bush.transform.SetParent(parent);
      bush.transform.localPosition = p;
      bush.transform.localScale = (i % 2 == 0) ? new Vector3(3.0f, 1.5f, 3.0f) : new Vector3(2.6f, 1.3f, 2.6f);
      bush.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.24f, 0.55f, 0.30f));
    }
    // Tree line INSIDE the hedge (r22), clearly on the lawn (B1R2: no tree at
    // the rim reads as "outside" because the outer field never ends).
    float[] treeAng = { 8f, 38f, 72f, 105f, 140f, 172f, 205f, 238f, 270f, 302f, 332f };
    for (int i = 0; i < treeAng.Length; i++) {
      float rad = treeAng[i] * Mathf.Deg2Rad;
      Vector3 p = new Vector3(Mathf.Sin(rad) * 22f, 0f, Mathf.Cos(rad) * 22f);
      string model = (i % 3 == 0) ? "tree_default" : (i % 3 == 1) ? "tree_oak" : "tree_detailed";
      float s = 2.0f + (i % 3) * 0.35f;
      PlaceProp(parent, model, "MathPropTree" + i, p, i * 33f, s);
    }
    // Backdrop blobs stay on the island behind the tree line.
    DressBackdrop(parent, "MathBackdropN", new Vector3(-8f, 0f, 22.5f), 2.4f);
    DressBackdrop(parent, "MathBackdropS", new Vector3(7f, 0f, -22.5f), 2.6f);
    DressBackdrop(parent, "MathBackdropE", new Vector3(22.5f, 0f, 6f), 2.2f);
    DressBackdrop(parent, "MathBackdropW", new Vector3(-22.5f, 0f, -4f), 2.3f);
  }

  void BuildPads(Transform parent) {
    // Pads top 0.015, paths top 0.047 (32mm apart, never coplanar).
    // Pad scale = DIAMETER (primitive cylinder radius 0.5): pass 2x radius.
    Pad(parent, "MathLobbyPad", new Vector3(0f, -0.005f, 0f), 11f, CourtyardSand);
    Pad(parent, "MathEntryPad", new Vector3(0f, -0.005f, -12f), 3.6f, new Color(0.80f, 0.68f, 0.48f));
    Pad(parent, "CountingGardenPad", new Vector3(-16f, -0.005f, 5f), 13f, new Color(0.55f, 0.75f, 0.55f));
    Pad(parent, "NumberBridgePad", new Vector3(15.5f, -0.005f, -5f), 8f, new Color(0.90f, 0.80f, 0.55f));
    Transform entryRoot = parent.Find("LearningEntryRoot");
    if (entryRoot == null) entryRoot = parent;
    Mark(entryRoot, MathLearningEntries.Lobby, new Vector3(0f, 0f, 0f));
    Mark(entryRoot, MathLearningEntries.CountingGarden, new Vector3(-16f, 0f, 5f));
    Mark(entryRoot, MathLearningEntries.NumberBridge, new Vector3(15.5f, 0f, -5f));
  }

  // ---- hub courtyard + host nook ----------------------------------------------

  void BuildHubCourtyard(Transform parent) {
    // Open courtyard: rim stones only (the old pot cluster + mouth markers
    // read as clutter in the user round). Four path mouths stay visible by
    // geometry (paths + rim gaps), not by prop piles.
    // B1R6: 4 larger rim stones on the diagonals only (the old 6 small pale
    // discs read as coins scattered around Tess).
    float[] diag = { 45f, 135f, 225f, 315f };
    for (int i = 0; i < diag.Length; i++) {
      float rad = diag[i] * Mathf.Deg2Rad;
      Vector3 p = new Vector3(Mathf.Sin(rad) * 5.4f, 0.1f, Mathf.Cos(rad) * 5.4f);
      GameObject stone = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      stone.name = "MathRimStone" + i;
      stone.transform.SetParent(parent);
      stone.transform.localPosition = p;
      stone.transform.localScale = new Vector3(0.7f, 0.1f, 0.55f);
      stone.transform.localRotation = Quaternion.Euler(0f, diag[i], 0f);
      stone.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.52f, 0.50f, 0.46f));
      StripCollider(stone);
    }
  }

  // ---- math identity (B1R2: "look once and know this is the math yard") -------
  // Domino line 1..6 flanking the entry axis (read from spawn), a 5-cube
  // Number Tower ahead-right, and + / − signs on the east lawn. All
  // collider-free primitives on shared materials.
  void BuildMathIdentity(Transform parent) {
    // B1R6: tidy number walk — 6 dominoes hugging the entry path shoulders
    // (x = +-1.9, evenly spaced 1.5m), instead of discs scattered on the lawn.
    Vector3[] dominoAt = {
      new Vector3(-1.9f, 0.03f, -4.5f), new Vector3(-1.9f, 0.03f, -6f), new Vector3(-1.9f, 0.03f, -7.5f),
      new Vector3(1.9f, 0.03f, -4.5f), new Vector3(1.9f, 0.03f, -6f), new Vector3(1.9f, 0.03f, -7.5f),
    };
    for (int i = 0; i < dominoAt.Length; i++) {
      GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      tile.name = "MathDomino" + i;
      tile.transform.SetParent(parent);
      tile.transform.localPosition = dominoAt[i];
      tile.transform.localScale = new Vector3(0.72f, 0.03f, 0.72f);
      tile.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.97f, 0.95f, 0.90f));
      StripCollider(tile);
      int count = i + 1;
      for (int p = 0; p < count; p++) {
        GameObject pip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pip.name = "MathDominoPip" + i + "_" + p;
        pip.transform.SetParent(parent);
        pip.transform.localPosition = dominoAt[i] + new Vector3(
          -0.19f + (p % 3) * 0.19f, 0.02f, -0.15f + (p / 3) * 0.30f);
        pip.transform.localScale = new Vector3(0.11f, 0.015f, 0.11f);
        pip.GetComponent<Renderer>().sharedMaterial = Lit(SoilBrown);
        StripCollider(pip);
      }
    }
    // Number Tower: 5 cubes, 1.0 -> 0.4, alternating blue/gold, 3.5m tall.
    // S1-final: relocated from the east foreground (it competed with the hub
    // in the spawn frame) to the east backdrop as a distant silhouette.
    // Names kept (P41D pins existence, positions are free).
    float[] size = { 1.0f, 0.85f, 0.7f, 0.55f, 0.4f };
    float y = 0f;
    for (int i = 0; i < size.Length; i++) {
      float h = size[i];
      y += h * 0.5f;
      Box(parent, "MathTower" + i, new Vector3(17f, y, 9f),
        new Vector3(h, h, h), i % 3 == 0 ? AbacusBlue : (i % 3 == 1 ? QuestGold : PinkAccent));
      y += h * 0.5f;
    }
    // Plus / minus signs: S1-final relocated from the east lawn (cluttered the
    // orchard approach) to the entry-path flanks as welcome markers.
    Box(parent, "MathSignPlusV", new Vector3(-2.8f, 0.85f, -8f),
      new Vector3(0.26f, 1.0f, 0.26f), AbacusBlue);
    Box(parent, "MathSignPlusH", new Vector3(-2.8f, 0.85f, -8f),
      new Vector3(1.0f, 0.26f, 0.26f), AbacusBlue);
    Box(parent, "MathSignMinus", new Vector3(2.8f, 0.85f, -8f),
      new Vector3(1.0f, 0.24f, 0.26f), QuestGold);
    Box(parent, "MathSignPost", new Vector3(-2.8f, 0.35f, -8f),
      new Vector3(0.16f, 0.7f, 0.16f), SoilBrown);
    Box(parent, "MathSignPost2", new Vector3(2.8f, 0.35f, -8f),
      new Vector3(0.16f, 0.7f, 0.16f), SoilBrown);
  }

  void BuildHostNook(Transform parent) {
    GameObject mat = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    mat.name = "MathHostMat";
    mat.transform.SetParent(parent);
    mat.transform.localPosition = new Vector3(HostAnchorLocal.x, 0.016f, HostAnchorLocal.z);
    mat.transform.localScale = new Vector3(2.2f, 0.02f, 2.2f);
    mat.GetComponent<Renderer>().sharedMaterial = Lit(new Color(0.72f, 0.78f, 0.62f));
    StripCollider(mat);
    // Two Kenney flower planters flank the host (clean backdrop behind her).
    PlaceProp(parent, "flower_redA", "MathHostFlowerA", new Vector3(2.2f, 0f, 2.6f), 0f, 2.4f);
    PlaceProp(parent, "flower_yellowA", "MathHostFlowerB", new Vector3(4.6f, 0f, 2.4f), 0f, 2.4f);
    GameObject basket = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    basket.name = "MathHostBasket";
    basket.transform.SetParent(parent);
    basket.transform.localPosition = new Vector3(4.2f, 0.2f, 0.2f);
    basket.transform.localScale = new Vector3(0.34f, 0.4f, 0.34f);
    basket.GetComponent<Renderer>().sharedMaterial = Lit(BasketTan);
    StripCollider(basket);
  }

  // ---- Counting Frame landmark (lobby) ----------------------------------------

  void BuildLobby(Transform parent) {
    // 5 rods x 3 beads, 2.0m tall: the hub landmark, clear of the x=0 arrival
    // corridor (P41E keeps the >0.9m rule) and off the garden/bridge lines.
    Vector3 p = new Vector3(-5.2f, 0f, -3.2f);
    Box(parent, "MathAbacusPostL", p + new Vector3(-1.0f, 0.95f, 0f),
      new Vector3(0.14f, 1.9f, 0.14f), AbacusBlue);
    Box(parent, "MathAbacusPostR", p + new Vector3(1.0f, 0.95f, 0f),
      new Vector3(0.14f, 1.9f, 0.14f), AbacusBlue);
    Box(parent, "MathAbacusBarT", p + new Vector3(0f, 1.86f, 0f),
      new Vector3(2.2f, 0.14f, 0.14f), AbacusBlue);
    Box(parent, "MathAbacusBarB", p + new Vector3(0f, 0.14f, 0f),
      new Vector3(2.2f, 0.14f, 0.14f), AbacusBlue);
    for (int r = 0; r < 5; r++) {
      float y = 0.42f + r * 0.36f;
      GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      rod.name = "MathAbacusRod" + r;
      rod.transform.SetParent(parent);
      rod.transform.localPosition = p + new Vector3(0f, y, 0f);
      rod.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
      rod.transform.localScale = new Vector3(0.06f, 1.9f, 0.06f);
      rod.GetComponent<Renderer>().sharedMaterial = Lit(SoilBrown);
      StripCollider(rod);
      for (int i = 0; i < 3; i++) {
        GameObject bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bead.name = "MathBead" + r + "_" + i;
        bead.transform.SetParent(parent);
        bead.transform.localPosition = p + new Vector3(-0.5f + i * 0.5f, y, 0f);
        bead.transform.localScale = new Vector3(0.26f, 0.26f, 0.26f);
        bead.GetComponent<Renderer>().sharedMaterial =
          Lit(i % 3 == 0 ? QuestGold : (i % 3 == 1 ? AbacusBlue : PinkAccent));
        StripCollider(bead);
      }
    }
  }

  // ---- world-name column (B1R3) ------------------------------------------------
  // The child spawns mid-world; the arrival beat frames this column ("Toán")
  // first, then the camera returns to the character.
  void BuildWorldSign(Transform parent) {
    Vector3 p = SignLocal;
    Box(parent, "MathWorldSignPost", p + new Vector3(0f, 1.1f, 0f),
      new Vector3(0.2f, 2.2f, 0.2f), SoilBrown);
    Vector3 boardCenter = p + new Vector3(0f, 2.15f, 0f);
    Box(parent, "MathWorldSignBoard", boardCenter,
      new Vector3(2.1f, 0.8f, 0.08f), FenceWood);
    // B1R6: the floating label used to sit INSIDE the board box (dark board,
    // no readable text). Use the proven hub-gate pattern: a yaw-locked label
    // parked 22cm in front of the board, facing the courtyard.
    Vector3 hostLocal = new Vector3(0f, 2.15f, 0f);
    Vector3 facing = (hostLocal - boardCenter);
    facing.y = 0f;
    if (facing.sqrMagnitude < 0.001f) facing = new Vector3(0f, 0f, 1f);
    facing.Normalize();
    GameObject labelGo = new GameObject("MathWorldSignLabel");
    labelGo.transform.SetParent(parent);
    WorldNameLabel label = labelGo.AddComponent<WorldNameLabel>();
    label.SetupLocked("Toán",
      parent.TransformPoint(boardCenter + facing * 0.22f),
      parent.TransformPoint(hostLocal));
    label.Show();
    for (int i = 0; i < 3; i++) {
      Ball(parent, "MathWorldSignBead" + i,
        p + new Vector3(-0.4f + i * 0.4f, 2.62f, 0f), 0.2f,
        i == 1 ? PinkAccent : QuestGold, false);
    }
  }

  // (B1R6: the courtyard bunting was removed — the diamonds read as floating
  // noise over every camera angle; red/pink now lives on flowers, mushrooms,
  // the Number Tower, the Counting Frame and the entry beads.)

  // ---- Counting Garden (west, -16,5) ------------------------------------------

  void BuildCountingGarden(Transform parent) {
    // Soil beds (baked obstacles; crops are collider-free dressing).
    Box(parent, "GardenBedW", new Vector3(-19.6f, 0.15f, 6.2f),
      new Vector3(1.5f, 0.3f, 0.7f), SoilBrown);
    Box(parent, "GardenBedE", new Vector3(-12.4f, 0.15f, 6.2f),
      new Vector3(1.5f, 0.3f, 0.7f), SoilBrown);
    Box(parent, "GardenBedN", new Vector3(-16f, 0.15f, 9.0f),
      new Vector3(1.7f, 0.3f, 0.7f), SoilBrown);
    Box(parent, "GardenBerryBed", new Vector3(-14.0f, 0.15f, 1.8f),
      new Vector3(1.5f, 0.3f, 0.7f), SoilBrown);
    // Pinned abstract counting row (1/2/3 cube pedestals) + gold One target.
    Pedestal(parent, "GardenPedestal2", new Vector3(-17.4f, 0f, 4.0f), 2, AbacusBlue);
    Pedestal(parent, "GardenPedestal3", new Vector3(-14.6f, 0f, 4.0f), 3, AbacusBlue);
    Box(parent, "GardenPedestal1", new Vector3(-16f, 0.25f, 4.0f),
      new Vector3(0.5f, 0.5f, 0.5f), SoilBrown);
    MakeCountable(parent, "MathOneCube", new Vector3(-16f, 0.7f, 4.0f), 0.4f, QuestGold, "one");
    BuildGardenCrops(parent);
    BuildGardenFence(parent);
    BuildCountingTree(parent);
    BuildGardenStones(parent);
    BuildBloomRoot(parent);
    // Garden gate at the east opening (toward the hub).
    PlaceProp(parent, "fence_gate", "MathGardenGate", new Vector3(-9.8f, 0f, 3.06f), 107.3f, 1.15f);
  }

  // S2 micro-world travel contract (v2): hub portal reference + hub-side
  // landing outside its fire radius; the garden scene's OWN anchors are pushed
  // in by GameInstaller on each lazy load (the garden is a separate scene now).
  public MicroWorldPortal CountingGardenPortal { get; private set; }
  public static readonly Vector3 GardenHubReturnLocal = new Vector3(-7.1f, 0f, -1.9f);
  // S3-P2Z14 gameplay #4: the build_yard gate's walk-in portal + the mini-tower
  // landmark beside it (its height reflects the last completed target).
  public MicroWorldPortal BuildTowerPortal { get; private set; }
  public BuildYardLandmark BuildTowerLandmark { get; private set; }
  // Exit landing: 3.5m hub-side of the portal (fireRadius 1.8 + rearm 0.8 =
  // 2.6 -> the arrival can never instantly re-fire the portal).
  public static readonly Vector3 BuildYardHubReturnLocal = new Vector3(4.4f, 0f, -8.8f);

  void BuildGardenCrops(Transform parent) {
    // Kenney Food Kit (CC0): identical crops per bed, child-scale counts.
    // BedW: 3 carrots. BedE: 2 pumpkins. BedN: 4 corn. BerryBed: 5 strawberries.
    for (int i = 0; i < 3; i++)
      PlaceProp(parent, "carrot", "MathCropCarrot" + i,
        new Vector3(-19.9f + i * 0.30f, 0.3f, 6.2f), i * 24f, 0.85f);
    for (int i = 0; i < 2; i++)
      PlaceProp(parent, "pumpkin", "MathCropPumpkin" + i,
        new Vector3(-12.75f + i * 0.66f, 0.3f, 6.2f), i * 40f, 1.25f);
    for (int i = 0; i < 4; i++)
      PlaceProp(parent, "corn", "MathCropCorn" + i,
        new Vector3(-16.6f + i * 0.4f, 0.3f, 9.0f), i * 30f, 0.95f);
    for (int i = 0; i < 5; i++)
      PlaceProp(parent, "strawberry", "MathCropStrawberry" + i,
        new Vector3(-14.6f + (i % 3) * 0.34f, 0.3f, 1.62f + (i / 3) * 0.34f), i * 47f, 1.1f);
    // Bed-front pips: each bed shows its count on the timber edge (B1R2 math
    // identity: the quantity is readable before any quest starts).
    BedPips(parent, "MathBedPipsW", new Vector3(-19.6f, 0f, 6.2f), 5.95f, 3);
    BedPips(parent, "MathBedPipsE", new Vector3(-12.4f, 0f, 6.2f), 5.95f, 2);
    BedPips(parent, "MathBedPipsN", new Vector3(-16f, 0f, 9.0f), 8.72f, 4);
    BedPips(parent, "MathBedPipsS", new Vector3(-14f, 0f, 1.8f), 1.52f, 5);
  }

  void BedPips(Transform parent, string name, Vector3 bedCenter, float frontZ, int count) {
    for (int p = 0; p < count; p++) {
      GameObject pip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      pip.name = name + p;
      pip.transform.SetParent(parent);
      pip.transform.localPosition = new Vector3(
        bedCenter.x - (count - 1) * 0.16f + p * 0.32f, 0.33f, frontZ);
      pip.transform.localScale = new Vector3(0.12f, 0.015f, 0.12f);
      pip.GetComponent<Renderer>().sharedMaterial = Lit(QuestGold);
      StripCollider(pip);
    }
  }

  void BuildGardenFence(Transform parent) {
    // Kenney fence modules around r6.5 with two openings (east gate 107.3 deg,
    // north 180 deg). Modules are 1m; openings skip ~2.4m so the eroded nav
    // corridor stays walkable. Meshes bake as a real fence (no colliders).
    Vector3 c = new Vector3(-16f, 0f, 5f);
    const float r = 6.5f;
    const float step = 8.82f; // ~1m chord at r6.5
    for (int i = 0; i < 41; i++) {
      float ang = i * step;
      if (Mathf.Abs(Mathf.DeltaAngle(ang, 107.3f)) < 11f) continue; // east gate
      if (Mathf.Abs(Mathf.DeltaAngle(ang, 180f)) < 11f) continue;   // north exit
      float rad = ang * Mathf.Deg2Rad;
      Vector3 p = c + new Vector3(Mathf.Sin(rad) * r, 0f, Mathf.Cos(rad) * r);
      string model = (i % 5 == 0) ? "fence_simpleLow" : "fence_simple";
      PlaceProp(parent, model, "MathFence" + i, p, ang, 1.06f);
    }
  }

  void BuildCountingTree(Transform parent) {
    Vector3 tp = new Vector3(-20.8f, 0f, 8.2f);
    PlaceProp(parent, "tree_default", "MathCountingTree", tp, 20f, 2.4f);
    for (int i = 0; i < 5; i++) {
      Ball(parent, "MathTreeBead" + i,
        tp + new Vector3(0.75f + (i % 3) * 0.24f, 1.75f + (i / 3) * 0.32f, 0.7f),
        0.26f, i % 2 == 0 ? QuestGold : AbacusBlue, false);
    }
    // Giant Sunflower: the plot's terminus + future reward reveal anchor.
    Vector3 sp = new Vector3(-21.5f, 0f, 6.8f);
    GameObject gstem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    gstem.name = "MathGiantSunflowerStem";
    gstem.transform.SetParent(parent);
    gstem.transform.localPosition = sp + new Vector3(0f, 0.8f, 0f);
    gstem.transform.localScale = new Vector3(0.10f, 0.8f, 0.10f);
    gstem.GetComponent<Renderer>().sharedMaterial = Lit(CropGreen);
    StripCollider(gstem);
    Ball(parent, "MathGiantSunflowerHead", sp + new Vector3(0f, 1.65f, 0f),
      0.60f, SunflowerYellow, false).transform.localScale =
        new Vector3(0.90f, 0.22f, 0.90f);
  }

  void BuildGardenStones(Transform parent) {
    // 5 Kenney stepping stones with 1..5 gold pips along the inner path.
    Vector3[] at = {
      new Vector3(-11.0f, 0.04f, 3.35f), new Vector3(-12.8f, 0.04f, 3.35f),
      new Vector3(-14.6f, 0.04f, 3.00f), new Vector3(-16.6f, 0.04f, 2.50f),
      new Vector3(-18.2f, 0.04f, 1.20f),
    };
    for (int i = 0; i < at.Length; i++) {
      PlaceProp(parent, "path_stoneCircle", "MathGardenStone" + i, at[i], 0f, 0.62f);
      for (int p = 0; p <= i; p++) {
        GameObject pip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pip.name = "MathGardenStonePip" + i + "_" + p;
        pip.transform.SetParent(parent);
        pip.transform.localPosition = at[i] + new Vector3(
          -0.14f + (p % 3) * 0.14f, 0.09f, -0.10f + (p / 3) * 0.20f);
        pip.transform.localScale = new Vector3(0.08f, 0.012f, 0.08f);
        pip.GetComponent<Renderer>().sharedMaterial = Lit(QuestGold);
        StripCollider(pip);
      }
    }
  }

  // ---- Number Bridge (east, 15.5,-5) ------------------------------------------

  void BuildNumberBridge(Transform parent) {
    // Brook (water denied by BuildCarves except the deck corridor) + Kenney
    // boardwalk modules (visual-only, ignored by the bake) over a walkable
    // primitive deck + far-bank clearing.
    GameObject stream = GameObject.CreatePrimitive(PrimitiveType.Cube);
    stream.name = "BridgeStream";
    stream.transform.SetParent(parent);
    stream.transform.localPosition = new Vector3(15.5f, 0.012f, -5f);
    stream.transform.localScale = new Vector3(9f, 0.024f, 1.8f);
    stream.GetComponent<Renderer>().sharedMaterial = Lit(BrookWater);
    StripCollider(stream);
    // Walkable deck (bakes) + Kenney bridge modules on top (ignored).
    Box(parent, "BridgeDeck", new Vector3(15.5f, 0.10f, -5f),
      new Vector3(1.6f, 0.12f, 3.8f), FenceWood);
    string[] modules = { "bridge_side_wood", "bridge_center_wood", "bridge_side_wood" };
    for (int i = 0; i < modules.Length; i++) {
      GameObject m = PropKit.Place(parent, modules[i], new Vector3(15.5f, 0.16f, -6.2f + i * 1.2f),
        i == 0 ? 0f : (i == 2 ? 180f : 0f), 1.15f, true);
      if (m != null) m.name = "MathBridgeModule" + i;
    }
    Box(parent, "BridgeRailW", new Vector3(14.75f, 0.45f, -5f),
      new Vector3(0.1f, 0.5f, 3.8f), FenceWood);
    Box(parent, "BridgeRailE", new Vector3(16.25f, 0.45f, -5f),
      new Vector3(0.1f, 0.5f, 3.8f), FenceWood);
    for (int i = 0; i < 5; i++) {
      GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
      post.name = "MathBridgeCountPost" + i;
      post.transform.SetParent(parent);
      post.transform.localPosition = new Vector3(14.65f, 0.45f, -6.5f + i * 0.75f);
      post.transform.localScale = new Vector3(0.14f, 0.9f, 0.14f);
      post.GetComponent<Renderer>().sharedMaterial = Lit(FenceWood);
      StripCollider(post);
    }
    // Banks: Kenney rocks/plants + reeds + lilies (collider-free dressing).
    PlaceProp(parent, "rock_largeA", "MathBrookRockA", new Vector3(12.4f, 0f, -6.6f), 20f, 1.8f);
    PlaceProp(parent, "rock_largeB", "MathBrookRockB", new Vector3(18.8f, 0f, -3.4f), 140f, 1.6f);
    PlaceProp(parent, "plant_bush", "MathBrookPlantA", new Vector3(11.6f, 0f, -3.5f), 0f, 2.2f);
    PlaceProp(parent, "plant_bushSmall", "MathBrookPlantB", new Vector3(19.2f, 0f, -6.4f), 0f, 2.4f);
    DressReeds(parent, "MathReedsW", new Vector3(12.2f, 0f, -4.15f));
    DressReeds(parent, "MathReedsE", new Vector3(18.6f, 0f, -5.85f));
    for (int i = 0; i < 3; i++) {
      GameObject lily = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
      lily.name = "MathBrookLily" + i;
      lily.transform.SetParent(parent);
      lily.transform.localPosition = new Vector3(12.8f + i * 2.2f, 0.03f, -5.05f);
      lily.transform.localScale = new Vector3(0.36f, 0.012f, 0.36f);
      lily.GetComponent<Renderer>().sharedMaterial = Lit(CropGreen);
      StripCollider(lily);
    }
    Box(parent, "BridgeBlockA", new Vector3(17.4f, 0.13f, -3.2f),
      new Vector3(0.26f, 0.26f, 0.26f), AbacusBlue);
    Box(parent, "BridgeBlockB", new Vector3(17.4f, 0.39f, -3.2f),
      new Vector3(0.26f, 0.26f, 0.26f), QuestGold);
    Box(parent, "BridgeBlockC", new Vector3(13.6f, 0.13f, -6.8f),
      new Vector3(0.26f, 0.26f, 0.26f), QuestGold);
    BuildStoneClearing(parent);
  }

  void BuildStoneClearing(Transform parent) {
    // Far-bank (north) number stones + reward bead pile: the reason to cross.
    Vector3 c = new Vector3(15.5f, 0f, -8.5f);
    for (int i = 0; i < 5; i++) {
      float ang = (i * 72f + 36f) * Mathf.Deg2Rad;
      Vector3 p = c + new Vector3(Mathf.Sin(ang) * 1.7f, 0.04f, Mathf.Cos(ang) * 1.7f);
      PlaceProp(parent, "path_stoneCircle", "MathClearingStone" + i, p, i * 30f, 0.72f);
      for (int q = 0; q <= i; q++) {
        GameObject pip = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pip.name = "MathClearingPip" + i + "_" + q;
        pip.transform.SetParent(parent);
        pip.transform.localPosition = p + new Vector3(
          -0.14f + (q % 3) * 0.14f, 0.09f, -0.10f + (q / 3) * 0.20f);
        pip.transform.localScale = new Vector3(0.08f, 0.012f, 0.08f);
        pip.GetComponent<Renderer>().sharedMaterial = Lit(QuestGold);
        StripCollider(pip);
      }
    }
    for (int i = 0; i < 3; i++) {
      Ball(parent, "MathRewardBead" + i,
        new Vector3(15.5f, 0.22f + i * 0.30f, -10.2f),
        0.42f - i * 0.06f, i == 1 ? AbacusBlue : QuestGold, false);
    }
    PlaceProp(parent, "rock_smallA", "MathClearingRockA", new Vector3(13.2f, 0f, -9.6f), 0f, 2.2f);
    PlaceProp(parent, "rock_smallB", "MathClearingRockB", new Vector3(17.8f, 0f, -9.8f), 90f, 2.0f);
  }

  // ---- paths ------------------------------------------------------------------

  void BuildPaths(Transform parent) {
    Flat(parent, "MathPathEntry", new Vector3(0f, 0.032f, -6f), new Vector3(1.8f, 0.03f, 12.4f));
    Flat(parent, "MathPathReturn", new Vector3(0f, 0.032f, 6f), new Vector3(1.8f, 0.03f, 12.4f));
    Seg(parent, "MathPathGarden", new Vector3(0f, 0f, 0f), new Vector3(-16f, 0f, 5f), 1.7f);
    Seg(parent, "MathPathBridge", new Vector3(0f, 0f, 0f), new Vector3(15.5f, 0f, -5f), 1.7f);
    // Garden inner path: east gate -> plot center -> north exit.
    Seg(parent, "MathPathGardenInnerA", new Vector3(-9.8f, 0f, 3.06f), new Vector3(-12.2f, 0f, 3.6f), 1.3f);
    Seg(parent, "MathPathGardenInnerB", new Vector3(-12.2f, 0f, 3.6f), new Vector3(-14.6f, 0f, 3.0f), 1.3f);
    Seg(parent, "MathPathGardenInnerC", new Vector3(-14.6f, 0f, 3.0f), new Vector3(-18.2f, 0f, 1.2f), 1.3f);
    Seg(parent, "MathPathGardenInnerD", new Vector3(-18.2f, 0f, 1.2f), new Vector3(-16f, 0f, -1.5f), 1.3f);
    // Meadow loop: far-bank clearing -> west meadow -> garden north exit.
    Seg(parent, "MathPathMeadow1", new Vector3(15.5f, 0f, -8.5f), new Vector3(9f, 0f, -9.2f), 1.5f);
    Seg(parent, "MathPathMeadow2", new Vector3(9f, 0f, -9.2f), new Vector3(2f, 0f, -9.6f), 1.5f);
    Seg(parent, "MathPathMeadow3", new Vector3(2f, 0f, -9.6f), new Vector3(-5f, 0f, -9.0f), 1.5f);
    Seg(parent, "MathPathMeadow4", new Vector3(-5f, 0f, -9.0f), new Vector3(-11f, 0f, -6.8f), 1.5f);
    Seg(parent, "MathPathMeadow5", new Vector3(-11f, 0f, -6.8f), new Vector3(-16f, 0f, -1.5f), 1.5f);
    // Bridge approach chevrons.
    for (int i = 0; i < 3; i++)
      Flat(parent, "MathChevron" + i, new Vector3(11.4f + i * 0.9f, 0.032f, -4.15f - i * 0.28f),
        new Vector3(0.34f, 0.03f, 0.34f), PathTan);
  }

  // ---- entry / return arches --------------------------------------------------

  void BuildEntryBoard(Transform parent) {
    // Entry bead arch at z=-12: two blue posts + beam + 5 beads (the world's
    // number on the door). Beam + beads above 2m and bake-ignored.
    Box(parent, "MathEntryPostL", new Vector3(-1.1f, 1.0f, -12.0f),
      new Vector3(0.14f, 2.0f, 0.14f), AbacusBlue);
    Box(parent, "MathEntryPostR", new Vector3(1.1f, 1.0f, -12.0f),
      new Vector3(0.14f, 2.0f, 0.14f), AbacusBlue);
    GameObject beamTop = Box(parent, "MathEntryBeam", new Vector3(0f, 2.06f, -12.0f),
      new Vector3(2.4f, 0.12f, 0.12f), AbacusBlue);
    StripCollider(beamTop);
    IgnoreFromBuild(beamTop);
    for (int i = 0; i < 5; i++) {
      GameObject bead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      bead.name = "MathEntryBead" + i;
      bead.transform.SetParent(parent);
      bead.transform.localPosition = new Vector3(-0.7f + i * 0.35f, 2.2f, -12.0f);
      bead.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
      bead.GetComponent<Renderer>().sharedMaterial =
        Lit(i % 3 == 0 ? QuestGold : (i % 3 == 1 ? AbacusBlue : PinkAccent));
      StripCollider(bead);
      IgnoreFromBuild(bead);
    }
  }

  void BuildReturnArch(Transform parent) {
    // S2 user round ("quá nhiều cổng thừa"): the way home is a MARKER, not a
    // second gate. The old gold pillars + beam competed with the 10
    // destination gates; the proven contract (gold disc + "Về" label +
    // invisible one-way trigger, same as the 3 spatial districts) stays.
    Flat(parent, "MathReturnDisc", new Vector3(0f, 0.05f, 12f),
      new Vector3(2.6f, 0.024f, 2.6f), new Color(0.98f, 0.78f, 0.25f));
    // Way home named like the 3 spatial subjects: gold disc + "Về" label.
    GameObject retAnchor = new GameObject("MathReturnLabelAnchor");
    retAnchor.transform.SetParent(parent);
    retAnchor.transform.localPosition = new Vector3(0f, 0f, 12f);
    GameObject retLabelGo = new GameObject("MathReturnLabel");
    retLabelGo.transform.SetParent(parent);
    WorldNameLabel retLabel = retLabelGo.AddComponent<WorldNameLabel>();
    retLabel.Setup("Về", retAnchor.transform, 2.2f);
    retLabel.Show();
  }

  // ---- identity + decor -------------------------------------------------------

  void BuildShapeTrio(Transform parent) {
    // S1 declutter: the trio read as random toys at the garden mouth. Same
    // three shapes, recomposed as totems flanking the Great Abacus landmark
    // (names kept — P41D pins existence, positions are free).
    Box(parent, "MathShapeCube", new Vector3(4.2f, 0.25f, 9.0f),
      new Vector3(0.5f, 0.5f, 0.5f), AbacusBlue);
    GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    ball.name = "MathShapeBall";
    ball.transform.SetParent(parent);
    ball.transform.localPosition = new Vector3(6.8f, 0.25f, 9.0f);
    ball.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
    ball.GetComponent<Renderer>().sharedMaterial = Lit(QuestGold);
    StripCollider(ball);
    GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pillar.name = "MathShapePillar";
    pillar.transform.SetParent(parent);
    pillar.transform.localPosition = new Vector3(5.5f, 0.25f, 10.6f);
    pillar.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
    pillar.GetComponent<Renderer>().sharedMaterial = Lit(SoilBrown);
    StripCollider(pillar);
  }

  void BuildBridgeDiscs(Transform parent) {
    // Bead garland hanging over the boardwalk (visual-only, above headroom).
    GameObject rod = GameObject.CreatePrimitive(PrimitiveType.Cube);
    rod.name = "MathBridgeLanternRod";
    rod.transform.SetParent(parent);
    rod.transform.localPosition = new Vector3(15.5f, 2.55f, -5.0f);
    rod.transform.localScale = new Vector3(0.08f, 0.08f, 1.8f);
    rod.GetComponent<Renderer>().sharedMaterial = Lit(FenceWood);
    StripCollider(rod);
    IgnoreFromBuild(rod);
    for (int i = 0; i < 3; i++) {
      GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Sphere);
      disc.name = "MathBridgeDisc" + i;
      disc.transform.SetParent(parent);
      disc.transform.localPosition = new Vector3(15.5f, 2.30f, -5.6f + i * 0.6f);
      disc.transform.localScale = new Vector3(0.28f, 0.28f, 0.28f);
      disc.GetComponent<Renderer>().sharedMaterial =
        Lit(i == 1 ? AbacusBlue : QuestGold);
      StripCollider(disc);
      IgnoreFromBuild(disc);
    }
  }

  void BuildDecor(Transform parent) {
    // S1-final declutter: the two stray pebbles + four far-flung blooms were
    // pure filler (kept: return-arch flowers = navigation framing).
    // Host-side + hub accents only (the hub stays open; no center clutter).
    DressBloom(parent, "MathReturnFlowerL", new Vector3(-2.2f, 0f, 13.0f), BloomPink);
    DressBloom(parent, "MathReturnFlowerR", new Vector3(2.2f, 0f, 13.0f), BloomWhite);
    // Kenney grass/flowers/mushrooms: cluster-gap rhythm around the meadows.
    PlaceProp(parent, "grass_large", "MathPropGrassA", new Vector3(-6f, 0f, -2.5f), 0f, 1.6f);
    PlaceProp(parent, "grass", "MathPropGrassB", new Vector3(5.5f, 0f, 2.5f), 40f, 1.8f);
    PlaceProp(parent, "grass_large", "MathPropGrassC", new Vector3(-9f, 0f, 10f), 90f, 1.7f);
    PlaceProp(parent, "grass", "MathPropGrassD", new Vector3(9f, 0f, 9f), 130f, 1.8f);
    PlaceProp(parent, "grass_large", "MathPropGrassE", new Vector3(3f, 0f, -14f), 200f, 1.6f);
    PlaceProp(parent, "grass", "MathPropGrassF", new Vector3(-3f, 0f, 14f), 300f, 1.7f);
    PlaceProp(parent, "flower_purpleA", "MathPropFlowerA", new Vector3(-7.5f, 0f, 0.5f), 0f, 1.8f);
    PlaceProp(parent, "flower_redA", "MathPropFlowerB", new Vector3(8.5f, 0f, -0.5f), 0f, 1.8f);
    PlaceProp(parent, "flower_yellowA", "MathPropFlowerC", new Vector3(-5f, 0f, 8.5f), 0f, 1.8f);
    PlaceProp(parent, "mushroom_red", "MathPropMushroomA", new Vector3(-18f, 0f, 12.5f), 0f, 2.2f);
    PlaceProp(parent, "mushroom_tan", "MathPropMushroomB", new Vector3(18.5f, 0f, 8.5f), 60f, 2.2f);
    // B1R3 red/pink accent clusters (the world read too yellow/green).
    PlaceProp(parent, "flower_redA", "MathPropFlowerD", new Vector3(-4.4f, 0f, 3.6f), 0f, 2.2f);
    PlaceProp(parent, "flower_purpleA", "MathPropFlowerE", new Vector3(4.6f, 0f, -3.2f), 0f, 2.2f);
    PlaceProp(parent, "flower_redA", "MathPropFlowerF", new Vector3(-14.5f, 0f, -2.6f), 0f, 2.4f);
    PlaceProp(parent, "flower_purpleA", "MathPropFlowerG", new Vector3(12.5f, 0f, -1.2f), 0f, 2.4f);
    PlaceProp(parent, "mushroom_red", "MathPropMushroomC", new Vector3(9.5f, 0f, 7.5f), 30f, 2.6f);
    PlaceProp(parent, "plant_bush", "MathPropBushA", new Vector3(-11f, 0f, -4.5f), 0f, 2.0f);
    PlaceProp(parent, "plant_bushSmall", "MathPropBushB", new Vector3(11f, 0f, 3.5f), 0f, 2.2f);
    PlaceProp(parent, "plant_bush", "MathPropBushC", new Vector3(6f, 0f, -12f), 0f, 2.0f);
    PlaceProp(parent, "plant_bushSmall", "MathPropBushD", new Vector3(-14f, 0f, -8f), 0f, 2.2f);
    PlaceProp(parent, "rock_largeA", "MathPropRockA", new Vector3(19.5f, 0f, 1.5f), 30f, 2.0f);
    PlaceProp(parent, "rock_largeB", "MathPropRockB", new Vector3(-21f, 0f, -6f), 120f, 1.9f);
    PlaceProp(parent, "rock_smallA", "MathPropRockC", new Vector3(-2.5f, 0f, -17f), 210f, 2.2f);
    PlaceProp(parent, "stump_round", "MathPropStumpA", new Vector3(-20f, 0f, 12f), 0f, 2.4f);
    PlaceProp(parent, "log", "MathPropLogA", new Vector3(16f, 0f, 12.5f), 25f, 2.4f);
    BuildBeautyPass(parent);
  }

  // S6 BEAUTY + PINK PASS (user rounds: "world đẹp hơn" + "gam hồng cho con
  // gái"): blossom trees on the lawns, petal carpets, pink flower drifts and
  // a pastel rainbow landing behind the north gates (the arrival/lobby view
  // looks north, so the rainbow frames the hub). S7 FULL-BLOOM (user round:
  // "đẩy tới nóc"): more trees/drifts everywhere, falling pink petals over
  // the hub, flapping pastel butterflies around the flower spots and a
  // blossom crown over the entry board. All collider-free dressing, off every
  // walking line, deterministic (seeded, no Random at build time).
  void BuildBeautyPass(Transform parent) {
    // Blossom trees (pink canopies) on the open lawns between the spokes.
    Vector3[] trees = {
      new Vector3(7.2f, 0f, 6.5f), new Vector3(-6.8f, 0f, 7.5f),
      new Vector3(9.5f, 0f, -6f), new Vector3(-9.5f, 0f, -7.5f),
      new Vector3(2.5f, 0f, 11.5f), new Vector3(10.2f, 0f, 6.2f),
      // S7: west/east/north/south perimeter blush + garden + bridge flanks.
      // S8: (21,-1.5)/(21.5,8.5) keep clear of the real Number Bridge deck and
      // the Orchard gate trees (the old spots visually stuck to them).
      new Vector3(-20.5f, 0f, -1.5f), new Vector3(21.5f, 0f, 8.5f),
      new Vector3(-1.5f, 0f, 18f), new Vector3(21f, 0f, -1.5f),
      new Vector3(2f, 0f, -18f), new Vector3(-19.5f, 0f, -11f),
    };
    for (int i = 0; i < trees.Length; i++) {
      float s = 0.9f + (i % 3) * 0.12f;
      WorldBeauty.BlossomTree(parent, "MathBlossomTree" + i, trees[i], s);
      WorldBeauty.PetalCarpet(parent, "MathPetalCarpet" + i, trees[i], 2.8f * s);
    }
    // Pink flower drifts: lobby rim (between the path mouths) + entry edges
    // + the far zones (garden mouth, bridge bank, north ring, south lawn).
    Vector3[] drifts = {
      new Vector3(3.4f, 0f, 5.6f), new Vector3(-3.2f, 0f, 5.8f),
      new Vector3(4.0f, 0f, -5.4f), new Vector3(-4.2f, 0f, -5.2f),
      new Vector3(-2.6f, 0f, -3.6f), new Vector3(2.6f, 0f, -3.6f),
      new Vector3(-2.6f, 0f, -8.5f), new Vector3(2.6f, 0f, -8.5f),
      new Vector3(-17f, 0f, 12f), new Vector3(12.5f, 0f, -3.5f),
      new Vector3(-13f, 0f, 11f), new Vector3(9.5f, 0f, 11.5f),
      new Vector3(-4.6f, 0f, -4.6f), new Vector3(4.8f, 0f, 4.6f),
    };
    for (int i = 0; i < drifts.Length; i++)
      WorldBeauty.FlowerDrift(parent, "MathFlowerDrift" + i, drifts[i], 1.3f + (i % 2) * 0.25f);
    // Falling petals over the hub (S7) + blossom crown on the entry board.
    WorldBeauty.PetalFall(parent, "MathPetalFall", new Vector3(0f, 0f, 0f), 13f, 20, 60601);
    WorldBeauty.Ball(parent, "MathEntryBlossom0", new Vector3(0f, 2.78f, -12f), 1.1f, WorldBeauty.BlossomPink);
    WorldBeauty.Ball(parent, "MathEntryBlossom1", new Vector3(-0.72f, 2.58f, -12f), 0.8f, WorldBeauty.BlossomDeep);
    WorldBeauty.Ball(parent, "MathEntryBlossom2", new Vector3(0.72f, 2.64f, -12f), 0.85f, WorldBeauty.BlossomCream);
    // Flapping butterflies around the flower spots (S7).
    WorldBeauty.Butterfly(parent, "MathButterfly0", new Vector3(3.4f, 0f, 5.6f), 2.2f, 0.0f,
      WorldBeauty.BlossomDeep, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "MathButterfly1", new Vector3(-3.2f, 0f, 5.8f), 2.0f, 0.35f,
      WorldBeauty.Lilac, WorldBeauty.BlossomPink);
    WorldBeauty.Butterfly(parent, "MathButterfly2", new Vector3(0f, 0f, -4.5f), 2.4f, 0.6f,
      WorldBeauty.CreamGold, WorldBeauty.BlossomPink);
    WorldBeauty.Butterfly(parent, "MathButterfly3", new Vector3(2.5f, 0f, 11.5f), 2.2f, 0.15f,
      WorldBeauty.Peach, WorldBeauty.BlossomCream);
    WorldBeauty.Butterfly(parent, "MathButterfly4", new Vector3(-6.8f, 0f, 7.5f), 2.0f, 0.8f,
      WorldBeauty.Sky, WorldBeauty.BlossomPink);
    // Pastel rainbow (pink-forward) landing on the lawn behind the north
    // gates — visible in the arrival shot and from the whole lobby.
    WorldBeauty.PastelRainbow(parent, "MathRainbow", new Vector3(0f, 0f, 21f), 11f);
  }

  // ---- Kenney nature placement (MathProp*) ------------------------------------

  void BuildNature(Transform parent) {
    // Extra tree clusters (composed, gaps kept clear of every walking line).
    PlaceProp(parent, "tree_oak", "MathPropTreeA", new Vector3(-19f, 0f, 16.5f), 20f, 2.4f);
    PlaceProp(parent, "tree_detailed", "MathPropTreeB", new Vector3(20f, 0f, 15.5f), 140f, 2.5f);
    PlaceProp(parent, "tree_default", "MathPropTreeC", new Vector3(21f, 0f, -15f), 75f, 2.3f);
    PlaceProp(parent, "tree_oak", "MathPropTreeD", new Vector3(-21.5f, 0f, -13f), 300f, 2.4f);
    PlaceProp(parent, "tree_detailed", "MathPropTreeE", new Vector3(0f, 0f, 19f), 10f, 2.6f);
    PlaceProp(parent, "tree_pineTallA", "MathPropPineA", new Vector3(-13f, 0f, 17f), 200f, 2.6f);
    PlaceProp(parent, "tree_pineTallA", "MathPropPineB", new Vector3(13f, 0f, 17.5f), 90f, 2.4f);
  }

  static void PlaceProp(Transform parent, string prop, string goName, Vector3 pos, float yaw, float scale) {
    GameObject go = PropKit.Place(parent, prop, pos, yaw, scale);
    if (go != null) go.name = goName;
  }

  // ---- small builders ---------------------------------------------------------

  static void Pedestal(Transform parent, string name, Vector3 basePos, int cubes, Color color) {
    Box(parent, name, basePos + new Vector3(0f, 0.25f, 0f),
      new Vector3(0.5f, 0.5f, 0.5f), SoilBrown);
    for (int i = 0; i < cubes; i++) {
      GameObject cube = Box(parent, name + "Cube" + i,
        basePos + new Vector3(0f, 0.63f + i * 0.26f, 0f),
        new Vector3(0.26f, 0.26f, 0.26f), color);
      StripCollider(cube);
    }
  }

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
    go.AddComponent<MathBeacon>();
    CountingObjects.Add(inter);
  }

  void BuildBloomRoot(Transform parent) {
    GameObject root = new GameObject("MathBloomRoot");
    root.transform.SetParent(parent);
    root.transform.localPosition = new Vector3(-16f, 0f, 5f);
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
    root.SetActive(true);
    BloomRoot = root.transform;
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

  void BuildSun(Transform parent) {
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
    // P3.0.1 journey fix (P2): CollectObjects.All collected EVERY loaded
    // scene — the Math bake log listed Main-only meshes (LwGrass/Flowers/
    // Bush_2), so the Math load produced overlapping navmesh data over Main
    // and baked the Main player/NPCs as obstacles into it. Bake ONLY this
    // root's children: the surface lives ON the root and uses Children.
    NavMeshSurface surface = parent.gameObject.GetComponent<NavMeshSurface>();
    if (surface == null) surface = parent.gameObject.AddComponent<NavMeshSurface>();
    surface.collectObjects = CollectObjects.Children;
    surface.BuildNavMesh();
  }

  // Brook carves (after the bake): deny feet on the water except the deck
  // corridor (x 14.6..16.4 stays open).
  void BuildCarves(Transform parent) {
    CarveBox(parent, "MathBrookCarveW", new Vector3(13.05f, 0.5f, -5f), new Vector3(3.1f, 1f, 2.2f));
    CarveBox(parent, "MathBrookCarveE", new Vector3(17.95f, 0.5f, -5f), new Vector3(3.1f, 1f, 2.2f));
  }

  static void CarveBox(Transform parent, string name, Vector3 localPos, Vector3 size) {
    GameObject go = new GameObject(name);
    go.transform.SetParent(parent);
    go.transform.localPosition = localPos;
    NavMeshObstacle obstacle = go.AddComponent<NavMeshObstacle>();
    obstacle.shape = NavMeshObstacleShape.Box;
    obstacle.center = Vector3.zero;
    obstacle.size = size;
    obstacle.carving = true;
    obstacle.carveOnlyStationary = true;
  }

  void BindReturnGate(Transform parent, IWorldNavService nav, Transform playerT) {
    GameObject gateGo = new GameObject("MathReturnGate");
    gateGo.transform.SetParent(parent);
    gateGo.transform.position = parent.TransformPoint(new Vector3(0f, 0f, 12f));
    SubjectGate gate = gateGo.AddComponent<SubjectGate>();
    gate.fireRadius = 1.6f;
    // P3.0.1 journey bug (P1): the return gate was bound to SubjectIds.Main,
    // but SubjectGate's return branch fires only when Current == target — in
    // MathScene Current is "math", so the arch could NEVER trigger and the
    // child was trapped (REAL journey: player stood 0.8m from the arch, no
    // return). Bind the SUBJECT id, exactly like SubjectWorldBuilder's
    // spatial return arches (SetWorldNav).
    gate.Bind(nav, SubjectIds.Math, true, playerT);
  }

  static void Pad(Transform parent, string name, Vector3 localPos, float diameter, Color color) {
    // Visual ground treatment only: the primitive cylinder collider scales
    // into a giant sphere (B1R2 click-ray bug) — strip it; the navmesh still
    // bakes the flat pad mesh and clicks land on the ground plane below.
    GameObject pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    pad.name = name;
    pad.transform.SetParent(parent);
    pad.transform.localPosition = localPos;
    pad.transform.localScale = new Vector3(diameter, 0.02f, diameter);
    pad.GetComponent<Renderer>().sharedMaterial = Lit(color);
    StripCollider(pad);
  }

  static void Mark(Transform parent, string id, Vector3 localPos) {
    GameObject m = new GameObject(id);
    m.transform.SetParent(parent);
    m.transform.localPosition = localPos;
  }

  static void Flat(Transform parent, string name, Vector3 localPos, Vector3 scale) {
    Flat(parent, name, localPos, scale, PathTan);
  }

  static void Flat(Transform parent, string name, Vector3 localPos, Vector3 scale, Color color) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent);
    go.transform.localPosition = localPos;
    go.transform.localScale = scale;
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
  }

  static void Seg(Transform parent, string name, Vector3 a, Vector3 b, float width) {
    Vector3 flatA = new Vector3(a.x, 0f, a.z);
    Vector3 flatB = new Vector3(b.x, 0f, b.z);
    Vector3 mid = (flatA + flatB) * 0.5f;
    mid.y = 0.032f;
    float len = Vector3.Distance(flatA, flatB);
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
    go.name = name;
    go.transform.SetParent(parent);
    go.transform.localPosition = mid;
    go.transform.localRotation = Quaternion.LookRotation((flatB - flatA).normalized);
    go.transform.localScale = new Vector3(width, 0.03f, len);
    go.GetComponent<Renderer>().sharedMaterial = Lit(PathTan);
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

  static GameObject Ball(Transform parent, string goName, Vector3 localPos, float diameter, Color color, bool keepCollider) {
    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    go.name = goName;
    go.transform.SetParent(parent);
    go.transform.localPosition = localPos;
    go.transform.localScale = new Vector3(diameter, diameter, diameter);
    go.GetComponent<Renderer>().sharedMaterial = Lit(color);
    if (!keepCollider) StripCollider(go);
    return go;
  }

  static void StripCollider(GameObject go) {
    try {
      Collider c = go.GetComponent<Collider>();
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

  static readonly Dictionary<string, Material> _litCache = new Dictionary<string, Material>();

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
