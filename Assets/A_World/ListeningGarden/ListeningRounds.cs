// A_World/ListeningGarden/ListeningRounds.cs — pure round planner for the
// listening game (no UnityEngine): deterministic level pools, target order,
// distractor sets and answer rotation. EditMode-testable without a scene.
// C# 9.0 only.

public static class ListeningRounds {
  public const int FirstLevel = 1;
  public const int LastLevel = 4;

  static readonly string[][] Pools = {
    new[] { "chair", "table" },
    new[] { "car", "truck", "desk" },
    new[] { "tree", "flower", "rock", "books" },
    new[] { "chair", "table", "desk", "books", "car", "truck", "tree", "rock", "flower", "mushroom", "apple", "ball", "banana", "orange", "carrot", "tomato", "watermelon", "grape" },
  };
  static readonly int[] ChoicesPerLevel = { 2, 3, 4, 4 };
  static readonly int[] RoundsPerLevel = { 2, 3, 3, 4 };

  static int Index(int level) {
    int i = level - FirstLevel;
    if (i < 0) i = 0;
    if (i >= Pools.Length) i = Pools.Length - 1;
    return i;
  }

  public static string[] PoolFor(int level) {
    return (string[])Pools[Index(level)].Clone();
  }

  public static int ChoicesFor(int level) {
    return ChoicesPerLevel[Index(level)];
  }

  public static int RoundsFor(int level) {
    return RoundsPerLevel[Index(level)];
  }

  public static int TotalRounds() {
    int sum = 0;
    for (int i = 0; i < RoundsPerLevel.Length; i++) sum += RoundsPerLevel[i];
    return sum;
  }

  // Deterministic round: the target walks the pool, the answer position
  // rotates (no side bias), distractors are distinct pool words.
  public static void BuildRound(int level, int roundIndex, out string target,
      out string[] choices, out int answerIndex) {
    int li = Index(level);
    string[] pool = Pools[li];
    int n = ChoicesPerLevel[li];
    if (n > pool.Length) n = pool.Length;
    int targetIdx = (roundIndex + li) % pool.Length;
    target = pool[targetIdx];
    answerIndex = (roundIndex + level) % n;
    choices = new string[n];
    int walk = (targetIdx + 1 + roundIndex + li) % pool.Length;
    for (int i = 0; i < n; i++) {
      if (i == answerIndex) { choices[i] = target; continue; }
      while (walk == targetIdx) walk = (walk + 1) % pool.Length;
      choices[i] = pool[walk];
      walk = (walk + 1) % pool.Length;
    }
  }
}
