// CT-P48: S3 P2W COUNTING DEMO — TWO-NPC MINI LESSON (Number 2).
// Pins: lesson stage geometry (board + number 2, five-ball field, basket,
// hidden result, shot A/B camera markers), the two staged actors (teacher +
// child student, face kits, click-through, no gameplay surface), the full
// acted sequence + tidy loop reset, and the two-shot camera contract.
// C# 9.0 only.
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

public class CT_P48_CountingDemo {
  static Transform FindDeep(Transform t, string name) {
    if (t == null) return null;
    if (t.name == name) return t;
    for (int i = 0; i < t.childCount; i++) {
      Transform f = FindDeep(t.GetChild(i), name);
      if (f != null) return f;
    }
    return null;
  }

  static float Dist2D(Vector3 a, Vector3 b) {
    float dx = a.x - b.x, dz = a.z - b.z;
    return Mathf.Sqrt(dx * dx + dz * dz);
  }

  static void BuildDemo(out GameObject garden, out CountingGardenBuilder builder,
      out CountingDemo demo) {
    garden = new GameObject("P48GardenWorld");
    builder = garden.AddComponent<CountingGardenBuilder>();
    builder.BuildContent(garden.transform);
    demo = garden.AddComponent<CountingDemo>();
    demo.Build(builder, null, null, null);
  }

  static void TearDown(GameObject garden) {
    if (garden != null) Object.DestroyImmediate(garden);
  }

  // A. Lesson stage: board + number, FIVE balls, basket, hidden result, both
  // shot markers, all inside the demo theatre plot.
  [Test] public void P48A_LessonStageBuilt() {
    GameObject garden;
    CountingGardenBuilder builder;
    CountingDemo demo;
    BuildDemo(out garden, out builder, out demo);
    try {
      Assert.IsNotNull(demo, "demo controller built");
      Vector3 plot = builder.DemoStageCenter;
      string[] kit = { "CGDemoBoardPanel", "CGDemoBoardL", "CGDemoBoardR",
        "CGDemoNumber2", "CGDemoBallField", "CGDemoBasket",
        "CGDemoResultFrame", "CGDemoResultTwo", "CGDemoResultCheckArm" };
      foreach (string n in kit) {
        Transform t = FindDeep(garden.transform, n);
        Assert.IsNotNull(t, "lesson static " + n);
        Assert.Less(Dist2D(t.position, plot), 4.2f, n + " sits inside the demo theatre");
      }
      Assert.AreEqual(5, builder.DemoBalls.Count, "five balls on the field");
      Assert.IsNull(FindDeep(garden.transform, "CGDemoBall5"), "exactly five balls");
      for (int i = 0; i < builder.DemoBalls.Count; i++) {
        Assert.IsNotNull(builder.DemoBalls[i], "ball " + i);
        Assert.Less(Dist2D(builder.DemoBalls[i].transform.localPosition, plot), 4.2f,
          "ball " + i + " on the stage");
      }
      // The field row must be spread (no stacking ambiguity).
      float minGap = 999f;
      for (int i = 0; i < builder.DemoBalls.Count - 1; i++) {
        float gap = Dist2D(builder.DemoBalls[i].transform.localPosition,
          builder.DemoBalls[i + 1].transform.localPosition);
        if (gap < minGap) minGap = gap;
      }
      Assert.Greater(minGap, 0.5f, "balls read as distinct objects");
      Assert.IsFalse(builder.DemoResult.activeSelf, "result hidden until the balls are in");
      Assert.IsNotNull(builder.DemoCam, "shot A camera marker");
      Assert.IsNotNull(builder.DemoLook, "shot A look marker");
      Assert.IsNotNull(builder.DemoActionCam, "shot B camera marker");
      Assert.IsNotNull(builder.DemoActionLook, "shot B look marker");
      // Shot A frames the whole lesson (board behind teacher).
      Assert.Less(builder.DemoCam.localPosition.z, plot.z - 2f,
        "shot A sits north (plaza side) of the stage");
      Assert.Less(builder.DemoCam.localPosition.y, 3f, "shot A at child-comfort height");
      // Shot B is tighter and lower than shot A (the action card).
      Assert.Less(Vector3.Distance(builder.DemoActionCam.localPosition, plot),
        Vector3.Distance(builder.DemoCam.localPosition, plot),
        "shot B sits closer than shot A");
      Assert.Less(builder.DemoActionCam.localPosition.y, builder.DemoCam.localPosition.y,
        "shot B is lower than shot A");
      Assert.Less(Dist2D(builder.DemoActionLook.localPosition, builder.DemoActionCam.localPosition), 4.5f,
        "shot B keeps the action large in frame");
    } finally { TearDown(garden); }
  }

