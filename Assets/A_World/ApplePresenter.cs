// A_World/ApplePresenter.cs — Agent A (World & Visual), W1 vertical slice.
// Owns the apple carry visual + crate hint glow. BusBehaviour pattern: subscribe
// with On<T> in OnEnable (via EnsureSubscribed so late Bind still works), base
// OnDisable auto-disposes.
//   WordSeenEvent(apple)      -> attach mini-apple to the player hand, clear glow.
//   HintLevelChanged(level>=1)-> enable glow/pulse on the crate apple.
//   QuestCompletedEvent       -> hide the carried apple.
// Wiring: Bind(bus) + SetCrateApple + AttachHand (MarketBuilder.BuildServices).
// C# 9.0 only. No audio calls (router owns vocab playback).
using UnityEngine;

[DisallowMultipleComponent]
public class ApplePresenter : BusBehaviour {
  const string AppleWord = "apple";

  IGameEventBus _bus;
  bool _subscribed;

  GameObject _crateApple;
  Transform _hand;
  GameObject _carriedApple;
  Light _hintLight;
  Vector3 _crateBaseScale;
  bool _glowOn;
  float _pulseT;

  void Update() {
    if (!_glowOn || _crateApple == null) return;
    _pulseT += Time.deltaTime;
    float pulse = 1f + 0.08f * Mathf.Sin(_pulseT * 4f);
    _crateApple.transform.localScale = _crateBaseScale * pulse;
    if (_hintLight != null) _hintLight.intensity = 1.2f + 0.8f * Mathf.Sin(_pulseT * 4f);
  }

  // Injection boundary (wired by MarketBuilder.BuildServices).
  public void Bind(IGameEventBus bus) {
    _bus = bus;
    EnsureSubscribed();
  }

  // Crate apple built by MarketBuilder (the big red apple Interactable sits on).
  public void SetCrateApple(GameObject crateApple) {
    _crateApple = crateApple;
    if (_crateApple != null) _crateBaseScale = _crateApple.transform.localScale;
  }

  // Player hand anchor built by MarketBuilder (child of the player capsule).
  public void AttachHand(Transform hand) {
    _hand = hand;
    if (_carriedApple != null && _hand != null) _carriedApple.transform.SetParent(_hand);
  }

  void OnEnable() {
    EnsureSubscribed();
  }

  protected override void OnDisable() {
    _subscribed = false;
    base.OnDisable();
  }

  void EnsureSubscribed() {
    if (_subscribed || _bus == null || !isActiveAndEnabled) return;
    On<WordSeenEvent>(OnWordSeen, _bus);
    On<HintLevelChanged>(OnHint, _bus);
    On<QuestCompletedEvent>(OnQuestDone, _bus);
    _subscribed = true;
  }

  void OnWordSeen(WordSeenEvent e) {
    if (e.WordId.Value != AppleWord) return;
    SetGlow(false);
    AttachCarriedApple();
  }

  void OnHint(HintLevelChanged e) {
    if (e.Level >= 1) SetGlow(true);
  }

  void OnQuestDone(QuestCompletedEvent e) {
    HideCarriedApple();
  }

  void AttachCarriedApple() {
    if (_carriedApple == null) _carriedApple = BuildMiniApple();
    if (_carriedApple == null) return;
    _carriedApple.SetActive(true);
    if (_hand != null) {
      _carriedApple.transform.SetParent(_hand);
      _carriedApple.transform.localPosition = Vector3.zero;
      _carriedApple.transform.localRotation = Quaternion.identity;
    }
  }

  void HideCarriedApple() {
    if (_carriedApple != null) _carriedApple.SetActive(false);
  }

  void SetGlow(bool on) {
    _glowOn = on;
    if (_crateApple != null && !on) _crateApple.transform.localScale = _crateBaseScale;
    if (on && _hintLight == null) _hintLight = BuildHintLight();
    if (_hintLight != null) {
      _hintLight.enabled = on;
      _hintLight.gameObject.SetActive(on);
    }
  }

  GameObject BuildMiniApple() {
    GameObject mini = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    mini.name = "CarriedApple";
    mini.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
    Renderer renderer = mini.GetComponent<Renderer>();
    if (renderer != null) {
      Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      mat.SetColor("_BaseColor", new Color(0.85f, 0.15f, 0.15f));
      renderer.sharedMaterial = mat;
    }
    Collider collider = mini.GetComponent<Collider>();
    if (collider != null) Destroy(collider); // carried decoy must not eat clicks
    GameObject stem = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
    stem.name = "CarriedStem";
    stem.transform.SetParent(mini.transform);
    stem.transform.localPosition = new Vector3(0f, 0.65f, 0f);
    stem.transform.localScale = new Vector3(0.15f, 0.4f, 0.15f);
    Collider stemCollider = stem.GetComponent<Collider>();
    if (stemCollider != null) Destroy(stemCollider);
    return mini;
  }

  Light BuildHintLight() {
    GameObject glow = new GameObject("AppleHintGlow");
    if (_crateApple != null) {
      glow.transform.SetParent(_crateApple.transform);
      glow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
    }
    Light light = glow.AddComponent<Light>();
    light.type = LightType.Point;
    light.color = new Color(1f, 0.85f, 0.4f);
    light.intensity = 1.2f;
    light.range = 3f;
    glow.SetActive(false);
    return light;
  }
}
