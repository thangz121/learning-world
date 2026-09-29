// A_World/CountingGarden/RabbitBowlDrag.cs — S3-P2Z26/30 (user: "kéo lại carrot
// từ trên bục về lại vườn" + "bấm vào củ trên bục rồi bấm vào vườn"). Two ways
// to take a fed carrot off the bowl:
//   * DRAG it with the pointer and drop it over the carrot patch;
//   * TAP the carrot (it pops / becomes selected), then TAP the garden.
// Both end in RabbitFeed.ReturnFromBowl (the count drops). ClickRouter skips the
// carrot presses through the shared IDragTarget contract, so a tap/drag never
// also walks the child; a tap on the garden still routes normally (the child
// walks over). Mouse and touch both arrive through Pointer.current. C# 9.0 only.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class RabbitBowlDrag : MonoBehaviour {
  const float DragThreshold = 14f; // screen px before a press counts as a drag

  RabbitFeed _game;
  RabbitCarrot _drag;
  RabbitCarrot _selected;
  bool _hasPress;
  RabbitCarrot _pressCarrot;
  Vector2 _pressScreen;
  bool _pressGarden;
  bool _pressMoved;
  float _planeY;
  Vector3 _grabOffset;

  public void Bind(RabbitFeed game) {
    _game = game;
  }

  void OnDisable() { Cancel(); }

  void Update() {
    if (_game == null) return;
    if (_game.Current != RabbitFeed.Phase.Feeding) { Cancel(); return; }
    // S3-P2Z33 (UX): leaving the bowl cancels a pending removal — a carrot
    // selected at the bowl must NOT be returned by a later garden tap made from
    // across the arena (the child changed their mind by walking away).
    if (_selected != null && !_game.PlayerAtBowl) ClearSelection();
    Pointer pointer = Pointer.current;
    if (pointer == null) return;
    Camera cam = Camera.main;
    if (cam == null) return;
    Vector2 pos = pointer.position.ReadValue();

    if (!_hasPress) {
      if (!pointer.press.wasPressedThisFrame) return;
      if (IsPointerOverUi()) return;
      _hasPress = true;
      _pressScreen = pos;
      _pressMoved = false;
      _pressCarrot = PickDraggable(cam, pos);
      // A press that lands on ANY carrot (not just a draggable one) is never a
      // "tap the garden" — otherwise picking a carrot from the patch counted as
      // a garden tap and returned an accidentally-selected bowl carrot.
      _pressGarden = _pressCarrot == null && !RayHitsCarrot(cam, pos) && GroundOverGarden(cam, pos);
      return;
    }

    if (pointer.press.isPressed) {
      if (Vector2.Distance(pos, _pressScreen) > DragThreshold) _pressMoved = true;
      if (_pressCarrot != null && _pressMoved) {
        if (_drag == null) {
          _pressCarrot.DragBegin();
          ClearSelection(); // a dragged carrot is no longer "selected"
          _drag = _pressCarrot;
          _planeY = _drag.transform.position.y;
          Vector3 world = PlanePoint(cam, _pressScreen, _planeY);
          _grabOffset = _drag.transform.position - world;
          _grabOffset.y = 0f;
        }
        Vector3 w = PlanePoint(cam, pos, _planeY) + _grabOffset;
        _drag.DragMoveTo(new Vector3(w.x, _planeY, w.z));
      }
      return;
    }

    // Released.
    _hasPress = false;
    RabbitCarrot pressCarrot = _pressCarrot;
    bool garden = _pressGarden;
    bool moved = _pressMoved;
    _pressCarrot = null;
    _pressGarden = false;
    if (_drag != null) {
      RabbitCarrot d = _drag;
      _drag = null;
      d.DragEnd();
      Vector3 drop = PlanePoint(cam, pos, 0f);
      if (_game.IsOverGarden(drop)) _game.ReturnFromBowl(d);
      else _game.SnapCarrotToBowl(d);
      return;
    }
    if (pressCarrot != null) {
      if (!moved) ToggleSelect(pressCarrot); // tap a bowl carrot -> select
      return;
    }
    if (!moved && garden && _selected != null) {
      RabbitCarrot s = _selected;
      ClearSelection();
      _game.ReturnFromBowl(s); // tap the garden -> the selected carrot goes home
    }
  }

  void Cancel() {
    if (_drag != null) {
      _drag.DragEnd();
      _drag = null;
    }
    _hasPress = false;
    _pressCarrot = null;
    _pressGarden = false;
    ClearSelection();
  }

  void ToggleSelect(RabbitCarrot c) {
    if (c == null) return;
    if (_selected == c) {
      c.Selected = false;
      _selected = null;
      return;
    }
    ClearSelection();
    _selected = c;
    c.Selected = true;
    try { Debug.Log("[RabbitBowlDrag] selected " + c.name + " (tap the garden to return it).", this); }
    catch (Exception) { }
  }

  void ClearSelection() {
    if (_selected != null) _selected.Selected = false;
    _selected = null;
  }

  static bool RayHitsCarrot(Camera cam, Vector2 screen) {
    Ray ray = cam.ScreenPointToRay(screen);
    RaycastHit[] hits = Physics.RaycastAll(ray, 200f, ~0);
    for (int i = 0; i < hits.Length; i++) {
      if (hits[i].collider != null
          && hits[i].collider.GetComponentInParent<RabbitCarrot>() != null) return true;
    }
    return false;
  }

  static RabbitCarrot PickDraggable(Camera cam, Vector2 screen) {    Ray ray = cam.ScreenPointToRay(screen);
    RaycastHit[] hits = Physics.RaycastAll(ray, 200f, ~0);
    RabbitCarrot best = null;
    float bestDist = float.MaxValue;
    for (int i = 0; i < hits.Length; i++) {
      if (hits[i].distance >= bestDist) continue;
      RabbitCarrot c = hits[i].collider != null
        ? hits[i].collider.GetComponentInParent<RabbitCarrot>() : null;
      if (c == null || !c.CanDragNow) continue;
      best = c;
      bestDist = hits[i].distance;
    }
    return best;
  }

  bool GroundOverGarden(Camera cam, Vector2 screen) {
    if (_game == null) return false;
    return _game.IsOverGarden(PlanePoint(cam, screen, 0f));
  }

  static bool IsPointerOverUi() {
    EventSystem events = EventSystem.current;
    if (events == null) return false;
    return events.IsPointerOverGameObject();
  }

  static Vector3 PlanePoint(Camera cam, Vector2 screen, float y) {
    Ray ray = cam.ScreenPointToRay(screen);
    Plane plane = new Plane(Vector3.up, new Vector3(0f, y, 0f));
    float dist;
    if (plane.Raycast(ray, out dist)) return ray.GetPoint(dist);
    return ray.origin;
  }
}
