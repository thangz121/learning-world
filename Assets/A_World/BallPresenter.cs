// A_World/BallPresenter.cs — Agent A (World & Visual), Phase 2F.
// Owns the ball carry visual + crate hint glow. BusBehaviour pattern: subscribe
// with On<T> in OnEnable (via EnsureSubscribed so late Bind still works), base
// OnDisable auto-disposes.
//   WordSeenEvent(ball)      -> attach mini-ball to the player hand, clear glow.
//   HintLevelChanged(level>=1)-> enable glow/pulse on the crate ball.
//   QuestCompletedEvent       -> hide the carried ball.
// Wiring: Bind(bus) + SetCrateBall + AttachHand (MarketBuilder.BuildServices).
// C# 9.0 only. No audio calls (router owns vocab playback).
using UnityEngine;

[DisallowMultipleComponent]
public class BallPresenter : BusBehaviour {
  const string BallWord = "ball";
  // W1 slice quest this presenter serves (Content/quests/w1_mia_ball.json).
  const string W1QuestId = "w1_mia_ball";

  IGameEventBus _bus;
  bool _subscribed;

  GameObject _crateBall;
  Transform _hand;
  GameObject _carriedBall;
  Light _hintLight;
  Vector3 _crateBaseScale;
  bool _glowOn;
  float _pulseT;

  void Update() {
    if (_crateBall == null) return;
    if (!_glowOn) return;
    _pulseT += Time.deltaTime;
    float pulse = 1f + 0.08f * Mathf.Sin(_pulseT * 4f);
    _crateBall.transform.localScale = _crateBaseScale * pulse;
    if (_hintLight != null) _hintLight.intensity = 1.2f + 0.8f * Mathf.Sin(_pulseT * 4f);
  }

  // Injection boundary (wired by MarketBuilder.BuildServices).
  public void Bind(IGameEventBus bus) {
    _bus = bus;
    EnsureSubscribed();
  }

  // Crate ball built by MarketBuilder (the blue ball Interactable sits on).
  public void SetCrateBall(GameObject crateBall) {
    _crateBall = crateBall;
    if (_crateBall != null) _crateBaseScale = _crateBall.transform.localScale;
  }

  // Player hand anchor built by MarketBuilder (child of the player capsule).
  public void AttachHand(Transform hand) {
    _hand = hand;
    if (_carriedBall != null && _hand != null) _carriedBall.transform.SetParent(_hand);
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
    On<QuestStartedEvent>(OnQuestBreathe, _bus);
    On<QuestCompletedEvent>(OnQuestDone, _bus);
    _subscribed = true;
  }

  // R7-class guard (mirrors ApplePresenter): the taken-ball may be seen BEFORE
  // the ball quest starts (distractor pickup during the apple quest publishes
  // the same WordSeen(ball)). Only the IN-QUEST find empties the crate, so
  // find_ball stays recoverable and distractor play never touches staging.
  bool _questActive;

  void OnQuestBreathe(QuestStartedEvent e) {
    if (e.QuestId.Value != W1QuestId) return;
    _questActive = true;
  }

  void OnWordSeen(WordSeenEvent e) {
    if (e.WordId.Value != BallWord) return;
    if (!_questActive) return;
    SetGlow(false);
    // Ball is TAKEN — the crate goes empty in the same frame the carried ball appears in the hand
    if (_crateBall != null) _crateBall.SetActive(false);
    AttachCarriedBall();
  }

  void OnHint(HintLevelChanged e) {
    if (e.QuestId.Value != W1QuestId) return;
    if (e.Level >= 1) {
      // Never glow a taken (hidden) crate — the carried ball in the hand is the active visual now
      if (_crateBall == null || !_crateBall.activeInHierarchy) return;
      SetGlow(true);
    }
  }

  void OnQuestDone(QuestCompletedEvent e) {
    if (e.QuestId.Value != W1QuestId) return;
    _questActive = false;
    SetGlow(false);
    HideCarriedBall();
  }

  void AttachCarriedBall() {
    if (_carriedBall == null) _carriedBall = BuildMiniBall();
    if (_carriedBall == null) return;
    _carriedBall.SetActive(true);
    if (_hand != null) {
      _carriedBall.transform.SetParent(_hand);
      _carriedBall.transform.localPosition = Vector3.zero;
      _carriedBall.transform.localRotation = Quaternion.identity;
    }
  }

  void HideCarriedBall() {
    if (_carriedBall != null) _carriedBall.SetActive(false);
  }

  void SetGlow(bool on) {
    _glowOn = on;
    if (_crateBall != null && !on) _crateBall.transform.localScale = _crateBaseScale;
    if (on && _hintLight == null) _hintLight = BuildHintLight();
    if (_hintLight != null) {
      _hintLight.enabled = on;
      _hintLight.gameObject.SetActive(on);
    }
  }

  GameObject BuildMiniBall() {
    GameObject mini = GameObject.CreatePrimitive(PrimitiveType.Sphere);
    mini.name = "CarriedQuestBall"; // distinct from the distractor's CarriedBall mini
    mini.transform.localScale = new Vector3(0.24f, 0.24f, 0.24f);
    Renderer renderer = mini.GetComponent<Renderer>();
    if (renderer != null) {
      Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
      mat.SetColor("_BaseColor", new Color(0.20f, 0.42f, 0.90f)); // blue ball
      renderer.sharedMaterial = mat;
    }
    Collider collider = mini.GetComponent<Collider>();
    if (collider != null) CharacterPresentation.DestroyNow(collider); // carried decoy must not eat clicks
    return mini;
  }

  Light BuildHintLight() {
    GameObject glow = new GameObject("BallHintGlow");
    if (_crateBall != null) {
      glow.transform.SetParent(_crateBall.transform);
      glow.transform.localPosition = new Vector3(0f, 1.2f, 0f);
    }
    Light light = glow.AddComponent<Light>();
    light.type = LightType.Point;
    light.color = new Color(0.4f, 0.7f, 1f);
    light.intensity = 1.2f;
    light.range = 3f;
    glow.SetActive(false);
    return light;
  }
}