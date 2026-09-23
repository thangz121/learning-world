// A_World/UiFont.cs — S3-P2Z7 (user order): EVERY piece of text in the game
// uses the project font "UTM Avo.ttf" (Vietnamese-first typeface, full diacritic
// coverage). The face lives at Assets/A_World/Resources/Fonts/UTMAvo.ttf so the
// code-built UI can load it at runtime without scene references (same
// Resources pattern as the NPC visuals).
// One cached lookup; if the asset is ever missing the call falls back to the
// engine font so text NEVER disappears (a missing font is a warning, not a
// black screen). C# 9.0 only.
using System;
using UnityEngine;

public static class UiFont {
  public const string FontAssetName = "UTMAvo";
  public const string BoldAssetName = "UTMAvoBold";

  static Font _regular;
  static Font _bold;

  public static Font Get() {
    if (_regular != null) return _regular;
    _regular = Load(FontAssetName);
    Prewarm(_regular);
    return _regular;
  }

  // Dynamic fonts rasterize on demand, so the FIRST frame that shows a brand
  // new character can flash the missing-glyph placeholder (a journey shot
  // caught exactly that). Warm the atlas once with the Vietnamese alphabet and
  // the sizes the UI actually uses — a one-time cost that removes the class.
  static readonly string WarmChars =
    "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz" +
    "ÀÁÂÃÈÉÊÌÍÒÓÔÕÙÚÝàáâãèéêìíòóôõùúýĂăĐđĨĩŨũƠơƯư" +
    "ẠạẢảẤấẦầẨẩẪẫẬậẮắẰằẲẳẴẵẶặẸẹẺẻẼẽẾếỀềỂểỄễỆệỈỉỊịỌọỎỏỐốỒồỔổỖỗỘộỚớỜờỞởỠỡỢợ" +
    "ỤụỦủỨứỪừỬửỮữỰựỲỳỶỷỸỹ!?.,:;'\"()[]-–—·✓●▲ +/&%";

  static readonly int[] WarmSizes = { 18, 20, 23, 26, 28, 32, 40, 44, 48, 60, 88 };

  static void Prewarm(Font font) {
    if (font == null) return;
    try {
      foreach (int size in WarmSizes) {
        font.RequestCharactersInTexture(WarmChars, size, FontStyle.Normal);
        font.RequestCharactersInTexture(WarmChars, size, FontStyle.Bold);
      }
    } catch (Exception) { }
  }

  public static Font GetBold() {
    if (_bold != null) return _bold;
    _bold = Load(BoldAssetName);
    if (_bold == null) _bold = Get();
    return _bold;
  }

  static Font Load(string assetName) {
    Font font = null;
    try { font = Resources.Load<Font>("Fonts/" + assetName); } catch (Exception) { }
    if (font == null) {
      try { Debug.LogWarning("[UiFont] Missing Fonts/" + assetName + "; using the engine font."); }
      catch (Exception) { }
      try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch (Exception) { }
    }
    return font;
  }
}
