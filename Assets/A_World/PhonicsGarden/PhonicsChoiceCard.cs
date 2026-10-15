// A_World/PhonicsGarden/PhonicsChoiceCard.cs — Interactive choice card for Vietnamese Phonics.
// IClickTarget only. C# 9.0 only. Pure UnityEngine.
using UnityEngine;

[DisallowMultipleComponent]
public class PhonicsChoiceCard : MonoBehaviour, IClickTarget {
  public string DisplayText = "";
  public bool IsCorrect;
  public PhonicsGarden Game;

  TextMesh _textMesh;
  Renderer _cardRend;
  Collider _col;

  public void Bind(PhonicsGarden game, string text, bool isCorrect) {
    Game = game;
    DisplayText = text;
    IsCorrect = isCorrect;
    _col = GetComponent<Collider>();
    UpdateLabel();
  }

  void Awake() {
    _textMesh = GetComponentInChildren<TextMesh>(true);
    _cardRend = GetComponent<Renderer>();
    _col = GetComponent<Collider>();
  }

  public void UpdateLabel() {
    if (_textMesh != null) _textMesh.text = DisplayText;
  }

  public void SetHighlight(Color col) {
    if (_cardRend != null && _cardRend.material != null) {
      _cardRend.material.SetColor("_BaseColor", col);
    }
  }

  public void SetClickable(bool active) {
    if (_col != null) _col.enabled = active;
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TrySelectChoice(this);
  }
}
