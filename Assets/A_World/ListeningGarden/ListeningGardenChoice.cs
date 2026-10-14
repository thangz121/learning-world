using UnityEngine;

[DisallowMultipleComponent]
public class ListeningGardenChoice : MonoBehaviour, IClickTarget {
  public string WordId = "";
  public bool IsAnswer;
  public ListeningGarden Game;

  Transform _propTransform;
  Vector3 _basePropLocalPos;
  float _floatOffset;
  float _animTime;

  void Start() {
    // Find the spawned prop on this pod
    for (int i = 0; i < transform.childCount; i++) {
      Transform child = transform.GetChild(i);
      if (child.name != "Pedestal" && child.name != "CardTop") {
        _propTransform = child;
        _basePropLocalPos = child.localPosition;
        _floatOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        break;
      }
    }
  }

  bool _isFlyingUp = false;
  float _flyUpT = 0f;

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f || _propTransform == null) return;

    if (_isFlyingUp) {
      _flyUpT += dt;
      // Elegant arc fly-up: rises 1.2m, scales up smoothly, and gently spins
      float t = Mathf.Clamp01(_flyUpT / 0.85f);
      float easeOut = Mathf.Sin(t * Mathf.PI * 0.5f);
      float yOffset = easeOut * 1.2f;
      _propTransform.localPosition = _basePropLocalPos + new Vector3(0f, yOffset, 0f);
      _propTransform.localRotation = Quaternion.Euler(0f, easeOut * 360f, 0f);
      return;
    }

    _animTime += dt;
    // Gentle idle breathing floating effect
    float hover = Mathf.Sin(_animTime * 2.5f + _floatOffset) * 0.035f;
    _propTransform.localPosition = _basePropLocalPos + new Vector3(0f, hover, 0f);
  }

  public void PlayCorrectFx() {
    _isFlyingUp = true;
    _flyUpT = 0f;
    if (_propTransform != null) {
      GameJuice.Pop(_propTransform, 0.35f, 0.5f);
      MagicJuice.FairySparkleBurst(transform, _basePropLocalPos + new Vector3(0f, 0.4f, 0f), 20);
      MagicJuice.MagicHaloRing(transform, Vector3.zero, MagicJuice.MagicGold, 2.0f);
    } else {
      GameJuice.Pop(transform, 0.25f, 0.4f);
      MagicJuice.FairySparkleBurst(transform, new Vector3(0f, 0.5f, 0f), 14);
    }
  }

  public void PlayWrongFx() {
    if (_propTransform != null) {
      GameJuice.Wobble(_propTransform, 10f, 0.4f);
    } else {
      GameJuice.Wobble(transform, 8f, 0.35f);
    }
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TryTapChoice(this);
  }
}
