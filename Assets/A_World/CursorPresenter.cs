// A_World/CursorPresenter.cs — Agent A (World & Visual). In-game arrow cursor
// + world-space hover marker for the constrained market world.
//
// WHY (R7 real-player report, R8 refinement, R9 heading cue): the OS arrow
// says nothing, and a 4yo cannot tell what is clickable. The hardware cursor
// hides; an in-game ARROW follows the pointer. When the arrow points at
// anything clickable (Interactable objects, IClickTarget NPCs) TWO things
// answer at once:
//   (1) the arrow itself turns golden + grows slightly,
//   (2) a golden DOWN-ARROW bounces ABOVE THE TARGET (world-space, owned
//       here, one reusable object) — visible from every camera, readable
//       before the click, never after. (R9: was an exclamation "!" — the
//       down-arrow says WHERE to tap, not just "look here".)
// R9: the pointer arrow also ROTATES with the player's facing (screen-space
// heading cue: the arrow leans where the child will walk). Pure presentation:
// no gameplay state, no events, no services, no audio.
// Player-report follow-up: the arrow now leans toward the LAST CLICKED
// direction (the assumed walk direction) instead of the current facing —
// clicking somewhere swings the arrow where the child is about to go.
// Pointer confinement: the first click confines the OS pointer to the game
// window (the custom arrow keeps working inside); M releases back to the
// normal OS cursor (toggle).
// Presentation ONLY: no gameplay state, no events, no services, no audio.
// One Physics.Raycast per frame (same default mask as the click router).
// Neither layer EVER eats clicks: overlay Image.raycastTarget=false + no
// GraphicRaycaster; the marker's collider is destroyed at build (it would
// otherwise swallow the very raycast that found its target). Null-guarded
// throughout (batch-safe: no mouse/camera -> hardware cursor stays).
// C# 9.0 only.
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CursorPresenter : MonoBehaviour {
  // Hover language (shared constants so tests pin them, never magic numbers
  // scattered across callers).
  public const float IdleScale = 1f;
  public const float HoverScale = 1.25f;
  public static readonly Color IdleColor = Color.white;
  public static readonly Color HoverColor = new Color(1f, 0.78f, 0.25f);
  public const int CanvasOrder = 100; // above the HUD chip (10): cursor is always on top
  const float ArrowPixels = 34f;
  // World marker: gold DOWN-ARROW above the hovered target (classic "tap
  // here" cue). Rides above the target's rendered top so it never sinks into
  // hats, apples, or name labels; gentle bob only (never spins/flashes).
  // Shape language: shaft on top + two slanted chevron arms meeting at a
  // BOTTOM tip (primitives only — Unity ships no cone primitive), the same
  // "tap here!" cue as the quest thought-bubble, shrunk to hover scale.
  public static readonly Color MarkerColor = new Color(1f, 0.78f, 0.2f);
  const float MarkerLift = 0.34f; // above the target collider top
  const float MarkerBob = 0.08f;
  const float MarkerHertz = 2f;

  Image _arrow;
  RectTransform _arrowRt;
  Canvas _canvas;
  float _scale = IdleScale;
  float _angle; // R9 heading cue: smoothed screen-space lean (deg, clockwise from up)
  Transform _playerT; // cached (Find once; re-found if destroyed)
  GameObject _marker;
  float _bobPhase;

  void Awake() {
    BuildCursorImmediate();
  }

  // Deterministic build hook (tests/snapshot tools): same pattern as
  // BuildUiImmediate/BuildBubbleImmediate — batch EditMode does not guarantee
  // Awake delivery.
  public void BuildCursorImmediate() {
    if (_arrow != null) return;
    GameObject canvasGo = new GameObject("CursorCanvas");
    canvasGo.transform.SetParent(transform, false);
    Canvas canvas = canvasGo.AddComponent<Canvas>();
    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    canvas.sortingOrder = CanvasOrder;
    _canvas = canvas;
    // NOTE: no GraphicRaycaster added on purpose (see header).
    GameObject arrowGo = new GameObject("CursorArrow");
    arrowGo.transform.SetParent(canvasGo.transform, false);
    _arrow = arrowGo.AddComponent<Image>();
    _arrow.sprite = MakeArrowSprite();
    _arrow.color = IdleColor;
    // CRITICAL: a raycastable cursor would eat every world click (the router
    // drops clicks over UI). The arrow is decoration only.
    _arrow.raycastTarget = false;
    _arrowRt = arrowGo.GetComponent<RectTransform>();
    _arrowRt.sizeDelta = new Vector2(ArrowPixels, ArrowPixels);
    _arrowRt.pivot = new Vector2(0.5f, 0.92f); // hotspot at the arrow tip
    BuildMarker();
  }

  // One reusable world-space marker (never duplicated, never left behind).
  void BuildMarker() {
    _marker = new GameObject("HoverMarker");
    _marker.transform.SetParent(transform, false);
    Shader lit = Shader.Find("Universal Render Pipeline/Lit");
    Material mat = new Material(lit != null ? lit : Shader.Find("Standard"));
    if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", MarkerColor);
    else if (mat.HasProperty("_Color")) mat.SetColor("_Color", MarkerColor);
    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.3f);
    if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
    AddMarkPart("MarkShaft", PrimitiveType.Cylinder,
      new Vector3(0f, 0.10f, 0f), new Vector3(0.09f, 0.24f, 0.09f), mat, Quaternion.identity);
    // Chevron head pointing DOWN (∨): the arms meet at a BOTTOM tip (0,-0.12)
    // with their outer ends higher — left arm runs upper-left -> lower-center.
    AddMarkPart("MarkHeadL", PrimitiveType.Cube,
      new Vector3(-0.05f, -0.07f, 0f), new Vector3(0.14f, 0.06f, 0.06f), mat,
      Quaternion.Euler(0f, 0f, -45f));
    AddMarkPart("MarkHeadR", PrimitiveType.Cube,
      new Vector3(0.05f, -0.07f, 0f), new Vector3(0.14f, 0.06f, 0.06f), mat,
      Quaternion.Euler(0f, 0f, 45f));
    _marker.SetActive(false);
  }

  void AddMarkPart(string partName, PrimitiveType kind, Vector3 localPos, Vector3 localScale, Material mat, Quaternion localRot) {
    GameObject part = GameObject.CreatePrimitive(kind);
    part.name = partName;
    part.transform.SetParent(_marker.transform, false);
    part.transform.localPosition = localPos;
    part.transform.localScale = localScale;
    part.transform.localRotation = localRot;
    Renderer r = part.GetComponent<Renderer>();
    if (r != null) {
      r.sharedMaterial = mat;
      // No shadow casting (tiny floating decoy); receive defaults stay.
      r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
    // CRITICAL (see header): the marker must never intercept clicks or hover.
    CharacterPresentation.DestroyNow(part.GetComponent<Collider>());
  }

  // Pure cursor language (tests pin this directly; Update forwards live state).
  public static void ComputeCursor(bool hovering, out float scale, out Color color) {
    scale = hovering ? HoverScale : IdleScale;
    color = hovering ? HoverColor : IdleColor;
  }

  // R9 heading cue (pure, tests pin this): screen-space rotation (degrees,
  // clockwise from straight-up) that leans the pointer arrow where the player
  // faces. Degenerate input (no facing) reads as straight-up, never NaN.
  public static float ComputeArrowAngle(Vector2 originPx, Vector2 facingPx) {
    Vector2 d = facingPx - originPx;
    if (d.sqrMagnitude < 0.0001f) return 0f;
    return Mathf.Atan2(d.x, d.y) * Mathf.Rad2Deg;
  }

  // R9b heading-smoothing (pure, tests pin this): LerpAngle alone is NOT
  // enough — its result roams outside [-180,180] (389/442/635 seen in the R9
  // survey log), and the render mapping (rotation.z = -angle) only contracts
  // normalized inputs, so mid-swing frames render mirrored headings. One wrap
  // restores the render contract every frame (settled values photo-match the
  // logged angles to <1deg: flower +82, hedge +47).
  public static float SmoothAngle(float current, float target, float t) {
    float a = Mathf.LerpAngle(current, target, t);
    if (a > 180f) a -= 360f;
    else if (a < -180f) a += 360f;
    return a;
  }

  // Assumed-walk direction (player-report follow-up): the last clicked screen
  // direction, in the same angle contract as the heading cue. Empty until the
  // first click lands; the facing cue covers the pre-click frames.
  float _clickAngle;
  bool _hasClick;

  // Pointer-lock state machine (pure, tests pin this): first click confines
  // the OS pointer to the window (custom arrow keeps working inside); M
  // toggles back to the normal OS cursor (and back again).
  public static CursorLockMode NextLockState(CursorLockMode current, bool clicked, bool mPressed) {
    if (mPressed) return current == CursorLockMode.None ? CursorLockMode.Confined : CursorLockMode.None;
    if (clicked && current == CursorLockMode.None) return CursorLockMode.Confined;
    return current;
  }

  // Test seam: register a click direction deterministically (no live mouse).
  public void RegisterClickForTests(Vector2 playerScreenPos, Vector2 clickScreenPos) {
    _clickAngle = ComputeArrowAngle(playerScreenPos, clickScreenPos);
    _hasClick = true;
    _angle = _clickAngle;
  }

  public bool HasClickDirection {
    get { return _hasClick; }
  }

  // Test seam: drive one frame deterministically without a live mouse.
  // Marker placement has its own seam (PlaceMarkerForTests) since hover needs
  // a live camera raycast.
  public void RefreshForTests(Vector2 screenPos, bool hovering, float headingDeg = 0f) {
    if (_arrow == null) BuildCursorImmediate();
    ComputeCursor(hovering, out float target, out Color color);
    _scale = target;
    _angle = headingDeg;
    ApplyArrow(screenPos, color);
    if (!hovering && _marker != null) _marker.SetActive(false);
  }

  // Test seam for the marker (hover needs a live camera, so tests place it).
  public void PlaceMarkerForTests(Vector3 anchor) {
    if (_marker == null) BuildCursorImmediate();
    _marker.transform.position = anchor;
    _marker.SetActive(true);
  }

  public bool IsMarkerVisible {
    get { return _marker != null && _marker.activeSelf; }
  }

  // Lead introspection (survey telemetry): the smoothed heading now rendered.
  public float CurrentAngle {
    get { return _angle; }
  }

  void Update() {
    Mouse mouse = Mouse.current;
    if (mouse == null) {
      if (_canvas != null) _canvas.enabled = false;
      if (_marker != null) _marker.SetActive(false);
      Cursor.visible = true;
      return;
    }
    if (_arrow == null) BuildCursorImmediate();
    if (_canvas != null) _canvas.enabled = true;
    Vector2 px = mouse.position.ReadValue();
    // Pointer confinement: first click keeps the OS pointer inside the game
    // window; M releases back to the normal OS cursor (toggle). Read-only
    // keyboard poll — never consumes anything.
    bool clicked = mouse.leftButton.wasPressedThisFrame;
    Keyboard keyboard = Keyboard.current;
    bool mPressed = keyboard != null && keyboard.mKey.wasPressedThisFrame;
    Cursor.lockState = NextLockState(Cursor.lockState, clicked, mPressed);
    // The in-game arrow replaces the hardware arrow while confined; a
    // released (None) pointer shows the normal OS cursor again.
    Cursor.visible = Cursor.lockState == CursorLockMode.None;
    if (clicked) RegisterClick(px);
    Collider target = HoverScan();
    bool hovering = target != null;
    ComputeCursor(hovering, out float scaleTarget, out Color colorTarget);
    float t = 1f - Mathf.Exp(-12f * Mathf.Max(Time.deltaTime, 0.0001f));
    _scale = Mathf.Lerp(_scale, scaleTarget, t);
    _angle = SmoothAngle(_angle, _hasClick ? _clickAngle : ReadPlayerHeading(), t);
    _arrow.color = Color.Lerp(_arrow.color, colorTarget, t);
    ApplyArrow(px, _arrow.color);
    TickMarker(target);
  }

  // Assumed-walk direction: player screen position -> clicked screen point,
  // in the heading angle contract. Holds the last angle when the player
  // cannot be resolved (degenerate click keeps the previous cue).
  void RegisterClick(Vector2 clickPx) {
    if (!TryPlayerScreenPos(out Vector2 playerPx)) return;
    _clickAngle = ComputeArrowAngle(playerPx, clickPx);
    _hasClick = true;
  }

  bool TryPlayerScreenPos(out Vector2 screenPos) {
    screenPos = Vector2.zero;
    if (_playerT == null) {
      GameObject player = GameObject.Find("Player");
      if (player == null) return false;
      _playerT = player.transform;
    }
    Camera cam = Camera.main;
    if (cam == null) return false;
    Vector3 projected = cam.WorldToScreenPoint(_playerT.position);
    if (projected.z < 0f) return false;
    screenPos = new Vector2(projected.x, projected.y);
    return true;
  }

  void ApplyArrow(Vector2 screenPos, Color color) {
    if (_arrowRt == null) return;
    _arrowRt.position = new Vector3(screenPos.x, screenPos.y, 0f);
    _arrowRt.localScale = Vector3.one * _scale;
    _arrowRt.localRotation = Quaternion.Euler(0f, 0f, -_angle);
    if (_arrow != null) _arrow.color = color;
  }

  // R9 heading cue: where the player faces, in screen space. Holds the last
  // angle when nothing is readable (no player/camera yet, degenerate facing).
  float ReadPlayerHeading() {
    if (_playerT == null) {
      GameObject player = GameObject.Find("Player");
      if (player == null) return _angle;
      _playerT = player.transform;
    }
    Camera cam = Camera.main;
    if (cam == null) return _angle;
    Vector3 fwd = _playerT.forward;
    fwd.y = 0f;
    if (fwd.sqrMagnitude < 0.0001f) return _angle;
    Vector3 origin = cam.WorldToScreenPoint(_playerT.position);
    Vector3 facing = cam.WorldToScreenPoint(_playerT.position + fwd.normalized);
    if (origin.z < 0f || facing.z < 0f) return _angle;
    return ComputeArrowAngle(
      new Vector2(origin.x, origin.y), new Vector2(facing.x, facing.y));
  }

  // Gentle attention bounce above the hovered target's rendered top.
  void TickMarker(Collider target) {
    if (_marker == null) return;
    if (target == null) {
      _marker.SetActive(false);
      return;
    }
    _bobPhase += Mathf.Max(Time.deltaTime, 0.0001f) * Mathf.PI * 2f * MarkerHertz;
    Vector3 c = target.bounds.center;
    _marker.transform.position = new Vector3(
      c.x, target.bounds.max.y + MarkerLift + Mathf.Sin(_bobPhase) * MarkerBob, c.z);
    if (!_marker.activeSelf) _marker.SetActive(true);
  }

  // Same click contract as ClickRouter (Interactable objects + IClickTarget
  // NPCs), read-only: never moves the player, never fires anything.
  Collider HoverScan() {
    Camera cam = Camera.main;
    Mouse mouse = Mouse.current;
    if (cam == null || mouse == null) return null;
    Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
    if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) return null;
    if (hit.collider == null) return null;
    if (hit.collider.GetComponentInParent<Interactable>() != null) return hit.collider;
    if (hit.collider.GetComponentInParent<IClickTarget>() != null) return hit.collider;
    return null;
  }

  void OnDisable() {
    Cursor.visible = true; // editor/play-mode safety: never trap the user cursorless
    Cursor.lockState = CursorLockMode.None;
  }

  void OnDestroy() {
    Cursor.visible = true;
    Cursor.lockState = CursorLockMode.None;
  }

  // Straight-up arrow (no imported assets): dark bordered silhouette with a
  // white core, drawn in two passes (outer dark, inset white). Straight-up
  // reads identically from every camera orbit (a tilted classic arrow does
  // not). Tip at top-center = the hotspot (texture y=63 is the TOP row).
  static Sprite MakeArrowSprite() {
    const int size = 64;
    Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
    tex.wrapMode = TextureWrapMode.Clamp;
    Color dark = new Color(0.16f, 0.12f, 0.1f, 1f);
    Color clear = new Color(1f, 1f, 1f, 0f);
    // Arrow shape in pixel space, y-UP = texture top: head triangle + shaft.
    Vector2 tip = new Vector2(32f, 60f);
    Vector2 headL = new Vector2(17f, 33f);
    Vector2 headR = new Vector2(47f, 33f);
    Vector2 shaftOuterMin = new Vector2(27f, 5f);
    Vector2 shaftOuterMax = new Vector2(37f, 33f);
    Vector2 shaftInnerMin = new Vector2(30f, 8f);
    Vector2 shaftInnerMax = new Vector2(34f, 30f);
    for (int y = 0; y < size; y++) {
      for (int x = 0; x < size; x++) {
        Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
        bool outer = InTri(p, tip, headL, headR, 4f) || InRect(p, shaftOuterMin, shaftOuterMax, 4f);
        if (!outer) { tex.SetPixel(x, y, clear); continue; }
        bool inner = InTri(p, tip, headL, headR, -3f) || InRect(p, shaftInnerMin, shaftInnerMax, 0f);
        tex.SetPixel(x, y, inner ? Color.white : dark);
      }
    }
    tex.Apply();
    return Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
  }

  static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c, float pad) {
    // Barycentric inside-test with an approximate edge padding (pixels).
    Vector2 v0 = c - a, v1 = b - a, v2 = p - a;
    float d00 = Vector2.Dot(v0, v0), d01 = Vector2.Dot(v0, v1), d11 = Vector2.Dot(v1, v1);
    float d20 = Vector2.Dot(v2, v0), d21 = Vector2.Dot(v2, v1);
    float den = d00 * d11 - d01 * d01;
    if (Mathf.Abs(den) < 0.0001f) return false;
    float v = (d11 * d20 - d01 * d21) / den;
    float w = (d00 * d21 - d01 * d20) / den;
    float u = 1f - v - w;
    float m = pad * 0.02f; // pad in pixels -> barycentric margin (approx)
    return u >= -m && v >= -m && w >= -m;
  }

  static bool InRect(Vector2 p, Vector2 min, Vector2 max, float pad) {
    return p.x >= min.x - pad && p.x <= max.x + pad
      && p.y >= min.y - pad && p.y <= max.y + pad;
  }
}