  // B. Two acted roles: teacher + child student, both with bodies/faces, both
  // click-through, and NO gameplay surface anywhere in the lesson.
  [Test] public void P48B_TwoActorsAndFirewall() {
    GameObject garden;
    CountingGardenBuilder builder;
    CountingDemo demo;
    BuildDemo(out garden, out builder, out demo);
    try {
      Assert.IsTrue(demo.ActorsBuilt, "teacher + student bodies built");
      Transform teacher = FindDeep(garden.transform, "CGDemoTeacher");
      Transform student = FindDeep(garden.transform, "CGDemoStudent");
      Assert.IsNotNull(teacher, "teacher rooted in the garden scene");
      Assert.IsNotNull(student, "student rooted in the garden scene");
      foreach (Transform t in new[] { teacher, student }) {
        Assert.IsNotNull(t.GetComponentInChildren<Animator>(true), t.name + " has Animator");
        Assert.IsNotNull(t.GetComponentInChildren<CharacterPresentation>(true), t.name + " has face kit");
        Assert.IsNull(t.GetComponentInChildren<Collider>(true), t.name + " is click-through");
        Assert.IsNull(t.GetComponentInChildren<Interactable>(true), t.name + " is not gameplay");
        Assert.IsNotNull(FindDeep(t, t.name + "CarryAnchor"), t.name + " has a visible carry anchor");
      }
      // The student is a CHILD next to the teacher (user script roles).
      float teacherH = teacher.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
        ? teacher.GetComponentInChildren<SkinnedMeshRenderer>(true).bounds.size.y : 0f;
      float studentH = student.GetComponentInChildren<SkinnedMeshRenderer>(true) != null
        ? student.GetComponentInChildren<SkinnedMeshRenderer>(true).bounds.size.y : 0f;
      Assert.Greater(teacherH, 0.5f, "teacher has a body");
      Assert.Greater(studentH, 0.3f, "student has a body");
      Assert.Less(studentH, teacherH, "student reads smaller than the teacher (a child)");
      Assert.IsFalse(typeof(IClickTarget).IsAssignableFrom(typeof(CountingDemo)),
        "demo is not a click target (the child cannot drive the lesson)");
      System.Reflection.FieldInfo[] fields = typeof(CountingDemo).GetFields(
        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
      foreach (System.Reflection.FieldInfo f in fields) {
        string tn = f.FieldType.FullName ?? f.FieldType.Name;
        Assert.IsFalse(tn.Contains("Save") || tn.Contains("Mastery") || tn.Contains("Score")
          || tn.Contains("Manager"), "no progression/manager types on the demo (" + f.Name + ")");
      }
      MicroWorldPortal[] portals = garden.GetComponentsInChildren<MicroWorldPortal>(true);
      Assert.AreEqual(1, portals.Length, "the lesson adds no portals (exit only)");
    } finally { TearDown(garden); }
  }

  // C. Full lesson loop headless: the role script order holds, the student
  // takes exactly 2 of the 5 balls to the basket, the result appears only
  // after both are in, the reset tidies everything, nobody teleports.
  [Test] public void P48C_LessonLoopsClean() {
    GameObject garden;
    CountingGardenBuilder builder;
    CountingDemo demo;
    BuildDemo(out garden, out builder, out demo);
    try {
      DemoPhase[] story = { DemoPhase.TeacherLookBoard, DemoPhase.TeacherSayBoard,
        DemoPhase.TeacherSayTwo, DemoPhase.TeacherSayToday, DemoPhase.TeacherAssign,
        DemoPhase.StudentLook, DemoPhase.WalkBalls, DemoPhase.PickOne, DemoPhase.PickTwo,
        DemoPhase.ShowTwo, DemoPhase.WalkBasket, DemoPhase.PlaceOne, DemoPhase.PlaceTwo,
        DemoPhase.TeacherAsks, DemoPhase.Confirm, DemoPhase.Celebrate,
        DemoPhase.HoldResult, DemoPhase.ResetBalls, DemoPhase.StudentReturn,
        DemoPhase.TeacherReturn, DemoPhase.Ready };
      List<DemoPhase> order = new List<DemoPhase>();
      DemoPhase last = demo.Phase;
      order.Add(last);
      Transform teacher = FindDeep(garden.transform, "CGDemoTeacher");
      Transform student = FindDeep(garden.transform, "CGDemoStudent");
      Vector3 prevS = student.localPosition, prevT = teacher.localPosition;
      float maxStep = 0f;
      bool sawResult = false, ballsInBasket = false;
      int iter = 0;
      while (iter < 4000) {
        iter++;
        demo.Step(0.1f);
        if (demo.Phase != last) {
          last = demo.Phase;
          order.Add(last);
          if (last == DemoPhase.Celebrate) {
            // Confirm just ran: result visible + both chosen balls at the basket.
            sawResult = demo.ResultShown;
            float d2 = Dist2D(builder.DemoBalls[2].transform.localPosition,
              builder.DemoBasket.localPosition);
            float d3 = Dist2D(builder.DemoBalls[3].transform.localPosition,
              builder.DemoBasket.localPosition);
            ballsInBasket = d2 < 0.7f && d3 < 0.7f;
          }
        }
        Vector3 sp = student.localPosition, tp = teacher.localPosition;
        float step = Mathf.Max(Dist2D(sp, prevS), Dist2D(tp, prevT));
        if (step > maxStep) maxStep = step;
        prevS = sp;
        prevT = tp;
        if (demo.LoopCount >= 1 && demo.Phase == DemoPhase.TeacherLookBoard) break;
      }
      Assert.Less(iter, 4000, "the lesson completes (no stall)");
      Assert.GreaterOrEqual(demo.LoopCount, 1, "loop counter advances");
      int cursor = 0;
      foreach (DemoPhase p in order) {
        if (cursor < story.Length && p == story[cursor]) cursor++;
      }
      Assert.AreEqual(story.Length, cursor,
        "role script intact: teacher explains -> assigns -> student fetches two -> carries -> teacher confirms -> celebrate -> reset");
      Assert.LessOrEqual(maxStep, 0.9f * 0.1f + 0.001f, "actors never teleport (walk speed bound)");
      Assert.IsTrue(sawResult, "result appears only after both balls land");
      Assert.IsTrue(ballsInBasket, "the two chosen balls rest by the basket at confirm time");
      Assert.IsFalse(demo.ResultShown, "result hides again on reset");
      for (int i = 0; i < builder.DemoBalls.Count; i++) {
        float home = Dist2D(builder.DemoBalls[i].transform.localPosition,
          CountingGardenBuilder.DemoBallHomes[i]);
        Assert.Less(home, 0.05f, "ball " + i + " home after reset");
      }
      float back = Dist2D(student.localPosition, CountingGardenBuilder.DemoStudentStart);
      Assert.Less(back, 0.2f, "student back at the start position");
    } finally { TearDown(garden); }
  }

  // D. Camera contract: walking up fires the lesson card once and holds it;
  // the sequence reframes from shot A to shot B for the student action;
  // leaving releases the beat and re-entering re-arms it.
  [Test] public void P48D_TwoShotCameraBeat() {
    GameObject garden = new GameObject("P48GardenCam");
    try {
      CountingGardenBuilder builder = garden.AddComponent<CountingGardenBuilder>();
      builder.BuildContent(garden.transform);
      GameObject camGo = new GameObject("P48Camera");
      SmartCamera cam = camGo.AddComponent<SmartCamera>();
      GameObject playerGo = new GameObject("P48Player");
      playerGo.transform.position = new Vector3(0f, 0f, 0f);
      CountingDemo demo = garden.AddComponent<CountingDemo>();
      demo.Build(builder, playerGo.transform, cam, null);
      for (int i = 0; i < 20; i++) demo.Step(0.1f);
      Assert.AreEqual(0, demo.DemoBeatsFired, "no beat while the child is away");
      playerGo.transform.position = CountingGardenBuilder.WorldOffset + builder.DemoMouth;
      for (int i = 0; i < 20; i++) demo.Step(0.1f);
      Assert.AreEqual(1, demo.DemoBeatsFired, "walking up frames the lesson once");
      Assert.IsFalse(demo.ShotIsAction, "the lesson starts on shot A (board + teacher + student)");
      int guard = 0;
      while (!demo.ShotIsAction && guard < 900) { demo.Step(0.1f); guard++; }
      Assert.IsTrue(demo.ShotIsAction, "the camera reframes to shot B when the student acts");
      for (int i = 0; i < 20; i++) demo.Step(0.1f);
      Assert.AreEqual(1, demo.DemoBeatsFired, "beat does not spam while staying");
      playerGo.transform.position = new Vector3(60f, 0f, 0f);
      for (int i = 0; i < 30; i++) demo.Step(0.1f);
      playerGo.transform.position = CountingGardenBuilder.WorldOffset + builder.DemoMouth;
      for (int i = 0; i < 30; i++) demo.Step(0.1f);
      Assert.AreEqual(2, demo.DemoBeatsFired, "re-entering re-frames (re-arm)");
      Object.DestroyImmediate(camGo);
      Object.DestroyImmediate(playerGo);
    } finally { Object.DestroyImmediate(garden); }
  }
}
