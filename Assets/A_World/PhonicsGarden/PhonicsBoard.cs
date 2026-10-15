// A_World/PhonicsGarden/PhonicsBoard.cs — Magic Wooden Board for Vietnamese Phonics.
// Controls the main word display, 100% real photo frame, and phonics segment sequence highlights.
// C# 9.0 only. Pure UnityEngine.
using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class PhonicsBoard : MonoBehaviour {
  public TextMesh WordLabel;
  public SpriteRenderer PhotoRenderer;
  public GameObject PhotoFrameRoot;
  public Transform BoardPlank;

  Material _boardMat;
  Coroutine _highlightRoutine;
  GameObject _pictogramRoot;

  public bool PictogramActive { get { return _pictogramRoot != null; } }

  void Awake() {
    if (WordLabel == null) WordLabel = GetComponentInChildren<TextMesh>(true);
    if (PhotoRenderer == null) PhotoRenderer = GetComponentInChildren<SpriteRenderer>(true);
    if (BoardPlank != null) {
      Renderer r = BoardPlank.GetComponent<Renderer>();
      if (r != null) _boardMat = r.material;
    }
  }

  public void SetWord(string word) {
    if (WordLabel != null) {
      WordLabel.text = word;
      WordLabel.color = Color.white;
    }
  }

  public void ShowPhotoFrame(Sprite realPhotoSprite) {
    HidePictureClue();
    if (PhotoFrameRoot != null) PhotoFrameRoot.SetActive(true);
    if (PhotoRenderer != null) {
      PhotoRenderer.sprite = realPhotoSprite;
      PhotoRenderer.enabled = (realPhotoSprite != null);
    }
    if (PhotoFrameRoot != null) {
      GameJuice.Pop(PhotoFrameRoot.transform, 0.2f, 0.35f);
    }
  }

  // Honest picture clue: a REAL photo when one is registered; otherwise a simple procedural
  // ILLUSTRATION (pictogram) — never a fabricated "photo".
  public void ShowPictureClue(Sprite photoOrNull, string photoKeyOrWordId) {
    HidePictureClue();
    if (photoOrNull != null) {
      ShowPhotoFrame(photoOrNull);
      return;
    }
    if (PhotoFrameRoot != null) PhotoFrameRoot.SetActive(true);
    if (PhotoRenderer != null) {
      PhotoRenderer.sprite = null;
      PhotoRenderer.enabled = false;
    }
    Transform parent = PhotoFrameRoot != null ? PhotoFrameRoot.transform : transform;
    _pictogramRoot = PhonicsPictograms.Build(parent, photoKeyOrWordId);
    if (PhotoFrameRoot != null) {
      GameJuice.Pop(PhotoFrameRoot.transform, 0.2f, 0.35f);
    }
  }

  void HidePictureClue() {
    if (_pictogramRoot != null) {
      CharacterPresentation.DestroyNow(_pictogramRoot);
      _pictogramRoot = null;
    }
  }

  public void HidePhotoFrame() {
    HidePictureClue();
    if (PhotoRenderer != null) {
      PhotoRenderer.sprite = null;
      PhotoRenderer.enabled = false;
    }
    if (PhotoFrameRoot != null) PhotoFrameRoot.SetActive(false);
  }

  public void HighlightWord(Color color) {
    if (WordLabel != null) WordLabel.color = color;
  }

  // Explicit Phonics Instruction: Highlight segments one by one (C -> A -> CA -> Sắc -> CÁ)
  public void PlayExplicitPhonics(PhonicsSegment[] segments, Action onComplete) {
    if (!Application.isPlaying) {
      if (segments != null && segments.Length > 0 && WordLabel != null) {
        WordLabel.text = segments[segments.Length - 1].SegmentText;
      }
      onComplete?.Invoke();
      return;
    }
    if (_highlightRoutine != null) StopCoroutine(_highlightRoutine);
    _highlightRoutine = StartCoroutine(ExplicitPhonicsRoutine(segments, onComplete));
  }

  IEnumerator ExplicitPhonicsRoutine(PhonicsSegment[] segments, Action onComplete) {
    if (segments == null || segments.Length == 0) {
      onComplete?.Invoke();
      yield break;
    }

    Color activeColor = new Color(1f, 0.88f, 0.2f); // gold illumination
    for (int i = 0; i < segments.Length; i++) {
      PhonicsSegment seg = segments[i];
      if (WordLabel != null) {
        WordLabel.text = seg.SegmentText;
        WordLabel.color = activeColor;
      }
      if (BoardPlank != null) GameJuice.Pop(BoardPlank, 0.08f, 0.2f);

      float dur = Mathf.Max(0.2f, seg.DurationSeconds);
      yield return new WaitForSeconds(dur);
    }

    if (WordLabel != null) {
      WordLabel.color = Color.white;
    }
    _highlightRoutine = null;
    onComplete?.Invoke();
  }
}
