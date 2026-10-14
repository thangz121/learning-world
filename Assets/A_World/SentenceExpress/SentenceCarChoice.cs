// A_World/SentenceExpress/SentenceCarChoice.cs — Interactive train car choice.
// When tapped in the correct order, it lights up with MagicJuice sparkles and couples to the train!
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class SentenceCarChoice : MonoBehaviour, IClickTarget {
  public string CardText = "";
  public string SpokenAudioKey = "";
  public int CorrectStepOrder = 0;
  public bool IsCoupled { get; private set; }
  public SentenceExpress Game;

  Transform _boardTransform;
  Vector3 _baseBoardLocalPos;
  float _animTime;
  float _floatOffset;

  void Start() {
    Transform board = transform.Find("CardBoard");
    if (board != null) {
      _boardTransform = board;
      _baseBoardLocalPos = board.localPosition;
    }
    _floatOffset = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
  }

  void Update() {
    float dt = Time.deltaTime;
    if (dt <= 0f) return;
    _animTime += dt;

    if (IsCoupled) {
      // Gentle cheerful train bobbing
      float bob = Mathf.Sin(_animTime * 6f) * 0.02f;
      transform.localPosition = new Vector3(transform.localPosition.x, bob, transform.localPosition.z);
      return;
    }

    if (_boardTransform != null) {
      float hover = Mathf.Sin(_animTime * 2.8f + _floatOffset) * 0.035f;
      _boardTransform.localPosition = _baseBoardLocalPos + new Vector3(0f, hover, 0f);
    }
  }

  public void PlayCoupleFx() {
    IsCoupled = true;
    GameJuice.Pop(transform, 0.28f, 0.4f);
    MagicJuice.FairySparkleBurst(transform, new Vector3(0f, 1.4f, 0f), 18);
    MagicJuice.MagicHaloRing(transform, Vector3.zero, MagicJuice.MagicGold, 2.0f);
  }

  public void RevealLetter(string letter) {
    Transform label = transform.Find("CarLabel");
    if (label != null) {
      TextMesh tm = label.GetComponent<TextMesh>();
      if (tm != null) {
        tm.text = letter;
        tm.color = new Color(0.2f, 0.15f, 0.3f);
      }
    }
    PlayCoupleFx();
  }

  public void PlayWrongWobble() {
    GameJuice.Wobble(transform, 10f, 0.4f);
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TryTapCar(this);
  }
}
