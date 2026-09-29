// TempPlayLog.cs — MANUAL-PLAY SESSION LOGGER (tooling, committed).
// Drop-in path Assets/A_World/SelectionYard/TempPlayLog.cs.
// Inert in production: boots ONLY with the `-playlog` CLI flag.
// Purpose (user order 2026-09-29): the human plays the game manually on
// maynode and the session is logged for analysis:
//   - every mouse press (screen position);
//   - every new click-to-move destination (world XZ) + player XZ;
//   - the player trail (every 2s when the player actually moved >= 0.6m);
//   - every scene load/unload;
//   - selection-yard state changes (inside / level / subject / skill);
//   - the HUD objective line with the trail;
//   - a 10s heartbeat (keeps the trace reconstructable while idle);
//   - screenshots on scene loads into -shot-dir (default E:/LWW/play-shots).
// No input injection, no state pokes, no gameplay changes. ASCII-only source.
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public static class TempPlayLogBoot {
  [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
  static void Boot() {
    string[] args = Environment.GetCommandLineArgs();
    bool want = false;
    foreach (string a in args) {
      if (string.Equals(a, "-playlog", StringComparison.OrdinalIgnoreCase)) { want = true; break; }
    }
    if (!want) return;
    GameObject go = new GameObject("TempPlayLog");
    GameObject.DontDestroyOnLoad(go);
    go.AddComponent<TempPlayLog>();
  }
}

public class TempPlayLog : MonoBehaviour {
  const string DefaultShotDir = "E:/LWW/play-shots";
  string _shotDir = DefaultShotDir;
  int _shots;
  long _clicks;
  float _started;
  Vector3 _lastPlayerPos;
  Vector3 _lastDestination;
  float _trailT;
  float _tickT;
  bool _lastInside;
  string _lastLevel = "";
  string _lastContext = "";
  Camera _cam;
  ClickToMove _player;
  SelectionYardArea _area;
  MarketHUD _hud;

  void Start() {
    _started = Time.realtimeSinceStartup;
    try {
      string[] args = Environment.GetCommandLineArgs();
      for (int i = 0; i + 1 < args.Length; i++) {
        if (string.Equals(args[i], "-shot-dir", StringComparison.OrdinalIgnoreCase)) {
          if (!string.IsNullOrEmpty(args[i + 1])) _shotDir = args[i + 1];
          break;
        }
      }
    } catch (Exception) { }
    try { System.IO.Directory.CreateDirectory(_shotDir); } catch (Exception) { }
    try { SceneManager.sceneLoaded += OnSceneLoaded; } catch (Exception) { }
    try { SceneManager.sceneUnloaded += OnSceneUnloaded; } catch (Exception) { }
    Log("PLAYLOG_START t=0 screen=" + Screen.width + "x" + Screen.height
      + " shotDir=" + _shotDir);
    Shot("boot");
  }

  void OnDestroy() {
    try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch (Exception) { }
    try { SceneManager.sceneUnloaded -= OnSceneUnloaded; } catch (Exception) { }
  }

  void OnApplicationQuit() {
    Log("PLAYLOG_END t=" + T() + " clicks=" + _clicks + " shots=" + _shots);
  }

  void OnSceneLoaded(Scene s, LoadSceneMode mode) {
    Log("PLAYLOG_SCENE loaded '" + s.name + "' mode=" + mode);
    Shot("scene_" + Sanitize(s.name));
  }

  void OnSceneUnloaded(Scene s) {
    Log("PLAYLOG_SCENE unloaded '" + s.name + "'");
  }

  string T() {
    return (Time.realtimeSinceStartup - _started).ToString("F1") + "s";
  }

  void Log(string m) {
    try { Debug.Log("[PLAYLOG] " + m); } catch (Exception) { }
  }

  static string Sanitize(string s) {
    if (string.IsNullOrEmpty(s)) return "x";
    char[] buf = new char[s.Length];
    for (int i = 0; i < s.Length; i++) {
      char c = s[i];
      buf[i] = (char.IsLetterOrDigit(c) || c == '-' || c == '_') ? c : '_';
    }
    return new string(buf);
  }

  string Fmt2(Vector3 v) {
    return "(" + v.x.ToString("F1") + "," + v.z.ToString("F1") + ")";
  }

  void Shot(string label) {
    try {
      string path = _shotDir + "/" + _shots.ToString("00") + "_" + label + ".png";
      ScreenCapture.CaptureScreenshot(path);
      Log("PLAYLOG_SHOT " + _shots.ToString("00") + " " + label);
      _shots++;
    } catch (Exception e) { Log("shot failed: " + e.Message); }
  }

  void Refresh() {
    try {
      if (_player == null) _player = FindObjectOfType<ClickToMove>();
      if (_area == null) _area = FindObjectOfType<SelectionYardArea>();
      if (_hud == null) _hud = FindObjectOfType<MarketHUD>();
      _cam = Camera.main;
    } catch (Exception) { }
  }

  void Update() {
    // Mouse press -> screen position.
    try {
      Mouse mouse = Mouse.current;
      if (mouse != null && mouse.leftButton.wasPressedThisFrame) {
        Vector2 p = mouse.position.ReadValue();
        _clicks++;
        Log("PLAYLOG_CLICK " + _clicks + " screen=(" + p.x.ToString("F0") + ","
          + p.y.ToString("F0") + ") t=" + T());
      }
    } catch (Exception) { }

    Refresh();

    // New movement destination.
    try {
      if (_player != null && _player.HasDestination) {
        Vector3 d = _player.Destination;
        if ((d - _lastDestination).sqrMagnitude > 0.01f) {
          _lastDestination = d;
          Log("PLAYLOG_MOVE to=" + Fmt2(d) + " from=" + Fmt2(_player.transform.position)
            + " t=" + T());
        }
      }
    } catch (Exception) { }

    // Player trail every 2s when the player moved.
    _trailT += Time.deltaTime;
    if (_trailT >= 2f) {
      _trailT = 0f;
      try {
        if (_player != null) {
          Vector3 p = _player.transform.position;
          if ((p - _lastPlayerPos).magnitude >= 0.6f) {
            _lastPlayerPos = p;
            string obj = "";
            try { if (_hud != null) obj = _hud.CurrentObjective; } catch (Exception) { }
            Log("PLAYLOG_POS " + Fmt2(p) + " t=" + T()
              + (string.IsNullOrEmpty(obj) ? "" : " objective=\"" + obj + "\""));
          }
        }
      } catch (Exception) { }
    }

    // Selection-yard state changes.
    try {
      if (_area != null) {
        string level = _area.CurrentLevel.ToString();
        string context = _area.CurrentSubjectId + "|" + _area.CurrentSkillId;
        if (_area.IsInside != _lastInside || level != _lastLevel || context != _lastContext) {
          _lastInside = _area.IsInside;
          _lastLevel = level;
          _lastContext = context;
          Log("PLAYLOG_AREA inside=" + _area.IsInside + " level=" + level
            + " subject=" + _area.CurrentSubjectId + " skill=" + _area.CurrentSkillId
            + " t=" + T());
        }
      }
    } catch (Exception) { }

    // Heartbeat every 10s.
    _tickT += Time.deltaTime;
    if (_tickT >= 10f) {
      _tickT = 0f;
      try {
        string pos = _player != null ? Fmt2(_player.transform.position) : "?";
        int sceneCount = SceneManager.sceneCount;
        string scenes = "";
        for (int i = 0; i < sceneCount; i++) {
          Scene s = SceneManager.GetSceneAt(i);
          if (s.IsValid() && s.isLoaded) scenes += (scenes.Length > 0 ? "+" : "") + s.name;
        }
        Log("PLAYLOG_TICK t=" + T() + " pos=" + pos + " scenes=" + scenes);
      } catch (Exception) { }
    }
  }
}
