// CT-P34: world-transition state machine (Phase 3.0.x subject scenes).
// Pure contract tests with fake scene ops — no Unity player, no scenes:
// enter loads, return unloads, spam/double calls are no-ops, failures restore
// truthful states. The machine never fakes progress (load fail -> Idle,
// unload fail/still-loaded -> InSubject). C# 9.0 only.
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public class CT_P34_WorldTransition {
  sealed class FakeOps : ISceneOps {
    public readonly HashSet<string> Loaded = new HashSet<string>();
    public bool FailLoad;
    public bool FailUnload;
    public bool StickOnUnload; // unload "succeeds" but scene stays loaded
    public bool IsLoaded(string s) { return Loaded.Contains(s); }
    public Task LoadAdditiveAsync(string s) {
      if (FailLoad) throw new InvalidOperationException("load fail");
      Loaded.Add(s);
      return Task.CompletedTask;
    }
    public Task UnloadAsync(string s) {
      if (FailUnload) throw new InvalidOperationException("unload fail");
      if (!StickOnUnload) Loaded.Remove(s);
      return Task.CompletedTask;
    }
  }

  static bool Run(Task<bool> t) { return t.GetAwaiter().GetResult(); }

  [Test] public void P34A_EnterLoadsScene() {
    var ops = new FakeOps();
    var m = new WorldTransition(SubjectIds.Main);
    Assert.IsTrue(Run(m.EnterAsync(ops, SubjectIds.Math, "MathScene")));
    Assert.IsTrue(m.Current == SubjectIds.Math);
    Assert.AreEqual(WorldTransitionState.InSubject, m.State);
    Assert.IsTrue(ops.IsLoaded("MathScene"));
  }

  [Test] public void P34B_EnterWithoutSceneIsNoOp() {
    // Spatial (scene-less) subjects stay on the legacy walk-in path: the
    // machine refuses, the caller keeps existing behavior. Nothing happens.
    var ops = new FakeOps();
    var m = new WorldTransition(SubjectIds.Main);
    Assert.IsFalse(Run(m.EnterAsync(ops, SubjectIds.Thinking, "")));
    Assert.IsFalse(Run(m.EnterAsync(ops, SubjectIds.Thinking, null)));
    Assert.AreEqual(WorldTransitionState.Idle, m.State);
    Assert.IsTrue(m.Current == SubjectIds.Main);
    Assert.AreEqual(0, ops.Loaded.Count);
  }

  [Test] public void P34C_SpamEnterIsSafe() {
    var ops = new FakeOps();
    var m = new WorldTransition(SubjectIds.Main);
    Assert.IsTrue(Run(m.EnterAsync(ops, SubjectIds.Math, "MathScene")));
    Assert.IsFalse(Run(m.EnterAsync(ops, SubjectIds.English, "EnglishScene")), "busy: second enter refused");
    Assert.IsFalse(Run(m.EnterAsync(ops, SubjectIds.Math, "MathScene")), "re-enter current refused");
    Assert.IsTrue(m.Current == SubjectIds.Math);
    Assert.AreEqual(WorldTransitionState.InSubject, m.State);
    Assert.IsFalse(ops.IsLoaded("EnglishScene"));
  }

  [Test] public void P34D_ReturnUnloadsToMain() {
    var ops = new FakeOps();
    var m = new WorldTransition(SubjectIds.Main);
    Run(m.EnterAsync(ops, SubjectIds.Math, "MathScene"));
    Assert.IsTrue(Run(m.ReturnAsync(ops, "MathScene")));
    Assert.IsTrue(m.Current == SubjectIds.Main);
    Assert.AreEqual(WorldTransitionState.Idle, m.State);
    Assert.IsFalse(ops.IsLoaded("MathScene"));
  }

  [Test] public void P34E_ReturnWhenIdleIsNoOp() {
    var ops = new FakeOps();
    var m = new WorldTransition(SubjectIds.Main);
    Assert.IsFalse(Run(m.ReturnAsync(ops, "MathScene")));
    Assert.AreEqual(WorldTransitionState.Idle, m.State);
  }

  [Test] public void P34F_FailuresRestoreTruth() {
    var ops = new FakeOps { FailLoad = true };
    var m = new WorldTransition(SubjectIds.Main);
    Assert.IsFalse(Run(m.EnterAsync(ops, SubjectIds.Math, "MathScene")));
    Assert.AreEqual(WorldTransitionState.Idle, m.State, "failed load must read Idle, never stuck Loading");
    var ops2 = new FakeOps();
    var m2 = new WorldTransition(SubjectIds.Main);
    Run(m2.EnterAsync(ops2, SubjectIds.Math, "MathScene"));
    ops2.StickOnUnload = true;
    Assert.IsFalse(Run(m2.ReturnAsync(ops2, "MathScene")));
    Assert.AreEqual(WorldTransitionState.InSubject, m2.State, "scene still there must read InSubject");
    var ops3 = new FakeOps();
    var m3 = new WorldTransition(SubjectIds.Main);
    Run(m3.EnterAsync(ops3, SubjectIds.Math, "MathScene"));
    ops3.FailUnload = true;
    Assert.IsFalse(Run(m3.ReturnAsync(ops3, "MathScene")));
    Assert.AreEqual(WorldTransitionState.InSubject, m3.State, "failed unload must not fake Idle");
  }

  static string ReadRepoFile(params string[] parts) {
    var all = new System.Collections.Generic.List<string>();
    all.Add(UnityEngine.Application.dataPath);
    all.AddRange(parts);
    return System.IO.File.ReadAllText(System.IO.Path.Combine(all.ToArray()));
  }

  [Test] public void P34G_MathSceneInBuildSettings() {
    // The subject scene must SHIP in the player (Build Settings), not just
    // exist as a file. Real file read — no fake.
    string text = ReadRepoFile("..", "ProjectSettings", "EditorBuildSettings.asset");
    StringAssert.Contains("MathScene.unity", text, "MathScene must ship in the player build");
  }

  [Test] public void P34H_MathShellOwnsOnlyContent() {
    // S1b ownership: the shell carries world roots/markers and ZERO Core
    // scripts (no second Player/Camera/GameInstaller/services). Real YAML read.
    string scene = ReadRepoFile("A_World", "MathWorld", "MathScene.unity");
    foreach (string root in new string[] {
      "MathWorld", "EnvironmentRoot", "LobbyRoot", "GameplayAreasRoot",
      "NpcRoot", "LearningEntryRoot", "ReturnPoint", "EntryPoint" }) {
      StringAssert.Contains("m_Name: " + root, scene, "shell must carry " + root);
    }
    foreach (string banned in new string[] {
      "MonoBehaviour", "GameInstaller", "MarketBuilder", "MarketBootstrap",
      "AudioDirector", "QuestManager", "LocalSave" }) {
      Assert.IsFalse(scene.Contains(banned), "shell must not own Core: " + banned);
    }
  }
}
