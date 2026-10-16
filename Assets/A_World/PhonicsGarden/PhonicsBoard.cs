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
  AudioSource _player;

  public bool PictogramActive { get { return _pictogramRoot != null; } }

  AudioSource EnsurePlayer() {
    if (_player == null) {
      _player = GetComponent<AudioSource>();
      if (_player == null) _player = gameObject.AddComponent<AudioSource>();
      _player.playOnAwake = false;
      _player.spatialBlend = 0f;
    }
    return _player;
  }

  // One-shot clip on the board (word name / discovery name / feedback lines)
  public void PlayClip(AudioClip clip) {
    if (clip == null) return;
    EnsurePlayer().PlayOneShot(clip, 1f);
  }

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
  // v2: each segment ALSO plays its mastered voice clip (AudioKey -> VnVoicePack); after the
  // chain, the full teaching spell-line (e.g. spell_hoa) plays as the pay-off.
  public void PlayExplicitPhonics(PhonicsSegment[] segments, Action onComplete, string spellLineKey = null) {
    if (!Application.isPlaying) {
      if (segments != null && segments.Length > 0 && WordLabel != null) {
        WordLabel.text = segments[segments.Length - 1].SegmentText;
      }
      onComplete?.Invoke();
      return;
    }
    if (_highlightRoutine != null) StopCoroutine(_highlightRoutine);
    _highlightRoutine = StartCoroutine(ExplicitPhonicsRoutine(segments, onComplete, spellLineKey));
  }

  IEnumerator ExplicitPhonicsRoutine(PhonicsSegment[] segments, Action onComplete, string spellLineKey) {
    if (segments == null || segments.Length == 0) {
      onComplete?.Invoke();
      yield break;
    }
    EnsurePlayer();

    Color activeColor = new Color(1f, 0.88f, 0.2f); // gold illumination
    for (int i = 0; i < segments.Length; i++) {
      PhonicsSegment seg = segments[i];
      if (WordLabel != null) {
        WordLabel.text = seg.SegmentText;
        WordLabel.color = activeColor;
      }
      if (BoardPlank != null) GameJuice.Pop(BoardPlank, 0.08f, 0.2f);

      AudioClip clip = VnVoicePack.Get(seg.AudioKey);
      float dur;
      if (clip != null) {
        _player.PlayOneShot(clip, 1f);
        dur = clip.length + 0.15f; // stay in sync with the actual stretched clip
      } else {
        dur = Mathf.Max(0.2f, seg.DurationSeconds);
      }
      yield return new WaitForSeconds(dur);
    }

    // Full teaching line (the "o a oa hờ oa hoa" pay-off)
    AudioClip line = VnVoicePack.Get(spellLineKey);
    if (line != null) {
      if (WordLabel != null) {
        WordLabel.text = segments[segments.Length - 1].SegmentText;
        WordLabel.color = activeColor;
      }
      _player.PlayOneShot(line, 1f);
      yield return new WaitForSeconds(line.length + 0.25f);
    }

    if (WordLabel != null) {
      WordLabel.color = Color.white;
    }
    _highlightRoutine = null;
    onComplete?.Invoke();
  }
}
