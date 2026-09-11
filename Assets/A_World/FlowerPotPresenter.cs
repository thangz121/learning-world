// A_World/FlowerPotPresenter.cs — Agent A (World & Visual), W1 vertical slice.
// Reveals the hidden flower-pot group when the W1 quest completes.
// BusBehaviour pattern: subscribe with On<T> in OnEnable (via EnsureSubscribed
// so late Bind still works), base OnDisable auto-disposes.
//
// ASMDEF NOTE (why Bind takes only the bus): QuestRewardService is a B_Brain
// class and LWE.World does NOT reference LWE.Brain, so this presenter MUST NOT
// reference that type (no asmdef change can fix a frozen-ownership violation).
// Instead it listens for QuestCompletedEvent whose QuestId.Value ==
// "w1_mia_apple" (documented W1 slice decision: that quest's reward block is
// {friendship_mia:10, world_change:flower_pot}). Reveal = activate the flower
// group + tiny grow animation. EditMode reward STATE stays B-owned (CT-003).
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class FlowerPotPresenter : BusBehaviour {
  // W1 slice quest whose completion grows the flowers (Content/quests/w1_mia_apple.json).
  public const string W1QuestId = "w1_mia_apple";

  const float GrowDuration = 1.2f;

  IGameEventBus _bus;
  bool _subscribed;

  GameObject _flowerRoot;
  bool _revealed;
  bool _growing;
  float _growT;

  void Update() {
    if (!_growing || _flowerRoot == null) return;
    _growT += Time.deltaTime / GrowDuration;
    float t = Mathf.Clamp01(_growT);
    float scale = Mathf.Lerp(0.2f, 1f, t * t * (3f - 2f * t)); // smoothstep grow
    _flowerRoot.transform.localScale = new Vector3(scale, scale, scale);
    if (t >= 1f) {
      _growing = false;
      _flowerRoot.transform.localScale = Vector3.one;
    }
  }

  // Injection boundary (wired by MarketBuilder.BuildServices). Presenters from
  // other agents are Lead-wired; this one only needs the bus.
  public void Bind(IGameEventBus bus) {
    _bus = bus;
    EnsureSubscribed();
  }

  // Flower group built (hidden) by MarketBuilder at the flower-bed anchor.
  public void SetFlowerRoot(GameObject root) {
    _flowerRoot = root;
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
    On<QuestCompletedEvent>(OnQuestDone, _bus);
    _subscribed = true;
  }

  void OnQuestDone(QuestCompletedEvent e) {
    if (_revealed) return;
    if (e.QuestId.Value != W1QuestId) return;
    Reveal();
  }

  void Reveal() {
    _revealed = true;
    if (_flowerRoot == null) {
      Debug.LogWarning("[FlowerPotPresenter] Quest complete but no flower root wired; nothing to reveal.");
      return;
    }
    _flowerRoot.SetActive(true); // visible to the player is mandatory
    _flowerRoot.transform.localScale = new Vector3(0.2f, 0.2f, 0.2f);
    _growT = 0f;
    _growing = true;
  }
}
