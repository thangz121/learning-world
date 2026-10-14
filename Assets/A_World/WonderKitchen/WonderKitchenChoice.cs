// A_World/WonderKitchen/WonderKitchenChoice.cs — Interactive ingredient tray.
// Tapping floats the food ingredient into the magical cauldron with MagicJuice sparkles!
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class WonderKitchenChoice : MonoBehaviour, IClickTarget {
  public string WordId = "";
  public bool IsTarget;
  public WonderKitchen Game;

  Transform _foodTransform;
  Vector3 _baseFoodLocalPos;
  float _animTime;
  float _floatOffset;
  bool _isFlownToPot;
  Vector3 _flightStart;
  Vector3 _flightTarget;
  float _flightProgress;

  void Start() {
    for (int i = 0; i < transform.childCount; i++) {
      Transform child = transform.GetChild(i);
      if (child.name != "Pedestal" && child.name != "PlatterTop") {
        _foodTransform = child;
        _baseFoodLocalPos = child.localPosition;
        _floatOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        break;
      }
    }
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f || _foodTransform == null) return;

    if (_isFlownToPot) {
      _flightProgress += dt * 1.8f;
      float t = Mathf.Clamp01(_flightProgress);
      // Parabolic flight arch into cauldron
      float height = Mathf.Sin(t * Mathf.PI) * 1.5f;
      Vector3 current = Vector3.Lerp(_flightStart, _flightTarget, t);
      current.y += height;
      _foodTransform.position = current;
      _foodTransform.Rotate(Vector3.up, 360f * dt);
      _foodTransform.localScale = Vector3.Lerp(Vector3.one * 0.65f, Vector3.zero, t * t);
      if (t >= 1f) {
        _foodTransform.gameObject.SetActive(false);
      }
      return;
    }

    _animTime += dt;
    // Gentle floating breathing animation
    float hover = Mathf.Sin(_animTime * 2.8f + _floatOffset) * 0.04f;
    _foodTransform.localPosition = _baseFoodLocalPos + new Vector3(0f, hover, 0f);
  }

  public void FlyToCauldron(Vector3 cauldronWorldPos) {
    if (_foodTransform == null) return;
    _isFlownToPot = true;
    _flightStart = _foodTransform.position;
    _flightTarget = cauldronWorldPos + new Vector3(0f, 0.9f, 0f);
    _flightProgress = 0f;

    MagicJuice.FairySparkleBurst(transform, _baseFoodLocalPos + new Vector3(0f, 0.4f, 0f), 16);
    MagicJuice.MagicHaloRing(transform, Vector3.zero, MagicJuice.MagicPink, 1.8f);
  }

  public void PlayWrongWobble() {
    if (_foodTransform != null) {
      GameJuice.Wobble(_foodTransform, 12f, 0.45f);
    } else {
      GameJuice.Wobble(transform, 8f, 0.35f);
    }
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TrySelectChoice(this);
  }
}
