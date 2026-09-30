// A_World/OrderingStation/OrderingTypes.cs — Ga Thứ Tự.
// Ordering model: pure C#9 logic, EditMode-testable. C# 9.0 only.

public enum OrderDim {
  Size = 0,
  Height = 1,
  Length = 2,
}

public enum OrderDir {
  Asc = 0,
  Desc = 1,
}

public static class OrderLogic {
  // Indices that sort values ascending (stable for equal values).
  public static int[] SortIndices(float[] v, bool desc) {
    int n = v.Length;
    int[] idx = new int[n];
    for (int i = 0; i < n; i++) idx[i] = i;
    for (int i = 1; i < n; i++) {
      int key = idx[i];
      int j = i - 1;
      while (j >= 0 && Before(v[key], v[idx[j]], desc)) {
        idx[j + 1] = idx[j];
        j--;
      }
      idx[j + 1] = key;
    }
    return idx;
  }

  static bool Before(float a, float b, bool desc) {
    return desc ? a > b : a < b;
  }

  // Whole-sequence validation: ranks in slot order satisfy the direction.
  public static bool IsOrdered(float[] slotRanks, bool desc) {
    for (int i = 1; i < slotRanks.Length; i++) {
      if (desc) { if (slotRanks[i - 1] < slotRanks[i]) return false; }
      else { if (slotRanks[i - 1] > slotRanks[i]) return false; }
    }
    return true;
  }

  // Where a missing rank belongs among sorted present ranks (insert position).
  public static int InsertPosition(float[] sortedPresent, float rank, bool desc) {
    for (int i = 0; i < sortedPresent.Length; i++) {
      if (desc) { if (rank > sortedPresent[i]) return i; }
      else { if (rank < sortedPresent[i]) return i; }
    }
    return sortedPresent.Length;
  }

  // First / middle / last slot index for a filled track of n slots.
  public static int FirstSlot(int n) { return 0; }
  public static int LastSlot(int n) { return n - 1; }
  public static int MiddleSlot(int n) { return n / 2; }
}
