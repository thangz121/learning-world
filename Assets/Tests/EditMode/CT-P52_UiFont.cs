// CT-P52: S3-P2Z7 GAME-WIDE FONT (user order: "tất cả các chữ trong game đều
// phải dùng font UTM Avo.ttf").
// Pins: the font asset loads from Resources (the code-built UI never carries
// scene references), it is the UTM Avo face (not the engine fallback), and no
// A_World UI file bypasses UiFont with the builtin engine font any more.
// C# 9.0 only.
using NUnit.Framework;
using System.IO;
using UnityEngine;

public class CT_P52_UiFont {
  // A. The project font loads and IS the UTM Avo face.
  [Test] public void P52A_AvoFontLoads() {
    Font font = UiFont.Get();
    Assert.IsNotNull(font, "UTM Avo font asset loads from Resources/Fonts");
    Assert.AreEqual(UiFont.FontAssetName, font.name, "the loaded face is UTM Avo, not a fallback");
    Font bold = UiFont.GetBold();
    Assert.IsNotNull(bold, "bold face available (falls back to regular if absent)");
    Assert.AreEqual(UiFont.BoldAssetName, bold.name, "bold face is UTMAvoBold");
    Assert.AreSame(font, UiFont.Get(), "font lookup is cached (one asset instance)");
  }

  // B. No UI file creates text with the engine font directly: every Text goes
  // through UiFont (source-level firewall, same pattern as CT-P37).
  [Test] public void P52B_NoLegacyFontBypass() {
    string root = Path.Combine(Application.dataPath, "A_World");
    Assert.IsTrue(Directory.Exists(root), "A_World exists");
    string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
    Assert.Greater(files.Length, 20, "A_World has its UI sources");
    foreach (string file in files) {
      if (Path.GetFileName(file) == "UiFont.cs") continue; // the fallback lives there
      string text = File.ReadAllText(file);
      if (text.Contains("//")) {
        // ignore comment lines (they explain the old engine font)
        string[] lines = text.Split('\n');
        foreach (string line in lines) {
          int c = line.IndexOf("//");
          string code = c >= 0 ? line.Substring(0, c) : line;
          Assert.IsFalse(code.Contains("GetBuiltinResource<Font>"),
            Path.GetFileName(file) + " must use UiFont.Get(), not the engine font: " + line.Trim());
        }
      } else {
        Assert.IsFalse(text.Contains("GetBuiltinResource<Font>"),
          Path.GetFileName(file) + " must use UiFont.Get()");
      }
    }
  }
}
