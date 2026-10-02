// A_World/ClassificationCity/ClassificationItem.cs — sortable town object.
// IClickTarget only. C# 9.0 only.
using UnityEngine;

[DisallowMultipleComponent]
public class ClassificationItem : MonoBehaviour, IClickTarget {
  public enum ItemState { Idle, Carried, Placed, Example }

  public ClassItem Props;
  public string ItemId = "";
  public ItemState State { get; private set; }
  public Vector3 HomeLocal;
  public ClassificationCity Game;

  Collider _col;
  Transform _root;
  Vector3 _baseScale = Vector3.one;

  public void Bind(ClassificationCity game, Transform island, ClassItem props,
      string id, Vector3 homeLocal, bool example) {
    Game = game;
    _root = island;
    Props = props;
    ItemId = id;
    HomeLocal = homeLocal;
    State = example ? ItemState.Example : ItemState.Idle;
    _col = GetComponent<Collider>();
    _baseScale = transform.localScale;
    if (example && _col != null) _col.enabled = false;
  }

  public void OnClicked() {
    if (Game == null) return;
    Game.TrySelect(this);
  }

  // Demo/pre-sorted hint item: never sortable, never counted as a town object.
  public void MarkExample() {
    State = ItemState.Example;
    if (_col == null) _col = GetComponent<Collider>();
    if (_col != null) _col.enabled = false;
  }

  public void BeginCarry(Transform holder) {
    if (State == ItemState.Placed || State == ItemState.Example) return;
    State = ItemState.Carried;
    transform.SetParent(holder, true);
    transform.localPosition = new Vector3(0.55f, 0.88f, 0.95f);
    transform.localRotation = Quaternion.identity;
    if (_col != null) _col.enabled = false;
  }

  public void ReturnHome() {
    if (State == ItemState.Example) return;
    State = ItemState.Idle;
    if (_root != null) transform.SetParent(_root, true);
    transform.localPosition = HomeLocal;
    transform.localRotation = Quaternion.identity;
    transform.localScale = _baseScale;
    if (_col != null) _col.enabled = true;
  }

  public void PlaceAt(Transform bin) {
    State = ItemState.Placed;
    transform.SetParent(bin, true);
    int n = 0;
    ClassificationBin binComp = bin.GetComponent<ClassificationBin>();
    if (binComp != null) n = Mathf.Max(0, binComp.Occupants.Count - 1);
    transform.localPosition = new Vector3((n % 3 - 1) * 0.45f, 0.4f, (n / 3) * 0.4f);
    transform.localRotation = Quaternion.identity;
    if (_col != null) _col.enabled = false;
  }
}
