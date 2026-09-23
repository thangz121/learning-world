// A_World/CountingGarden/GardenZoneSpot.cs — S3 P2X ZONE PICKER (user order §47B).
// ONE reusable zone door for the Counting Garden's 5 plots (never one script per
// zone): a walk-up / click-in focus anchor with its own scene-authored camera
// pair. Clicking it (ClickRouter IClickTarget, same proven arrival contract as
// Interactable/NPCs) focuses the zone; walking within FocusRadius does the same.
// The focus BEAT itself (camera frame, HUD name, panel, cancel) lives in
// CountingGardenArea — this component is pure data + the click hook.
// playEnabled: only the zone with a staged activity (today the demo theatre,
// index 2) offers the "Play" door; the 4 skeleton plots stay look-only.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class GardenZoneSpot : MonoBehaviour, IClickTarget {
  public const float FocusRadius = 2.0f;

  public int zoneIndex;
  public bool playEnabled;
  public Transform CameraAnchor;
  public Transform LookAnchor;
  public CountingGardenArea Area;

  public void Bind(CountingGardenArea area) { Area = area; }

  // Zone name pair: derived from the plot's own crop identity (the beds carry
  // carrot/strawberry/corn/pumpkin; the middle plot is the lesson stage).
  public static string NameOf(int index) {
    switch (index) {
      case 0: return DialogueLang.T("Carrot patch", "Vườn cà rốt");
      case 1: return DialogueLang.T("Strawberry patch", "Vườn dâu");
      case 2: return DialogueLang.T("Counting stage", "Sân đếm");
      case 3: return DialogueLang.T("Corn patch", "Vườn ngô");
      case 4: return DialogueLang.T("Pumpkin patch", "Vườn bí");
      default: return DialogueLang.T("Garden", "Khu vườn");
    }
  }

  public string ZoneName { get { return NameOf(zoneIndex); } }

  public void OnClicked() {
    if (Area != null) Area.FocusZone(zoneIndex);
  }
}
