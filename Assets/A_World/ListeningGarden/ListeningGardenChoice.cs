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

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f || _propTransform == null) return;
    _animTime += dt;
    // Gentle idle breathing floating effect
    float hover = Mathf.Sin(_animTime * 2.5f + _floatOffset) * 0.035f;
    _propTransform.localPosition = _basePropLocalPos + new Vector3(0f, hover, 0f);
  }

  public void PlayCorrectFx() {
    if (_propTransform != null) {
      GameJuice.Pop(_propTransform, 0.32f, 0.45f);
      MagicJuice.FairySparkleBurst(transform, _basePropLocalPos + new Vector3(0f, 0.3f, 0f), 16);
      MagicJuice.MagicHaloRing(transform, Vector3.zero, MagicJuice.MagicPink, 1.8f);
    } else {
      GameJuice.Pop(transform, 0.22f, 0.35f);
      MagicJuice.FairySparkleBurst(transform, new Vector3(0f, 0.5f, 0f), 12);
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
