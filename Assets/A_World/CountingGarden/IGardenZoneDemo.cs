// A_World/CountingGarden/IGardenZoneDemo.cs — S3-P2Z12b (gameplay #2 polish,
// user report: "NPC dạy trẻ chơi ở đâu? Sao không hiện?").
// The garden zone previews (the miniature lessons that play inside a plot and
// drive the "panel after one try-run" gate) share ONE contract, so the area can
// route each plot to its own demo. CountingDemo (ball lesson, zone 2) and
// StairLessonDemo (number stairs, zone 5) both implement it; the area never
// knows which lesson runs where.
// C# 9.0 only.
public interface IGardenZoneDemo {
  int LoopCount { get; }
  bool PassDone { get; }
  // Zone focus owns the run: start a pass now / release it (cut voice + reset).
  void StartFocusedLesson();
  void StopFocusedLesson();
}
