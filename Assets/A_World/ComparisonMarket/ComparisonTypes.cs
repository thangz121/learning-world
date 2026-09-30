// A_World/ComparisonMarket/ComparisonTypes.cs — Khu Chợ Của Bé.
// Comparison model: pure C#9 logic, EditMode-testable. C# 9.0 only.

public enum ComparisonType {
  Quantity = 0,
  Size = 1,
  Length = 2,
  Ordinal = 3,
}

public enum TargetRelation {
  More = 0,
  Less = 1,
  Equal = 2,
  Larger = 3,
  Smaller = 4,
  Longer = 5,
  Shorter = 6,
  Largest = 7,
  Smallest = 8,
  Longest = 9,
  Shortest = 10,
  Most = 11,
}

public static class ComparisonLogic {
  // +1 a wins, -1 b wins, 0 tie.
  public static int CompareCounts(int a, int b) {
    if (a > b) return 1;
    if (a < b) return -1;
    return 0;
  }

  // Answer index for a two-way MORE/LESS ask. 0 = left, 1 = right.
  public static int AnswerMoreLess(int left, int right, bool askMore) {
    int c = CompareCounts(left, right);
    if (c == 0) return -1;
    if (askMore) return c > 0 ? 0 : 1;
    return c < 0 ? 0 : 1;
  }

  public static int LargestIndex(int[] v) {
    int best = 0;
    for (int i = 1; i < v.Length; i++) if (v[i] > v[best]) best = i;
    return best;
  }

  public static int SmallestIndex(int[] v) {
    int best = 0;
    for (int i = 1; i < v.Length; i++) if (v[i] < v[best]) best = i;
    return best;
  }

  public static int LongestIndex(float[] v) {
    int best = 0;
    for (int i = 1; i < v.Length; i++) if (v[i] > v[best]) best = i;
    return best;
  }

  public static int ShortestIndex(float[] v) {
    int best = 0;
    for (int i = 1; i < v.Length; i++) if (v[i] < v[best]) best = i;
    return best;
  }

  public static bool AllEqual(int[] v) {
    for (int i = 1; i < v.Length; i++) if (v[i] != v[0]) return false;
    return true;
  }
}
