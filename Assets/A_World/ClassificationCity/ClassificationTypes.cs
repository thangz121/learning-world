// A_World/ClassificationCity/ClassificationTypes.cs — Thành Phố Phân Loại.
// Criterion model: pure C#9 logic, EditMode-testable. C# 9.0 only.

public enum ClassCriterion {
  ObjectType = 0,
  Size = 1,
  Wheels = 2,
  Corners = 3,
  Function = 4,
  OddOneOut = 5,
  Discover = 6,
  Color = 7,
}

public enum ClassFunction {
  Eat = 0,
  Move = 1,
  Play = 2,
}

public struct ClassItem {
  public string Category; // "animal" | "vehicle" | "food" | "toy"
  public bool Big;
  public int ColorIdx;
  public bool HasWheels;
  public bool HasCorners;
  public ClassFunction Function;

  public ClassItem(string category, bool big, int color, bool wheels, bool corners, ClassFunction fn) {
    Category = category; Big = big; ColorIdx = color;
    HasWheels = wheels; HasCorners = corners; Function = fn;
  }
}

public static class ClassLogic {
  // Which group (0/1/2) an item belongs to under a criterion.
  // groups: the group keys for this round (e.g. {"animal","vehicle"}).
  public static int GroupFor(ClassItem item, ClassCriterion criterion, string[] groups) {
    string key = KeyFor(item, criterion);
    for (int i = 0; i < groups.Length; i++)
      if (groups[i] == key) return i;
    return -1;
  }

  public static string KeyFor(ClassItem item, ClassCriterion criterion) {
    if (criterion == ClassCriterion.Size) return item.Big ? "big" : "small";
    if (criterion == ClassCriterion.Wheels) return item.HasWheels ? "wheels" : "nowheels";
    if (criterion == ClassCriterion.Corners) return item.HasCorners ? "corners" : "round";
    if (criterion == ClassCriterion.Color) return item.ColorIdx == 0 ? "red" : "notred";
    if (criterion == ClassCriterion.Function) {
      if (item.Function == ClassFunction.Eat) return "food";
      if (item.Function == ClassFunction.Move) return "transport";
      return "toy";
    }
    return item.Category;
  }

  // Odd-one-out: index of the item whose key differs from all others.
  // Returns -1 when there is no single odd item.
  public static int OddIndex(ClassItem[] items, ClassCriterion criterion) {
    if (items == null || items.Length < 3) return -1;
    string[] keys = new string[items.Length];
    for (int i = 0; i < items.Length; i++) keys[i] = KeyFor(items[i], criterion);
    for (int i = 0; i < keys.Length; i++) {
      bool unique = true;
      for (int j = 0; j < keys.Length; j++) {
        if (i != j && keys[j] == keys[i]) { unique = false; break; }
      }
      if (unique) {
        // The rest must all agree with each other.
        bool restSame = true;
        for (int j = 0; j < keys.Length; j++) {
          if (j == i) continue;
          for (int k = j + 1; k < keys.Length; k++) {
            if (k == i) continue;
            if (keys[j] != keys[k]) { restSame = false; break; }
          }
          if (!restSame) break;
        }
        if (restSame) return i;
      }
    }
    return -1;
  }

  // Rule discovery: given pre-sorted example groups, infer the criterion key
  // that separates them. Returns the criterion, or ObjectType as fallback.
  public static ClassCriterion InferCriterion(ClassItem[] groupA, ClassItem[] groupB) {
    ClassCriterion[] order = {
      ClassCriterion.Size, ClassCriterion.Wheels, ClassCriterion.Corners,
      ClassCriterion.Color, ClassCriterion.Function, ClassCriterion.ObjectType
    };
    for (int c = 0; c < order.Length; c++) {
      bool aSame = AllSameKey(groupA, order[c]);
      bool bSame = AllSameKey(groupB, order[c]);
      if (!aSame || !bSame) continue;
      if (KeyFor(groupA[0], order[c]) != KeyFor(groupB[0], order[c])) return order[c];
    }
    return ClassCriterion.ObjectType;
  }

  static bool AllSameKey(ClassItem[] g, ClassCriterion c) {
    if (g == null || g.Length == 0) return false;
    string k = KeyFor(g[0], c);
    for (int i = 1; i < g.Length; i++)
      if (KeyFor(g[i], c) != k) return false;
    return true;
  }
}
