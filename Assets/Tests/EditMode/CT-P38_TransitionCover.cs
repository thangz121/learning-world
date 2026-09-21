// CT-P38: S3A transition-cover structure (Phase 3.0.x).
// MarketHUD hosts a fullscreen click-through black cover (SceneBridge pattern
// ADAPTED — no prefab, no new system, no progress bar): Bootstrap fades it
// over the load/warp/unload beat, then lifts it. Pure EditMode structure +
// API pins (rendering proof belongs to S5 journey, never to unit tests):
// cover exists, fullscreen, topmost sibling, raycast-transparent, alpha
// clamps, starts disabled. No player, no scenes, no movement. C# 9.0 only.
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class CT_P38_TransitionCover {
  static MarketHUD NewHud(out GameObject go) {
    go = new GameObject("HudCoverTest");
    MarketHUD hud = go.AddComponent<MarketHUD>();
    hud.BuildUiImmediate();
    return hud;
  }

  [Test] public void P38A_CoverExistsFullscreenTopmost() {
    GameObject go;
    MarketHUD hud = NewHud(out go);
    try {
      Assert.IsTrue(hud.HasTransitionCover, "HUD must build the transition cover");
      Transform cover = go.transform.Find("MarketCanvas/TransitionCover");
      Assert.IsNotNull(cover, "cover lives in the HUD canvas");
      RectTransform rt = cover.GetComponent<RectTransform>();
      Assert.IsNotNull(rt);
      Assert.AreEqual(Vector2.zero, rt.anchorMin, "cover spans the full screen");
      Assert.AreEqual(Vector2.one, rt.anchorMax);
      Assert.AreEqual(go.transform.Find("MarketCanvas").childCount - 1, cover.GetSiblingIndex(),
        "cover is the last sibling (topmost)");
      Image img = cover.GetComponent<Image>();
      Assert.IsNotNull(img);
      Assert.IsFalse(img.raycastTarget, "taps must pass through mid-transition");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void P38B_CoverAlphaClampsAndDisables() {
    GameObject go;
    MarketHUD hud = NewHud(out go);
    try {
      Assert.AreEqual(0f, hud.TransitionCoverAlpha, 0.001f, "cover starts invisible");
      hud.SetTransitionCover(1f);
      Assert.AreEqual(1f, hud.TransitionCoverAlpha, 0.001f);
      hud.SetTransitionCover(0.5f);
      Assert.AreEqual(0.5f, hud.TransitionCoverAlpha, 0.001f, "mid values pass through (fade steps)");
      hud.SetTransitionCover(5f);
      Assert.AreEqual(1f, hud.TransitionCoverAlpha, 0.001f, "clamps high");
      hud.SetTransitionCover(-2f);
      Assert.AreEqual(0f, hud.TransitionCoverAlpha, 0.001f, "clamps low");
      Transform cover = go.transform.Find("MarketCanvas/TransitionCover");
      Assert.IsFalse(cover.GetComponent<Image>().enabled, "disabled at zero (zero overdraw at rest)");
      hud.SetTransitionCover(1f);
      Assert.IsTrue(cover.GetComponent<Image>().enabled, "enabled while covering");
    } finally { Object.DestroyImmediate(go); }
  }

  [Test] public void P38C_ApiSurvivesHudTeardown() {
    // Unload edge: Bootstrap may touch the HUD after teardown — the API must
    // degrade silently, never throw (Awake always builds, so "before build"
    // cannot occur; teardown is the real hazard).
    GameObject go = new GameObject("HudCoverNullTest");
    MarketHUD hud = go.AddComponent<MarketHUD>();
    hud.BuildUiImmediate();
    Assert.IsTrue(hud.HasTransitionCover);
    Object.DestroyImmediate(go);
    Assert.IsFalse(hud.HasTransitionCover, "dead cover reads absent, not crashing");
    Assert.AreEqual(0f, hud.TransitionCoverAlpha, 0.001f);
    hud.SetTransitionCover(1f); // must not throw
    Assert.AreEqual(0f, hud.TransitionCoverAlpha, 0.001f);
  }
}
