// A_World/MathWorld/MathLearningEntries.cs — stable Learning Entry IDs (S2 seam).
// Phase 3.1 will open learning experiences through THESE identities, never
// through coordinates, prefab paths or hierarchy paths. The world resolves
// them (entry-marker GameObjects with matching names under LearningEntryRoot).
// Strings only here: no behavior, no curriculum, no save schema. C# 9.0 only.
public static class MathLearningEntries {
  public const string Lobby = "math.lobby";
  public const string CountingGarden = "math.counting_garden";
  public const string NumberBridge = "math.number_bridge";

  public static readonly string[] All = { Lobby, CountingGarden, NumberBridge };
}
