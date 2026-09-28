// A_World/CountingGarden/GardenZoneSpot.cs — ZONE PICKER.
// ONE reusable zone door for the Counting Garden's two plots (never one script
// per zone): a walk-up / click-in focus anchor with its own scene-authored
// camera pair. Clicking it (ClickRouter IClickTarget, same proven arrival
// contract as Interactable/NPCs) focuses the zone; walking within FocusRadius
// does the same. The focus BEAT itself (camera frame, HUD name, panel, cancel)
// lives in CountingGardenArea — this component is pure data + the click hook.
// Both plots (carrot at zone 0, stair hill at zone 1) offer the "Play" door.
// C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class GardenZoneSpot : MonoBehaviour, IClickTarget {
  public const float FocusRadius = 2.0f;

  public int zoneIndex;
  public bool playEnabled;
  // Which lazy play scene "Vào chơi" loads for this plot. Empty = no arena.
  public string playSceneName = "";
  // A staged plot's lesson is previewed by the garden MINIATURE: its focus
  // waits for one full try-run before the panel opens.
  public bool demoGate;
  public Transform CameraAnchor;
  public Transform LookAnchor;
  // S3-P2Z22: the plot's gate root; hidden while the demo focus is active.
  public GameObject GateRoot;
  public CountingGardenArea Area;

  public void Bind(CountingGardenArea area) { Area = area; }

  // Zone name pair: derived from the plot's own identity (zone 0 = the carrot
  // patch / rabbit arena; zone 1 = the number-stair hill / gameplay #2).
  public static string NameOf(int index) {
    switch (index) {
      case 0: return DialogueLang.T("Carrot patch", "Vườn cà rốt");
      case 1: return DialogueLang.T("Stair hill", "Đồi Bậc Thang");
      default: return DialogueLang.T("Garden", "Khu vườn");
    }
  }

  public string ZoneName { get { return NameOf(zoneIndex); } }

  public void OnClicked() {
    if (Area != null) Area.FocusZone(zoneIndex);
  }
}
