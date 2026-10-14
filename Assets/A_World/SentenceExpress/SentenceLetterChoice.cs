// A_World/SentenceExpress/SentenceLetterChoice.cs — Interactive multiple choice letter pill for 4-year-olds.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class SentenceLetterChoice : MonoBehaviour, IClickTarget {
  public string Letter = "";
  public bool IsCorrect;
  public SentenceExpress Game;

  Transform _billboardBoard;
  Vector3 _baseLocalPos;
  float _animTime;
  float _floatOffset;
  bool _answered;

  void Start() {
    _baseLocalPos = transform.localPosition;
    _floatOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
    _billboardBoard = transform.Find("ChoiceStand/SignBoard");
    if (_billboardBoard == null) _billboardBoard = transform.Find("PillPad");
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f || _answered) return;
    _animTime += dt;

    // Gentle rhythmic bobbing and breathing animation to catch preschooler's attention
    float bob = Mathf.Sin(_animTime * 3.0f + _floatOffset) * 0.05f;
    transform.localPosition = _baseLocalPos + new Vector3(0f, bob, 0f);
  }

  public void PlayCorrectFx() {
    _answered = true;
    GameJuice.Pop(transform, 0.45f, 0.6f);
    MagicJuice.FairySparkleBurst(transform, new Vector3(0f, 0.9f, 0f), 24);
    MagicJuice.MagicHaloRing(transform, new Vector3(0f, 0.2f, 0f), MagicJuice.MagicGold, 2.2f);

    // Turn board into glowing emerald/mint gold
    Renderer r = GetComponentInChildren<Renderer>();
    if (r != null) {
      if (Application.isPlaying) r.material.color = new Color(0.4f, 0.95f, 0.55f);
    }
  }

  public void PlayWrongFx() {
    GameJuice.Wobble(transform, 14f, 0.5f);
    MagicJuice.FairySparkleBurst(transform, new Vector3(0f, 0.7f, 0f), 8);

    // Flash gentle pastel pink/coral indicating retry
    Transform pad = transform.Find("ChoiceStand/SignBoard");
    if (pad == null) pad = transform.Find("PillPad");
    if (pad != null) {
      Renderer r = pad.GetComponent<Renderer>();
      if (r != null && Application.isPlaying) {
        r.material.color = new Color(1.0f, 0.55f, 0.55f);
      }
    }
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TrySelectLetter(this);
  }
}

